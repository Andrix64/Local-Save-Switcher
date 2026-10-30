namespace Lss.App.ViewModels;

/// <summary>
/// Una cartella segnata come "non di salvataggi", nella finestra delle cartelle
/// che non sono salvataggi: dove vale, quando è stata segnata, e come tornare indietro.
/// </summary>
public sealed class IgnoredFolderItem
{
    public IgnoredFolderItem(IgnoredFolder entry, string scope, Action<IgnoredFolderItem> remove)
    {
        Entry = entry;
        Scope = scope;
        DateText = entry.Added == DateTime.MinValue ? "" : entry.Added.ToString("d", System.Globalization.CultureInfo.CurrentCulture);
        RemoveCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => remove(this));
    }

    public IgnoredFolder Entry { get; }

    /// <summary>La cartella, come si legge nel file (percorso portabile per tutti i giochi).</summary>
    public string Folder => Entry.Folder;

    /// <summary>"tutti i giochi" o il nome del gioco.</summary>
    public string Scope { get; }

    /// <summary>Quando è stata segnata, o "" se non si sa.</summary>
    public string DateText { get; }

    /// <summary>true se vale per tutti i giochi.</summary>
    public bool IsForAll => Entry.GameKey == "";

    public System.Windows.Input.ICommand RemoveCommand { get; }
}
