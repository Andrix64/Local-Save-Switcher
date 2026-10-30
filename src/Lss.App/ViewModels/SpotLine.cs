using CommunityToolkit.Mvvm.Input;

namespace Lss.App.ViewModels;

/// <summary>
/// Una riga dell'elenco "Cartelle dei salvataggi" di una partita: un posto
/// del gioco oltre il primo. RemoveCommand c'è solo per i posti aggiunti da LSS.
/// </summary>
public sealed class SpotLine
{
    public SpotLine(int id, string text, string folder, IAsyncRelayCommand openCommand,
        IAsyncRelayCommand? removeCommand)
    {
        Id = id;
        Text = text;
        Folder = folder;
        OpenCommand = openCommand;
        RemoveCommand = removeCommand;
    }

    /// <summary>Id del posto nel gioco.</summary>
    public int Id { get; }

    /// <summary>Cartella e maschera, come si leggono nell'elenco.</summary>
    public string Text { get; }

    /// <summary>Cartella del posto (quella che si apre).</summary>
    public string Folder { get; }

    public IAsyncRelayCommand OpenCommand { get; }

    /// <summary>Togliere il posto dall'elenco; null per i posti che LSS trova da sé.</summary>
    public IAsyncRelayCommand? RemoveCommand { get; }

    public bool Removable => RemoveCommand != null;
}
