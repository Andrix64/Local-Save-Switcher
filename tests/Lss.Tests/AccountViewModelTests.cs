using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Scelta dell'account all'avvio: preselezione e risultato.
/// </summary>
public sealed class AccountViewModelTests
{
    private static readonly GameAccount First = new("steam", "111", "Anna", @"C:\userdata\111", null);
    private static readonly GameAccount Second = new("steam", "222", "", @"C:\userdata\222", null);

    [Fact]
    public void PreselectsTheLastUsedAccount()
    {
        var vm = new AccountViewModel([First, Second], "222");

        Assert.Equal("222", vm.SelectedAccount?.Account.Id);
    }

    [Fact]
    public void WithoutLastAccount_PreselectsTheFirst()
    {
        var vm = new AccountViewModel([First, Second], null);

        Assert.Equal("111", vm.SelectedAccount?.Account.Id);
    }

    [Fact]
    public void Use_ReturnsTheSelectedAccount()
    {
        var vm = new AccountViewModel([First, Second], null);

        Assert.True(vm.Use());
        Assert.Same(First, vm.Chosen);
    }
}
