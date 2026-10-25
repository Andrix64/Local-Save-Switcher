using System.Text;

namespace Lss.Tests;

/// <summary>
/// BinaryVdf: i dati binari sono costruiti nel test, nessun file di Steam.
/// </summary>
public sealed class BinaryVdfTests
{
    /// <summary>
    /// Byte costruiti con la scrittura scelta dal test.
    /// </summary>
    private static byte[] Build(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            write(writer);

        return stream.ToArray();
    }

    private static BinaryReader ReaderOf(byte[] data) => new(new MemoryStream(data), Encoding.UTF8, leaveOpen: false);

    /// <summary>Chiave o valore di testo terminato da 0.</summary>
    private static void Text(BinaryWriter writer, string text)
    {
        writer.Write(Encoding.UTF8.GetBytes(text));
        writer.Write((byte)0);
    }

    [Fact]
    public void NestedObjectsAndStrings()
    {
        byte[] data = Build(w =>
        {
            w.Write((byte)0x00); Text(w, "appinfo");
            w.Write((byte)0x00); Text(w, "ufs");
            w.Write((byte)0x01); Text(w, "name"); Text(w, "Control");
            w.Write((byte)0x08);
            w.Write((byte)0x08);
        });

        using BinaryReader reader = ReaderOf(data);
        Vdf.Node node = BinaryVdf.Read(reader, null);

        Assert.Equal("Control", Vdf.Find(node, "appinfo", "ufs", "name"));
    }

    [Fact]
    public void NonAsciiStringsAreKept()
    {
        byte[] data = Build(w =>
        {
            w.Write((byte)0x01); Text(w, "città"); Text(w, "Perché è ñandú");
            w.Write((byte)0x08);
        });

        using BinaryReader reader = ReaderOf(data);
        Vdf.Node node = BinaryVdf.Read(reader, null);

        Assert.Equal("Perché è ñandú", Vdf.Find(node, "città"));
    }

    [Fact]
    public void Int32IsDecimalText()
    {
        byte[] data = Build(w =>
        {
            w.Write((byte)0x02); Text(w, "count"); w.Write(2);
            w.Write((byte)0x02); Text(w, "negative"); w.Write(-1);
            w.Write((byte)0x08);
        });

        using BinaryReader reader = ReaderOf(data);
        Vdf.Node node = BinaryVdf.Read(reader, null);

        Assert.Equal("2", Vdf.Find(node, "count"));
        Assert.Equal("-1", Vdf.Find(node, "negative"));
    }

    [Fact]
    public void UInt64IsDecimalText()
    {
        byte[] data = Build(w =>
        {
            w.Write((byte)0x07); Text(w, "token"); w.Write(76561197960265728UL);
            w.Write((byte)0x08);
        });

        using BinaryReader reader = ReaderOf(data);
        Vdf.Node node = BinaryVdf.Read(reader, null);

        Assert.Equal("76561197960265728", Vdf.Find(node, "token"));
    }

    [Fact]
    public void KeysFromStringTable()
    {
        // Formato v29: ogni chiave è un indice int32 nella tabella.
        string[] table = ["appinfo", "name"];
        byte[] data = Build(w =>
        {
            w.Write((byte)0x00); w.Write(0);
            w.Write((byte)0x01); w.Write(1); Text(w, "Terraria");
            w.Write((byte)0x08);
        });

        using BinaryReader reader = ReaderOf(data);
        Vdf.Node node = BinaryVdf.Read(reader, table);

        Assert.Equal("Terraria", Vdf.Find(node, "appinfo", "name"));
    }

    [Fact]
    public void KeyIndexOutsideTableIsInvalid()
    {
        string[] table = ["appinfo"];
        byte[] data = Build(w =>
        {
            w.Write((byte)0x01); w.Write(5); Text(w, "x");
            w.Write((byte)0x08);
        });

        using BinaryReader reader = ReaderOf(data);

        Assert.Throws<InvalidDataException>(() => BinaryVdf.Read(reader, table));
    }

    [Fact]
    public void TruncatedInsideObjectIsInvalid()
    {
        // Il blocco "a" non viene chiuso prima della fine dei dati.
        byte[] data = Build(w =>
        {
            w.Write((byte)0x00); Text(w, "a");
            w.Write((byte)0x01); Text(w, "b"); Text(w, "x");
        });

        using BinaryReader reader = ReaderOf(data);

        Assert.Throws<InvalidDataException>(() => BinaryVdf.Read(reader, null));
    }

    [Fact]
    public void TruncatedValueIsInvalid()
    {
        // Un int32 con solo due byte.
        byte[] data = Build(w =>
        {
            w.Write((byte)0x02); Text(w, "n");
            w.Write((byte)1); w.Write((byte)0);
        });

        using BinaryReader reader = ReaderOf(data);

        Assert.Throws<InvalidDataException>(() => BinaryVdf.Read(reader, null));
    }

    [Fact]
    public void UnknownTypeIsInvalid()
    {
        byte[] data = Build(w =>
        {
            w.Write((byte)0x03); Text(w, "x");
            w.Write((byte)0x08);
        });

        using BinaryReader reader = ReaderOf(data);

        Assert.Throws<InvalidDataException>(() => BinaryVdf.Read(reader, null));
    }

    [Fact]
    public void EndOfDataAtTopLevelIsNotAnError()
    {
        byte[] data = Build(w =>
        {
            w.Write((byte)0x01); Text(w, "x"); Text(w, "y");
        });

        using BinaryReader reader = ReaderOf(data);
        Vdf.Node node = BinaryVdf.Read(reader, null);

        Assert.Equal("y", Vdf.Find(node, "x"));
    }
}
