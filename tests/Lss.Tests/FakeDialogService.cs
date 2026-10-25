using Avalonia.Controls;
using Lss.App.Services;
using Lss.App.ViewModels;

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

    public Task<Person?> ChoosePersonAsync(string title, string text, IReadOnlyList<Person> people, string? cancelText = null)
    {
        Calls.Add("Choose: " + title);
        return Task.FromResult(ChosenPerson);
    }

    /// <summary>L'admin che l'utente sceglie nella finestra "Chi sei?" (null = annulla).</summary>
    public Person? ChosenAdmin { get; set; }

    // Come il servizio vero: un admin noto e senza PIN non apre la finestra.
    public Task<Person?> ChooseAdminAsync(People people, Person? current = null)
    {
        if (AdminPickerViewModel.WithoutWindow(people, current) is Person known)
            return Task.FromResult<Person?>(known);

        Calls.Add("Admin");
        return Task.FromResult(ChosenAdmin);
    }

    /// <summary>Il testo che l'utente scrive nella finestra di richiesta (null = annulla).</summary>
    public string? PromptAnswer { get; set; }

    public Task<string?> PromptAsync(string title, string caption, string hint, string confirm, string initial = "")
    {
        Calls.Add("Prompt: " + title);
        return Task.FromResult(PromptAnswer);
    }

    /// <summary>La scelta in "Rimuovere …?" (null = annulla).</summary>
    public RemovalChoice? RemovalAnswer { get; set; }

    public Task<RemovalChoice?> ChooseRemovalAsync(Person person, IReadOnlyList<Person> others)
    {
        Calls.Add("Remove: " + person.Name);
        return Task.FromResult(RemovalAnswer);
    }

    /// <summary>L'azione scelta in ChooseAsync (-1 = annulla).</summary>
    public int ChooseAnswer { get; set; } = -1;

    public Task<int> ChooseAsync(string heading, string text, IReadOnlyList<string> options, InfoKind kind = InfoKind.Warning)
    {
        Calls.Add("Choose action: " + heading);
        return Task.FromResult(ChooseAnswer);
    }

    /// <summary>Il PIN che l'utente scrive in "PIN di …" (null = annulla).</summary>
    public string? PinAnswer { get; set; }

    public Task<bool> SetPinAsync(People people, Person admin)
    {
        Calls.Add("SetPin: " + admin.Name);
        return Task.FromResult(PinAnswer != null && people.SetPin(admin.Id, PinAnswer).Success);
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
