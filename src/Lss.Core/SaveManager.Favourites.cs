using System.Globalization;
using System.Text.Json.Serialization;

namespace Lss;

/// <summary>Come preferite.json è scritto: persona -> partita preferita, e persona -> data dell'ultima partita.</summary>
internal sealed class FavouritesFileData
{
    [JsonPropertyName("Preferite")]
    public Dictionary<string, string> Favourites { get; set; } = [];

    [JsonPropertyName("Ultima")]
    public Dictionary<string, string> LastPlayed { get; set; } = [];
}

/// <summary>
/// Partita preferita di ogni persona per questo gioco (preferite.json). È quella
/// che la modalità semplice mette al suo posto, e la data dell'ultima partita
/// giocata da ogni persona (Ultima) serve a ordinare le copertine.
/// </summary>
public sealed partial class SaveManager
{
    private const string FavouritesFile = "preferite.json";

    private string FavouritesPath => Path.Combine(dataFolder, FavouritesFile);

    // Il file letto (vuoto se manca o è rotto), con i nomi delle persone senza distinguere maiuscole.
    private FavouritesFileData ReadFavourites()
    {
        var file = JsonFile.Read<FavouritesFileData>(FavouritesPath);

        return new FavouritesFileData
        {
            Favourites = new Dictionary<string, string>(file?.Favourites ?? [], StringComparer.OrdinalIgnoreCase),
            LastPlayed = new Dictionary<string, string>(file?.LastPlayed ?? [], StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// Partita preferita di una persona per questo gioco, per nome di cartella,
    /// o null se non ne ha.
    /// </summary>
    public string? FavouriteOf(string personId)
    {
        string profile = ReadFavourites().Favourites.GetValueOrDefault(personId, "");

        return profile != "" && ProfileExists(profile) ? profile : null;
    }

    /// <summary>
    /// Quando una persona ha giocato per ultima a questo gioco, o null se mai.
    /// </summary>
    public DateTime? LastPlayedOf(string personId)
    {
        string text = ReadFavourites().LastPlayed.GetValueOrDefault(personId, "");

        return DateTime.TryParseExact(text, InfoDateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime last)
            ? last
            : null;
    }

    /// <summary>
    /// Sceglie la partita preferita di una persona. Deve essere una sua partita.
    /// </summary>
    public Result SetFavourite(string personId, string profileName)
    {
        return Log.Write($"Preferita di {personId}: {profileName}", SetFavouriteCore(personId, profileName));
    }

    private Result SetFavouriteCore(string personId, string profileName)
    {
        if (!ProfileExists(profileName))
            return Result.Fail(Strings.ProfileNotFound);

        if (GetInfo(profileName).Owner != personId)
            return Result.Fail(Strings.NotYourPlaythrough);

        try
        {
            FavouritesFileData file = ReadFavourites();
            file.Favourites[personId] = profileName;
            JsonFile.Write(FavouritesPath, file);
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.ErrorWithMessage, ex.Message));
        }

        return Result.Ok("");
    }

    /// <summary>
    /// Rimette preferite.json a posto dopo un'operazione riuscita. Ogni persona con
    /// partite ha una preferita: quella di prima se c'è ancora, altrimenti la
    /// partita giocata per ultima. Una partita rinominata resta la preferita.
    /// Senza partite, la persona non ha preferita. Non lancia eccezioni.
    /// </summary>
    private void RepairFavourites(string? renamedFrom = null, string? renamedTo = null)
    {
        try
        {
            FavouritesFileData current = ReadFavourites();

            if (renamedFrom != null && renamedTo != null)
            {
                foreach (string key in current.Favourites.Keys.ToList())
                {
                    if (current.Favourites[key] == renamedFrom)
                        current.Favourites[key] = renamedTo;
                }
            }

            var owned = GetProfiles()
                .Select(profile => (Name: profile, Info: GetInfo(profile)))
                .Where(item => item.Info.Owner != "")
                .ToList();

            var values = new FavouritesFileData();

            foreach (string personId in owned.Select(item => item.Info.Owner).Distinct())
            {
                List<string> mine = owned
                    .Where(item => item.Info.Owner == personId)
                    .OrderByDescending(item => item.Info.LastSession ?? DateTime.MinValue)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(item => item.Name)
                    .ToList();

                string favourite = current.Favourites.GetValueOrDefault(personId, "");

                values.Favourites[personId] = mine.Contains(favourite) ? favourite : mine[0];

                if (current.LastPlayed.TryGetValue(personId, out string? last))
                    values.LastPlayed[personId] = last;
            }

            JsonFile.Write(FavouritesPath, values);
        }
        catch (Exception ex)
        {
            Log.Write("Preferite non aggiornate", ex);
        }
    }

    /// <summary>
    /// Partite senza proprietario (o con una persona che non esiste più): le dà al
    /// primo amministratore e rimette a posto le preferite. Poi V1Compat dà una
    /// preferita alle persone delle partite fatte prima delle persone. Non lancia
    /// eccezioni.
    /// </summary>
    internal void AssignExistingPlaythroughs()
    {
        Person? first = Players?.Admins.FirstOrDefault();

        if (first == null)
            return;

        try
        {
            // Senza proprietario, o con una persona che non esiste più: va al primo amministratore.
            // Si controlla a ogni apertura, anche dopo ProprietariOk (una partita recuperata senza
            // persona non passerebbe più dal passo una tantum).
            foreach (string profile in GetProfiles())
            {
                string owner = GetInfo(profile).Owner;

                if (owner != "" && Players!.Find(owner) != null)
                    continue;

                WriteOwner(profile, first.Id);
                Log.Write($"Partita {profile} senza persona valida: data a {first.Name}");
            }

            RepairFavourites();

            // V1Compat: le partite fatte prima delle persone non hanno una preferita
            V1Compat.AssignFirstFavourites(this);
        }
        catch (Exception ex)
        {
            Log.Write("Partite esistenti non assegnate alle persone", ex);
        }
    }

    /// <summary>
    /// Segna la data dell'ultima partita giocata dalla persona a cui appartiene la partita.
    /// Non lancia eccezioni.
    /// </summary>
    private void MarkPlayed(string profileName, DateTime when)
    {
        try
        {
            string owner = GetInfo(profileName).Owner;

            if (owner == "")
                return;

            FavouritesFileData file = ReadFavourites();
            file.LastPlayed[owner] = when.ToString(InfoDateFormat, CultureInfo.InvariantCulture);
            JsonFile.Write(FavouritesPath, file);
        }
        catch (Exception ex)
        {
            Log.Write("Ultima partita di " + profileName + " non segnata", ex);
        }
    }
}
