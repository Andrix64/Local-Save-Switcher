using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Un testo breve da scrivere (la nota di una partita). Il risultato è il
/// testo, o null se annullata.
/// </summary>
public partial class TextPromptWindow : Window
{
    public TextPromptWindow() => InitializeComponent();

    public TextPromptWindow(TextPromptViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private TextPromptViewModel ViewModel => (TextPromptViewModel)DataContext!;

    private void Confirm_Click(object? sender, RoutedEventArgs e) => Close(ViewModel.Result);

}
