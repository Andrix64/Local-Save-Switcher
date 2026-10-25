using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Lss.App.Services;

/// <summary>
/// Tiene una finestra dentro l'area di lavoro dello schermo (lo spazio senza la barra
/// delle applicazioni). Sugli schermi piccoli con la scala alta (Steam Deck: 1280×800
/// con scala 1,34) la dimensione minima o quella iniziale di una finestra superano lo
/// spazio: ingrandita, il gestore delle finestre non può fermarla sopra la barra e ci
/// finisce sotto. Minimo e dimensione si riducono allo spazio che c'è davvero.
/// </summary>
internal static class WindowFit
{
    /// <summary>Da chiamare quando la finestra è aperta (serve la cornice).</summary>
    public static void Apply(Window window)
    {
        Screen? screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;

        if (screen is null)
            return;

        Size client = window.ClientSize;
        Size frame = window.FrameSize ?? client;
        double availableWidth = screen.WorkingArea.Width / screen.Scaling - (frame.Width - client.Width);
        double availableHeight = screen.WorkingArea.Height / screen.Scaling - (frame.Height - client.Height);

        bool fits = availableWidth >= window.MinWidth && availableHeight >= window.MinHeight
                    && availableWidth >= client.Width && availableHeight >= client.Height;

        if (fits)
            return;

        Log.Write($"Finestra adattata allo schermo ({window.GetType().Name}): spazio {availableWidth:0}x{availableHeight:0} " +
                  $"punti, area di lavoro {screen.WorkingArea}, scala {screen.Scaling}");

        window.MinWidth = Math.Min(window.MinWidth, availableWidth);
        window.MinHeight = Math.Min(window.MinHeight, availableHeight);

        if (client.Width <= availableWidth && client.Height <= availableHeight)
            return;

        window.SizeToContent = SizeToContent.Manual;
        window.Width = Math.Min(client.Width, availableWidth);
        window.Height = Math.Min(client.Height, availableHeight);
        window.Position = screen.WorkingArea.Position;
    }
}
