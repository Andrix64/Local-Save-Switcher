using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Lss.App.ViewModels;

/// <summary>Per i pallini delle persone nelle viste che legano direttamente una Person.</summary>
public static class PersonConverters
{
    /// <summary>L'iniziale maiuscola del nome, o "?".</summary>
    public static readonly IValueConverter Initial =
        new FuncValueConverter<string?, string>(name => string.IsNullOrEmpty(name) ? "?" : char.ToUpperInvariant(name[0]).ToString());

    /// <summary>Il colore "#RRGGBB" della persona come pennello.</summary>
    public static readonly IValueConverter Brush =
        new FuncValueConverter<string?, IBrush>(color => new SolidColorBrush(Color.Parse(string.IsNullOrEmpty(color) ? "#4A4A50" : color)));
}
