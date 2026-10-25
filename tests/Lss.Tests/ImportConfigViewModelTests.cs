using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Finestra di importazione (N9c): un blocco per gioco, la casella accesa solo se un posto
/// si può importare, Importa applica solo i giochi accesi, Annulla non applica nulla.
/// </summary>
public sealed class ImportConfigViewModelTests
{
    private static SaveConfigSpot Spot(string path) => new("gameinstall", path, "", true);

    private static SaveConfigGame ConfigGame(string name) =>
        new("steam", "1", name, name, false, [Spot("saves")], "per Andrea");

    private static GameInfo Game(string name) =>
        new("steam", name, name, "", "", "", [], SupportState.Supported);

    private static SaveConfigFile ConfigFile(params ConfigGamePlan[] plans) =>
        new("windows", "Windows", false, "LSS test", "2026-10-25", plans.Select(plan => plan.Config).ToList());

    /// <summary>Un gioco di questo computer con il suo posto trovato.</summary>
    private static ConfigGamePlan Ready(string name) =>
        new(ConfigGame(name), Game(name), [new ConfigSpotPlan(Spot("saves"), @"C:\Saves\" + name, ConfigSpotState.Found, "")], false);

    /// <summary>Un gioco con il solo posto rifiutato: non si può importare.</summary>
    private static ConfigGamePlan Refused(string name) =>
        new(ConfigGame(name), Game(name), [new ConfigSpotPlan(Spot("saves"), @"C:\Saves\" + name, ConfigSpotState.Refused, "troppo ampia")], false);

    /// <summary>Un gioco che non è su questo computer.</summary>
    private static ConfigGamePlan Missing(string name) => new(ConfigGame(name), null, [], false);

    /// <summary>La finestra; applied raccoglie i giochi a cui si scrive, ognuno con un posto aggiunto.</summary>
    private static ImportConfigViewModel Model(List<string> applied, params ConfigGamePlan[] plans) =>
        new(ConfigFile(plans), plans, _ => "Steam", plan =>
        {
            applied.Add(plan.Config.Name);
            return Task.FromResult(1);
        });

    [Fact]
    public void OnlyGamesWithAPlaceToImportAreChecked()
    {
        ImportConfigViewModel model = Model([], Ready("Alfa"), Refused("Beta"), Missing("Gamma"));

        Assert.True(model.Games[0].IsChecked);
        Assert.True(model.Games[0].CanImport);
        Assert.False(model.Games[1].IsChecked);
        Assert.False(model.Games[1].CanImport);
        Assert.False(model.Games[2].HasGame);
        Assert.False(model.Games[2].CanImport);
    }

    [Fact]
    public async Task ImportAppliesOnlyTheCheckedGames()
    {
        var applied = new List<string>();
        ImportConfigViewModel model = Model(applied, Ready("Alfa"), Ready("Beta"));
        model.Games[1].IsChecked = false;
        bool? finished = null;
        model.Finished += imported => finished = imported;

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Equal(["Alfa"], applied);
        Assert.Equal(1, model.Applied);
        Assert.True(finished);
    }

    [Fact]
    public async Task AGameWithNothingToImportIsNotAppliedEvenWhenChecked()
    {
        var applied = new List<string>();
        ImportConfigViewModel model = Model(applied, Ready("Alfa"), Refused("Beta"));
        model.Games[1].IsChecked = true;

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Equal(["Alfa"], applied);
    }

    [Fact]
    public void CancelAppliesNothing()
    {
        var applied = new List<string>();
        ImportConfigViewModel model = Model(applied, Ready("Alfa"));
        bool? finished = null;
        model.Finished += imported => finished = imported;

        model.CancelCommand.Execute(null);

        Assert.False(finished);
        Assert.Empty(applied);
    }

    [Fact]
    public async Task AGameWithNoSpotAddedDoesNotCountAsImported()
    {
        var model = new ImportConfigViewModel(ConfigFile(Ready("Alfa")), [Ready("Alfa")], _ => "Steam",
            _ => Task.FromResult(0));

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Equal(0, model.Applied);
    }
}
