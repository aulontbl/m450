using Moq;
using TestDoublesExercise.Tests.Doubles;

namespace TestDoublesExercise.Tests;

public class ReservationServiceTests
{
    private const string Email = "anna@example.com";
    private const string EventCode = "CONCERT-2026";

    // ---------------------------------------------------------------
    // Aufgabe 1: Nur die Schnittstelle ist bekannt (Stub)
    // ---------------------------------------------------------------

    [Fact]
    public void Reserve_EnoughSeatsAvailable_ReturnsTrue()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(EventCode, 3);
        var service = new ReservationService(availability, new FakeReservationRepository(), new ConfirmationSenderSpy());

        // Act
        var result = service.Reserve(Email, EventCode, 2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Reserve_TooFewSeatsAvailable_ReturnsFalse()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(EventCode, 1);
        var service = new ReservationService(availability, new FakeReservationRepository(), new ConfirmationSenderSpy());

        // Act
        var result = service.Reserve(Email, EventCode, 2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Reserve_ExactlyAsManySeatsAsRequested_ReturnsTrue()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(EventCode, 2);
        var service = new ReservationService(availability, new FakeReservationRepository(), new ConfirmationSenderSpy());

        // Act
        var result = service.Reserve(Email, EventCode, 2);

        // Assert
        Assert.True(result);
    }

    // ---------------------------------------------------------------
    // Aufgabe 2: Grenzfälle und Isolation
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reserve_NonPositiveSeatCount_ThrowsAndCallsNoDependency(int seatCount)
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(EventCode, 10);
        var repository = new FakeReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);

        // Act
        Action act = () => service.Reserve(Email, EventCode, seatCount);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Empty(availability.RequestedEventCodes);
        Assert.Equal(0, repository.SaveCalls);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public void Reserve_EmptyEventCode_ThrowsAndCallsNoDependency()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats("", 10);
        var repository = new FakeReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);

        // Act
        Action act = () => service.Reserve(Email, "", 2);

        // Assert
        Assert.Throws<ArgumentException>(act);
        Assert.Empty(availability.RequestedEventCodes);
        Assert.Equal(0, repository.SaveCalls);
        Assert.Empty(sender.Sent);
    }

    // ---------------------------------------------------------------
    // Aufgabe 3: Spy von Hand
    // ---------------------------------------------------------------

    [Fact]
    public void Reserve_SaveSucceeds_SendsExactlyOneConfirmationWithReservationData()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(EventCode, 5);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, new FakeReservationRepository(true), sender);

        // Act
        service.Reserve(Email, EventCode, 2);

        // Assert
        var confirmation = Assert.Single(sender.Sent);
        Assert.Equal(Email, confirmation.Email);
        Assert.Equal(EventCode, confirmation.EventCode);
        Assert.Equal(2, confirmation.SeatCount);
    }

    [Fact]
    public void Reserve_TooFewSeats_SendsNoConfirmation()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(EventCode, 1);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, new FakeReservationRepository(true), sender);

        // Act
        service.Reserve(Email, EventCode, 2);

        // Assert
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public void Reserve_SaveFails_ReturnsFalseAndSendsNoConfirmation()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(EventCode, 5);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, new FakeReservationRepository(false), sender);

        // Act
        var result = service.Reserve(Email, EventCode, 2);

        // Assert
        Assert.False(result);
        Assert.Empty(sender.Sent);
    }

    // ---------------------------------------------------------------
    // Aufgabe 4: Moq für das Repository
    // ---------------------------------------------------------------

    [Fact]
    public void Reserve_EnoughSeatsAndSaveSucceeds_SavesCorrectReservationOnce()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(EventCode, 3);
        var repository = new Mock<IReservationRepository>();
        repository.Setup(r => r.TrySave(It.IsAny<Reservation>())).Returns(true);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);

        // Act
        var result = service.Reserve(Email, EventCode, 2);

        // Assert
        Assert.True(result);
        repository.Verify(r => r.TrySave(It.Is<Reservation>(res =>
            res.Email == Email && res.EventCode == EventCode && res.SeatCount == 2)), Times.Once());
        Assert.Single(sender.Sent);
    }

    [Fact]
    public void Reserve_EnoughSeatsButSaveFails_SavesOnceAndSendsNoConfirmation()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(EventCode, 3);
        var repository = new Mock<IReservationRepository>();
        repository.Setup(r => r.TrySave(It.IsAny<Reservation>())).Returns(false);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);

        // Act
        var result = service.Reserve(Email, EventCode, 2);

        // Assert
        Assert.False(result);
        repository.Verify(r => r.TrySave(It.Is<Reservation>(res =>
            res.Email == Email && res.EventCode == EventCode && res.SeatCount == 2)), Times.Once());
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public void Reserve_TooFewSeats_NeverTriesToSave()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(EventCode, 1);
        var repository = new Mock<IReservationRepository>();
        repository.Setup(r => r.TrySave(It.IsAny<Reservation>())).Returns(true);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);

        // Act
        var result = service.Reserve(Email, EventCode, 2);

        // Assert
        Assert.False(result);
        repository.Verify(r => r.TrySave(It.IsAny<Reservation>()), Times.Never());
        Assert.Empty(sender.Sent);
    }

    // ---------------------------------------------------------------
    // Aufgabe 5: Handgeschrieben oder Moq?
    // Verfügbarkeit: handgeschriebener Stub – er liefert nur Daten und zählt Abfragen,
    //   das ist von Hand kürzer und lesbarer als ein Setup.
    // Repository: Moq – ein Verify(..., Times.Never()) drückt "nie aufgerufen" direkt aus,
    //   ohne dass wir dafür eine eigene Zähl-Klasse brauchen.
    // Bestätigung: handgeschriebener Spy – wir haben ihn schon und Assert.Empty(sender.Sent)
    //   liest sich wie die Anforderung.
    // ---------------------------------------------------------------

    [Fact]
    public void Reserve_WhitespaceEmail_ThrowsAndNeitherSavesNorConfirms()
    {
        // Arrange
        var availability = new SeatAvailabilityStub().WithSeats(EventCode, 10);
        var repository = new Mock<IReservationRepository>();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);

        // Act
        Action act = () => service.Reserve("   ", EventCode, 2);

        // Assert
        Assert.Throws<ArgumentException>(act);
        Assert.Empty(availability.RequestedEventCodes);
        repository.Verify(r => r.TrySave(It.IsAny<Reservation>()), Times.Never());
        Assert.Empty(sender.Sent);
    }
}
