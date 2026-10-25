namespace Lss;

/// <summary>
/// Cartelle dell'utente usate per trovare i salvataggi. "" se non esistono
/// su questo sistema (per esempio AppData su Linux).
/// </summary>
public sealed record KnownFolders(
    string Home, string AppData, string LocalAppData, string LocalAppDataLow,
    string Documents, string SavedGames, string Public, string ProgramData,
    string XdgData, string XdgConfig, string UserName);
