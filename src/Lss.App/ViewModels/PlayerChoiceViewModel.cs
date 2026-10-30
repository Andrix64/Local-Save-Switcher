namespace Lss.App.ViewModels;

/// <summary>
/// Scelta di una persona tra alcune (chi riceve una partita, chi gioca...).
/// Il risultato è Chosen, o null se si annulla o se non c'è nessuno da scegliere.
/// </summary>
public sealed class PlayerChoiceViewModel
{
    internal PlayerChoiceViewModel(string title, string text, IReadOnlyList<Person> people, string? cancelText = null)
    {
        Title = title;
        Text = text;
        CancelText = cancelText ?? Strings.CancelChoice;
        Tiles = people
            .Select(person => new PersonTile(
                person,
                person.Name.Length > 0 ? char.ToUpperInvariant(person.Name[0]).ToString() : "?",
                new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(person.Color))))
            .ToList();
    }

    public string Title { get; }

    public string Text { get; }

    public IReadOnlyList<PersonTile> Tiles { get; }

    public string CancelText { get; }

    /// <summary>La persona scelta, o null.</summary>
    public Person? Chosen { get; private set; }

    /// <summary>Sceglie una persona dell'elenco. Ritorna false se non è in elenco.</summary>
    public bool Choose(Person person)
    {
        if (Tiles.All(tile => tile.Person.Id != person.Id))
            return false;

        Chosen = person;
        return true;
    }
}
