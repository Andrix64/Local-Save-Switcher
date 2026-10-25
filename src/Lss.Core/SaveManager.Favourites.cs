using System.Globalization;

namespace Lss;

/// <summary>
/// Partita preferita di ogni persona per questo gioco (preferite.ini). È quella
/// che la modalità semplice mette al suo posto, e la data dell'ultima partita
/// giocata da ogni persona (Ultima) serve a ordinare le copertine.
/// </summary>
public sealed partial class SaveManager
{
    private const string FavouritesFile = "preferite.ini";
    private const string LastPlayedSuffix = ".Ultima";

    private string FavouritesPath => Path.Combine(dataFolder, FavouritesFile);

    /// <summary>
    /// Partita preferita di una persona per questo gioco, per nome di cartella,
    /// o null se non ne ha.
    /// </summary>
    public string? FavouriteOf(string personId)
    {
        string profile = IniFile.Read(FavouritesPath).GetValueOrDefault(personId, "");

        return profile != "" && ProfileExists(profile) ? profile : null;
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
            Dictionary<string, string> values = IniFile.Read(FavouritesPath);
            values[personId] = profileName;
            IniFile.Write(FavouritesPath, values);
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.F(Strings.ErrorWithMessage, ex.Message));
        }

        return Result.Ok("");
    }

    /// <summary>
    /// Rimette preferite.ini a posto dopo un'operazione riuscita. Ogni persona con
    /// partite ha una preferita: quella di prima se c'è ancora, altrimenti la
    /// partita giocata per ultima. Una partita rinominata resta la preferita.
    /// Senza partite, la persona non ha preferita. Non lancia eccezioni.
    /// </summary>
    private void RepairFavourites(string? renamedFrom = null, string? renamedTo = null)
    {
        try
        {
            Dictionary<string, string> current = IniFile.Read(FavouritesPath);

            if (renamedFrom != null && renamedTo != null)
            {
                foreach (string key in current.Keys.ToList())
                {
                    if (current[key] == renamedFrom)
                        current[key] = renamedTo;
                }
            }

            var owned = GetProfiles()
                .Select(profile => (Name: profile, Info: GetInfo(profile)))
                .Where(item => item.Info.Owner != "")
                .ToList();

            var values = new Dictionary<string, string>();

            foreach (string personId in owned.Select(item => item.Info.Owner).Distinct())
            {
                List<string> mine = owned
                    .Where(item => item.Info.Owner == personId)
                    .OrderByDescending(item => item.Info.LastSession ?? DateTime.MinValue)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(item => item.Name)
                    .ToList();

                string favourite = current.GetValueOrDefault(personId, "");

                values[personId] = mine.Contains(favourite) ? favourite : mine[0];

                if (current.TryGetValue(personId + LastPlayedSuffix, out string? last))
                    values[personId + LastPlayedSuffix] = last;
            }

            IniFile.Write(FavouritesPath, values);
        }
        catch (Exception ex)
        {
            Log.Write("Preferite non aggiornate", ex);
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

            Dictionary<string, string> values = IniFile.Read(FavouritesPath);
            values[owner + LastPlayedSuffix] = when.ToString(InfoDateFormat, CultureInfo.InvariantCulture);
            IniFile.Write(FavouritesPath, values);
        }
        catch (Exception ex)
        {
            Log.Write("Ultima partita di " + profileName + " non segnata", ex);
        }
    }
}
