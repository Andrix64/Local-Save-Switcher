namespace Lss;

/// <summary>
/// Stato dell'applicazione, salvato in stato.ini.
/// </summary>
public sealed class AppState
{
    private const string EmptyValue = "-";

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
    /// File da tenere uguali in tutte le partite (FileCondivisi in stato.ini),
    /// o null se non è mai stato impostato: allora valgono quelli di GameDefaults.
    /// </summary>
    public IReadOnlyList<string>? SharedFiles { get; set; }

    /// <summary>
    /// Le partite fatte prima delle persone hanno un proprietario (ProprietariOk in stato.ini).
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
        ActiveProfile = "";
        ActiveSince = "";
        LastSaved = "";
        BackupsToKeep = DefaultBackupsToKeep;
        LaunchGame = true;
        SaveOnGameExit = true;
        SharePreferences = false;
        SharedFiles = null;
        OwnersAssigned = false;
        CloudAllowedInSimple = false;

        if (!File.Exists(path))
            return;

        foreach (string line in File.ReadAllLines(path))
        {
            string[] parts = line.Split('=', 2);

            if (parts.Length != 2)
                continue;

            string value = parts[1] == EmptyValue ? "" : parts[1];

            switch (parts[0])
            {
                case "ProfiloAttivo":
                    ActiveProfile = value;
                    break;

                case "AttivatoIl":
                    ActiveSince = value;
                    break;

                case "SalvatoIl":
                    LastSaved = value;
                    break;

                case "BackupDaConservare":
                    if (int.TryParse(value, out int keep) && keep >= 0)
                        BackupsToKeep = keep;
                    break;

                case "AvviaGioco":
                    LaunchGame = value != "0";
                    break;

                case "SalvaAllaChiusura":
                    SaveOnGameExit = value != "0";
                    break;

                case "OpzioniCondivise":
                    SharePreferences = value == "1";
                    break;

                case "CloudConsentito":
                    CloudAllowedInSimple = value == "1";
                    break;

                case "ProprietariOk":
                    OwnersAssigned = value == "1";
                    break;

                case "FileCondivisi":
                    SharedFiles = value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
            }
        }
    }

    /// <summary>
    /// Scrive lo stato su disco. Lancia un'eccezione in caso di errore.
    /// Scrive prima un file temporaneo e poi sostituisce stato.ini, così
    /// un'interruzione non lo lascia vuoto o a metà.
    /// </summary>
    public void Save()
    {
        string temp = path + ".tmp";

        using (FileStream stream = new(temp, FileMode.Create, FileAccess.Write))
        using (StreamWriter writer = new(stream))
        {
            writer.WriteLine("ProfiloAttivo=" + Format(ActiveProfile));
            writer.WriteLine("AttivatoIl=" + Format(ActiveSince));
            writer.WriteLine("SalvatoIl=" + Format(LastSaved));
            writer.WriteLine("BackupDaConservare=" + BackupsToKeep);
            writer.WriteLine("AvviaGioco=" + (LaunchGame ? "1" : "0"));
            writer.WriteLine("SalvaAllaChiusura=" + (SaveOnGameExit ? "1" : "0"));
            writer.WriteLine("OpzioniCondivise=" + (SharePreferences ? "1" : "0"));

            if (SharedFiles != null)
                writer.WriteLine("FileCondivisi=" + Format(string.Join("|", SharedFiles)));

            if (OwnersAssigned)
                writer.WriteLine("ProprietariOk=1");

            if (CloudAllowedInSimple)
                writer.WriteLine("CloudConsentito=1");

            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }

    private static string Format(string value)
    {
        return value == "" ? EmptyValue : value;
    }
}
