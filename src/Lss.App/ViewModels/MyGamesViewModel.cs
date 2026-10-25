using System.Collections.ObjectModel;

namespace Lss.App.ViewModels;

/// <summary>Una partita di una persona nel menu "Le mie partite".</summary>
public sealed record MyGameRow(string ProfileName, string Name, string LastText, bool IsFavourite);

/// <summary>La scelta: una partita (il suo nome di cartella) o una nuova (null).</summary>
public sealed record MyGamesChoice(string? ProfileName);

/// <summary>
/// "Le mie partite" di una copertina: le partite della persona per quel gioco, con la
/// preferita (★) per prima. Scegliere una la rende la preferita e la gioca. Non si
/// elimina e non si rinomina niente qui.
/// </summary>
public sealed partial class MyGamesViewModel
{
    private readonly SaveManager manager;
    private readonly Person player;

    internal MyGamesViewModel(SaveManager manager, Person player)
    {
        this.manager = manager;
        this.player = player;

        Title = Loc.F(Strings.SimpleMyGamesTitle, manager.Game.Name);
        NewText = Strings.SimpleNewGame;
        FavouriteTooltip = Strings.SimpleFavourite;

        string? favourite = manager.FavouriteOf(player.Id);

        foreach (string profile in manager.GetProfilesOf(player.Id)
                     .OrderByDescending(profile => profile == favourite)
                     .ThenByDescending(profile => manager.GetInfo(profile).LastSession ?? DateTime.MinValue)
                     .ThenBy(profile => profile, StringComparer.OrdinalIgnoreCase))
        {
            DateTime? last = manager.GetInfo(profile).LastSession ?? manager.GetProfileLastModified(profile);

            Rows.Add(new MyGameRow(profile, SaveManager.ProfileDisplayName(profile),
                last is DateTime when ? Loc.F(Strings.SimpleLastPlay, when.ToString("d")) : "",
                profile == favourite));
        }
    }

    public string Title { get; }

    public string NewText { get; }

    public string FavouriteTooltip { get; }

    public ObservableCollection<MyGameRow> Rows { get; } = [];

    /// <summary>La scelta fatta: una partita, o una nuova. Null finché non si sceglie.</summary>
    public MyGamesChoice? Choice { get; private set; }

    /// <summary>Gioca questa partita (diventa la preferita di chi gioca).</summary>
    public void Choose(MyGameRow row) => Choice = new MyGamesChoice(row.ProfileName);

    /// <summary>"Inizia una nuova partita".</summary>
    public void ChooseNew() => Choice = new MyGamesChoice(null);

    /// <summary>La persona che gioca (per le scritte della finestra).</summary>
    public string PlayerName => player.Name;
}
