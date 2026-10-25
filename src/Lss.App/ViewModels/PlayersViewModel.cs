using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>Una riga della finestra Giocatori: il nome si modifica nella riga.</summary>
public sealed partial class PlayerRow : ObservableObject
{
    public PlayerRow(Person person, string initial, IBrush brush)
    {
        Person = person;
        Initial = initial;
        Brush = brush;
        editName = person.Name;
    }

    public Person Person { get; }

    public string Initial { get; }

    public IBrush Brush { get; }

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

    public Result Rename(Person person, string name) => Done(people.Rename(person.Id, name));

    public Result SetColor(Person person, string color) => Done(people.SetColor(person.Id, color));

    /// <summary>Passa al colore successivo della tavolozza.</summary>
    public Result NextColor(Person person)
    {
        int index = People.Palette.ToList().IndexOf(person.Color);
        return SetColor(person, People.Palette[(index + 1) % People.Palette.Count]);
    }

    public Result SetAdmin(Person person, bool isAdmin) => Done(people.SetAdmin(person.Id, isAdmin));

    /// <summary>Sposta una persona di un posto in su (-1) o in giù (+1).</summary>
    public Result Move(Person person, int step)
    {
        int index = people.All.ToList().FindIndex(p => p.Id == person.Id);
        return Done(people.Move(person.Id, index + step));
    }

    /// <summary>
    /// Rimozione in due passi: prima la scelta "Elimina le sue partite", poi (se si
    /// dice no) a chi darle. Annullando, non cambia nulla.
    /// </summary>
    public async Task<Result> RemoveAsync(Person person)
    {
        if (people.All.Count == 1)
            return Done(Result.Fail(Strings.LastPlayer));

        bool delete = await dialogs.ConfirmAsync(
            Loc.F(Strings.RemovePlayerText, person.Name),
            Strings.DeleteThemText,
            Strings.DeleteThemVerb,
            destructive: true);

        if (delete)
            return Done(removePlayer(person, null));

        IReadOnlyList<Person> others = people.All.Where(other => other.Id != person.Id).ToList();
        Person? giveTo = await dialogs.ChoosePersonAsync(Loc.F(Strings.RemovePlayerText, person.Name),
            Strings.GiveToText, others);

        return giveTo == null ? Result.Ok("") : Done(removePlayer(person, giveTo.Id));
    }

    // Ogni modifica aggiorna le righe; un errore resta in Message.
    private Result Done(Result result)
    {
        Refresh();
        Message = result.Success ? "" : result.Message;
        return result;
    }
}
