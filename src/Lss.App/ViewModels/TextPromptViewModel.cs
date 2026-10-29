using CommunityToolkit.Mvvm.ComponentModel;

namespace Lss.App.ViewModels;

/// <summary>
/// Testo breve da scrivere in una finestra (TextPromptForm in WinForms): la nota.
/// </summary>
public partial class TextPromptViewModel : ObservableObject
{
    /// <param name="maxLength">Lunghezza massima del testo; 0 = nessun limite.</param>
    public TextPromptViewModel(string title, string caption, string initial, string hint, int maxLength, string confirmText)
    {
        Title = title;
        Caption = caption;
        Hint = hint;
        MaxLength = maxLength;
        ConfirmText = confirmText;
        value = initial;
    }

    public string Title { get; }

    public string Caption { get; }

    public string Hint { get; }

    public int MaxLength { get; }

    public string ConfirmText { get; }

    [ObservableProperty]
    private string value = "";

    /// <summary>Il testo come va salvato: senza spazi ai bordi.</summary>
    public string Result => Value.Trim();
}
