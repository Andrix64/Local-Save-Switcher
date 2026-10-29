using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Lss;

/// <summary>
/// Una persona che gioca. Non è un account di Windows né di Steam.
/// </summary>
/// <param name="HasPin">true se la persona ha un PIN (vedi People.SetPin).</param>
public sealed record Person(string Id, string Name, string Color, bool IsAdmin, bool HasPin = false);

/// <summary>Una persona come è scritta in persone.json: nomi corti in italiano.</summary>
internal sealed class PersonEntry
{
    public string Id { get; set; } = "";

    [JsonPropertyName("Nome")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Colore")]
    public string Color { get; set; } = "";

    /// <summary>"admin" o "utente".</summary>
    [JsonPropertyName("Ruolo")]
    public string Role { get; set; } = "utente";

    /// <summary>"salt:hash" in base64, o vuoto se non ha un PIN.</summary>
    public string Pin { get; set; } = "";
}

/// <summary>Come persone.json è scritto: le persone, nell'ordine scelto.</summary>
internal sealed class PeopleFile
{
    [JsonPropertyName("Persone")]
    public List<PersonEntry> People { get; set; } = [];
}

/// <summary>
/// Le persone di LSS, in persone.json. Non lancia mai eccezioni in lettura:
/// un file rotto diventa una persona sola, quella del sistema.
/// </summary>
public sealed class People
{
    /// <summary>
    /// Colori dei giocatori: bianco sopra con almeno 4,5:1 di contrasto.
    /// </summary>
    public static readonly IReadOnlyList<string> Palette =
    [
        "#C2410C", "#1D6FD0", "#2F7D3B", "#7C3AED",
        "#B42365", "#0F766E", "#8A5A00", "#4B5563",
    ];

    private const int MaxNameLength = 30;

    // PBKDF2-SHA256: il PIN non si scrive mai in chiaro.
    private const int PinIterations = 100_000;
    private const int PinSaltSize = 16;
    private const int PinHashSize = 32;

    private readonly string path;
    private readonly List<Person> all = [];

    // Id persona -> "salt:hash" in base64, per le persone con PIN.
    private readonly Dictionary<string, string> pins = [];

    /// <summary>I PIN sbagliati di ogni persona, condivisi da tutte le finestre del PIN.</summary>
    public PinGuard Guard { get; } = new();

    public People(string dataRoot)
    {
        path = Path.Combine(dataRoot, "persone.json");
    }

    /// <summary>Tutte le persone, nell'ordine scelto dall'utente.</summary>
    public IReadOnlyList<Person> All => all;

    /// <summary>Solo le persone con ruolo amministratore.</summary>
    public IReadOnlyList<Person> Admins => all.Where(person => person.IsAdmin).ToList();

    public Person? Find(string id) =>
        all.FirstOrDefault(person => person.Id == id);

    /// <summary>
    /// Legge persone.json. Se non c'è una persona, ne crea una col nome del
    /// sistema, amministratore. Se non c'è un amministratore, lo è la prima.
    /// </summary>
    public void Load()
    {
        all.Clear();
        pins.Clear();

        foreach (PersonEntry entry in JsonFile.Read<PeopleFile>(path)?.People ?? [])
        {
            string id = entry.Id ?? "";

            if (id == "" || !IsValidName(entry.Name, out string cleanName) || all.Any(person => person.Id == id))
                continue;

            string color = entry.Color != null && Palette.Contains(entry.Color)
                ? entry.Color
                : Palette[all.Count % Palette.Count];

            bool isAdmin = entry.Role == "admin";

            if (entry.Pin != null && IsStoredPin(entry.Pin))
                pins[id] = entry.Pin;

            all.Add(new Person(id, cleanName, color, isAdmin, pins.ContainsKey(id)));
        }

        if (all.Count == 0)
        {
            all.Add(new Person(NewId(), FirstName(), Palette[0], true));
            Save();
        }
        else if (all.All(person => !person.IsAdmin))
        {
            // File a mano o rotto: la prima persona diventa amministratore,
            // e il file si corregge al prossimo Save.
            all[0] = all[0] with { IsAdmin = true };
        }
    }

    /// <summary>
    /// Aggiunge una persona come utente, col primo colore libero della tavolozza.
    /// </summary>
    public Result<Person> Add(string name)
    {
        if (!IsValidName(name, out string cleanName))
            return Result<Person>.Fail(Strings.InvalidName);

        if (NameTaken(cleanName, exceptId: null))
            return Result<Person>.Fail(Strings.PlayerNameTaken);

        string color = Palette.FirstOrDefault(c => all.All(person => person.Color != c))
                       ?? Palette[all.Count % Palette.Count];

        Person person = new(NewId(), cleanName, color, IsAdmin: false);
        all.Add(person);

        return Save() is { Success: true }
            ? Result<Person>.Ok(person)
            : Result<Person>.Fail(Strings.PlayersNotSaved);
    }

    /// <summary>Cambia il nome di una persona.</summary>
    public Result Rename(string id, string name)
    {
        int index = IndexOf(id);

        if (index < 0)
            return Result.Fail(Strings.PlayerNotFound);

        if (!IsValidName(name, out string cleanName))
            return Result.Fail(Strings.InvalidName);

        if (NameTaken(cleanName, exceptId: id))
            return Result.Fail(Strings.PlayerNameTaken);

        all[index] = all[index] with { Name = cleanName };
        return Save();
    }

    /// <summary>Cambia il colore: solo uno della tavolozza.</summary>
    public Result SetColor(string id, string color)
    {
        int index = IndexOf(id);

        if (index < 0)
            return Result.Fail(Strings.PlayerNotFound);

        if (!Palette.Contains(color))
            return Result.Fail(Strings.PlayerColorInvalid);

        all[index] = all[index] with { Color = color };
        return Save();
    }

    /// <summary>Sposta una persona nell'elenco, fino alla posizione indicata.</summary>
    public Result Move(string id, int newIndex)
    {
        int index = IndexOf(id);

        if (index < 0)
            return Result.Fail(Strings.PlayerNotFound);

        Person person = all[index];
        all.RemoveAt(index);
        all.Insert(Math.Clamp(newIndex, 0, all.Count), person);

        return Save();
    }

    /// <summary>Rende la persona amministratore o utente. Non toglie l'ultimo amministratore.</summary>
    public Result SetAdmin(string id, bool isAdmin)
    {
        int index = IndexOf(id);

        if (index < 0)
            return Result.Fail(Strings.PlayerNotFound);

        if (!isAdmin && all[index].IsAdmin && Admins.Count == 1)
            return Result.Fail(Strings.LastAdmin);

        // Un admin ha sempre il PIN: prima si imposta, poi si promuove.
        if (isAdmin && !all[index].HasPin)
            return Result.Fail(Strings.AdminNeedsPin);

        // Il PIN resta, anche diventando utente.
        all[index] = all[index] with { IsAdmin = isAdmin };
        return Save();
    }

    /// <summary>
    /// Toglie la persona da persone.json. Le sue partite non si toccano qui:
    /// le sistema SaveManager (fase P, P5).
    /// </summary>
    public Result Remove(string id)
    {
        int index = IndexOf(id);

        if (index < 0)
            return Result.Fail(Strings.PlayerNotFound);

        if (all.Count == 1)
            return Result.Fail(Strings.LastPlayer);

        if (all[index].IsAdmin && Admins.Count == 1)
            return Result.Fail(Strings.LastAdmin);

        pins.Remove(id);
        all.RemoveAt(index);
        return Save();
    }

    /// <summary>
    /// Scrive persone.json. Non lancia eccezioni: l'esito è nel Result.
    /// </summary>
    private Result Save()
    {
        try
        {
            var file = new PeopleFile();

            foreach (Person person in all)
            {
                file.People.Add(new PersonEntry
                {
                    Id = person.Id,
                    Name = person.Name,
                    Color = person.Color,
                    Role = person.IsAdmin ? "admin" : "utente",
                    Pin = pins.GetValueOrDefault(person.Id, ""),
                });
            }

            JsonFile.Write(path, file);
            return Result.Ok("");
        }
        catch (Exception ex)
        {
            Log.Write("Salvataggio di persone.json", ex);
            return Result.Fail(Strings.PlayersNotSaved);
        }
    }

    /// <summary>
    /// Imposta o cambia il PIN di una persona: 4 cifre, salvate con PBKDF2.
    /// Per gli utenti il PIN non è obbligatorio (senza, la persona si sceglie da sola); per gli amministratori sì.
    /// </summary>
    public Result SetPin(string id, string pin)
    {
        int index = IndexOf(id);

        if (index < 0)
            return Result.Fail(Strings.PlayerNotFound);

        if (!IsPinFormat(pin))
            return Result.Fail(Strings.PinFormat);

        pins[id] = HashPin(pin);
        all[index] = all[index] with { HasPin = true };
        return Save();
    }

    /// <summary>Toglie il PIN a un utente. Vale anche senza il PIN attuale (vedi P4a: si chiede al cambio). Un amministratore deve averlo.</summary>
    public Result RemovePin(string id)
    {
        int index = IndexOf(id);

        if (index < 0)
            return Result.Fail(Strings.PlayerNotFound);

        if (all[index].IsAdmin)
            return Result.Fail(Strings.AdminNeedsPin);

        pins.Remove(id);
        all[index] = all[index] with { HasPin = false };
        return Save();
    }

    /// <summary>
    /// Il PIN è quello di questa persona? Confronto a tempo costante. Senza PIN, false.
    /// </summary>
    public bool CheckPin(string id, string pin)
    {
        if (!pins.TryGetValue(id, out string? stored) || !IsPinFormat(pin))
            return false;

        string[] parts = stored.Split(':');
        byte[] expected = Convert.FromBase64String(parts[1]);
        byte[] actual = Pbkdf2(pin, Convert.FromBase64String(parts[0]));

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool IsPinFormat(string pin) =>
        pin.Length == 4 && pin.All(c => c is >= '0' and <= '9');

    /// <summary>Sale casuale e hash, come "salt:hash" in base64.</summary>
    private static string HashPin(string pin)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(PinSaltSize);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(Pbkdf2(pin, salt));
    }

    private static byte[] Pbkdf2(string pin, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(pin, salt, PinIterations, HashAlgorithmName.SHA256, PinHashSize);

    /// <summary>Valore di persone.ini: salt e hash in base64, separati da ':'.</summary>
    private static bool IsStoredPin(string value)
    {
        string[] parts = value.Split(':');

        return parts.Length == 2 &&
               Convert.TryFromBase64String(parts[0], new byte[PinSaltSize], out _) &&
               Convert.TryFromBase64String(parts[1], new byte[PinHashSize], out _);
    }

    private int IndexOf(string id) =>
        all.FindIndex(person => person.Id == id);

    private bool NameTaken(string name, string? exceptId) =>
        all.Any(person => person.Id != exceptId &&
                          string.Equals(person.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Nome valido: come i nomi dei playthrough, al massimo 30 caratteri.
    /// </summary>
    private static bool IsValidName(string? name, out string cleanName)
    {
        cleanName = (name ?? "").Trim();
        return cleanName.Length <= MaxNameLength && SaveManager.IsValidName(cleanName);
    }

    /// <summary>Nome del primo giocatore: quello del sistema, con la maiuscola.</summary>
    private static string FirstName()
    {
        string userName = (Environment.UserName ?? "").Trim();

        if (!IsValidName(userName, out string cleanName))
            return Strings.PlayerDefaultName;

        return char.ToUpperInvariant(cleanName[0]) + cleanName[1..];
    }

    /// <summary>Id di 8 caratteri esadecimali minuscoli.</summary>
    private static string NewId() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
}
