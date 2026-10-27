using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Lss.App.Controls;

/// <summary>
/// Una riga di testo: se è troppo lunga finisce con "…". Quando è attiva (il puntatore è sopra, o
/// IsActive è vero, per esempio perché la riga è selezionata) il testo scorre piano e senza fine;
/// quando smette di esserlo torna subito il "…". L'animazione gira solo se serve: attiva, visibile
/// e con il testo troppo lungo.
/// </summary>
public sealed class MarqueeText : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MarqueeText, string?>(nameof(Text));

    public static readonly StyledProperty<double> FontSizeProperty =
        TextBlock.FontSizeProperty.AddOwner<MarqueeText>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextBlock.FontWeightProperty.AddOwner<MarqueeText>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextBlock.FontFamilyProperty.AddOwner<MarqueeText>();

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextBlock.ForegroundProperty.AddOwner<MarqueeText>();

    /// <summary>Attivo da fuori (la riga selezionata). Il puntatore sopra il controllo vale lo stesso.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<MarqueeText, bool>(nameof(IsActive));

    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch clock = new();

    static MarqueeText()
    {
        AffectsMeasure<MarqueeText>(TextProperty, FontSizeProperty, FontWeightProperty, FontFamilyProperty);
        AffectsRender<MarqueeText>(TextProperty, FontSizeProperty, FontWeightProperty, FontFamilyProperty,
            ForegroundProperty, IsActiveProperty);
    }

    public MarqueeText()
    {
        timer.Tick += (_, _) => InvalidateVisual();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private FormattedText Format() =>
        new(Text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground);

    protected override Size MeasureOverride(Size availableSize)
    {
        FormattedText text = Format();
        double width = double.IsInfinity(availableSize.Width) ? text.Width : Math.Min(text.Width, availableSize.Width);

        return new Size(width, text.Height);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopTimer();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        UpdateTimer();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        UpdateTimer();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsActiveProperty || change.Property == TextProperty ||
            change.Property == FontSizeProperty || change.Property == FontWeightProperty ||
            change.Property == BoundsProperty || change.Property == IsVisibleProperty)
        {
            UpdateTimer();
        }
    }

    private bool Active => IsActive || IsPointerOver;

    /// <summary>true se il testo, com'è ora, va scorso: attivo, visibile e troppo lungo.</summary>
    private bool Scrolling =>
        Active && IsEffectivelyVisible && VisualRoot != null &&
        MarqueeMath.NeedsScroll(Format().Width, Bounds.Width);

    private void UpdateTimer()
    {
        if (Scrolling)
        {
            if (!timer.IsEnabled)
            {
                clock.Restart();
                timer.Start();
            }
        }
        else
        {
            StopTimer();
        }

        InvalidateVisual();
    }

    private void StopTimer()
    {
        timer.Stop();
        clock.Reset();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Un fondo trasparente: il controllo riceve il puntatore su tutta la sua area.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));

        FormattedText text = Format();
        double top = Math.Max(0, (Bounds.Height - text.Height) / 2);

        if (!Scrolling)
        {
            text.MaxTextWidth = Math.Max(0, Bounds.Width);
            text.MaxLineCount = 1;
            text.Trimming = TextTrimming.CharacterEllipsis;
            context.DrawText(text, new Point(0, top));
            return;
        }

        double offset = MarqueeMath.Offset(text.Width, clock.Elapsed.TotalSeconds);

        using (context.PushClip(new Rect(Bounds.Size)))
        {
            context.DrawText(text, new Point(offset, top));
            context.DrawText(text, new Point(offset + MarqueeMath.LoopLength(text.Width), top));
        }
    }
}
