using Avalonia.Media;

namespace Lss.App.ViewModels;

/// <summary>Una persona nella scelta "Chi sei?": iniziale sul suo colore.</summary>
public sealed record AdminTile(Person Person, string Initial, IBrush Brush);

/// <summary>
/// "Chi sei?": si sceglie un amministratore. Solo gli admin compaiono, e
/// scegliere un altro è rifiutato. Il risultato è Chosen, o null se si annulla.
/// </summary>
public sealed class AdminPickerViewModel
{
    internal AdminPickerViewModel(People people)
    {
        Tiles = people.Admins
            .Select(person => new AdminTile(
                person,
                person.Name.Length > 0 ? char.ToUpperInvariant(person.Name[0]).ToString() : "?",
                new SolidColorBrush(Color.Parse(person.Color))))
            .ToList();
    }

    public IReadOnlyList<AdminTile> Tiles { get; }

    public string Title => Strings.AdminPickerTitle;

    public string OnlyAdminText => Strings.AdminOnlyText;

    public string CancelText => Strings.AdminPickerCancel;

    /// <summary>L'amministratore scelto, o null.</summary>
    public Person? Chosen { get; private set; }

    /// <summary>Sceglie una persona, se è tra gli amministratori. Ritorna false altrimenti.</summary>
    public bool Choose(Person person)
    {
        if (Tiles.All(tile => tile.Person.Id != person.Id))
            return false;

        Chosen = person;
        return true;
    }
}
