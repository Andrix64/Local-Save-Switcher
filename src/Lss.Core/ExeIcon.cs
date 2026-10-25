using System.Buffers.Binary;

namespace Lss;

/// <summary>
/// L'icona dentro un eseguibile Windows (.exe), letta a mano dal formato PE: nessun
/// DllImport e nessun pacchetto, e funziona anche su Linux (giochi Proton, Heroic,
/// aggiunti a mano). Il file si apre in sola lettura e non si esegue mai.
/// </summary>
internal static class ExeIcon
{
    // Un eseguibile più grande di così non si legge (e un'icona non lo è mai).
    private const long MaxFileSize = 1L << 30;
    private const long MaxResourceSize = 64L << 20;

    private const uint RtIcon = 3;
    private const uint RtGroupIcon = 14;
    private const uint HighBit = 0x80000000;

    // Dimensione di una voce della tabella delle sezioni, e lingua (inglese USA).
    private const int SectionSize = 40;
    private const int LanguageId = 0x409;

    // File già segnalati nel log: lo stesso errore non si ripete a ogni avvio.
    private static readonly HashSet<string> reported = [];

    // Una voce di una cartella delle risorse: id (o nome) e posizione relativa alla sezione.
    private sealed record Entry(uint Id, long Offset, bool IsDirectory);

    // Il file e le sezioni letti, con l'inizio della sezione delle risorse.
    private sealed record Layout(FileStream Stream, long ResourceBase, byte[] Sections, int SectionCount);

    /// <summary>
    /// L'icona più grande dentro un eseguibile Windows, come file .ico con una
    /// sola immagine; null se non c'è o il file non si legge. Non lancia mai.
    /// </summary>
    public static byte[]? Extract(string exePath)
    {
        try
        {
            var info = new FileInfo(exePath);

            if (!info.Exists || info.Length > MaxFileSize)
                return null;

            using var stream = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            return Icon(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            Report(exePath, ex);
            return null;
        }
    }

    private static byte[]? Icon(FileStream stream)
    {
        // Intestazione DOS: "MZ" e l'offset dell'intestazione PE.
        byte[] dos = Read(stream, 0, 64);

        if (dos[0] != 'M' || dos[1] != 'Z')
            return null;

        long pe = U32(dos, 0x3C);

        if (!Read(stream, pe, 4).AsSpan().SequenceEqual("PE\0\0"u8))
            return null;

        // Intestazione del file: numero di sezioni e dimensione dell'intestazione opzionale.
        byte[] coff = Read(stream, pe + 4, 20);
        int sectionCount = U16(coff, 2);
        int optionalSize = U16(coff, 16);
        long optional = pe + 24;

        byte[] header = Read(stream, optional, optionalSize);

        // PE32 (0x10B) e PE32+ (0x20B) mettono le directory dei dati in posizioni diverse.
        int directories = U16(header, 0) switch
        {
            0x10B => 96,
            0x20B => 112,
            _ => throw new InvalidDataException("Formato PE sconosciuto"),
        };

        if (header.Length < directories + 3 * 8)
            throw new InvalidDataException("Intestazione opzionale troppo corta");

        // La directory delle risorse è la numero 2 (RVA e dimensione).
        if (U32(header, directories - 4) <= 2)
            return null;

        uint resourceRva = U32(header, directories + 16);

        if (resourceRva == 0)
            return null;

        byte[] sections = Read(stream, optional + optionalSize, sectionCount * SectionSize);
        var layout = new Layout(stream, FileOffset(sections, sectionCount, resourceRva), sections, sectionCount);

        return IconFromResources(layout);
    }

    /// <summary>
    /// L'icona più grande del gruppo (RT_GROUP_ICON), con i suoi dati, come .ico.
    /// </summary>
    private static byte[]? IconFromResources(Layout layout)
    {
        byte[]? group = Resource(layout, RtGroupIcon, null);

        if (group is null || group.Length < 6)
            return null;

        int count = U16(group, 4);

        if (group.Length < 6 + count * 14)
            throw new InvalidDataException("Gruppo di icone troncato");

        if (count == 0)
            return null;

        // Voci di 14 byte: larghezza, altezza, colori, riservato, piani, bit, dimensione, id.
        byte[] best = Enumerable.Range(0, count)
            .Select(i => group.AsSpan(6 + i * 14, 14).ToArray())
            .OrderByDescending(entry => Side(entry[0]) * Side(entry[1]))
            .ThenByDescending(entry => U16(entry, 6))
            .First();

        byte[]? image = Resource(layout, RtIcon, U16(best, 12));

        if (image is null)
            return null;

        // Un .ico con una sola immagine: intestazione, una voce di 16 byte, poi l'immagine.
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write(best, 0, 4);
            writer.Write(U16(best, 4));
            writer.Write(U16(best, 6));
            writer.Write((uint)image.Length);
            writer.Write((uint)22);
            writer.Write(image);
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Dati di una risorsa: tipo, nome (o il primo, se name è null) e prima lingua.
    /// </summary>
    private static byte[]? Resource(Layout layout, uint type, uint? name)
    {
        if (Entries(layout, 0).FirstOrDefault(entry => entry.Id == type && entry.IsDirectory) is not { } typeDir)
            return null;

        if (Entries(layout, typeDir.Offset).FirstOrDefault(entry => name is null || entry.Id == name) is not { IsDirectory: true } nameDir)
            return null;

        if (Entries(layout, nameDir.Offset).FirstOrDefault() is not { IsDirectory: false } leaf)
            return null;

        return Data(layout, leaf.Offset);
    }

    // Voci di una cartella delle risorse (offset relativi alla sezione delle risorse).
    private static List<Entry> Entries(Layout layout, long directory)
    {
        byte[] head = Read(layout.Stream, layout.ResourceBase + directory, 16);
        int count = U16(head, 12) + U16(head, 14);
        byte[] list = Read(layout.Stream, layout.ResourceBase + directory + 16, count * 8);
        var entries = new List<Entry>(count);

        for (int i = 0; i < count; i++)
        {
            uint offset = U32(list, i * 8 + 4);
            entries.Add(new Entry(U32(list, i * 8), offset & ~HighBit, (offset & HighBit) != 0));
        }

        return entries;
    }

    // Dati di una voce foglia: RVA e dimensione, letti dalla sezione in cui stanno.
    private static byte[] Data(Layout layout, long leaf)
    {
        byte[] entry = Read(layout.Stream, layout.ResourceBase + leaf, 16);
        uint size = U32(entry, 4);

        return Read(layout.Stream, FileOffset(layout.Sections, layout.SectionCount, U32(entry, 0)), size);
    }

    // Posizione nel file di un RVA, cercando la sezione che lo contiene.
    private static long FileOffset(byte[] sections, int count, uint rva)
    {
        for (int i = 0; i < count; i++)
        {
            int at = i * SectionSize;
            uint address = U32(sections, at + 12);
            uint size = Math.Max(U32(sections, at + 8), U32(sections, at + 16));

            if (rva >= address && rva - address < size)
                return U32(sections, at + 20) + (rva - address);
        }

        throw new InvalidDataException("RVA fuori dalle sezioni");
    }

    // Byte dal file: se va fuori dal file o è troppo grande, il file è rotto.
    private static byte[] Read(FileStream stream, long offset, long count)
    {
        if (offset < 0 || count < 0 || count > MaxResourceSize || offset + count > stream.Length)
            throw new InvalidDataException("Dati fuori dal file");

        var buffer = new byte[count];
        stream.Seek(offset, SeekOrigin.Begin);
        stream.ReadExactly(buffer);

        return buffer;
    }

    // Lato di un'icona: 0 vuol dire 256.
    private static int Side(byte value) => value == 0 ? 256 : value;

    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));

    // Un file rotto si segnala una volta sola.
    private static void Report(string exePath, Exception ex)
    {
        lock (reported)
        {
            if (!reported.Add(exePath))
                return;
        }

        Log.Write("Icona dell'eseguibile non letta: " + exePath, ex);
    }
}
