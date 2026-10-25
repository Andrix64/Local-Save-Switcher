using Avalonia.Controls;

namespace Lss.App.Services;

/// <summary>
/// Finestre di dialogo e di sistema, separate dai ViewModel per poterli
/// provare con una versione finta.
/// </summary>
public interface IDialogService
{
    /// <summary>Messaggio con un solo pulsante (Close/OK) e, se c'è, un pulsante d'azione: true se è stato premuto.</summary>
    Task<bool> InformAsync(string heading, string text, InfoKind kind, string? actionText = null);

    /// <summary>Domanda con il verbo sul pulsante: true se l'utente conferma.</summary>
    Task<bool> ConfirmAsync(string heading, string text, string verb, bool destructive = false, InfoKind kind = InfoKind.Warning);

    /// <summary>Mostra una finestra modale e restituisce il suo risultato.</summary>
    Task<T?> ShowAsync<T>(Window dialog);

    Task<string?> PickZipToSaveAsync(string title, string suggestedName, string filterName);

    Task<string?> PickZipToOpenAsync(string title, string filterName);

    /// <summary>Sceglie dove salvare una configurazione dei salvataggi (*.lss-config.json). Null se annullato.</summary>
    Task<string?> PickConfigToSaveAsync(string title, string suggestedName);

    /// <summary>Sceglie una configurazione dei salvataggi da importare. Null se annullato.</summary>
    Task<string?> PickConfigToOpenAsync(string title);

    /// <summary>Sceglie un file qualsiasi, partendo da una cartella. Null se annullato.</summary>
    Task<string?> PickFileAsync(string title, string startFolder);

    /// <summary>Sceglie una cartella, partendo da un'altra. Null se annullato.</summary>
    Task<string?> PickFolderAsync(string title, string startFolder);

    /// <summary>Apre una cartella nel file manager. false se non si riesce.</summary>
    Task<bool> OpenFolderAsync(string path);
}
