using CommunityToolkit.Mvvm.ComponentModel;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>Un posto nella finestra di importazione: la cartella (o il percorso portabile) e il suo stato qui.</summary>
public sealed record ConfigSpotRow(string Text, string StateText);

/// <summary>
/// Un gioco del file nella finestra di importazione (N9c): nome, launcher, data, note e un
/// rigo per posto. La casella è accesa se almeno un posto si può importare.
/// </summary>
public sealed partial class ConfigGameItem : ObservableObject
{
    public ConfigGameItem(ConfigGamePlan plan, string madeOn, string launcher)
    {
        Plan = plan;
        Name = plan.Config.Name;
        LauncherText = launcher;
        MadeOnText = Loc.F(Strings.ConfigMadeOn, madeOn);
        NotesText = plan.Config.Notes;
        WindowsInPrefixText = plan.WindowsInPrefix ? Strings.ConfigWindowsInPrefix : "";
        MissingText = plan.Game == null ? Loc.F(Strings.ConfigNoGame, plan.Config.Name) : "";
        Spots = plan.Spots.Select(SpotRow).ToList();
        CanImport = plan.Game != null && plan.Spots.Any(spot => spot.State is ConfigSpotState.Found or ConfigSpotState.NotYet);
        isChecked = CanImport;
    }

    /// <summary>Il gioco del file abbinato a uno di questo computer, con i suoi posti.</summary>
    public ConfigGamePlan Plan { get; }

    public string Name { get; }

    /// <summary>Il launcher di questo computer (o quello del file, se il gioco non c'è).</summary>
    public string LauncherText { get; }

    public bool HasLauncherText => LauncherText != "";

    public string MadeOnText { get; }

    public string NotesText { get; }

    public bool HasNotes => NotesText != "";

    public string WindowsInPrefixText { get; }

    public bool HasWindowsInPrefix => WindowsInPrefixText != "";

    /// <summary>Il gioco non è su questo computer: la riga lo dice e non c'è casella.</summary>
    public string MissingText { get; }

    public bool HasGame => MissingText == "";

    /// <summary>true se almeno un posto si può importare: solo allora la casella si può accendere.</summary>
    public bool CanImport { get; }

    public IReadOnlyList<ConfigSpotRow> Spots { get; }

    /// <summary>Se il gioco va importato alla conferma: di partenza, se almeno un posto si può importare.</summary>
    [ObservableProperty]
    private bool isChecked;

    private static ConfigSpotRow SpotRow(ConfigSpotPlan spot)
    {
        string text = spot.Folder == ""
            ? "{" + spot.Spot.Root + "}" + (spot.Spot.Path == "" ? "" : "/" + spot.Spot.Path)
            : spot.Spot.Pattern == "" ? spot.Folder : Loc.F(Strings.SpotFiles, spot.Folder, spot.Spot.Pattern);

        string state = spot.State switch
        {
            ConfigSpotState.Found => "✓ " + Strings.ConfigFound,
            ConfigSpotState.NotYet => "⏳ " + Strings.ConfigNotYet,
            ConfigSpotState.Refused => "✗ " + Loc.F(Strings.ConfigRefused, spot.Reason),
            _ => "— " + Strings.ConfigNotForThisSystem,
        };

        return new ConfigSpotRow(text, state);
    }
}
