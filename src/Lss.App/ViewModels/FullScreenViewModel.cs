using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>
/// Il pulsante dello schermo intero nell'intestazione della modalità semplice (R7a). Lo stato segue
/// quello vero della finestra, anche quando lo cambia il sistema; ridotta a icona non conta.
/// </summary>
public sealed partial class FullScreenViewModel : ObservableObject
{
    private IWindowControl? window;

    /// <summary>true se la finestra è a schermo intero.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tip))]
    [NotifyPropertyChangedFor(nameof(IsWindowed))]
    private bool isFullScreen;

    public bool IsWindowed => !IsFullScreen;

    /// <summary>"Schermo intero" o "Esci dallo schermo intero", per il tooltip e il nome accessibile.</summary>
    public string Tip => IsFullScreen ? Strings.FullScreenOff : Strings.FullScreenOn;

    /// <summary>La finestra che si comanda: la dà la vista quando la finestra è pronta.</summary>
    internal IWindowControl? Window
    {
        get => window;
        set
        {
            if (window != null)
                window.StateChanged -= OnStateChanged;

            window = value;

            if (window != null)
                window.StateChanged += OnStateChanged;

            Follow();
        }
    }

    private void OnStateChanged(object? sender, EventArgs e) => Follow();

    // Ridotta a icona (mentre gira un gioco) lo stato di prima resta.
    private void Follow()
    {
        if (window != null && !window.IsMinimized)
            IsFullScreen = window.IsFullScreen;
    }

    /// <summary>Entra o esce dallo schermo intero.</summary>
    [RelayCommand]
    internal void Toggle()
    {
        if (window == null)
            return;

        if (window.IsFullScreen)
            window.LeaveFullScreen();
        else
            window.EnterFullScreen();

        Follow();
    }

    /// <summary>Esce dallo schermo intero (passando alla modalità avanzata, o con Esc).</summary>
    internal void Leave()
    {
        window?.LeaveFullScreen();
        Follow();
    }

    /// <summary>
    /// Mentre gira un gioco: se la finestra è a schermo intero si riduce a icona (senza prendere il primo
    /// piano), e a lavoro finito (gioco chiuso, salvataggio fatto) torna davanti a schermo intero.
    /// </summary>
    internal async Task WhileAwayAsync(Func<Task> work)
    {
        bool wasFullScreen = IsFullScreen;

        if (wasFullScreen)
            window?.Minimize();

        try
        {
            await work();
        }
        finally
        {
            if (wasFullScreen)
            {
                window?.Restore(fullScreen: true);
                Follow();
            }
        }
    }
}
