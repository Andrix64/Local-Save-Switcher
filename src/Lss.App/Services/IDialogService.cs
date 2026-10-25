using Avalonia.Controls;
using Lss.App.ViewModels;

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

    /// <summary>Scelta tra più azioni (la prima è la principale) e Annulla: l'indice scelto, -1 se si annulla.</summary>
    Task<int> ChooseAsync(string heading, string text, IReadOnlyList<string> options, InfoKind kind = InfoKind.Warning);

    /// <summary>Mostra una finestra modale e restituisce il suo risultato.</summary>
    Task<T?> ShowAsync<T>(Window dialog);

    /// <summary>Sceglie una persona tra quelle date: la persona, o null se si annulla.</summary>
    Task<Person?> ChoosePersonAsync(string title, string text, IReadOnlyList<Person> people, string? cancelText = null);

    /// <summary>
    /// Chi è l'amministratore che autorizza: l'admin scelto (con PIN se ce l'ha), o null. Se
    /// l'admin è già noto (chi gioca è admin, o c'è un solo admin) non si chiede "Chi sei?": solo il PIN, se c'è.
    /// </summary>
    Task<Person?> ChooseAdminAsync(People people, Person? current = null);

    /// <summary>Chiede un testo (un nome), partendo da initial: il testo, o null se si annulla.</summary>
    Task<string?> PromptAsync(string title, string caption, string hint, string confirm, string initial = "");

    /// <summary>"Rimuovere …?": a chi dare le partite (o eliminarle); null se si annulla.</summary>
    Task<RemovalChoice?> ChooseRemovalAsync(Person person, IReadOnlyList<Person> others);

    /// <summary>"PIN di …": imposta o cambia il PIN di un admin. true se è salvato.</summary>
    Task<bool> SetPinAsync(People people, Person admin);

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
