# Save configuration

A save configuration says where a game keeps its saves, so that someone who
already found out can share it. It is a `*.lss-config.json` file, written by
**Export save configuration…** and read by **Import save configuration…** in a
game's settings. LSS never downloads one: you send the file by hand.

The file holds no user name, no Steam id and no saves, only folders written
relative to a standard root, so it works on another computer.

## Format

```json
{
  "format": "lss-save-config",
  "version": 1,
  "os": "windows",
  "distro": "Microsoft Windows 10.0.26100",
  "steamDeck": false,
  "createdWith": "LSS 2.0.0.0",
  "created": "2026-10-30",
  "games": [
    {
      "provider": "steam",
      "id": "870780",
      "name": "Control Ultimate Edition",
      "installDir": "Control",
      "proton": false,
      "notes": "",
      "spots": [
        { "root": "WinAppDataLocal", "path": "Remedy/Control", "pattern": "*.sav", "recursive": false }
      ]
    }
  ]
}
```

- `provider` and `id` identify the game (`steam`, `epic`, `gog`, `heroic`,
  `manual`); if they don't match, LSS tries the name and the install folder.
- `proton` is true when the folders are inside a Proton or Wine prefix.
- A **spot** is a save folder: `root` + `path`, and optionally a `pattern`
  (several masks separated by `;`, for example `*.sav;slot?.dat`) and
  `recursive` for subfolders. An empty `pattern` means the whole folder.
- Roots: `gameinstall`, `WinMyDocuments`, `WinAppDataRoaming`,
  `WinAppDataLocal`, `WinAppDataLocalLow`, `WinSavedGames`, `WinUserProfile`,
  `LinuxHome`, `LinuxXdgDataHome`, `LinuxXdgConfigHome`. Steam paths may use
  `{64BitSteamID}` and `{Steam3AccountID}`.
- A game has at most 8 spots, a file at most 1 MB and 200 games. Folders that
  are not under a standard root (for example `D:\Games\Saves`) are not
  exported: LSS lists them so you can tell the other person.

## What import checks

The file is treated as untrusted. LSS refuses it, with the reason, if the
format name or version is wrong (a newer version asks you to update LSS), if a
limit above is exceeded, or if a root, a path or a pattern is not valid (paths
can't leave their root).

Then, for each game, it shows a preview and, for each folder, one of:

- **found**: the folder exists here and is safe to use;
- **not yet**: it doesn't exist or is empty (the game may not have run);
- **not for this system**: the root doesn't exist on this system;
- **refused**: the folder fails the safety check (a system folder, the launcher's
  or the game's own folder, the LSS data folder, or one with too many files
  to be saves).

Nothing is written until you confirm. If the game already has playthroughs, LSS
asks, for each new folder, whether the existing playthroughs get a copy of it
or start empty there. Games that are not installed here are skipped.

## Writing or fixing one by hand

Copy the example, keep `format` and `version`, and list your games and spots.
Use `/` in `path`. Import it: the preview tells you what is wrong.
