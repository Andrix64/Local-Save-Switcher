using System.Globalization;
using System.Text;

namespace Lss;

/// <summary>
/// Lettore del formato VDF binario di Steam (appinfo.vdf, shortcuts.vdf).
/// Legge nello stesso <see cref="Vdf.Node"/> del formato testuale: stringhe
/// come testo, numeri interi in forma decimale.
/// </summary>
internal static class BinaryVdf
{
    // Un gioco non ha blocchi annidati così: oltre questo limite il file è rotto.
    private const int MaxDepth = 64;

    private const byte TypeObject = 0x00;
    private const byte TypeString = 0x01;
    private const byte TypeInt32 = 0x02;
    private const byte TypeUInt64 = 0x07;
    private const byte TypeEnd = 0x08;

    /// <summary>
    /// Legge le voci fino al marcatore di fine o alla fine del flusso al primo
    /// livello. strings: tabella delle chiavi (formato v29, le chiavi sono
    /// indici int32) oppure null (chiavi come testo terminato da 0).
    /// Dati non validi → InvalidDataException.
    /// </summary>
    public static Vdf.Node Read(BinaryReader reader, IReadOnlyList<string>? strings)
    {
        try
        {
            return ReadObject(reader, strings, depth: 0, topLevel: true);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("VDF binario troncato.", ex);
        }
    }

    private static Vdf.Node ReadObject(BinaryReader reader, IReadOnlyList<string>? strings, int depth, bool topLevel)
    {
        if (depth > MaxDepth)
            throw new InvalidDataException("VDF binario troppo annidato.");

        var node = new Vdf.Node();

        while (true)
        {
            int type = reader.BaseStream.ReadByte();

            // Al primo livello la fine del flusso è una fine normale; dentro un blocco no.
            if (type == -1)
            {
                if (topLevel)
                    return node;

                throw new InvalidDataException("VDF binario: blocco non chiuso.");
            }

            if (type == TypeEnd)
                return node;

            string key = ReadKey(reader, strings);

            switch (type)
            {
                case TypeObject:
                    node[key] = ReadObject(reader, strings, depth + 1, topLevel: false);
                    break;

                case TypeString:
                    node[key] = ReadCString(reader);
                    break;

                case TypeInt32:
                    node[key] = reader.ReadInt32().ToString(CultureInfo.InvariantCulture);
                    break;

                case TypeUInt64:
                    node[key] = reader.ReadUInt64().ToString(CultureInfo.InvariantCulture);
                    break;

                default:
                    throw new InvalidDataException("VDF binario: tipo " + type + " sconosciuto.");
            }
        }
    }

    /// <summary>
    /// Chiave: indice nella tabella (formato v29) o testo terminato da 0.
    /// </summary>
    private static string ReadKey(BinaryReader reader, IReadOnlyList<string>? strings)
    {
        if (strings == null)
            return ReadCString(reader);

        int index = reader.ReadInt32();

        if (index < 0 || index >= strings.Count)
            throw new InvalidDataException("VDF binario: chiave " + index + " fuori dalla tabella.");

        return strings[index];
    }

    /// <summary>
    /// Testo UTF-8 terminato da un byte 0 (il byte 0 non è incluso).
    /// </summary>
    private static string ReadCString(BinaryReader reader)
    {
        var bytes = new List<byte>();

        byte next;

        while ((next = reader.ReadByte()) != 0)
            bytes.Add(next);

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
