using Moq;
using TestDoublesExercise.Tests.Doubles;

namespace TestDoublesExercise.Tests;

public class ReservationServiceBatchTests
{
    private const string Concert = "CONCERT-2026";
    private const string Theatre = "THEATRE-2026";

    // ---------------------------------------------------------------
    // Regel 1: Validierung vor jedem Abhängigkeitsaufruf
    // ---------------------------------------------------------------

    [Fact]
    public void ReserveBatch_NullRequests_ThrowsArgumentNullException()
    {
        // Arrange
        var (service, availability, repository, sender) = CreateService();

        // Act
        Action act = () => service.ReserveBatch(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
        AssertNoDependencyCalled(availability, repository, sender);
    }

    [Fact]
    public void ReserveBatch_EmptyRequests_ThrowsArgumentException()
    {
        // Arrange
        var (service, availability, repository, sender) = CreateService();

        // Act
        Action act = () => service.ReserveBatch(Array.Empty<Reservation>());

        // Assert
        Assert.Throws<ArgumentException>(act);
        AssertNoDependencyCalled(availability, repository, sender);
    }

    [Theory]
    [InlineData("", Concert)]
    [InlineData("   ", Concert)]
    [InlineData("a@example.com", "")]
    [InlineData("a@example.com", "  ")]
    public void ReserveBatch_EntryWithEmptyEmailOrEventCode_ThrowsArgumentException(string email, string eventCode)
    {
        // Arrange
        var (service, availability, repository, sender) = CreateService();
        var requests = new[]
        {
            new Reservation("ok@example.com", Concert, 1),
            new Reservation(email, eventCode, 1),
        };

        // Act
        Action act = () => service.ReserveBatch(requests);

        // Assert
        Assert.Throws<ArgumentException>(act);
        AssertNoDependencyCalled(availability, repository, sender);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReserveBatch_EntryWithNonPositiveSeatCount_ThrowsArgumentOutOfRangeException(int seatCount)
    {
        // Arrange
        var (service, availability, repository, sender) = CreateService();
        var requests = new[]
        {
            new Reservation("ok@example.com", Concert, 1),
            new Reservation("b@example.com", Concert, seatCount),
        };

        // Act
        Action act = () => service.ReserveBatch(requests);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
        AssertNoDependencyCalled(availability, repository, sender);
    }

    // ---------------------------------------------------------------
    // Regel 2: Plätze pro Veranstaltung summieren, Verfügbarkeit einmal pro Code
    // ---------------------------------------------------------------

    [Fact]
    public void ReserveBatch_MultipleEntriesPerEvent_QueriesAvailabilityOncePerDistinctEventCode()
    {
        // Arrange
        var (service, availability, _, _) = CreateService(
            seats: new() { [Concert] = 10, [Theatre] = 10 });
        var requests = new[]
        {
            new Reservation("a@example.com", Concert, 1),
            new Reservation("b@example.com", Theatre, 1),
            new Reservation("c@example.com", Concert, 1),
        };

        // Act
        service.ReserveBatch(requests);

        // Assert
        Assert.Equal(2, availability.RequestedEventCodes.Count);
        Assert.Single(availability.RequestedEventCodes, Concert);
        Assert.Single(availability.RequestedEventCodes, Theatre);
    }

    // ---------------------------------------------------------------
    // Regel 3: Zu wenige Plätze -> false, nichts speichern, nichts senden
    // ---------------------------------------------------------------

    [Fact]
    public void ReserveBatch_TwoEntriesSameEventFitIndividuallyButNotTogether_ReturnsFalseAndDoesNothing()
    {
        // Arrange: je 2 Plätze passen einzeln in 3 freie Plätze, zusammen (4) aber nicht.
        var availability = new SeatAvailabilityStub().WithSeats(Concert, 3);
        var repository = new Mock<IReservationRepository>();
        repository.Setup(r => r.TrySaveBatch(It.IsAny<IReadOnlyList<Reservation>>())).Returns(true);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);
        var requests = new[]
        {
            new Reservation("a@example.com", Concert, 2),
            new Reservation("b@example.com", Concert, 2),
        };

        // Act
        var result = service.ReserveBatch(requests);

        // Assert
        Assert.False(result);
        repository.Verify(r => r.TrySaveBatch(It.IsAny<IReadOnlyList<Reservation>>()), Times.Never());
        repository.Verify(r => r.TrySave(It.IsAny<Reservation>()), Times.Never());
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public void ReserveBatch_OneOfSeveralEventsHasTooFewSeats_ReturnsFalseAndDoesNothing()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(Concert, 10).WithSeats(Theatre, 1);
        var repository = new Mock<IReservationRepository>();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);
        var requests = new[]
        {
            new Reservation("a@example.com", Concert, 2),
            new Reservation("b@example.com", Theatre, 2),
        };

        // Act
        var result = service.ReserveBatch(requests);

        // Assert
        Assert.False(result);
        repository.Verify(r => r.TrySaveBatch(It.IsAny<IReadOnlyList<Reservation>>()), Times.Never());
        Assert.Empty(sender.Sent);
    }

    // ---------------------------------------------------------------
    // Regel 4: Alle Reservierungen in einem einzigen Batch-Aufruf speichern
    // ---------------------------------------------------------------

    [Fact]
    public void ReserveBatch_EnoughSeats_SavesAllReservationsInOneBatchCall()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(Concert, 4).WithSeats(Theatre, 1);
        var repository = new Mock<IReservationRepository>();
        repository.Setup(r => r.TrySaveBatch(It.IsAny<IReadOnlyList<Reservation>>())).Returns(true);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);
        var requests = new[]
        {
            new Reservation("a@example.com", Concert, 2),
            new Reservation("b@example.com", Theatre, 1),
            new Reservation("c@example.com", Concert, 2),
        };

        // Act
        var result = service.ReserveBatch(requests);

        // Assert
        Assert.True(result);
        repository.Verify(r => r.TrySaveBatch(It.Is<IReadOnlyList<Reservation>>(saved =>
            saved.Count == 3
            && saved[0].Email == "a@example.com" && saved[0].EventCode == Concert && saved[0].SeatCount == 2
            && saved[1].Email == "b@example.com" && saved[1].EventCode == Theatre && saved[1].SeatCount == 1
            && saved[2].Email == "c@example.com" && saved[2].EventCode == Concert && saved[2].SeatCount == 2)),
            Times.Once());
        repository.Verify(r => r.TrySave(It.IsAny<Reservation>()), Times.Never());
    }

    // ---------------------------------------------------------------
    // Regel 5: Bestätigungen nur nach erfolgreichem Speichern, in Eingabereihenfolge
    // ---------------------------------------------------------------

    [Fact]
    public void ReserveBatch_SaveBatchFails_ReturnsFalseAndSendsNoConfirmation()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(Concert, 10);
        var repository = new Mock<IReservationRepository>();
        repository.Setup(r => r.TrySaveBatch(It.IsAny<IReadOnlyList<Reservation>>())).Returns(false);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);
        var requests = new[]
        {
            new Reservation("a@example.com", Concert, 1),
            new Reservation("b@example.com", Concert, 1),
        };

        // Act
        var result = service.ReserveBatch(requests);

        // Assert
        Assert.False(result);
        repository.Verify(r => r.TrySaveBatch(It.IsAny<IReadOnlyList<Reservation>>()), Times.Once());
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public void ReserveBatch_SaveBatchSucceeds_SendsOneConfirmationPerEntryInInputOrder()
    {
        // Arrange
        var (service, _, _, sender) = CreateService(
            seats: new() { [Concert] = 10, [Theatre] = 10 });
        var requests = new[]
        {
            new Reservation("a@example.com", Concert, 1),
            new Reservation("b@example.com", Theatre, 3),
            new Reservation("c@example.com", Concert, 2),
        };

        // Act
        var result = service.ReserveBatch(requests);

        // Assert
        Assert.True(result);
        Assert.Equal(requests, sender.Sent);
    }

    private static (ReservationService Service, SeatAvailabilityStub Availability, FakeReservationRepository Repository, ConfirmationSenderSpy Sender)
        CreateService(Dictionary<string, int>? seats = null, bool saveResult = true)
    {
        var availability = new SeatAvailabilityStub();
        foreach (var (code, count) in seats ?? new() { [Concert] = 10 })
        {
            availability.WithSeats(code, count);
        }

        var repository = new FakeReservationRepository(saveResult);
        var sender = new ConfirmationSenderSpy();
        return (new ReservationService(availability, repository, sender), availability, repository, sender);
    }

    private static void AssertNoDependencyCalled(
        SeatAvailabilityStub availability, FakeReservationRepository repository, ConfirmationSenderSpy sender)
    {
        Assert.Empty(availability.RequestedEventCodes);
        Assert.Equal(0, repository.SaveCalls);
        Assert.Equal(0, repository.SaveBatchCalls);
        Assert.Empty(sender.Sent);
    }
}
