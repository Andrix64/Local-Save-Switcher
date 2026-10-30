using Lss.App.Services;

namespace Lss.Tests;

/// <summary>La finestra principale finta: registra le richieste e fa come la vera.</summary>
public sealed class FakeWindowControl : IWindowControl
{
    public List<string> Calls { get; } = [];

    public bool IsFullScreen { get; private set; }

    public bool IsMinimized { get; private set; }

    public event EventHandler? StateChanged;

    public void EnterFullScreen()
    {
        Calls.Add("EnterFullScreen");
        IsFullScreen = true;
        IsMinimized = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void LeaveFullScreen()
    {
        Calls.Add("LeaveFullScreen");
        IsFullScreen = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Minimize()
    {
        Calls.Add("Minimize");
        IsFullScreen = false;
        IsMinimized = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Restore(bool fullScreen)
    {
        Calls.Add("Restore " + fullScreen);
        IsMinimized = false;
        IsFullScreen = fullScreen;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Il sistema cambia lo stato (per esempio con un tasto o il gestore delle finestre).</summary>
    public void SystemSets(bool fullScreen)
    {
        IsFullScreen = fullScreen;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
