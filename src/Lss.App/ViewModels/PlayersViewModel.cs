using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>Un colore della tavolozza nella riga di un giocatore.</summary>
public sealed record ColourSwatch(string Color, IBrush Brush, bool Selected);

/// <summary>Una riga della finestra Giocatori.</summary>
public sealed partial class PlayerRow : ObservableObject
{
    public PlayerRow(Person person, string initial, IBrush brush)
    {
        Person = person;
        Initial = initial;
        Brush = brush;
        editName = person.Name;
        Swatches = People.Palette
            .Select(color => new ColourSwatch(color, new SolidColorBrush(Color.Parse(color)), color == person.Color))
            .ToList();
    }

    public Person Person { get; }

    public string Initial { get; }

    public IBrush Brush { get; }

    /// <summary>I colori da scegliere: quello della persona è segnato.</summary>
    public IReadOnlyList<ColourSwatch> Swatches { get; }

    /// <summary>"Admin" o "Utente", accanto alla casella.</summary>
    public string RoleText => Person.IsAdmin ? Strings.PlayerAdmin : Strings.PlayerUser;

    /// <summary>"Imposta PIN…" o "Cambia PIN…": per ogni persona.</summary>
    public string PinActionText => Person.HasPin ? Strings.PinChangeMenu : Strings.PinSetMenu;

    /// <summary>Il PIN si può togliere solo a un utente: un amministratore deve averlo.</summary>
    public bool CanRemovePin => Person.HasPin && !Person.IsAdmin;

    /// <summary>Il nome che l'utente sta scrivendo (si salva con Rinomina).</summary>
    [ObservableProperty]
    private string editName = "";
}

/// <summary>
/// Finestra "Giocatori…": nomi, colori, amministratori, ordine, aggiunta e
/// rimozione. Le regole (almeno un giocatore, almeno un admin) stanno in People.
/// Togliere una persona toglie le sue partite: a un'altra persona, o eliminate.
/// </summary>
public sealed partial class PlayersViewModel : ObservableObject
{
    private readonly People people;
    private readonly IDialogService dialogs;
    private readonly Func<Person, string?, Result> removePlayer;

    /// <param name="removePlayer">Toglie le partite della persona (a chi, o eliminate: null) e poi la persona.</param>
    internal PlayersViewModel(People people, IDialogService dialogs, Func<Person, string?, Result> removePlayer)
    {
        this.people = people;
        this.dialogs = dialogs;
        this.removePlayer = removePlayer;
        Refresh();
    }

    public ObservableCollection<PlayerRow> Rows { get; } = [];

    public IReadOnlyList<string> Palette => People.Palette;

    /// <summary>Messaggio dell'ultima operazione che non è riuscita, o "".</summary>
    [ObservableProperty]
    private string message = "";

    public bool HasMessage => Message != "";

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    /// <summary>Ricostruisce le righe dalle persone.</summary>
    public void Refresh()
    {
        Rows.Clear();

        foreach (Person person in people.All)
        {
            Rows.Add(new PlayerRow(person,
                person.Name.Length > 0 ? char.ToUpperInvariant(person.Name[0]).ToString() : "?",
                new SolidColorBrush(Color.Parse(person.Color))));
        }
    }

    /// <summary>Aggiunge una persona (utente). Il nome vuoto o doppio si rifiuta.</summary>
    public Result Add(string name)
    {
        Result<Person> added = people.Add(name);
        return Done(added.Success ? Result.Ok("") : Result.Fail(added.Message));
    }

    /// <summary>"+ Aggiungi giocatore": chiede il nome e aggiunge. Annullando, non cambia nulla.</summary>
    public async Task<Result> AddAsync()
    {
        string? name = await dialogs.PromptAsync(Strings.AddPlayer, Strings.PlayerNameCaption, Strings.PlayerNameCaption,
            Strings.AddPlayer);

        return name == null ? Result.Ok("") : Add(name);
    }

    public Result Rename(Person person, string name) => Done(people.Rename(person.Id, name));

    /// <summary>"Rinomina…" dal menu ⋯: chiede il nome nuovo partendo da quello attuale.</summary>
    public async Task<Result> RenameAsync(Person person)
    {
        string? name = await dialogs.PromptAsync(Strings.RenameMenu, Strings.PlayerNameCaption, Strings.PlayerNameCaption,
            Strings.RenameMenu.TrimEnd('.', '…'), person.Name);

        return name == null || name == person.Name ? Result.Ok("") : Rename(person, name);
    }

    /// <summary>"Imposta PIN…" o "Cambia PIN…" di una persona.</summary>
    public async Task SetPinAsync(Person person)
    {
        await dialogs.SetPinAsync(people, person);
        Done(Result.Ok(""));
    }

    /// <summary>"Togli il PIN": non chiede quello attuale (un PIN dimenticato si toglie così).</summary>
    public Result RemovePin(Person person) => Done(people.RemovePin(person.Id));

    public Result SetColor(Person person, string color) => Done(people.SetColor(person.Id, color));

    /// <summary>Passa al colore successivo della tavolozza.</summary>
    public Result NextColor(Person person)
    {
        int index = People.Palette.ToList().IndexOf(person.Color);
        return SetColor(person, People.Palette[(index + 1) % People.Palette.Count]);
    }

    /// <summary>Rende admin o utente. Per diventare admin serve il PIN: si chiede subito, e senza non si cambia nulla.</summary>
    public async Task<Result> SetAdminAsync(Person person, bool isAdmin)
    {
        if (isAdmin && !person.HasPin && !await dialogs.SetPinAsync(people, person))
            return Done(Result.Fail(Strings.AdminNeedsPin));

        return Done(people.SetAdmin(person.Id, isAdmin));
    }

    /// <summary>Sposta una persona di un posto in su (-1) o in giù (+1).</summary>
    public Result Move(Person person, int step)
    {
        int index = people.All.ToList().FindIndex(p => p.Id == person.Id);
        return Done(people.Move(person.Id, index + step));
    }

    /// <summary>
    /// "Rimuovere …?": una domanda sola, dare le partite a qualcuno o eliminarle.
    /// Annullando, non cambia nulla.
    /// </summary>
    public async Task<Result> RemoveAsync(Person person)
    {
        if (people.All.Count == 1)
            return Done(Result.Fail(Strings.LastPlayer));

        IReadOnlyList<Person> others = people.All.Where(other => other.Id != person.Id).ToList();
        RemovalChoice? choice = await dialogs.ChooseRemovalAsync(person, others);

        return choice == null ? Result.Ok("") : Done(removePlayer(person, choice.GiveTo));
    }

    // Ogni modifica aggiorna le righe; un errore resta in Message.
    private Result Done(Result result)
    {
        Refresh();
        Message = result.Success ? "" : result.Message;
        return result;
    }
}
