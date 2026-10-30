namespace Lss;

/// <summary>
/// Regola di un launcher: dove sono i salvataggi di un gioco. Root è uno dei
/// nomi della tabella delle radici (gameinstall, WinMyDocuments...).
/// </summary>
/// <param name="Root">Nome della radice, come lo scrive Steam.</param>
/// <param name="Path">Sottocartella sotto la radice, con "/" e tokens come {64BitSteamID}.</param>
/// <param name="Pattern">Nome o maschera dei file ("*" se non c'è).</param>
/// <param name="Recursive">true se valgono anche le sottocartelle.</param>
/// <param name="Platforms">Sistemi della regola (Windows, Linux, MacOS); vuoto = tutti.</param>
public sealed record SaveRule(string Root, string Path, string Pattern, bool Recursive,
    IReadOnlyList<string> Platforms);

/// <summary>
/// Sostituzione di una radice su un altro sistema (rootoverrides di Steam).
/// </summary>
/// <param name="Root">Radice che viene sostituita.</param>
/// <param name="Os">Sistema della sostituzione (Linux, MacOS).</param>
/// <param name="OsCompare">Confronto con il sistema: "=" è l'unico usato.</param>
/// <param name="UseInstead">Radice da usare al suo posto.</param>
/// <param name="AddPath">Cartella da mettere davanti al percorso, o "".</param>
/// <param name="Transforms">Sostituzioni di testo nel percorso (find → replace).</param>
public sealed record RootOverride(string Root, string Os, string OsCompare, string UseInstead,
    string AddPath, IReadOnlyList<(string Find, string Replace)> Transforms);
