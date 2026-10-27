using System.Globalization;

namespace Lss.App.ViewModels;

/// <summary>Il titolo di un launcher nella colonna dei giochi: una riga a sé, non si seleziona.</summary>
public sealed record SidebarHeader(string Text)
{
    /// <summary>Il titolo di un launcher, in maiuscolo come nel design.</summary>
    public static SidebarHeader For(string launcher) => new(launcher.ToUpper(CultureInfo.CurrentUICulture));
}
