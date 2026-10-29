using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>Quali cartelle ignorate mostra la finestra.</summary>
public enum IgnoredScope
{
    /// <summary>Quelle di tutti i giochi e quelle del gioco.</summary>
    Both,

    /// <summary>Solo quelle del gioco (impostazioni del gioco).</summary>
    Game,

    /// <summary>Solo quelle di tutti i giochi (impostazioni di LSS).</summary>
    All,
}

/// <summary>
/// Le cartelle che non sono salvataggi: quelle di tutti i giochi e quelle del
/// gioco aperto nelle impostazioni. "Proponila di nuovo" le toglie dalla lista
/// di esclusione.
/// </summary>
public sealed partial class IgnoredFoldersViewModel : ObservableObject
{
    private readonly IgnoredFolders store = new(AppData.Root);
    private readonly string dataFolder;
    private readonly string gameName;
    private readonly IgnoredScope scope;

    /// <param name="dataFolder">Cartella dei dati del gioco (dove sta posti.json).</param>
    /// <param name="gameName">Nome del gioco, per l'ambito delle sue cartelle.</param>
    /// <param name="scope">Quali cartelle mostrare: di tutti i giochi, del gioco o tutte e due.</param>
    public IgnoredFoldersViewModel(string dataFolder, string gameName, IgnoredScope scope = IgnoredScope.Both)
    {
        this.dataFolder = dataFolder;
        this.gameName = gameName;
        this.scope = scope;
        Refresh();
    }

    public string Title => Strings.IgnoredFoldersTitle;

    public string EmptyText => Strings.IgnoredFoldersEmpty;

    public string CloseText => Strings.Close;

    /// <summary>Le cartelle da mostrare: prima quelle di tutti i giochi, poi quelle del gioco.</summary>
    public ObservableCollection<IgnoredFolderItem> Items { get; } = [];

    /// <summary>true se non c'è nessuna cartella da mostrare.</summary>
    [ObservableProperty]
    private bool isEmpty;

    /// <summary>Rilegge le cartelle dai file.</summary>
    private void Refresh()
    {
        Items.Clear();
        store.Load();

        if (scope != IgnoredScope.Game)
        {
            foreach (IgnoredFolder entry in store.Global)
                Items.Add(new IgnoredFolderItem(entry, Strings.IgnoredScopeAll, Remove));
        }

        if (scope != IgnoredScope.All)
        {
            foreach (IgnoredFolder entry in store.ForGame(dataFolder))
                Items.Add(new IgnoredFolderItem(entry, gameName, Remove));
        }

        IsEmpty = Items.Count == 0;
    }

    /// <summary>
    /// Toglie la cartella dalla lista di esclusione: LSS la potrà proporre di nuovo.
    /// </summary>
    private void Remove(IgnoredFolderItem item)
    {
        if (item.IsForAll)
            store.RemoveGlobal(item.Folder);
        else
            store.RemoveForGame(dataFolder, item.Folder);

        Refresh();
    }
}
