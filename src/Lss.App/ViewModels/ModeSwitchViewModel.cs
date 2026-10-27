using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lss.App.ViewModels;

/// <summary>
/// L'interruttore "Semplice | Avanzata" dell'intestazione (R7), uguale nelle due modalità. La modalità
/// la cambia chi lo possiede (MainWindowViewModel): un passaggio rifiutato o annullato lascia
/// l'interruttore com'era. Spento mentre si gioca dalla modalità semplice o un'operazione è in corso.
/// </summary>
public sealed partial class ModeSwitchViewModel : ObservableObject
{
    private readonly Func<Task> toSimple;
    private readonly Func<Task> toAdvanced;
    private readonly Func<bool> canSwitch;

    internal ModeSwitchViewModel(bool isSimple, Func<Task> toSimple, Func<Task> toAdvanced, Func<bool> canSwitch)
    {
        this.isSimple = isSimple;
        this.toSimple = toSimple;
        this.toAdvanced = toAdvanced;
        this.canSwitch = canSwitch;
    }

    /// <summary>true se la modalità attuale è la semplice.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdvanced))]
    private bool isSimple;

    public bool IsAdvanced => !IsSimple;

    public string SimpleText => Strings.ModeSimpleTitle;

    public string AdvancedText => Strings.ModeAdvancedTitle;

    public string SimpleTip => Strings.SwitchToSimple;

    public string AdvancedTip => Strings.SwitchToAdvanced;

    /// <summary>false mentre si gioca dalla modalità semplice o un'operazione è in corso.</summary>
    public bool Enabled => canSwitch();

    /// <summary>Da chiamare quando cambia ciò che decide Enabled.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Enabled));
        ShowSimpleCommand.NotifyCanExecuteChanged();
        ShowAdvancedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task ShowSimpleAsync()
    {
        if (!IsSimple)
            await toSimple();

        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanChange))]
    private async Task ShowAdvancedAsync()
    {
        if (IsSimple)
            await toAdvanced();

        Refresh();
    }

    private bool CanChange() => canSwitch();
}
