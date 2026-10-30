namespace Lss.App.Controls;

/// <summary>
/// I conti di MarqueeText senza grafica: quando serve scorrere, quanto è lungo il giro e
/// dov'è il testo dopo t secondi.
/// </summary>
internal static class MarqueeMath
{
    /// <summary>Velocità dello scorrimento, in pixel al secondo.</summary>
    public const double Speed = 30;

    /// <summary>Spazio tra la fine del testo e il suo ritorno.</summary>
    public const double Gap = 40;

    /// <summary>true se il testo non sta nello spazio e va scorso.</summary>
    public static bool NeedsScroll(double textWidth, double availableWidth) =>
        availableWidth > 0 && textWidth > availableWidth + 0.5;

    /// <summary>La lunghezza del giro: il testo e lo spazio che lo separa dal suo ritorno.</summary>
    public static double LoopLength(double textWidth) => textWidth + Gap;

    /// <summary>
    /// Lo spostamento a sinistra del testo dopo t secondi (0 o negativo): il giro ricomincia
    /// quando il testo ha percorso la sua lunghezza più lo spazio, senza salti.
    /// </summary>
    public static double Offset(double textWidth, double seconds)
    {
        double loop = LoopLength(textWidth);

        return seconds <= 0 ? 0 : -(seconds * Speed % loop);
    }
}
