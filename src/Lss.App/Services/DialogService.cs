using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Lss.App.Views;

namespace Lss.App.Services;

/// <summary>
/// Finestre di dialogo vere, aperte sopra la finestra principale.
/// </summary>
public sealed class DialogService(Func<Window?> owner) : IDialogService
{
    public async Task<bool> InformAsync(string heading, string text, InfoKind kind, string? actionText = null) =>
        await ShowAsync<bool>(MessageWindow.Inform(heading, text, kind, actionText));

    public async Task<bool> ConfirmAsync(string heading, string text, string verb, bool destructive = false, InfoKind kind = InfoKind.Warning) =>
        await ShowAsync<bool>(MessageWindow.Confirm(heading, text, verb, destructive, kind));

    // Serve una finestra padre: senza, il dialogo non sarebbe modale.
    public async Task<T?> ShowAsync<T>(Window dialog) =>
        await dialog.ShowDialog<T?>(owner() ?? throw new InvalidOperationException("Nessuna finestra principale."));

    public async Task<string?> PickZipToSaveAsync(string title, string suggestedName, string filterName)
    {
        Window? window = owner();

        if (window == null)
            return null;

        IStorageFile? file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = "zip",
            FileTypeChoices = [new FilePickerFileType(filterName) { Patterns = ["*.zip"] }],
            ShowOverwritePrompt = true,
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickZipToOpenAsync(string title, string filterName)
    {
        Window? window = owner();

        if (window == null)
            return null;

        IReadOnlyList<IStorageFile> files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(filterName) { Patterns = ["*.zip"] }],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickConfigToSaveAsync(string title, string suggestedName)
    {
        Window? window = owner();

        if (window == null)
            return null;

        IStorageFile? file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("LSS") { Patterns = ["*.lss-config.json"] }],
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickConfigToOpenAsync(string title)
    {
        Window? window = owner();

        if (window == null)
            return null;

        IReadOnlyList<IStorageFile> files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("LSS") { Patterns = ["*.lss-config.json", "*.json"] }],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFileAsync(string title, string startFolder)
    {
        Window? window = owner();

        if (window == null)
            return null;

        IStorageFolder? start = await window.StorageProvider.TryGetFolderFromPathAsync(new Uri(startFolder));

        IReadOnlyList<IStorageFile> files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFolderAsync(string title, string startFolder)
    {
        Window? window = owner();

        if (window == null)
            return null;

        IStorageFolder? start = startFolder != "" && Directory.Exists(startFolder)
            ? await window.StorageProvider.TryGetFolderFromPathAsync(new Uri(startFolder))
            : null;

        IReadOnlyList<IStorageFolder> folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<bool> OpenFolderAsync(string path)
    {
        Window? window = owner();

        if (window == null)
            return false;

        try
        {
            Directory.CreateDirectory(path);
            return await window.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path));
        }
        catch (Exception ex)
        {
            Log.Write("Apertura cartella fallita", ex);
            return false;
        }
    }
}
