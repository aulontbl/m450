using TestDoublesExercise;

namespace TestDoublesExercise.Tests.Doubles;

/// <summary>
/// Einfaches handgeschriebenes Repository-Double mit festem Ergebnis.
/// Zählt die Aufrufe, damit Tests das Ausbleiben von Speicherversuchen prüfen können.
/// </summary>
public sealed class FakeReservationRepository : IReservationRepository
{
    private readonly bool _result;

    public FakeReservationRepository(bool result = true) => _result = result;

    public int SaveCalls { get; private set; }

    public int SaveBatchCalls { get; private set; }

    public bool TrySave(Reservation reservation)
    {
        SaveCalls++;
        return _result;
    }

    public bool TrySaveBatch(IReadOnlyList<Reservation> reservations)
    {
        SaveBatchCalls++;
        return _result;
    }
}
