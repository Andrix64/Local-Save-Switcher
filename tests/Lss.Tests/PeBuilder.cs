using System.Text;

namespace Lss.Tests;

/// <summary>
/// Costruisce un eseguibile Windows minimo (PE32 o PE32+) con una sezione .rsrc che
/// contiene le icone date: serve ai test di ExeIcon e GameIcons, senza file veri.
/// </summary>
public static class PeBuilder
{
    // Dove sta la sezione .rsrc e l'RVA in cui è caricata.
    private const int SectionOffsetPe32 = 0x178;
    private const int SectionOffsetPe32Plus = 0x188;
    private const uint ResourceRva = 0x1000;
    private const int FileAlignment = 0x200;
    private const int LanguageId = 0x409;

    /// <summary>Posizione della tabella delle sezioni nel file, per PE32 o PE32+.</summary>
    public static int SectionTable(bool plus) => plus ? SectionOffsetPe32Plus : SectionOffsetPe32;

    /// <summary>
    /// Un eseguibile con le icone (larghezza, altezza, immagine). Con withResources false
    /// non ha la directory delle risorse.
    /// </summary>
    public static byte[] Build(bool plus, bool withResources = true, params (int Width, int Height, byte[] Image)[] icons)
    {
        byte[] resources = withResources ? Resources(icons) : [];
        int optionalSize = plus ? 240 : 224;

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            // Intestazione DOS: "MZ" e l'offset dell'intestazione PE (0x80).
            writer.Write("MZ"u8);
            writer.Write(new byte[0x3C - 2]);
            writer.Write(0x80);
            writer.Write(new byte[0x80 - 0x40]);

            // Firma PE e intestazione del file: una sezione.
            writer.Write("PE\0\0"u8);
            writer.Write((ushort)(plus ? 0x8664 : 0x14C));
            writer.Write((ushort)1);
            writer.Write(new byte[12]);
            writer.Write((ushort)optionalSize);
            writer.Write((ushort)(plus ? 0x22 : 0x102));

            // Intestazione opzionale: magic, e la directory delle risorse (numero 2).
            byte[] optional = new byte[optionalSize];
            BitConverter.GetBytes((ushort)(plus ? 0x20B : 0x10B)).CopyTo(optional, 0);
            BitConverter.GetBytes(16).CopyTo(optional, plus ? 108 : 92);
            int directories = plus ? 112 : 96;
            BitConverter.GetBytes(withResources ? ResourceRva : 0u).CopyTo(optional, directories + 16);
            BitConverter.GetBytes(withResources ? (uint)resources.Length : 0u).CopyTo(optional, directories + 20);
            writer.Write(optional);

            // Tabella delle sezioni: una sola, .rsrc, a 0x200 nel file.
            writer.Write(Encoding.ASCII.GetBytes(".rsrc\0\0\0"));
            writer.Write((uint)resources.Length);
            writer.Write(ResourceRva);
            writer.Write((uint)Align(resources.Length));
            writer.Write((uint)FileAlignment);
            writer.Write(new byte[4 + 4 + 2 + 2 + 4]);

            writer.Write(new byte[FileAlignment - (int)stream.Position]);
            writer.Write(resources);
            writer.Write(new byte[Align(resources.Length) - resources.Length]);
        }

        return stream.ToArray();
    }

    // Sezione delle risorse: tipo RT_ICON (3) con un nome per icona, tipo RT_GROUP_ICON (14) con un gruppo.
    private static byte[] Resources((int Width, int Height, byte[] Image)[] icons)
    {
        int n = icons.Length;

        // Posizioni relative alla sezione, nell'ordine in cui si scrivono.
        int iconType = 32;
        int groupType = iconType + 16 + 8 * n;
        int groupName = groupType + 24;
        int iconName = groupName + 24;
        int leaves = iconName + 24 * n;
        int data = leaves + 16 * (n + 1);

        byte[] group = GroupEntries(icons);
        int iconData = data + group.Length;

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            // Radice: due tipi, entrambi cartelle.
            Header(writer, 2);
            Entry(writer, 3, iconType);
            Entry(writer, 14, groupType);

            // Tipo RT_ICON: una cartella per icona (nome = numero dell'icona).
            Header(writer, n);

            for (int i = 0; i < n; i++)
                Entry(writer, (uint)i + 1, iconName + 24 * i);

            // Tipo RT_GROUP_ICON: un solo nome, con una lingua che porta alla foglia del gruppo.
            Header(writer, 1);
            Entry(writer, 1, groupName);

            Header(writer, 1);
            Entry(writer, LanguageId, leaves + 16 * n, directory: false);

            // Nomi delle icone: ognuno ha una lingua, che porta alla sua foglia.
            for (int i = 0; i < n; i++)
            {
                Header(writer, 1);
                Entry(writer, LanguageId, leaves + 16 * i, directory: false);
            }

            // Foglie: RVA, dimensione, pagina di codice (0), riservato (0).
            int position = iconData;

            for (int i = 0; i < n; i++)
            {
                Leaf(writer, position, icons[i].Image.Length);
                position += icons[i].Image.Length;
            }

            Leaf(writer, data, group.Length);

            // Dati: il gruppo, poi le immagini.
            writer.Write(group);

            foreach ((_, _, byte[] image) in icons)
                writer.Write(image);
        }

        return stream.ToArray();
    }

    // Gruppo di icone: GRPICONDIR e una voce di 14 byte per icona, con id da 1.
    private static byte[] GroupEntries((int Width, int Height, byte[] Image)[] icons)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)icons.Length);

            for (int i = 0; i < icons.Length; i++)
            {
                writer.Write((byte)icons[i].Width);
                writer.Write((byte)icons[i].Height);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)icons[i].Image.Length);
                writer.Write((ushort)(i + 1));
            }
        }

        return stream.ToArray();
    }

    private static void Header(BinaryWriter writer, int entries)
    {
        writer.Write(new byte[12]);
        writer.Write((ushort)0);
        writer.Write((ushort)entries);
    }

    // Voce di una cartella: id e offset (con il bit 0x80000000 se è una cartella).
    private static void Entry(BinaryWriter writer, uint id, int offset, bool directory = true)
    {
        writer.Write(id);
        writer.Write((uint)offset | (directory ? 0x80000000u : 0u));
    }

    private static void Leaf(BinaryWriter writer, int position, int size)
    {
        writer.Write(ResourceRva + (uint)position);
        writer.Write((uint)size);
        writer.Write(new byte[8]);
    }

    private static int Align(int size) => (size + FileAlignment - 1) / FileAlignment * FileAlignment;
}
