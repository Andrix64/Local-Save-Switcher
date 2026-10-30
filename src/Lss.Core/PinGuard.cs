namespace Lss;

/// <summary>
/// I PIN sbagliati di ogni persona, per tutta la vita dell'app: dopo cinque errori di
/// fila quella persona aspetta 30 secondi, anche se chiude e riapre la finestra.
/// Sta in memoria: riparte quando LSS si riavvia.
/// </summary>
public sealed class PinGuard
{
    public const int MaxWrongTries = 5;
    public const int WaitSeconds = 30;

    private readonly Dictionary<string, int> wrongTries = [];
    private readonly Dictionary<string, DateTime> waitUntil = [];

    /// <summary>Fino a quando la persona deve aspettare, o MinValue se non aspetta.</summary>
    public DateTime WaitUntil(string personId) =>
        waitUntil.GetValueOrDefault(personId, DateTime.MinValue);

    /// <summary>true se la persona deve ancora aspettare.</summary>
    public bool IsWaiting(string personId, DateTime now) => now < WaitUntil(personId);

    /// <summary>Un PIN sbagliato: al quinto di fila inizia l'attesa.</summary>
    public void Wrong(string personId, DateTime now)
    {
        int tries = wrongTries.GetValueOrDefault(personId) + 1;

        if (tries >= MaxWrongTries)
        {
            waitUntil[personId] = now.AddSeconds(WaitSeconds);
            tries = 0;
        }

        wrongTries[personId] = tries;
    }

    /// <summary>Il PIN è giusto: gli errori di prima non contano più.</summary>
    public void Right(string personId)
    {
        wrongTries.Remove(personId);
        waitUntil.Remove(personId);
    }
}
