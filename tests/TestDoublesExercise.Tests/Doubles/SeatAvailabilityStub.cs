using TestDoublesExercise;

namespace TestDoublesExercise.Tests.Doubles;

/// <summary>
/// Stub: liefert pro Veranstaltungscode eine fest vorgegebene Anzahl freier Plätze.
/// Berechnet keine echte Verfügbarkeit. Zählt zusätzlich die Abfragen, damit Tests
/// prüfen können, ob bzw. wie oft die Abhängigkeit benutzt wurde.
/// </summary>
public sealed class SeatAvailabilityStub : ISeatAvailability
{
    private readonly Dictionary<string, int> _seats = new(StringComparer.Ordinal);

    public List<string> RequestedEventCodes { get; } = new();

    public SeatAvailabilityStub WithSeats(string eventCode, int availableSeats)
    {
        _seats[eventCode] = availableSeats;
        return this;
    }

    public int GetAvailableSeats(string eventCode)
    {
        RequestedEventCodes.Add(eventCode);
        return _seats.TryGetValue(eventCode, out var seats) ? seats : 0;
    }
}
