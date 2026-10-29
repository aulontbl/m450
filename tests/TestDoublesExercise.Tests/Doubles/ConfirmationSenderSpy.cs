using TestDoublesExercise;

namespace TestDoublesExercise.Tests.Doubles;

/// <summary>
/// Spy: zeichnet alle übergebenen Reservierungen in Aufrufreihenfolge auf.
/// </summary>
public sealed class ConfirmationSenderSpy : IConfirmationSender
{
    private readonly List<Reservation> _sent = new();

    public IReadOnlyList<Reservation> Sent => _sent;

    public void Send(Reservation reservation) => _sent.Add(reservation);
}
