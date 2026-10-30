namespace Lss;

/// <summary>
/// Un posto in cui il gioco salva: una cartella intera (Pattern "") o solo
/// i file della cartella che corrispondono a Pattern ("*.sav;slot?.dat").
/// Recursive vale solo per i pattern; per una cartella intera è sempre vero.
/// </summary>
/// <param name="Id">Numero del posto nel gioco: 1 è il primo, mai riusato.</param>
/// <param name="Folder">Cartella del posto (per un pattern, la cartella che contiene i file).</param>
/// <param name="Pattern">Maschere dei file separate da ";", o "" per la cartella intera.</param>
/// <param name="Recursive">Se i file del pattern vanno cercati anche nelle sottocartelle.</param>
/// <param name="PortablePath">Percorso con le radici tra graffe ({WinAppDataLocal}/Discord), vedi SavePathResolver.ToPortable.</param>
/// <param name="Source">Da dove viene il posto.</param>
public sealed record SaveSpot(int Id, string Folder, string Pattern, bool Recursive,
    string PortablePath, SaveSource Source)
{
    /// <summary>true se il posto è una cartella intera.</summary>
    public bool IsWholeFolder => Pattern == "";
}
