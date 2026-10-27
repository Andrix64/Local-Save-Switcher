using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>
/// Una riga della cronologia: un punto, una copia automatica o una copia
/// di una versione precedente, con "Torna qui" ed eventualmente "Elimina".
/// </summary>
public sealed partial class HistoryRowViewModel : ObservableObject
{
    public HistoryRowViewModel(string title, DateTime date, bool strong, Func<Task> restore, Func<Task>? delete,
        string deleteName)
    {
        Title = title;
        DateText = date.ToString("g");
        IsStrong = strong;
        DeleteName = deleteName;
        GoBackCommand = new AsyncRelayCommand(restore);
        DeleteCommand = delete == null ? null : new AsyncRelayCommand(delete);
    }

    public string Title { get; }

    public string DateText { get; }

    /// <summary>Punti salvati dall'utente: in evidenza.</summary>
    public bool IsStrong { get; }

    public bool HasDelete => DeleteCommand != null;

    public string DeleteName { get; }

    public AsyncRelayCommand GoBackCommand { get; }

    public AsyncRelayCommand? DeleteCommand { get; }

    /// <summary>Falso mentre un'operazione è in corso o il gioco è aperto.</summary>
    [ObservableProperty]
    private bool enabled = true;
}

/// <summary>
/// Cronologia della partita nel gioco (HistoryForm in WinForms): punti
/// salvati, copie automatiche, copie di versioni precedenti.
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly SaveManager manager;
    private readonly string profileName;
    private readonly IDialogService dialogs;
    private readonly Func<Task<bool>> confirmCloud;
    private readonly Func<Task<bool>> openSettings;

    // Controlla ogni 2 secondi se il gioco è aperto, come la finestra principale.
    private readonly DispatcherTimer gameTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    private bool checkingGame;

    /// <param name="confirmCloud">Avviso su Steam Cloud della finestra principale.</param>
    /// <param name="openSettings">Apre le impostazioni; true se sono state salvate.</param>
    public HistoryViewModel(SaveManager manager, IDialogService dialogs,
        Func<Task<bool>> confirmCloud, Func<Task<bool>> openSettings)
    {
        this.manager = manager;
        this.dialogs = dialogs;
        this.confirmCloud = confirmCloud;
        this.openSettings = openSettings;

        profileName = manager.State.ActiveProfile;
        Heading = Loc.F(Strings.HistoryHeading, SaveManager.ProfileDisplayName(profileName));

        gameTimer.Tick += async (_, _) => await CheckGameAsync();
    }

    /// <summary>true se è stata fatta almeno un'operazione.</summary>
    public bool Changed { get; private set; }

    public string Title => Strings.HistoryTitle;

    public string Heading { get; }

    public string Intro => Strings.HistoryIntro;

    public string SavePointCaption => Strings.CaptionSavePoint;

    public string SaveNowText => Strings.SavePointNow;

    public string MyPointsCaption => Strings.CaptionMyPoints;

    public string PointsEmptyText => Strings.NoPoints;

    public string AutomaticCopiesCaption => Strings.CaptionAutomaticCopies;

    public string NoCopiesText => Strings.NoCopies;

    public string LegacyCaption => Strings.CaptionLegacyCopies;

    public string CloseText => Strings.Close;

    public string ChangeText => Strings.Change;

    public ObservableCollection<HistoryRowViewModel> Points { get; } = [];

    public ObservableCollection<HistoryRowViewModel> Copies { get; } = [];

    public ObservableCollection<HistoryRowViewModel> LegacyCopies { get; } = [];

    [ObservableProperty]
    private bool showPointsEmpty;

    [ObservableProperty]
    private bool showNoCopies;

    [ObservableProperty]
    private bool showLegacy;

    [ObservableProperty]
    private string keepText = "";

    [ObservableProperty]
    private string saveName = "";

    [ObservableProperty]
    private string saveHint = "";

    [ObservableProperty]
    private bool saveHintIsWarning;

    [ObservableProperty]
    private bool saveHintIsError;

    [ObservableProperty]
    private bool canSave;

    [ObservableProperty]
    private string status = "";

    [ObservableProperty]
    private bool statusIsSuccess;

    [ObservableProperty]
    private bool statusIsError;

    [ObservableProperty]
    private bool busy;

    [ObservableProperty]
    private bool gameRunning;

    // Il gioco e le operazioni fermano i pulsanti della cronologia.
    private bool CanTouchGame => !Busy && !GameRunning;

    /// <summary>Controllo iniziale: avvio della finestra.</summary>
    public void Start()
    {
        Reload();
        _ = CheckGameAsync();
        gameTimer.Start();
    }

    public void Stop() => gameTimer.Stop();

    partial void OnSaveNameChanged(string value) => UpdateControls();

    partial void OnBusyChanged(bool value) => UpdateControls();

    partial void OnGameRunningChanged(bool value) => UpdateControls();

    [RelayCommand]
    private async Task SavePointAsync()
    {
        string name = SaveName.Trim();

        if (!CanTouchGame || !SaveManager.IsValidSavestateName(name))
            return;

        Result result = await RunAsync(Strings.BusySavingPoint, () => manager.CreateSavestate(name));

        if (result.Success)
            SaveName = "";
    }

    [RelayCommand]
    private async Task SettingsAsync()
    {
        if (await openSettings())
        {
            Changed = true;
            Reload();
        }
    }

    /// <summary>
    /// Torna a un punto o a una copia. Nessuna conferma: la situazione attuale
    /// viene comunque copiata prima. Solo le copie delle versioni precedenti,
    /// che potrebbero essere di un'altra partita, chiedono conferma.
    /// </summary>
    private async Task RestoreAsync(Snapshot snapshot, bool isPoint, bool legacy)
    {
        if (!CanTouchGame)
            return;

        if (legacy && !await dialogs.ConfirmAsync(Strings.ConfirmLegacyHeading,
                Loc.F(Strings.ConfirmLegacyText, SaveManager.ProfileDisplayName(profileName)),
                Strings.GoBackHere, kind: InfoKind.Warning))
            return;

        if (!await confirmCloud())
            return;

        await RunAsync(Strings.BusyGoingBack, () => isPoint
            ? manager.RestoreSavestate(snapshot)
            : manager.RestoreBackup(snapshot));
    }

    private async Task DeletePointAsync(Snapshot point)
    {
        string name = point.Name == "" ? Strings.UnnamedLower : $"«{point.Name}»";

        if (!await dialogs.ConfirmAsync(Loc.F(Strings.ConfirmDeletePointHeading, name),
                Strings.ConfirmDeletePointText, Strings.DeletePoint, destructive: true))
            return;

        await RunAsync(Strings.BusyDeletingPoint, () => manager.DeleteSavestate(point));
    }

    private void Reload()
    {
        Points.Clear();
        Copies.Clear();
        LegacyCopies.Clear();

        try
        {
            IReadOnlyList<Snapshot> points = manager.GetSavestates();
            IReadOnlyList<Snapshot> copies = manager.GetBackupsOf(profileName);
            IReadOnlyList<Snapshot> legacy = V1Compat.BackupsWithoutOwner(manager); // V1Compat: copie senza partita della 1.0

            foreach (Snapshot point in points)
            {
                Snapshot p = point;

                Points.Add(new HistoryRowViewModel(
                    p.Name == "" ? Strings.Unnamed : p.Name, p.Date, strong: true,
                    () => RestoreAsync(p, isPoint: true, legacy: false),
                    () => DeletePointAsync(p),
                    Loc.F(Strings.DeletePointNamed, p.Name == "" ? Strings.Unnamed : p.Name)));
            }

            foreach (Snapshot copy in copies)
            {
                Snapshot c = copy;

                Copies.Add(new HistoryRowViewModel(
                    SaveManager.BackupDescription(c), c.Date, strong: false,
                    () => RestoreAsync(c, isPoint: false, legacy: false), null, ""));
            }

            foreach (Snapshot copy in legacy)
            {
                Snapshot c = copy;

                LegacyCopies.Add(new HistoryRowViewModel(
                    SaveManager.BackupDescription(c), c.Date, strong: false,
                    () => RestoreAsync(c, isPoint: false, legacy: true), null, ""));
            }
        }
        catch (Exception ex)
        {
            SetStatus(Loc.F(Strings.CannotReadList, ex.Message), error: true);
        }

        ShowPointsEmpty = Points.Count == 0;
        ShowNoCopies = Copies.Count == 0;
        ShowLegacy = LegacyCopies.Count > 0;

        KeepText = manager.State.BackupsToKeep == 0
            ? Strings.KeepAllCopies
            : Loc.F(Strings.KeepCopies, manager.State.BackupsToKeep);

        UpdateControls();
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

            if (running != GameRunning)
                GameRunning = running;
        }
        finally
        {
            checkingGame = false;
        }
    }

    private void UpdateControls()
    {
        foreach (HistoryRowViewModel row in Points.Concat(Copies).Concat(LegacyCopies))
            row.Enabled = CanTouchGame;

        bool valid = SaveManager.IsValidSavestateName(SaveName.Trim());
        CanSave = CanTouchGame && valid;

        SaveHintIsWarning = GameRunning;
        SaveHintIsError = !GameRunning && !valid;

        SaveHint = GameRunning
            ? Loc.F(Strings.HistoryGameRunning, manager.Game.Name)
            : !valid
                ? Strings.InvalidSavestateName
                : Strings.SavePointHint;
    }

    private void SetStatus(string message, bool error, bool success = false)
    {
        Status = message;
        StatusIsError = error;
        StatusIsSuccess = success;
    }
}
