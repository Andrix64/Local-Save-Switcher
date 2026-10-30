using Avalonia.Controls;

namespace Lss.App.Services;

/// <summary>
/// La finestra vera dietro IWindowControl. Mai Topmost: un gioco avviato da LSS deve restare davanti.
/// </summary>
internal sealed class WindowControl : IWindowControl
{
    private readonly Window window;

    // Lo stato di prima dello schermo intero: normale o ingrandita.
    private WindowState before = WindowState.Normal;

    public WindowControl(Window window)
    {
        this.window = window;
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty)
                StateChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public bool IsFullScreen => window.WindowState == WindowState.FullScreen;

    public bool IsMinimized => window.WindowState == WindowState.Minimized;

    public event EventHandler? StateChanged;

    public void EnterFullScreen()
    {
        if (IsFullScreen)
            return;

        if (window.WindowState is WindowState.Normal or WindowState.Maximized)
            before = window.WindowState;

        window.WindowState = WindowState.FullScreen;
    }

    public void LeaveFullScreen()
    {
        if (IsFullScreen)
            window.WindowState = before;
    }

    public void Minimize() => window.WindowState = WindowState.Minimized;

    public void Restore(bool fullScreen)
    {
        if (fullScreen)
            EnterFullScreen();
        else if (IsMinimized)
            window.WindowState = before;

        window.Activate();
    }
}
