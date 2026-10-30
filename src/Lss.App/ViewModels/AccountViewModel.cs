using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lss.App.ViewModels;

/// <summary>Un account nella lista: nome e ultimo salvataggio.</summary>
public sealed class AccountItemViewModel
{
    public AccountItemViewModel(GameAccount account)
    {
        Account = account;
        Name = account.DisplayName;

        string saved = account.LastSaved == null
            ? Strings.AccountNoSaves
            : Loc.F(Strings.AccountLastSaved, account.LastSaved.Value.ToString("g"));

        Details = $"ID {account.Id}  ·  {saved}";
    }

    public GameAccount Account { get; }

    public string Name { get; }

    public string Details { get; }
}

/// <summary>
/// Scelta dell'account Steam all'avvio, quando più account hanno salvataggi
/// (AccountForm in WinForms).
/// </summary>
public partial class AccountViewModel : ObservableObject
{
    /// <param name="launcher">Nome del launcher degli account, nei testi.</param>
    private readonly string launcher;

    public AccountViewModel(IReadOnlyList<GameAccount> accounts, string? lastId, string launcher = "")
    {
        this.launcher = launcher;

        foreach (GameAccount account in accounts)
            Accounts.Add(new AccountItemViewModel(account));

        // Preseleziona l'ultimo account usato, altrimenti il primo.
        SelectedAccount = Accounts.FirstOrDefault(a => AppSettings.Refers(lastId ?? "", a.Account.Provider, a.Account.Id)) ?? Accounts.FirstOrDefault();
    }

    public string Intro => Loc.F(Strings.AccountIntroAny, launcher);

    public string CaptionText => Loc.F(Strings.CaptionLauncherAccount, launcher.ToUpperInvariant());

    public string UseText => Strings.UseThisAccount;

    public string ExitText => Strings.Exit;

    public ObservableCollection<AccountItemViewModel> Accounts { get; } = [];

    [ObservableProperty]
    private AccountItemViewModel? selectedAccount;

    /// <summary>L'account scelto, o null se l'utente esce.</summary>
    public GameAccount? Chosen { get; private set; }

    public bool CanUse => SelectedAccount != null;

    partial void OnSelectedAccountChanged(AccountItemViewModel? value) => OnPropertyChanged(nameof(CanUse));

    /// <summary>Sceglie l'account selezionato. false se non ce n'è uno.</summary>
    public bool Use()
    {
        if (SelectedAccount == null)
            return false;

        Chosen = SelectedAccount.Account;
        return true;
    }
}
