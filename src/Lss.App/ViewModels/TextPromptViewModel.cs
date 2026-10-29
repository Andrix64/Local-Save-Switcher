using CommunityToolkit.Mvvm.ComponentModel;

namespace Lss.App.ViewModels;

/// <summary>
/// Testo breve da scrivere in una finestra (TextPromptForm in WinForms): la nota.
/// </summary>
public partial class TextPromptViewModel : ObservableObject
{
    /// <param name="maxLength">Lunghezza massima del testo; 0 = nessun limite.</param>
    /// <param name="multiline">true per una casella a più righe (la nota).</param>
    public TextPromptViewModel(string title, string caption, string initial, string hint, int maxLength, string confirmText,
        bool multiline = false)
    {
        Title = title;
        Caption = caption;
        Hint = hint;
        MaxLength = maxLength;
        ConfirmText = confirmText;
        Multiline = multiline;
        value = initial;
    }

    public string Title { get; }

    public string Caption { get; }

    public string Hint { get; }

    public bool HasHint => Hint != "";

    public int MaxLength { get; }

    public bool Multiline { get; }

    public bool SingleLine => !Multiline;

    public string ConfirmText { get; }

    [ObservableProperty]
    private string value = "";

    /// <summary>Il testo come va salvato: senza spazi ai bordi.</summary>
    public string Result => Value.Trim();
}
