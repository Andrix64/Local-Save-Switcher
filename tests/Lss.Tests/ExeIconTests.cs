namespace Lss.Tests;

/// <summary>
/// ExeIcon: l'icona più grande di un eseguibile, letta dal file PE (PE32 e PE32+).
/// Gli eseguibili sono costruiti dal test (PeBuilder), tranne uno smoke test su Windows.
/// </summary>
public sealed class ExeIconTests : IDisposable
{
    private readonly TestFolder folder = new();

    // Immagini finte: ExeIcon non le decodifica, le copia e basta.
    private static readonly byte[] Small = [1, 2, 3];
    private static readonly byte[] Large = [9, 8, 7, 6, 5];

    public void Dispose() => folder.Dispose();

    private string Exe(byte[] bytes)
    {
        string path = Path.Combine(folder.Root, "gioco.exe");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheLargestIconIsReturnedAsOneImageIco(bool plus)
    {
        byte[] exe = PeBuilder.Build(plus, true, (16, 16, Small), (0, 0, Large));

        byte[] icon = ExeIcon.Extract(Exe(exe))!;

        // Intestazione .ico: tipo 1, una immagine; poi la voce (256 = 0, offset 22) e l'immagine grande.
        Assert.Equal(new byte[] { 0, 0, 1, 0, 1, 0 }, icon[..6]);
        Assert.Equal((byte)0, icon[6]);
        Assert.Equal((uint)Large.Length, BitConverter.ToUInt32(icon, 14));
        Assert.Equal(22u, BitConverter.ToUInt32(icon, 18));
        Assert.Equal(Large, icon[22..]);
    }

    [Fact]
    public void AnExeWithoutResourcesGivesNull()
    {
        Assert.Null(ExeIcon.Extract(Exe(PeBuilder.Build(false, withResources: false))));
    }

    [Fact]
    public void AnExeWithoutIconsGivesNull()
    {
        Assert.Null(ExeIcon.Extract(Exe(PeBuilder.Build(false, true))));
    }

    [Fact]
    public void TruncatedFileGivesNull()
    {
        byte[] exe = PeBuilder.Build(false, true, (16, 16, Small));

        Assert.Null(ExeIcon.Extract(Exe(exe[..300])));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OffsetsOutsideTheFileGiveNull(bool plus)
    {
        byte[] exe = PeBuilder.Build(plus, true, (16, 16, Small));

        // PointerToRawData della sezione .rsrc, fuori dal file.
        BitConverter.GetBytes(0x7FFFFFF).CopyTo(exe, PeBuilder.SectionTable(plus) + 20);

        Assert.Null(ExeIcon.Extract(Exe(exe)));
    }

    [Fact]
    public void NotPeFileGivesNull()
    {
        Assert.Null(ExeIcon.Extract(Exe("testo"u8.ToArray())));
    }

    [Fact]
    public void AMissingFileGivesNull()
    {
        Assert.Null(ExeIcon.Extract(Path.Combine(folder.Root, "non-c-e.exe")));
    }

    [Fact]
    public void ASystemProgramOnWindowsHasAnIcon()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // Smoke test in sola lettura: explorer.exe ha la sua icona. Il programma del test
        // (testhost.exe) non ne ha una, quindi non si usa Environment.ProcessPath.
        string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        Assert.NotNull(ExeIcon.Extract(explorer));
    }
}
