namespace TestDoublesExercise;

public sealed record Reservation(string Email, string EventCode, int SeatCount);

public interface ISeatAvailability
{
    int GetAvailableSeats(string eventCode);
}

public interface IReservationRepository
{
    bool TrySave(Reservation reservation);

    bool TrySaveBatch(IReadOnlyList<Reservation> reservations);
}

public interface IConfirmationSender
{
    void Send(Reservation reservation);
}

public sealed class ReservationService
{
    private readonly ISeatAvailability _availability;
    private readonly IReservationRepository _repository;
    private readonly IConfirmationSender _confirmationSender;

    public ReservationService(
        ISeatAvailability availability,
        IReservationRepository repository,
        IConfirmationSender confirmationSender)
    {
        _availability = availability ?? throw new ArgumentNullException(nameof(availability));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _confirmationSender = confirmationSender ?? throw new ArgumentNullException(nameof(confirmationSender));
    }

    public bool Reserve(string email, string eventCode, int seatCount)
    {
        Validate(email, eventCode, seatCount);

        if (_availability.GetAvailableSeats(eventCode) < seatCount)
        {
            return false;
        }

        var reservation = new Reservation(email, eventCode, seatCount);
        if (!_repository.TrySave(reservation))
        {
            return false;
        }

        _confirmationSender.Send(reservation);
        return true;
    }

    public bool ReserveBatch(IReadOnlyList<Reservation> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
        {
            throw new ArgumentException("At least one reservation is required.", nameof(requests));
        }

        foreach (var request in requests)
        {
            if (request is null)
            {
                throw new ArgumentException("Reservations must not be null.", nameof(requests));
            }

            Validate(request.Email, request.EventCode, request.SeatCount);
        }

        var seatsPerEvent = requests
            .GroupBy(r => r.EventCode, StringComparer.Ordinal)
            .Select(g => (EventCode: g.Key, Seats: g.Sum(r => r.SeatCount)));

        foreach (var (eventCode, seats) in seatsPerEvent)
        {
            if (_availability.GetAvailableSeats(eventCode) < seats)
            {
                return false;
            }
        }

        if (!_repository.TrySaveBatch(requests))
        {
            return false;
        }

        foreach (var request in requests)
        {
            _confirmationSender.Send(request);
        }

        return true;
    }

    private static void Validate(string email, string eventCode, int seatCount)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email must not be empty.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(eventCode))
        {
            throw new ArgumentException("Event code must not be empty.", nameof(eventCode));
        }

        if (seatCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seatCount), seatCount, "Seat count must be positive.");
        }
    }
}
