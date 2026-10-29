using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Lss.App.ViewModels;

namespace Lss.App.Views;

/// <summary>
/// Partite eliminate: si recuperano da qui. Il risultato dice se qualcosa è cambiato.
/// </summary>
public partial class DeletedWindow : Window
{
    public DeletedWindow() => InitializeComponent();

    public DeletedWindow(DeletedViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private DeletedViewModel ViewModel => (DeletedViewModel)DataContext!;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ViewModel.Start();
    }

    // Non interrompere una copia a metà.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (ViewModel.Busy)
            e.Cancel = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ViewModel.Stop();
    }


    // I pulsanti della riga scelgono la partita, poi fanno come quelli in fondo.
    private async void Recover_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DeletedItemViewModel item } && !ViewModel.Busy)
        {
            ViewModel.Selected = item;
            await ViewModel.RecoverCommand.ExecuteAsync(null);
        }
    }

    private async void Purge_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DeletedItemViewModel item } && !ViewModel.Busy)
        {
            ViewModel.Selected = item;
            await ViewModel.PurgeCommand.ExecuteAsync(null);
        }
    }

    // Doppio clic su una partita: la recupera, come in WinForms.
    private async void List_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.CanUse)
            await ViewModel.RecoverCommand.ExecuteAsync(null);
    }
}
