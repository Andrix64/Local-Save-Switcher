using System.Text.Json.Serialization;

namespace Lss;

/// <summary>Come stato.json è scritto: nomi corti in italiano, come le chiavi del vecchio stato.ini.</summary>
internal sealed class AppStateFile
{
    [JsonPropertyName("ProfiloAttivo")]
    public string ActiveProfile { get; set; } = "";

    [JsonPropertyName("AttivatoIl")]
    public string ActiveSince { get; set; } = "";

    [JsonPropertyName("SalvatoIl")]
    public string LastSaved { get; set; } = "";

    [JsonPropertyName("BackupDaConservare")]
    public int BackupsToKeep { get; set; } = AppState.DefaultBackupsToKeep;

    [JsonPropertyName("AvviaGioco")]
    public bool LaunchGame { get; set; } = true;

    [JsonPropertyName("SalvaAllaChiusura")]
    public bool SaveOnGameExit { get; set; } = true;

    [JsonPropertyName("OpzioniCondivise")]
    public bool SharePreferences { get; set; }

    // null = mai impostato: valgono i file di GameDefaults.
    [JsonPropertyName("FileCondivisi")]
    public List<string>? SharedFiles { get; set; }

    [JsonPropertyName("ProprietariOk")]
    public bool OwnersAssigned { get; set; }

    [JsonPropertyName("CloudConsentito")]
    public bool CloudAllowedInSimple { get; set; }
}

/// <summary>
/// Stato dell'applicazione, salvato in stato.json.
/// </summary>
public sealed class AppState
{
    private readonly string path;

    public string ActiveProfile { get; set; } = "";
    public string ActiveSince { get; set; } = "";
    public string LastSaved { get; set; } = "";

    /// <summary>
    /// Quanti backup conservare (i più recenti). 0 = tutti.
    /// </summary>
    public int BackupsToKeep { get; set; } = DefaultBackupsToKeep;

    public const int DefaultBackupsToKeep = 20;

    /// <summary>
    /// "Gioca questa" avvia anche Control dopo il cambio di partita.
    /// </summary>
    public bool LaunchGame { get; set; } = true;

    /// <summary>
    /// Alla chiusura del gioco i progressi vengono salvati nella partita in uso.
    /// </summary>
    public bool SaveOnGameExit { get; set; } = true;

    /// <summary>
    /// Le opzioni del gioco (SharedFiles) restano le stesse cambiando partita.
    /// </summary>
    public bool SharePreferences { get; set; }

    /// <summary>
    /// File da tenere uguali in tutte le partite (FileCondivisi in stato.json),
    /// o null se non è mai stato impostato: allora valgono quelli di GameDefaults.
    /// </summary>
    public IReadOnlyList<string>? SharedFiles { get; set; }

    /// <summary>
    /// Le partite fatte prima delle persone hanno un proprietario (ProprietariOk in stato.json).
    /// </summary>
    public bool OwnersAssigned { get; set; }

    /// <summary>
    /// Il gioco con le copie online attive può andare in modalità semplice (CloudConsentito).
    /// Lo decide un amministratore; senza, la modalità semplice non lo fa partire.
    /// </summary>
    public bool CloudAllowedInSimple { get; set; }

    public AppState(string path)
    {
        this.path = path;
    }

    public void Load()
    {
        var file = JsonFile.Read<AppStateFile>(path) ?? new AppStateFile();

        ActiveProfile = file.ActiveProfile;
        ActiveSince = file.ActiveSince;
        LastSaved = file.LastSaved;
        BackupsToKeep = file.BackupsToKeep >= 0 ? file.BackupsToKeep : DefaultBackupsToKeep;
        LaunchGame = file.LaunchGame;
        SaveOnGameExit = file.SaveOnGameExit;
        SharePreferences = file.SharePreferences;
        SharedFiles = file.SharedFiles?
            .Select(name => name.Trim())
            .Where(name => name != "")
            .ToList();
        OwnersAssigned = file.OwnersAssigned;
        CloudAllowedInSimple = file.CloudAllowedInSimple;
    }

    /// <summary>
    /// Scrive lo stato su disco. Lancia un'eccezione in caso di errore.
    /// JsonFile scrive prima un file temporaneo e poi sostituisce stato.json, così
    /// un'interruzione non lo lascia vuoto o a metà.
    /// </summary>
    public void Save()
    {
        JsonFile.Write(path, new AppStateFile
        {
            ActiveProfile = ActiveProfile,
            ActiveSince = ActiveSince,
            LastSaved = LastSaved,
            BackupsToKeep = BackupsToKeep,
            LaunchGame = LaunchGame,
            SaveOnGameExit = SaveOnGameExit,
            SharePreferences = SharePreferences,
            SharedFiles = SharedFiles?.ToList(),
            OwnersAssigned = OwnersAssigned,
            CloudAllowedInSimple = CloudAllowedInSimple,
        });
    }
}
