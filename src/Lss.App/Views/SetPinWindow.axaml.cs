using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>"PIN di …": il risultato è true se il PIN nuovo è salvato.</summary>
public partial class SetPinWindow : Window
{
    public SetPinWindow() => InitializeComponent();

    public SetPinWindow(SetPinViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // Il cursore nel primo campo da riempire.
        Opened += (_, _) => (viewModel.NeedsCurrent ? CurrentBox : NewBox).Focus();
    }

    private SetPinViewModel ViewModel => (SetPinViewModel)DataContext!;

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Save())
            Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
