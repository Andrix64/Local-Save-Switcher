using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lss.App.ViewModels;

/// <summary>Una persona nella scelta "Chi sei?": iniziale sul suo colore.</summary>
public sealed record PersonTile(Person Person, string Initial, IBrush Brush);

/// <summary>
/// "Chi sei?": si sceglie un amministratore. Solo gli admin compaiono, e
/// scegliere un altro è rifiutato. Un admin con PIN apre il tastierino (Pad);
/// il risultato è Chosen, o null se si annulla.
/// </summary>
public partial class AdminPickerViewModel : ObservableObject
{
    private readonly People people;

    /// <param name="known">L'admin già noto (KnownAdmin): niente scelta, solo il suo tastierino.</param>
    internal AdminPickerViewModel(People people, Person? known = null)
    {
        this.people = people;

        Tiles = (known != null ? [known] : people.Admins)
            .Select(person => new PersonTile(
                person,
                person.Name.Length > 0 ? char.ToUpperInvariant(person.Name[0]).ToString() : "?",
                new SolidColorBrush(Color.Parse(person.Color))))
            .ToList();

        OnlyPin = known != null;

        if (known != null)
            Choose(known);
    }

    /// <summary>
    /// L'admin che autorizza senza chiedere "Chi sei?": chi gioca, se è admin;
    /// senza nessun giocatore scelto, l'unico admin se ce n'è uno solo. Un utente
    /// che gioca deve far scegliere un admin. Null se va scelto.
    /// </summary>
    public static Person? KnownAdmin(People people, Person? current)
    {
        if (current != null)
            return people.Find(current.Id) is { IsAdmin: true } admin ? admin : null;

        return people.Admins.Count == 1 ? people.Admins[0] : null;
    }

    /// <summary>
    /// Autorizza senza finestra: l'admin noto e senza PIN. Null se serve la
    /// finestra (scelta tra più admin, o il PIN).
    /// </summary>
    public static Person? WithoutWindow(People people, Person? current) =>
        KnownAdmin(people, current) is { HasPin: false } admin ? admin : null;

    /// <summary>true se si chiede solo il PIN dell'admin noto: annullarlo chiude tutto.</summary>
    public bool OnlyPin { get; }

    public IReadOnlyList<PersonTile> Tiles { get; }

    public string Title => Strings.AdminPickerTitle;

    public string OnlyAdminText => Strings.AdminOnlyText;

    public string CancelText => Strings.AdminPickerCancel;

    /// <summary>L'amministratore scelto, o null.</summary>
    public Person? Chosen { get; private set; }

    /// <summary>Il tastierino aperto per un admin con PIN, o null.</summary>
    [ObservableProperty]
    private PinPadViewModel? pad;

    public bool PadOpen => Pad != null;

    partial void OnPadChanged(PinPadViewModel? value) => OnPropertyChanged(nameof(PadOpen));

    /// <summary>
    /// Sceglie una persona, se è tra gli amministratori. Senza PIN è scelta
    /// subito; con PIN apre il tastierino. Ritorna false se non è un amministratore.
    /// </summary>
    public bool Choose(Person person)
    {
        if (Tiles.All(tile => tile.Person.Id != person.Id))
            return false;

        if (person.HasPin)
            Pad = new PinPadViewModel(people, person);
        else
            Chosen = person;

        return true;
    }

    /// <summary>Il PIN è giusto: l'amministratore del tastierino è scelto.</summary>
    public bool CompletePin()
    {
        if (Pad?.Accepted != true)
            return false;

        Chosen = Pad.Admin;
        return true;
    }

    /// <summary>Chiude il tastierino senza scegliere.</summary>
    public void CancelPin() => Pad = null;
}
