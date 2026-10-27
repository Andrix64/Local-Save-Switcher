using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lss.App.ViewModels;

/// <summary>
/// "PIN di …" dalla finestra Giocatori: nuovo PIN due volte. Se la persona ha già un
/// PIN, per cambiarlo serve quello attuale (per toglierlo no: è la via d'uscita
/// di un PIN dimenticato, dal menu ⋯ della finestra Giocatori, che apre un admin).
/// </summary>
public sealed partial class SetPinViewModel : ObservableObject
{
    private readonly People people;

    internal SetPinViewModel(People people, Person admin)
    {
        this.people = people;
        Admin = admin;
        Initial = admin.Name.Length > 0 ? char.ToUpperInvariant(admin.Name[0]).ToString() : "?";
        Brush = new SolidColorBrush(Color.Parse(admin.Color));
    }

    public Person Admin { get; }

    public string Initial { get; }

    public IBrush Brush { get; }

    public string Title => Loc.F(Strings.PinTitle, Admin.Name);

    /// <summary>true se l'admin ha già un PIN: si chiede quello attuale.</summary>
    public bool NeedsCurrent => Admin.HasPin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string current = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(Mismatch))]
    private string newPin = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(Mismatch))]
    private string again = "";

    /// <summary>Errore dell'ultimo Salva (PIN attuale sbagliato...), o "".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string message = "";

    public bool HasMessage => Message != "";

    /// <summary>Il secondo PIN è completo e diverso dal primo.</summary>
    public bool Mismatch => Again.Length == 4 && Again != NewPin;

    public bool CanSave => IsPin(NewPin) && Again == NewPin && (!NeedsCurrent || IsPin(Current));

    /// <summary>Salva il PIN nuovo. false (con Message) se il PIN attuale è sbagliato o il nuovo non va.</summary>
    public bool Save()
    {
        if (!CanSave)
            return false;

        if (NeedsCurrent && !people.CheckPin(Admin.Id, Current))
        {
            Message = Strings.PinWrong;
            return false;
        }

        Result result = people.SetPin(Admin.Id, NewPin);
        Message = result.Success ? "" : result.Message;
        return result.Success;
    }

    private static bool IsPin(string text) => text.Length == 4 && text.All(char.IsAsciiDigit);
}
