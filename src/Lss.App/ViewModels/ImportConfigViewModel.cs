using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lss.App.ViewModels;

/// <summary>
/// Importazione di una configurazione dei salvataggi (N9c): un blocco per gioco del file.
/// Importa solo i giochi con la casella accesa, uno alla volta, con apply (vedi
/// GameViewModel.AddSpotWithChoiceAsync per le partite). Annulla non scrive nulla.
/// </summary>
public partial class ImportConfigViewModel : ObservableObject
{
    private readonly Func<ConfigGamePlan, Task<int>> apply;

    /// <param name="launcherOf">Il nome del launcher di un gioco di questo computer.</param>
    /// <param name="apply">Scrive i posti di un gioco e dice quanti ne ha aggiunti.</param>
    public ImportConfigViewModel(SaveConfigFile file, IReadOnlyList<ConfigGamePlan> plans,
        Func<GameInfo, string> launcherOf, Func<ConfigGamePlan, Task<int>> apply)
    {
        this.apply = apply;

        Games = new ObservableCollection<ConfigGameItem>(plans.Select(plan => new ConfigGameItem(plan, file.Created,
            plan.Game != null ? launcherOf(plan.Game) : plan.Config.Provider)));
    }

    /// <summary>Esito della finestra: true se si è premuto Importa.</summary>
    public event Action<bool>? Finished;

    public string Title => Strings.ImportConfigTitle;

    public ObservableCollection<ConfigGameItem> Games { get; }

    /// <summary>Quanti giochi hanno avuto almeno un posto aggiunto. Vale dopo Importa.</summary>
    public int Applied { get; private set; }

    public string ImportText => Strings.ImportConfigNow;

    public string CancelText => Strings.Cancel;

    /// <summary>Importa i giochi con la casella accesa e chiude la finestra.</summary>
    [RelayCommand]
    private async Task ImportAsync()
    {
        int games = 0;

        foreach (ConfigGameItem item in Games.Where(item => item.IsChecked && item.CanImport).ToList())
        {
            if (await apply(item.Plan) > 0)
                games++;
        }

        Applied = games;
        Finished?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => Finished?.Invoke(false);
}
