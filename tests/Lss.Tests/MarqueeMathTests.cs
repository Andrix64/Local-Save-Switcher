using Lss.App.Controls;

namespace Lss.Tests;

/// <summary>I conti dello scorrimento dei nomi lunghi.</summary>
public sealed class MarqueeMathTests
{
    [Theory]
    [InlineData(100, 200, false)]
    [InlineData(200, 200, false)]
    [InlineData(260, 200, true)]
    [InlineData(260, 0, false)]
    public void ScrollsOnlyWhenTheTextDoesNotFit(double text, double available, bool expected)
    {
        Assert.Equal(expected, MarqueeMath.NeedsScroll(text, available));
    }

    [Fact]
    public void TheLoopIsTheTextAndTheGap()
    {
        Assert.Equal(300, MarqueeMath.LoopLength(260));
    }

    [Fact]
    public void TheTextMovesLeftAtThirtyPixelsPerSecond()
    {
        Assert.Equal(0, MarqueeMath.Offset(260, 0));
        Assert.Equal(-30, MarqueeMath.Offset(260, 1), 6);
        Assert.Equal(-150, MarqueeMath.Offset(260, 5), 6);
    }

    [Fact]
    public void TheLoopHasNoJump()
    {
        // Dopo un giro intero il testo è dov'era; un istante prima è al suo ritorno (testo + spazio a sinistra).
        double seconds = MarqueeMath.LoopLength(260) / MarqueeMath.Speed;

        Assert.Equal(0, MarqueeMath.Offset(260, seconds), 6);
        Assert.Equal(-MarqueeMath.LoopLength(260), MarqueeMath.Offset(260, seconds - 1e-9), 3);
    }

    [Fact]
    public void ItNeverScrollsBackwards()
    {
        Assert.Equal(0, MarqueeMath.Offset(260, -3));
        Assert.All(Enumerable.Range(0, 200), i => Assert.InRange(MarqueeMath.Offset(260, i * 0.37), -300, 0));
    }
}
