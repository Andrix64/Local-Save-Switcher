using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lss.App.Services;

namespace Lss.App.ViewModels;

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

    /// <param name="dataFolder">Cartella dei dati del gioco (dove sta posti.ini).</param>
    /// <param name="gameName">Nome del gioco, per l'ambito delle sue cartelle.</param>
    public IgnoredFoldersViewModel(string dataFolder, string gameName)
    {
        this.dataFolder = dataFolder;
        this.gameName = gameName;
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

        foreach (IgnoredFolder entry in store.Global)
            Items.Add(new IgnoredFolderItem(entry, Strings.IgnoredScopeAll, Remove));

        foreach (IgnoredFolder entry in store.ForGame(dataFolder))
            Items.Add(new IgnoredFolderItem(entry, gameName, Remove));

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
