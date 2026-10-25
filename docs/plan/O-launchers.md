# Phase O — Epic, GOG, Heroic and any other game

Goal: LSS finds by itself the games of **Steam, Epic Games and GOG** (on
Windows through their launchers, on Linux and Steam Deck through Heroic),
and lets the user **add any other game** by hand. Saves: what the launcher
itself knows (Epic's cloud-save folder, Heroic's GOG cloud locations),
otherwise the search of phase M.

Requires phases K–N. One branch per task (`launcher-epic`,
`launcher-gog`, `launcher-heroic`, `launcher-manual`, `launcher-shortcuts`).
Rules in [README.md](README.md).

**Facts marked "(check)" come from memory, not from a test.** Before coding
the task, look at a real file on Andrix's PC (ask in the notes and skip the
task if none is available) or at the launcher's public source; if the
format differs, stop and write what you found.

Each provider:

- implements `IGameProvider` (phase K), in `src/Lss.Core/<Name>Provider.cs`,
  OS-specific reads through `IPlatform` only;
- ends `FindGames` with `SaveLookup.Apply` (L10);
- adds its own folders to `launcherFolders` (protected by `SaveFolderSafety`);
- has tests with fake files in `TestFolder` (`<Name>ProviderTests.cs`);
- is added to `Providers.All`;
- games without cover get the generated tile of P6 (initials + colour).

---

## O1 — Epic Games Launcher (Windows)

- Installed games (check): `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests\*.item`,
  JSON: `DisplayName`, `AppName`, `InstallLocation`, `CatalogNamespace`,
  `CatalogItemId`, `bIsIncompleteInstall` (skip when true). No filter on
  `bIsApplication`: its meaning is not sure.
- Account: one, `GameAccount("epic", "local", "Epic Games", <Manifests folder>, null)`.
- Launch (check):
  `com.epicgames.launcher://apps/<CatalogNamespace>%3A<CatalogItemId>%3A<AppName>?action=launch&silent=true`
  via `OpenUri`.
- Running: `FindProcessesUnder(InstallLocation)`. Launcher running:
  `IsProcessRunning("EpicGamesLauncher")`.
- Saves: `SaveRules` from the game's **`CloudSaveFolder`**, the folder Epic
  itself uses for cloud saves (games without Epic cloud saves have none →
  search). Where it is on Windows (check, stop if not found on Andrix's PC):
  the launcher's catalog cache
  `%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Catalog\catcache.bin`
  (base64 of a JSON array of catalog items; the item with the game's
  `CatalogItemId` has `customAttributes.CloudSaveFolder.value`). Shared
  helper `EpicCloudFolder.ToRule(string value)` (also used by Heroic, O3)
  turns the value into a `SaveRule` with `Pattern = "*"`, `Recursive`:
  tokens (check, from Legendary's source) `{AppData}` → `WinAppDataLocal`,
  `{UserDir}` → `WinMyDocuments`, `{UserProfile}` → user profile (root
  `WinUserProfile`, in L6's table),
  `{UserSavedGames}` → `WinSavedGames`, `{InstallDir}` → `gameinstall`,
  `{EpicId}` → `*`; an unknown token → no rule, log.
- Cloud: `Enabled` when the game has a `CloudSaveFolder`, else `Unknown`.
  The Cloud warning text must not say "Steam" for this provider (new key
  `CloudOnGeneric`: "{0} keeps a copy of the saves online (cloud saves). It
  may bring back the saves of another playthrough: turn it off for this game
  in {1}.").
- Launcher folders: `%PROGRAMDATA%\Epic`, the parent of every
  `InstallLocation` (the Epic library folder).

Commit: `Epic Games Launcher`

## O2 — GOG Galaxy and GOG games (Windows)

- Installed games (check): registry
  `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GOG.com\Games\<gameID>`, values
  `gameName`, `path`, `exe`, `gameID`. Games installed with the offline
  installers are there too, so this works without Galaxy.
- Account: one, `GameAccount("gog", "local", "GOG", "", null)`.
- Launch: `StartProgram(exe, "", path)`.
- Running: `FindProcessesUnder(path)`. Launcher running:
  `IsProcessRunning("GalaxyClient")`.
- Saves: GOG keeps no save locations in a file LSS can read on Windows
  (Galaxy keeps them in a SQLite database: it would need a package) →
  `SaveRules` empty, every GOG game uses the search (phase M). Write a test
  that says so.
- Cloud: `Unknown`.
- Launcher folders: `%PROGRAMDATA%\GOG.com`, parent folders of `path`.

Commit: `GOG games`

## O3 — Heroic Games Launcher (Linux and Steam Deck, also Windows)

The usual way to play Epic and GOG games on a Steam Deck.

- Config folder, first that exists (check):
  `~/.config/heroic`, `~/.var/app/com.heroicgameslauncher.hgl/config/heroic`,
  `%APPDATA%\heroic`.
- Installed games (check):
  - Epic (Legendary): `<config>/legendaryConfig/legendary/installed.json`,
    object keyed by app name: `title`, `install_path`, `app_name`;
  - GOG: `<config>/gog_store/installed.json`, array `installed` with
    `appName` (GOG id), `install_path`;
  - Amazon (Nile): `<config>/nile_config/nile/installed.json` (if present);
  - sideloaded: `<config>/sideload_apps/library.json`.
- Names of GOG games: `<config>/store_cache/gog_library.json` (check), else
  the folder name.
- Prefix per game (check): `<config>/GamesConfig/<appName>.json` →
  `<appName>.winePrefix`; with Proton the user folder is under
  `<winePrefix>/pfx/drive_c/users/steamuser`, with Wine under
  `<winePrefix>/drive_c/users/<user>`: `GameInfo.PrefixPath` is the folder
  that contains `drive_c`. `PrefixIsPerGame` = true unless two games have
  the same prefix (then false for both).
- Account: one per Heroic install, `GameAccount("heroic", "local", "Heroic", <config>, null)`.
- Launch (check): `heroic://launch/<runner>/<appName>` with runner
  `legendary`, `gog`, `nile`, `sideload`. Not confirmed → `CanLaunch` false
  (the user starts it from Heroic; the wizard and simple mode handle it).
- Running: `FindProcessesUnder(install_path)` (with K3's Wine cmdline check).
- Saves:
  - Epic games: `CloudSaveFolder` from Legendary's metadata
    `<config>/legendaryConfig/legendary/metadata/<app_name>.json` →
    `metadata.customAttributes.CloudSaveFolder.value` (check), turned into a
    rule with `EpicCloudFolder.ToRule` (O1); the Windows roots go into the
    game's prefix like for Proton.
  - GOG games: the save locations Heroic stores after the first cloud sync,
    in `<config>/GamesConfig/<appName>.json` → `gogSaves` (array of
    `name`, `location`; check). Present → one rule per location (the path is
    already the real folder: root `Absolute` of L6's table). Missing → search.
  - Sideloaded and Amazon games: no rules → search.
- Launcher folders: `<config>`, every `winePrefix` parent folder
  (`~/Games/Heroic/Prefixes`).

Commit: `Heroic Games Launcher`

## O4 — Any other game, added by hand (`ManualProvider`)

For games without a supported launcher: DRM-free games, other launchers
(EA, Ubisoft, Battle.net, itch...), emulators, non-Steam games on the Deck.

- Account: one, `GameAccount("manual", "local", <localised "This computer">, <data>/manual/local, null)`.
- Each game: folder `<data>/manual/local/<id>/`, `id` = 8 random lowercase
  hex characters, file `gioco.ini`: `Nome`, `Eseguibile` (optional),
  `Argomenti` (optional), `Prefisso` (optional, Linux: a Wine/Proton
  prefix). The save folder is in `cartella.ini` (L9) like every game.
- `CanDetectRunning` = exe known. Running: `FindProcessesUnder(exe folder)`.
- `CanLaunch` = exe known and, on Linux, not a `.exe` file (a Windows exe
  can't be started without Wine: the user starts it from their launcher).
  Launch: `StartProgram(exe, arguments, exe folder)`.
- Saves: `SaveRules` empty. The game is in *Other
  games* with **Find the save folder…** until the search (phase M) or a
  folder chosen by hand gives it its save folders.
- Cloud: `Unknown`.

Interface (advanced mode, and the "+" tile of simple mode in P6):

- **Add a game…** at the bottom of the games list → `AddGameWindow`:
  1. **Program of the game** (file picker, optional; on Windows the filter
     is `.exe`), the name is filled from
     `FileVersionInfo.ProductName`, else the exe's folder name;
  2. **Name** (required, editable);
  3. Linux only, under *More*: **Wine / Proton folder** (folder picker,
     optional);
  4. **Add** → the game is added, then the learn wizard (M4) opens at once
    .
- ⋯ menu of a manual game: **Edit**, **Remove from LSS** (the
  playthroughs go to the deleted folder like a deleted playthrough; the
  save folder of the game is not touched).

Texts in both `resx`; tests for provider and view model.

Commit: `Add any game by hand`

## O5 — Non-Steam games added to Steam (Steam Deck)

- `userdata/<AccountID>/config/shortcuts.vdf` is **binary** VDF: read it
  with `BinaryVdf` (L3, version 28 style: keys are strings). Each shortcut: `appid`
  (uint32, may be stored as a negative int32), `AppName`, `Exe`,
  `StartDir`.
- Prefix: `compatdata/<appid as unsigned>/pfx` (per game).
- Launch (check): `steam://rungameid/<((ulong)appid << 32) | 0x02000000>`.
- Belongs to `SteamProvider`, shown in the Steam group. Saves: `SaveRules` empty for
  shortcuts: only the search (phase M) or a folder chosen by hand.
- Skip shortcuts whose `Exe` is a known launcher already handled
  (`heroic`, `EpicGamesLauncher`, `GalaxyClient`): the game is already listed
  by its own provider.

Commit: `Non-Steam games in Steam`

## O6 — The same game twice

A game can appear twice (Epic through the launcher and through Heroic on
Windows; a GOG game in Galaxy and as a Steam shortcut). When two games of
different providers have the same `InstallPath`, keep the one of the
provider listed first in `Providers.All` (Steam, Epic, GOG, Heroic,
manual) and log the other. Test.

Commit: `Same game in two launchers`

## O8 — An icon for every game

Asked by Andrix (2026-10-25): every game, of every launcher and on every
system, shows its own icon, not the initials. Only from files on the PC:
no network.

Order of the sources for a game (the first that gives an image wins):

1. what the launcher keeps: Steam's `librarycache` (as today), the `icon`
   field of a Steam shortcut (`shortcuts.vdf`, O5: a path to an image or an
   `.exe`);
2. the icon **inside the game's Windows executable** (`.exe`), read by our
   own code: works on Windows and on Linux / Steam Deck (Proton, Heroic,
   games added by hand);
3. Linux only, for native games: the `Icon=` of a `.desktop` file whose
   `Exec=` contains the game's executable (in `~/.local/share/applications`
   and `/usr/share/applications`), then an image next to the executable
   named `icon.png`, `<exe name>.png` or `<game folder name>.png`;
4. nothing → the initials, as today.

### The executable of a game

`IGameProvider` gets `string ExecutableOf(GameInfo game)` ("" when not
known; add it to `FakeProvider`):

- Epic: `InstallLocation` + `LaunchExecutable` of the `.item` manifest
  (check: the field name on a real file; this PC has Epic);
- GOG: `exe` of the registry key;
- Heroic: `executable` of Legendary's `installed.json` (check); for GOG and
  sideloaded games the field the files have, else the fallback below;
- games added by hand: `Eseguibile`;
- Steam: "" (Steam has its own images); shortcuts: `Exe`.

Fallback when the provider gives "" but the game has an `InstallPath`: the
largest `.exe` directly in `InstallPath` whose name doesn't contain
`unins`, `setup`, `crash`, `redist`, `vc_`, `dxsetup`, `launcher`
(ignoring case). Never search subfolders (too slow on big games).

### Reading the icon of an `.exe` (new file `src/Lss.Core/ExeIcon.cs`)

```csharp
internal static class ExeIcon
{
    /// <summary>
    /// L'icona più grande dentro un eseguibile Windows, come file .ico con una
    /// sola immagine; null se non c'è o il file non si legge. Non lancia mai.
    /// </summary>
    public static byte[]? Extract(string exePath);
}
```

Pure managed parsing of the PE file, read-only with a `FileStream`
(`FileShare.ReadWrite`), no `DllImport`, no new package, files over 1 GB
skipped:

1. DOS header: `MZ`, `e_lfanew` (int32 at 0x3C) → `PE\0\0`. COFF header:
   `NumberOfSections` (uint16 at +6), `SizeOfOptionalHeader` (uint16 at +20).
   Optional header magic `0x10B` (PE32, data directories at +96) or `0x20B`
   (PE32+, at +112); the resource directory is data directory 2 (RVA, size).
2. Section table right after the optional header, 40 bytes each:
   `VirtualSize` +8, `VirtualAddress` +12, `SizeOfRawData` +16,
   `PointerToRawData` +20. An RVA becomes a file offset through the section
   that contains it.
3. Resource tree: a directory is 16 bytes (`NumberOfNamedEntries` +12,
   `NumberOfIdEntries` +14) followed by 8-byte entries (`Name/Id`,
   `OffsetToData`; high bit of `OffsetToData` = subdirectory, offsets from
   the start of the resource section). Levels: type → name/id → language →
   data entry (16 bytes: data RVA, size). Types: `RT_ICON` = 3,
   `RT_GROUP_ICON` = 14.
4. Take the first `RT_GROUP_ICON`: `GRPICONDIR` (reserved, type, count:
   uint16 each) then `count` entries of 14 bytes (width, height, colors,
   reserved: bytes; planes, bit count: uint16; bytes in res: uint32; id:
   uint16). Width/height 0 means 256. Choose the largest, then the highest
   bit count.
5. Read the `RT_ICON` with that id and return a `.ico` with one image:
   header (0, 1, 1), one 16-byte directory entry (the same width, height,
   colors, planes, bit count, size, offset 22), then the image bytes as they
   are (PNG or DIB: Skia reads both inside an `.ico`).
6. Every offset and size is checked against the file length; anything out
   of range → null, logged once per file.

### Cache and use

- New file `src/Lss.App/Services/GameIcons.cs`: for a game without
  `IconPath`, find the source in the order above, turn it into a 64×64 PNG
  with Avalonia (`new Bitmap(stream)` then `CreateScaledBitmap`, `Save`),
  and keep it in `<data>/icone/<provider>_<id>.png`. The cache is used again
  while the source file's last write time is older than the cached PNG.
  Done in the background after the games list is shown (the list must not
  wait for it); the icon appears when ready. Any error → initials, log.
- `GameItemViewModel` (and the simple mode tiles later, phase P) use it.

### Tests

`ExeIconTests.cs`: a minimal PE32 and PE32+ built in the test with one
section holding a resource tree with a group icon and two icons (16×16 and
256×256 PNG bytes) → the 256 one is returned as a one-image `.ico`; no
resource section → null; a truncated file → null; offsets pointing outside
the file → null. On Windows only, a read-only smoke test on
`Environment.ProcessPath` (the test host exe has an icon) → not null.
`GameIconsTests.cs` (no real disk outside `TestFolder`): the source order
(shortcut icon before the exe, exe before `.desktop`), the cache reused and
refreshed when the exe is newer, the exe fallback skipping `unins000.exe`
and `UnityCrashHandler64.exe`.

Commit: `An icon for every game`

## O7 — 👤 Checks

Andrix: an Epic and a GOG game on Windows, Heroic on the Steam Deck, a game
added by hand on both. Notes for each: found, launched, running detected,
saves found (from the launcher / learned), switch works.
