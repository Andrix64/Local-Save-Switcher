using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>
/// Una partita eliminata nell'elenco: nome, persona a cui apparteneva (pallino e
/// nome, "—" se non esiste più), data dell'eliminazione e tempo di gioco.
/// </summary>
public sealed class DeletedItemViewModel
{
    public DeletedItemViewModel(Snapshot snapshot, Person? owner, PlaythroughInfo info)
    {
        Snapshot = snapshot;
        Name = SaveManager.ProfileDisplayName(snapshot.Owner);
        OwnerText = owner?.Name ?? "—";
        OwnerInitial = owner != null && owner.Name.Length > 0 ? char.ToUpperInvariant(owner.Name[0]).ToString() : "";
        OwnerBrush = owner == null ? null : new SolidColorBrush(Color.Parse(owner.Color));
        Details = Loc.F(Strings.DeletedOn, snapshot.Date.ToString("g")) + "  ·  " +
                  Loc.F(Strings.DeletedPlayTime, info.PlayTime.ToString(@"h\:mm"));
    }

    public Snapshot Snapshot { get; }

    public string Name { get; }

    public string OwnerText { get; }

    public string OwnerInitial { get; }

    /// <summary>Colore della persona, o null se non esiste più.</summary>
    public IBrush? OwnerBrush { get; }

    public string Details { get; }
}

/// <summary>
/// Partite eliminate che si possono ancora recuperare, finché la pulizia
/// delle copie automatiche non le elimina (DeletedForm in WinForms).
/// </summary>
public partial class DeletedViewModel : ObservableObject
{
    private readonly SaveManager manager;
    private readonly IDialogService dialogs;

    // Come nella finestra principale: il gioco viene controllato ogni 2 secondi.
    private readonly DispatcherTimer gameTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    private bool checkingGame;

    public DeletedViewModel(SaveManager manager, IDialogService dialogs)
    {
        this.manager = manager;
        this.dialogs = dialogs;

        gameTimer.Tick += async (_, _) => await CheckGameAsync();
    }

    /// <summary>true se è stata recuperata almeno una partita.</summary>
    public bool Changed { get; private set; }

    public string Title => Strings.DeletedTitle;

    public string EmptyText => Strings.NoDeleted;

    public string RecoverText => Strings.Recover;

    public string OpenFolderText => Strings.OpenFolder;

    public string CloseText => Strings.Close;

    public ObservableCollection<DeletedItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private DeletedItemViewModel? selected;

    [ObservableProperty]
    private string caption = "";

    [ObservableProperty]
    private bool showEmpty;

    [ObservableProperty]
    private bool listVisible;

    [ObservableProperty]
    private string status = "";

    [ObservableProperty]
    private bool statusIsWarning;

    [ObservableProperty]
    private bool statusIsError;

    [ObservableProperty]
    private bool statusIsSuccess;

    // true mentre un'operazione sui file è in corso.
    [ObservableProperty]
    private bool busy;

    // true mentre il gioco è aperto: i salvataggi non si toccano.
    [ObservableProperty]
    private bool gameRunning;

    public bool CanUse => !Busy && Selected != null;

    public string Intro => Strings.DeletedIntro;

    public string PurgeText => Strings.PurgeButton;

    public string PurgeAllText => Strings.PurgeAllButton;

    /// <summary>"Elimina tutte per sempre…" si vede da due partite eliminate in su.</summary>
    [ObservableProperty]
    private bool showPurgeAll;

    /// <summary>Elimina per sempre la partita selezionata, dopo conferma.</summary>
    [RelayCommand]
    private async Task PurgeAsync()
    {
        if (Selected is not DeletedItemViewModel item)
            return;

        bool confirmed = await dialogs.ConfirmAsync(Strings.PurgeTitle,
            Loc.F(Strings.PurgeText, item.Name), Strings.PurgeVerb, destructive: true);

        if (!confirmed)
            return;

        await RunAsync(Strings.BusyDeleting, () => manager.PurgeDeletedProfile(item.Snapshot));
    }

    /// <summary>Elimina per sempre tutte le partite eliminate, dopo conferma. Si ferma al primo errore.</summary>
    [RelayCommand]
    private async Task PurgeAllAsync()
    {
        int count = Items.Count;

        bool confirmed = await dialogs.ConfirmAsync(Strings.PurgeTitle,
            Loc.F(Strings.PurgeAllText, count), Strings.PurgeVerb, destructive: true);

        if (!confirmed)
            return;

        List<Snapshot> all = Items.Select(item => item.Snapshot).ToList();

        await RunAsync(Strings.BusyDeleting, () =>
        {
            foreach (Snapshot deleted in all)
            {
                Result purged = manager.PurgeDeletedProfile(deleted);

                if (!purged.Success)
                    return purged;
            }

            return Result.Ok(Strings.Purged);
        });
    }

    partial void OnSelectedChanged(DeletedItemViewModel? value) => OnPropertyChanged(nameof(CanUse));

    partial void OnBusyChanged(bool value) => OnPropertyChanged(nameof(CanUse));

    /// <summary>Carica l'elenco e avvia il controllo del gioco.</summary>
    public void Start()
    {
        Reload();
        _ = CheckGameAsync();
        gameTimer.Start();
    }

    public void Stop() => gameTimer.Stop();

    [RelayCommand]
    private async Task RecoverAsync()
    {
        if (Selected is not DeletedItemViewModel item)
            return;

        await RunAsync(Strings.BusyRecovering, () => manager.RecoverDeletedProfile(item.Snapshot, manager.RecoveryOwnerOf(item.Snapshot)));
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (Selected is not DeletedItemViewModel item)
            return;

        string path = item.Snapshot.Path;

        if (path == "")
            return;

        // Eliminata da fuori mentre la finestra era aperta.
        if (!Directory.Exists(path))
        {
            Reload();
            SetStatus(Loc.F(Strings.FolderNotFound, path).Replace('\n', ' '), error: true);
            return;
        }

        if (!await dialogs.OpenFolderAsync(path))
            await dialogs.InformAsync(Strings.DeletedTitle, Loc.F(Strings.CannotOpenFolder, path), InfoKind.Warning);
    }

    private void Reload()
    {
        Items.Clear();

        try
        {
            foreach (Snapshot deleted in manager.GetDeletedProfiles())
            {
                // Se la persona non esiste più, la riga dice "—".
                Person? owner = manager.Players?.Find(manager.DeletedOwnerOf(deleted));
                Items.Add(new DeletedItemViewModel(deleted, owner, manager.DeletedInfoOf(deleted)));
            }
        }
        catch (Exception ex)
        {
            SetStatus(Loc.F(Strings.CannotReadList, ex.Message), error: true);
        }

        int count = Items.Count;
        Caption = count == 0 ? Strings.CaptionDeleted : $"{Strings.CaptionDeleted}  ·  {count}";
        ShowEmpty = count == 0;
        ListVisible = count > 0;
        ShowPurgeAll = count >= 2;
    }

    private async Task<Result> RunAsync(string message, Func<Result> action)
    {
        Busy = true;
        SetStatus(message, error: false);

        Result result;

        try
        {
            result = await Task.Run(action);
        }
        catch (Exception ex)
        {
            Log.Write("Errore imprevisto (" + message + ")", ex);
            result = Result.Fail(Loc.F(Strings.UnexpectedError, ex.Message, Log.FilePath));
        }
        finally
        {
            Busy = false;
            Changed = true;
        }

        Reload();
        SetStatus(result.Message.Split('\n')[0], error: !result.Success, success: result.Success);

        if (!result.Success)
        {
            await dialogs.InformAsync(
                result.Critical ? Strings.TitleSeriousError : Strings.TitleOperationFailed,
                result.Message,
                result.Critical ? InfoKind.Error : InfoKind.Warning);
        }

        return result;
    }

    private async Task CheckGameAsync()
    {
        if (checkingGame)
            return;

        checkingGame = true;

        try
        {
            bool running = await Task.Run(manager.IsGameRunning);

            if (running == GameRunning)
                return;

            GameRunning = running;

            // Il messaggio sul gioco aperto prende il posto dell'ultimo esito.
            if (!Busy)
                SetStatus(running ? Loc.F(Strings.GameRunning, manager.Game.Name) : "", error: false, warning: running);
        }
        finally
        {
            checkingGame = false;
        }
    }

    private void SetStatus(string message, bool error, bool success = false, bool warning = false)
    {
        Status = message;
        StatusIsError = error;
        StatusIsSuccess = success;
        StatusIsWarning = warning;
    }
}
