# Game compatibility

Games tested with LSS. LSS works with Steam, Epic Games, GOG and Heroic games
and with any game added by hand (see the [README](../README.md#which-games-work)).
The columns say where the game was tested; *not tested* means nobody has
checked it yet, not that it doesn't work.

**Save folder** says how LSS found the saves: `userdata` (Steam's folder), a
`launcher` rule (Steam Auto-Cloud, Epic's cloud save folder), `learned` (found
by playing a bit and looking at the changed files) or `chosen` (picked by hand).

| Game | Launcher | Save folder | Windows | Linux | Notes |
|---|---|---|---|---|---|
| Control | Steam (870780) | `userdata` | Works (tested by hand) | Not tested yet | Shared file: `remote/preferences_data`, kept the same in every playthrough by default. Steam Cloud must be off. |
| Sekiro: Shadows Die Twice | Steam (814380) | `launcher` | Not tested yet | Works (Steam Deck, Proton) | Saves in the Proton prefix, `AppData/Roaming/Sekiro`: found by Steam's own rules and by *Find the saves*. |
| Terraria | Steam (105600) | `userdata` + `chosen` | Not tested yet | Not tested yet | Only the cloud characters and worlds are in `userdata`: add the folder with the other saves as an extra save folder. |

Games on the Steam Deck that keep their saves inside Proton are found through
the launcher's rules or with *Find the saves*. Tell us about yours with the
[game report form](https://github.com/Andrix64/Local-Save-Switcher/issues/new/choose).

## How to report a game

Tell us if a game works, partly works or doesn't work: open the
[game report form](https://github.com/Andrix64/Local-Save-Switcher/issues/new/choose)
on GitHub. Say which system and launcher you used, how LSS found the saves
and, for Steam, whether Steam Cloud was off. If you worked out where the game
saves, attach the `.lss-config.json` file from **Export save configuration**
(see [SAVE_CONFIG.md](SAVE_CONFIG.md)). The name and AppID are in the list of
games of LSS, or on the game's Steam page (the number in its address,
`store.steampowered.com/app/<AppID>`).
