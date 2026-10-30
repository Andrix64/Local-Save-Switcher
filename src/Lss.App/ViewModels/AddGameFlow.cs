using Lss.App.Services;

namespace Lss.App.ViewModels;

/// <summary>
/// "Aggiungi un gioco": prima il programma, poi il nome. Annullare una delle due
/// domande ferma tutto: non si aggiunge niente e non si apre altro.
/// </summary>
internal sealed class AddGameFlow(IDialogService dialogs)
{
    /// <summary>L'id del gioco aggiunto in ManualGames, o null se l'utente ha annullato.</summary>
    internal async Task<string?> AddAsync(string dataRoot, string startFolder)
    {
        string? exe = await dialogs.PickFileAsync(Strings.AddGamePickProgram, startFolder);

        if (exe is null)
            return null;

        string? name = await dialogs.PromptAsync(Strings.AddGameTitle, Strings.AddGameCaption,
            Strings.AddGameHint, Strings.AddGameConfirm, Path.GetFileNameWithoutExtension(exe), 80);

        if (name is null || name.Trim() == "")
            return null;

        return ManualGames.Add(dataRoot, new ManualGame(name.Trim(), exe, "", ""));
    }
}
