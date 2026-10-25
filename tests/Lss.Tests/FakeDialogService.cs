using Avalonia.Controls;
using Lss.App.Services;

namespace Lss.Tests;

/// <summary>
/// Finestre di dialogo finte: registra le chiamate e risponde con i valori
/// impostati dal test.
/// </summary>
public sealed class FakeDialogService : IDialogService
{
    public List<string> Calls { get; } = [];

    public bool ConfirmAnswer { get; set; }

    public bool InformAnswer { get; set; }

    public string? ZipToSave { get; set; }

    public string? ZipToOpen { get; set; }

    public object? ShowResult { get; set; }

    /// <summary>La persona che l'utente sceglie nel dialogo di scelta (null = annulla).</summary>
    public Person? ChosenPerson { get; set; }

    public Task<Person?> ChoosePersonAsync(string title, string text, IReadOnlyList<Person> people)
    {
        Calls.Add("Choose: " + title);
        return Task.FromResult(ChosenPerson);
    }

    /// <summary>L'admin che l'utente sceglie nella finestra "Chi sei?" (null = annulla).</summary>
    public Person? ChosenAdmin { get; set; }

    public Task<Person?> ChooseAdminAsync(People people)
    {
        Calls.Add("Admin");
        return Task.FromResult(ChosenAdmin);
    }

    /// <summary>Il testo che l'utente scrive nella finestra di richiesta (null = annulla).</summary>
    public string? PromptAnswer { get; set; }

    public Task<string?> PromptAsync(string title, string caption, string hint, string confirm)
    {
        Calls.Add("Prompt: " + title);
        return Task.FromResult(PromptAnswer);
    }

    public Task<bool> InformAsync(string heading, string text, InfoKind kind, string? actionText = null)
    {
        Calls.Add("Inform: " + heading);
        return Task.FromResult(InformAnswer);
    }

    public Task<bool> ConfirmAsync(string heading, string text, string verb, bool destructive = false, InfoKind kind = InfoKind.Warning)
    {
        Calls.Add("Confirm: " + heading);
        return Task.FromResult(ConfirmAnswer);
    }

    public Task<T?> ShowAsync<T>(Window dialog)
    {
        Calls.Add("Show: " + typeof(T).Name);
        return Task.FromResult((T?)ShowResult);
    }

    public Task<string?> PickZipToSaveAsync(string title, string suggestedName, string filterName)
    {
        Calls.Add("Save: " + suggestedName);
        return Task.FromResult(ZipToSave);
    }

    public Task<string?> PickZipToOpenAsync(string title, string filterName)
    {
        Calls.Add("Open");
        return Task.FromResult(ZipToOpen);
    }

    public string? ConfigToSave { get; set; }

    public string? ConfigToOpen { get; set; }

    public Task<string?> PickConfigToSaveAsync(string title, string suggestedName)
    {
        Calls.Add("SaveConfig: " + suggestedName);
        return Task.FromResult(ConfigToSave);
    }

    public Task<string?> PickConfigToOpenAsync(string title)
    {
        Calls.Add("OpenConfig");
        return Task.FromResult(ConfigToOpen);
    }

    public string? FileToPick { get; set; }

    public string? FolderToPick { get; set; }

    public Task<string?> PickFileAsync(string title, string startFolder)
    {
        Calls.Add("PickFile");
        return Task.FromResult(FileToPick);
    }

    public Task<string?> PickFolderAsync(string title, string startFolder)
    {
        Calls.Add("PickFolder: " + startFolder);
        return Task.FromResult(FolderToPick);
    }

    public Task<bool> OpenFolderAsync(string path)
    {
        Calls.Add("Folder: " + path);
        return Task.FromResult(true);
    }
}
