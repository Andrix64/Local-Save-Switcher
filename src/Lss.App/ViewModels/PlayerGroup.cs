using Avalonia.Media;

namespace Lss.App.ViewModels;

/// <summary>
/// Una persona e le sue partite di un gioco: un'intestazione nell'elenco
/// (pallino colorato, nome), poi le partite con la preferita per prima.
/// </summary>
public sealed record PlayerGroup(
    Person Person,
    string Initial,
    IBrush Brush,
    IReadOnlyList<PlaythroughViewModel> Cards)
{
    /// <summary>"2 partite" accanto al nome.</summary>
    public string CountText => Cards.Count == 1 ? Strings.PlaythroughCountOne : Loc.F(Strings.PlaythroughCount, Cards.Count);
}
