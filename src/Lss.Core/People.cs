using System.Security.Cryptography;

namespace Lss;

/// <summary>
/// Una persona che gioca. Non è un account di Windows né di Steam.
/// </summary>
public sealed record Person(string Id, string Name, string Color, bool IsAdmin);

/// <summary>
/// Le persone di LSS, in persone.ini. Non lancia mai eccezioni in lettura:
/// un file rotto diventa una persona sola, quella del sistema.
/// </summary>
internal sealed class People
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

    private readonly string path;
    private readonly List<Person> all = [];

    public People(string dataRoot)
    {
        path = Path.Combine(dataRoot, "persone.ini");
    }

    /// <summary>Tutte le persone, nell'ordine scelto dall'utente.</summary>
    public IReadOnlyList<Person> All => all;

    /// <summary>Solo le persone con ruolo amministratore.</summary>
    public IReadOnlyList<Person> Admins => all.Where(person => person.IsAdmin).ToList();

    public Person? Find(string id) =>
        all.FirstOrDefault(person => person.Id == id);

    /// <summary>
    /// Legge persone.ini. Se non c'è una persona, ne crea una col nome del
    /// sistema, amministratore. Se non c'è un amministratore, lo è la prima.
    /// </summary>
    public void Load()
    {
        all.Clear();

        Dictionary<string, string> values = IniFile.Read(path);
        string[] order = values.TryGetValue("Ordine", out string? ordine)
            ? ordine.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        foreach (string id in order)
        {
            if (!values.TryGetValue(id + ".Nome", out string? name) ||
                !IsValidName(name, out string cleanName) ||
                all.Any(person => person.Id == id))
                continue;

            string color = values.TryGetValue(id + ".Colore", out string? savedColor) &&
                           Palette.Contains(savedColor)
                ? savedColor
                : Palette[all.Count % Palette.Count];

            bool isAdmin = values.TryGetValue(id + ".Ruolo", out string? role) &&
                           role == "admin";

            all.Add(new Person(id, cleanName, color, isAdmin));
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

        all[index] = all[index] with { IsAdmin = isAdmin };
        return Save();
    }

    /// <summary>
    /// Toglie la persona da persone.ini. Le sue partite non si toccano qui:
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

        all.RemoveAt(index);
        return Save();
    }

    /// <summary>
    /// Scrive persone.ini. Non lancia eccezioni: l'esito è nel Result.
    /// </summary>
    private Result Save()
    {
        try
        {
            List<KeyValuePair<string, string>> values =
            [
                new("Ordine", string.Join(",", all.Select(person => person.Id))),
            ];

            foreach (Person person in all)
            {
                values.Add(new(person.Id + ".Nome", person.Name));
                values.Add(new(person.Id + ".Colore", person.Color));
                values.Add(new(person.Id + ".Ruolo", person.IsAdmin ? "admin" : "utente"));
            }

            IniFile.Write(path, values);
            return Result.Ok("");
        }
        catch (Exception ex)
        {
            Log.Write("Salvataggio di persone.ini", ex);
            return Result.Fail(Strings.PlayersNotSaved);
        }
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
