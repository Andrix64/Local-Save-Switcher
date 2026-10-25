using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Scelta dell'account all'avvio, prima della finestra principale. Dopo la
/// chiusura, <see cref="Chosen"/> è l'account scelto o null se l'utente esce.
/// </summary>
public partial class AccountWindow : Window
{
    public AccountWindow() => InitializeComponent();

    public AccountWindow(IReadOnlyList<GameAccount> accounts, string? lastId, string launcher = "") : this()
    {
        DataContext = new AccountViewModel(accounts, lastId, launcher);
    }

    private AccountViewModel ViewModel => (AccountViewModel)DataContext!;

    public GameAccount? Chosen { get; private set; }

    private void Use_Click(object? sender, RoutedEventArgs e) => Use();

    private void List_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e) => Use();

    private void Exit_Click(object? sender, RoutedEventArgs e) => Close();

    private void Use()
    {
        if (!ViewModel.Use())
            return;

        Chosen = ViewModel.Chosen;
        Close();
    }
}
