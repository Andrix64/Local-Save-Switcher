using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lss.App.ViewModels;

/// <summary>Cosa fare delle partite di chi si toglie: darle a qualcuno (GiveTo) o eliminarle (null).</summary>
public sealed record RemovalChoice(string? GiveTo);

/// <summary>
/// "Rimuovere …?": una domanda sola. Le partite si danno a un'altra persona (la
/// prima, all'inizio) o si eliminano (si recuperano dalle partite eliminate).
/// </summary>
public sealed partial class RemovePlayerViewModel : ObservableObject
{
    internal RemovePlayerViewModel(Person person, IReadOnlyList<Person> others)
    {
        Person = person;
        Initial = person.Name.Length > 0 ? char.ToUpperInvariant(person.Name[0]).ToString() : "?";
        Brush = new SolidColorBrush(Color.Parse(person.Color));
        Others = others
            .Select(other => new PersonTile(other,
                other.Name.Length > 0 ? char.ToUpperInvariant(other.Name[0]).ToString() : "?",
                new SolidColorBrush(Color.Parse(other.Color))))
            .ToList();
        selectedOther = Others.FirstOrDefault();
        giveAway = Others.Count > 0;
    }

    public Person Person { get; }

    public string Initial { get; }

    public IBrush Brush { get; }

    public IReadOnlyList<PersonTile> Others { get; }

    public string Title => Loc.F(Strings.RemovePlayerTitle, Person.Name);

    public string Question => Loc.F(Strings.RemovePlayerText, Person.Name);

    public string Verb => Loc.F(Strings.RemovePlayerVerb, Person.Name);

    /// <summary>true: si danno a SelectedOther; false: si eliminano.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteThem))]
    private bool giveAway;

    public bool DeleteThem
    {
        get => !GiveAway;
        set => GiveAway = !value;
    }

    [ObservableProperty]
    private PersonTile? selectedOther;

    /// <summary>La scelta fatta con "Rimuovi …".</summary>
    public RemovalChoice Choice => new(GiveAway ? SelectedOther?.Person.Id : null);
}
