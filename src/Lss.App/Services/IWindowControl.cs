namespace Lss.App.Services;

/// <summary>
/// La finestra principale vista dal resto dell'app: schermo intero, ridurre a icona e tornare.
/// Una piccola interfaccia, così i test non hanno bisogno di una finestra vera.
/// </summary>
public interface IWindowControl
{
    /// <summary>true se la finestra è a schermo intero.</summary>
    bool IsFullScreen { get; }

    /// <summary>true se la finestra è ridotta a icona.</summary>
    bool IsMinimized { get; }

    /// <summary>Cambiato lo stato della finestra (anche dal sistema).</summary>
    event EventHandler? StateChanged;

    /// <summary>Schermo intero; ricorda se prima era normale o ingrandita.</summary>
    void EnterFullScreen();

    /// <summary>Esce dallo schermo intero, nello stato di prima (normale o ingrandita).</summary>
    void LeaveFullScreen();

    /// <summary>Riduce a icona (mentre gira un gioco).</summary>
    void Minimize();

    /// <summary>Torna in primo piano dopo il gioco, a schermo intero se lo era.</summary>
    void Restore(bool fullScreen);
}
