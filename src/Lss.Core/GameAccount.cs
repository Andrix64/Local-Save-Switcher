namespace Lss;

/// <summary>
/// Account di un launcher con i salvataggi di un gioco (per Steam: la cartella
/// userdata\ID).
/// </summary>
/// <param name="Provider">Launcher che ha l'account: "steam" per Steam.</param>
/// <param name="Id">Nome della cartella in userdata (ID dell'account).</param>
/// <param name="Name">Nome Steam, o "" se non si riesce a leggerlo.</param>
/// <param name="Folder">Cartella userdata\ID.</param>
/// <param name="LastSaved">Data del salvataggio più recente, se c'è.</param>
public sealed record GameAccount(string Provider, string Id, string Name, string Folder, DateTime? LastSaved)
{
    public string DisplayName => Name != "" ? Name : Loc.F(Strings.AccountFallbackName, Id);

    /// <summary>Chiave salvata in lss.ini: provider/id.</summary>
    public string Key => Provider + "/" + Id;
}
