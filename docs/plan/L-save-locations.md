# Phase L — Save folders known by the launchers

Goal: LSS finds by itself the saves of the games of **Steam, Epic and GOG**
that save "normally" or through Proton, using **only what the launchers
already keep on the PC**: no list from third parties, nothing downloaded.
Every other game (and every game whose launcher says nothing) uses the
search of phase M, or a folder chosen by hand.

Why it matters most: on a Steam Deck almost every game runs through Proton
and saves inside `steamapps/compatdata/<AppID>/pfx/...`. Today LSS lists
all of them under *Other games* and can't do anything (commit `27146a0`).

This phase does Steam; phase O adds the save data of Epic (and says why
GOG has none) with the same model.

Requires phase K. Branch: `save-locations` (from `main` after K is merged).
Rules in [README.md](README.md).

---

## Facts (read before every task)

### Where a game's saves are, in order

1. **Steam `userdata` with files** (today's rule): unchanged for the games
   that work now.
2. **A folder chosen or learned** for that game (`cartella.ini`, task L9,
   and phase M). It wins over the launcher's rules.
3. **The launcher's own save rules** (`IGameProvider.SaveRules`, L5):
   - **Steam**: the **Steam Auto-Cloud** rules of the game, in Steam's own
     cache `<Steam>/appcache/appinfo.vdf` (L3–L4). Every game that uses
     Steam Cloud without the Steam API has them: they say in which root
     (Documents, AppData, game folder...), sub-path and file pattern the
     saves are. Steam itself uses them to sync, also for Proton games.
   - **Epic**: the `CloudSaveFolder` of the game (phase O, check).
   - **GOG**: nothing reliable on the PC → search.
   - games added by hand, non-Steam shortcuts, sideloaded games → no rules.
4. Nothing found → the game is in *Other games* with the reason and the
   button **Find the save folder** (phase M).

Games without Steam Cloud (and without Epic cloud saves) have no rules
anywhere on the PC: they **always** go through the search. That's expected.

### Steam `appinfo.vdf` (binary) — checked on a real file

**Checked on 2026-10-24** (Opus) on a copy of Andrix's
`C:\Program Files (x86)\Steam\appcache\appinfo.vdf` with a throw-away
reader: version 29, 310 apps, all read without error with the types below,
32 apps with `savefiles`. Of the six games installed there, the four that
don't use `userdata` (NieR:Automata, Disco Elysium, Sekiro, Clair Obscur:
Expedition 33) all have rules; Terraria and Control have `ufs` without
`savefiles` (they use the Steam Cloud API → `userdata`). The Steam Deck's
file was not read: L13 checks it.

- Header: `uint32 magic`, `uint32 universe`; magic `0x07564428` (version
  28) or `0x07564429` (version 29, the one found). Version 29 then has an
  `int64` offset of a **string table** near the end of the file:
  `uint32 count`, then `count` NUL-terminated UTF-8 strings; in v29 every
  key of the binary VDF is an `int32` index into that table.
- Then, per app: `uint32 appId` (0 = end), `uint32 size` (bytes of the rest
  of this entry), `uint32 infoState`, `uint32 lastUpdated`, `uint64 picsToken`,
  `20 bytes sha1`, `uint32 changeNumber`, `20 bytes sha1 of the data`, then
  the binary VDF of the app (root key `appinfo`). Use `size` to skip the
  apps you don't need.
- Binary VDF: type byte `0x00` = object (key, then children until `0x08`),
  `0x01` = string (key, NUL-terminated value), `0x02` = int32 (key, 4
  bytes), `0x07` = uint64, `0x08` = end of object; another type → stop
  reading that app and log.
- Only apps Steam has data for are in the file (owned or seen ones): a
  game missing there simply has no rules.
- Save rules, as found (`appinfo/ufs/`):

  ```
  savefiles/0/root = WinAppDataLocal
  savefiles/0/path = Sandfall/Saved/SaveGames/{64BitSteamID}
  savefiles/0/pattern = *.sav
  savefiles/0/recursive = 1
  savefiles/1/platforms/1 = Windows        ← keys "1", "2"...; values Windows, Linux, MacOS, all
  rootoverrides/0/root = WinAppDataLocalLow
  rootoverrides/0/os = MacOS
  rootoverrides/0/oscompare = =
  rootoverrides/0/useinstead = MacAppSupport
  rootoverrides/0/addpath = ...            (optional)
  rootoverrides/0/pathtransforms/0/find = ZAUM Studio/Disco Elysium/SaveGames
  rootoverrides/0/pathtransforms/0/replace = ...
  ```

  `platforms` missing = every platform. `recursive` missing = `0`.
  Patterns seen: `*.*` (= every file, like `*`), `*.dat`, `*.sav`, exact
  names (`S0000.sl2`, `GraphicsConfig.xml`). Several rules per game are
  common (Sekiro: the save in `Sekiro/{64BitSteamID}` and a settings file in
  `Sekiro`; Disco Elysium: `SaveGames` and `Settings`): Steam syncs them
  all, so LSS switches them all (phase N).
- Roots found: `gameinstall`, `WinMyDocuments`, `WinAppDataRoaming`,
  `WinAppDataLocal`, `WinAppDataLocalLow`, `WinSavedGames`,
  `LinuxXdgDataHome`, `MacAppSupport`. Also handle `LinuxHome`,
  `LinuxXdgConfigHome` (not seen, same meaning as their names), the
  `Mac*` roots (always skipped) and `SteamCloudDocuments` (that's
  `userdata`: skip). Any other root → skip the rule and log.
- Tokens found in `path`: `{64BitSteamID}`. Also replace
  `{Steam3AccountID}` (not seen); any other `{...}` → skip the rule, log.

### Roots and what they become

`U` = the user folder inside the game's prefix (`GameInfo.PrefixPath`):
`<prefix>/drive_c/users/steamuser` for Proton, `<prefix>/drive_c/users/<Linux user>`
for Wine (try both, keep the one that exists). `C` = `<prefix>/drive_c`.

| Root | Windows | Linux, native game | Linux, game in a prefix |
|---|---|---|---|
| `gameinstall` | `InstallPath` | same | same |
| `WinMyDocuments` | `SpecialFolder.MyDocuments` (follows OneDrive) | through `rootoverrides`, else — | `U/Documents`, else `U/My Documents` |
| `WinAppDataRoaming` | `SpecialFolder.ApplicationData` | — | `U/AppData/Roaming` |
| `WinAppDataLocal` | `SpecialFolder.LocalApplicationData` | — | `U/AppData/Local` |
| `WinAppDataLocalLow` | `<home>/AppData/LocalLow` | — | `U/AppData/LocalLow` |
| `WinSavedGames` | `<home>/Saved Games` | — | `U/Saved Games` |
| `WinUserProfile` (LSS only, for Epic) | user profile (`Home`) | — | `U` |
| `Absolute` (LSS only, for Heroic) | `Path` is the full folder | same | same |
| `LinuxHome` | — | `$HOME` | — |
| `LinuxXdgDataHome` | — | `$XDG_DATA_HOME`, else `$HOME/.local/share` | — |
| `LinuxXdgConfigHome` | — | `$XDG_CONFIG_HOME`, else `$HOME/.config` | — |
| `Mac*` | — | — | — |

"—" = no value there: the rule is skipped. A game with a prefix uses the
rules for `Windows`; a native Linux game the rules for `Linux` plus the
Windows rules turned into Linux roots by `rootoverrides` (that's what Steam
does). Tokens: `{64BitSteamID}` = `76561197960265728 + accountId`,
`{Steam3AccountID}` = the 32-bit account id (the `userdata` folder name).

### Prefixes

- Steam (Proton): `<library>/steamapps/compatdata/<AppID>/pfx/`, in the
  game's library first, then in every library (SD card included: it is in
  `libraryfolders.vdf`). **One prefix per game.**
- Heroic: per game, from its config (phase O). Manual games: the user can
  give it (phase O). These prefixes may be **shared** by several games:
  `SaveContext.PrefixIsPerGame` says which.
- In a prefix, names may differ in case (`My Games` vs `my games`): on Linux
  every path segment is matched **ignoring case**.
- Steam Cloud also syncs these folders: the Cloud warning stays as it is.

### Games without a Steam `userdata` folder

`SteamLibrary.FindGames` today walks only `userdata/<AccountID>/`. A game
without the Steam Cloud API has **no folder there**, so it is not even
listed. L2 adds every installed game (from `appmanifest_*.acf`).

### Safety: LSS replaces the whole save folder

`FileOps.ReplaceFolder` makes the save folder identical to the playthrough.
A wrong folder (the whole Documents, the game installation) would be
destroyed. So **every** folder — from the launcher, chosen, learned — must
pass `SaveFolderSafety` (L7) when it is found **and** again when the game is
opened. This is the most important rule of the plan: **never weaken it to
make a game work.**

---

## L1 — Known folders

New file `src/Lss.Core/KnownFolders.cs`:

```csharp
/// <summary>
/// Cartelle dell'utente usate per trovare i salvataggi. "" se non esistono
/// su questo sistema (per esempio AppData su Linux).
/// </summary>
public sealed record KnownFolders(
    string Home, string AppData, string LocalAppData, string LocalAppDataLow,
    string Documents, string SavedGames, string Public, string ProgramData,
    string XdgData, string XdgConfig, string UserName);
```

`IPlatform` gets `KnownFolders GetKnownFolders();`

- `WindowsPlatform`: Windows column, `Xdg*` = "". `Public` =
  `Environment.GetEnvironmentVariable("PUBLIC") ?? ""`.
- `LinuxPlatform`: `Home`, `XdgData`, `XdgConfig`, `UserName`, and
  `Documents` = `$HOME/Documents` if it exists; the others "". Pure helper
  `internal static KnownFolders KnownFoldersFor(string home, string? xdgData, string? xdgConfig, string userName)`
  (empty or relative `XDG_*` values are ignored, as the XDG spec says).
- `FakePlatform`: a settable `KnownFolders` property.

Tests in `LinuxPlatformTests.cs` (pure, every system): with and without
`XDG_DATA_HOME`, relative value ignored.

Commit: `Known folders for save locations`

## L2 — Installed Steam games without a `userdata` folder

1. New enum in `GameInfo.cs`:

   ```csharp
   /// <summary>Da dove viene la cartella dei salvataggi.</summary>
   public enum SaveSource { Userdata, Launcher, Chosen, Learned, None }
   ```

   (Italian `<summary>` on each value.) `GameInfo` gets optional
   `SaveSource Source = SaveSource.Userdata`, `string Reason = ""` (detail
   for the log, Italian, never shown as is) and `bool HasOtherSaves = false`.
   `SupportState` gets `NotFound` (the launcher has rules but no folder
   exists yet), `Unsure` (several folders or only loose files) and
   `Unsafe` (the folder failed `SaveFolderSafety`).

2. `SteamLibrary.FindGames`: after the loop over `userdata`, read every
   `appmanifest_*.acf` of every library, with the same filters
   (`SystemApps`, `IsSystemName`). Each AppID not already listed becomes a
   `GameInfo` with `SaveFolder = ""`, `Support = NoSaves`,
   `Source = None`. Put the reading in
   `internal static IEnumerable<(string AppId, string Name, string InstallDir, string Library)> InstalledApps(IReadOnlyList<string> libraries)`.
   Add `"SteamVR"` to `SystemNamePrefixes`.

Tests (`SteamLibraryTests.cs`): installed game without `userdata` folder
listed once as `NoSaves`; a game with both listed once (from `userdata`);
`Proton 9.0` and `Steam Linux Runtime 3.0 (sniper)` not listed.

Commit: `List installed games without a userdata folder`

## L3 — Binary VDF reader

New file `src/Lss.Core/BinaryVdf.cs`: reads the binary VDF of "Facts" into
the same `Vdf.Node` used by the text VDF (`Vdf.cs`), so `Vdf.Find` works on
both. Strings → `string`, int32 / uint64 → their decimal text.

```csharp
internal static class BinaryVdf
{
    /// <summary>Legge un oggetto VDF binario. strings: tabella delle chiavi (v29) o null (v28).</summary>
    public static Vdf.Node Read(BinaryReader reader, IReadOnlyList<string>? strings);
}
```

Malformed data → `InvalidDataException` (callers catch it). Phase O uses
this reader for `shortcuts.vdf` too.

Tests with bytes built in the test: nested objects, strings with non-ASCII
letters, int32, uint64, keys from a string table, truncated data →
exception.

Commit: `Binary VDF reader`

## L4 — Steam save rules from `appinfo.vdf`

New file `src/Lss.Core/SaveRule.cs`:

```csharp
/// <summary>
/// Regola di un launcher: dove sono i salvataggi di un gioco. Root è uno dei
/// nomi della tabella delle radici (gameinstall, WinMyDocuments...).
/// </summary>
public sealed record SaveRule(string Root, string Path, string Pattern, bool Recursive,
    IReadOnlyList<string> Platforms);

/// <summary>Sostituzione di una radice su un altro sistema (rootoverrides di Steam).</summary>
public sealed record RootOverride(string Root, string Os, string OsCompare, string UseInstead,
    string AddPath, IReadOnlyList<(string Find, string Replace)> Transforms);
```

New file `src/Lss.Core/SteamAppInfo.cs`:

```csharp
/// <summary>
/// Regole Auto-Cloud dei giochi dalla cache di Steam (appcache/appinfo.vdf).
/// Letto una volta per avvio; nessuna eccezione verso l'esterno.
/// </summary>
internal sealed class SteamAppInfo
{
    public static SteamAppInfo Load(string steamRoot, IReadOnlySet<string> appIds);
    public IReadOnlyList<SaveRule> RulesOf(string appId);
    public IReadOnlyList<RootOverride> OverridesOf(string appId);
}
```

- Read the header, the string table (v29), then the apps; for apps not in
  `appIds` only skip `size` bytes. Unknown magic → empty and log.
- `pattern` missing → `*`. `path` missing → `""`. `/` and `\` both accepted.
- The file can be hundreds of MB: read with a `FileStream` (no
  `ReadAllBytes`), `FileShare.ReadWrite` (Steam may be writing it), and
  stop as soon as every requested app was found.
- Copy one real `appinfo.vdf` entry (one app, with `ufs`) from Andrix's file
  into `tests/Lss.Tests/Data/appinfo-sample.bin` **only if Andrix agrees in
  the notes** (it is Valve data); otherwise build the test file in code.

Tests: v28 and v29 files built in the test with two apps (one with two
`savefiles` and a `rootoverrides`, one without `ufs`); unknown magic →
empty; truncated file → what was read before the error.

Commit: `Steam Auto-Cloud rules from appinfo.vdf`

## L5 — Rules from the provider

`IGameProvider` gets (add it to K4's interface and to `FakeProvider`):

```csharp
/// <summary>
/// Regole dei salvataggi che il launcher conosce per i suoi giochi. Vuoto se
/// non ne sa niente: il gioco passa dalla ricerca sul disco.
/// </summary>
IReadOnlyList<SaveRule> SaveRules(GameAccount account, GameInfo game);
IReadOnlyList<RootOverride> RootOverrides(GameAccount account, GameInfo game);
```

`SteamProvider`: `SteamAppInfo.Load` once per `FindGames` with the AppIDs
of the list, then `RulesOf` / `OverridesOf`. Non-Steam shortcuts (phase O)
→ empty.

Tests: `SteamProvider` with a fake Steam folder and a v29 `appinfo.vdf`
built in the test.

Commit: `Save rules from the launcher`

## L6 — From a rule to real paths

New file `src/Lss.Core/SavePathResolver.cs`:

```csharp
/// <summary>Dati per trasformare le regole in cartelle.</summary>
public sealed record SaveContext(
    KnownFolders Folders,
    string PrefixPath,                       // GameInfo.PrefixPath o ""
    bool PrefixIsPerGame,
    string InstallPath,
    string AccountId,                        // 32 bit per Steam, "" altrove
    bool IgnoreCase);                        // true su Linux

internal static class SavePathResolver
{
    /// <summary>"Windows" se il gioco ha un prefisso o siamo su Windows, altrimenti "Linux".</summary>
    public static string PlatformFor(SaveContext context);
    /// <summary>Cartella dell'utente dentro il prefisso, o "" (U della tabella).</summary>
    public static string PrefixUser(SaveContext context);
    /// <summary>Cartella di una radice, o "" se non ha valore qui.</summary>
    public static string RootFolder(string root, SaveContext context);
    /// <summary>Percorsi che esistono per la regola: cartelle (pattern "*") o file.</summary>
    public static IReadOnlyList<string> Resolve(SaveRule rule, IReadOnlyList<RootOverride> overrides,
        SaveContext context);
}
```

`Resolve`:

1. The rule applies if `Platforms` is empty, contains `all`, or contains
   `PlatformFor(context)` (ignoring case). On native Linux, a rule whose
   root has an override with `os` = `Linux` and `oscompare` = `=` gets the
   root `UseInstead`, `AddPath` in front of its path, and each
   `pathtransforms` entry applied (`find` → `replace` in the path, ordinal).
   Another `oscompare` → skip that override and log. `RootOverride` gets
   `IReadOnlyList<(string Find, string Replace)> Transforms`.
2. Root → folder with the table ("" → empty result). Tokens replaced.
3. Reject (empty + log) a path containing `..` or that is not under the
   root after combining.
4. Walk segment by segment: a segment without `*`/`?` and with
   `IgnoreCase == false` is `Path.Combine`; otherwise enumerate the folder
   and keep the entries that match (`*` any characters, `?` one; ignoring
   case when `IgnoreCase`). Matcher written by hand in
   `internal static class Glob`, reused by phase N.
5. The folder found + `Pattern` (`*.*` means the same as `*`): `*` with
   `Recursive` → the folder itself (a whole-folder result); anything else →
   a **pattern result** (folder + pattern + recursive flag) with the
   matching files (top level, or every level when `Recursive`). L8 treats
   pattern results as `Unsure` until phase N supports them.
6. IO errors → that branch gives nothing (try/catch per folder). Full
   paths, no duplicates, sorted ordinal.

Tests `SavePathResolverTests.cs` (fake home and prefix in `TestFolder`):
`WinMyDocuments` on Windows; `WinAppDataLocal` into a Proton prefix
(`steamuser`) and into a Wine prefix (user name); `My Documents` fallback;
`LinuxXdgDataHome` without prefix; a `rootoverrides` with `pathtransforms`; `*.*` same as `*`;
`*.sav` with `Recursive` (files in subfolders too); a `rootoverrides` from `WinMyDocuments`
to `LinuxHome` with `addpath`; `{64BitSteamID}`; `*` in a segment matching
two folders; case-insensitive match with `IgnoreCase` (Linux only for the
"no match without it" half); `..` rejected; `Mac*` roots → empty; pattern
`*.sav` non-recursive → files.

Commit: `Resolve save rules into folders`

## L7 — Safety of a save folder

New file `src/Lss.Core/SaveFolderSafety.cs`:

```csharp
internal enum SafetyProblem { None, Missing, Empty, ProtectedFolder, TooBig, InsideLssData }

internal static class SaveFolderSafety
{
    public const long MaxBytes = 2L * 1024 * 1024 * 1024;   // 2 GB
    public const int MaxFiles = 20_000;

    /// <summary>Se la cartella si può sostituire per intero cambiando partita.</summary>
    public static SafetyProblem Check(string folder, SaveContext context,
        IReadOnlyList<string> launcherFolders, string lssDataRoot);
}
```

`launcherFolders` = every folder of a launcher found on the PC (Steam roots,
libraries and their `steamapps`, `steamapps/common`, `steamapps/compatdata`,
`userdata`, `appcache`; Epic, GOG, Heroic folders added in phase O).

`ProtectedFolder` when the full path (no trailing separator) **is equal to
or is a parent of** any of:

- every non-empty `KnownFolders` value and its parents (`C:\Users`,
  `/home` too), every drive root, `/`;
- the prefix, `C`, `C/users`, `U` and the folders of the prefix column
  (`AppData/Roaming`, `Documents`, ...);
- every `launcherFolders` entry;
- `InstallPath` itself (a folder **inside** the installation is allowed).

Also: inside `lssDataRoot` → `InsideLssData`; doesn't exist → `Missing`;
no files at any depth → `Empty`; more than `MaxFiles` files or `MaxBytes`
→ `TooBig` (stop counting at the limit). Compare ignoring case on Windows,
ordinal on Linux, after `Path.GetFullPath` and resolving links
(`ResolveLinkTarget(returnFinalTarget: true)`).

Tests `SaveFolderSafetyTests.cs`: Documents refused, folder inside it
accepted; profile, drive root, prefix `steamuser`, installation, Steam
library refused; folder inside the installation accepted; LSS data refused;
empty, missing, too many files (an `internal` overload takes the limits, so
the test is fast).

Commit: `Safety check for save folders`

## L8 — Locate the saves of a game

New file `src/Lss.Core/SaveLocator.cs`:

```csharp
/// <summary>Cartella dei salvataggi trovata, o il motivo per cui manca.</summary>
public sealed record SaveLocation(string Folder, SaveSource Source, SupportState Support,
    string Reason, bool HasOtherSaves);

internal static class SaveLocator
{
    public static SaveLocation Locate(GameInfo game, IReadOnlyList<SaveRule> rules,
        IReadOnlyList<RootOverride> overrides, SaveContext context,
        IReadOnlyList<string> launcherFolders, string lssDataRoot, GameSaveSettings settings);
}
```

Order:

1. `settings.Folder != ""` (L9) → safety check → `Chosen`/`Learned` +
   `Supported`, or `Unsafe` (reason = the problem). The rules are **not**
   tried after an unsafe choice: the user must see why.
2. Steam game with `userdata` files (`Support == Supported`, `Source ==
   Userdata`) → unchanged, but set `HasOtherSaves` if the rules also
   resolve to existing folders.
3. No rules → keep the game as it is (`SavesElsewhere` or `NoSaves`).
4. Resolve every rule, join the results, remove every path inside another.
5. Nothing → `NotFound`. One folder → safety → `Launcher` + `Supported`,
   or `Unsafe`. Only files, a pattern in subfolders, or several folders →
   `Unsure` (phase N makes them supported).

Tests `SaveLocatorTests.cs`: one folder → supported; folder + file inside
→ folder; two folders → `Unsure`; only files → `Unsure`; rules only for
`MacOS` → `NotFound`; chosen wins; unsafe chosen → `Unsafe`; userdata game
untouched, with `HasOtherSaves` when Documents also has saves.

Commit: `Locate saves outside userdata`

## L9 — Folder chosen or learned

New file `src/Lss.Core/GameSaveSettings.cs`: `cartella.ini` in the game's
data folder (`<data>/<provider>/<account>/<game>/cartella.ini`), keys
`CartellaSalvataggi` and `Origine` (`scelta` or `imparata`), read and
written with `IniFile` like `AppSettings` (never throws, errors logged).

```csharp
internal sealed class GameSaveSettings
{
    public GameSaveSettings(string gameDataFolder);
    public string Folder { get; set; }
    public SaveSource Origin { get; set; }   // Chosen o Learned
    public void Load();
    public void Save();                      // Folder "" → elimina il file
}
```

Tests: round trip, missing file, "" deletes it, path with spaces and
non-ASCII letters.

Commit: `Remember a save folder chosen or learned`

## L10 — Every provider uses the locator

`SteamProvider.FindGames` (and, in phase O, every provider) ends with one
shared helper:

```csharp
internal static class SaveLookup
{
    /// <summary>Applica SaveLocator a ogni gioco e registra quelli trovati fuori da userdata.</summary>
    public static IReadOnlyList<GameInfo> Apply(IReadOnlyList<GameInfo> games, IGameProvider provider,
        GameAccount account, KnownFolders folders,
        IReadOnlyList<string> launcherFolders, string lssDataRoot);
}
```

For Steam: `PrefixPath` from `SteamLibrary.FindProtonPrefix` (new,
returns "" on Windows; tests Linux-only), `PrefixIsPerGame = true`,
`AccountId` = the account folder name, rules from `provider.SaveRules`.
Result: `game with { SaveFolder, Source, Support, Reason, HasOtherSaves }`;
one Italian log line per game found outside `userdata`. Sorting unchanged.

Tests: fake Steam with a Proton game (Linux only) and a Documents game
(fake `KnownFolders`), both with rules in a test `appinfo.vdf`, found as
`Launcher`; a game without rules stays as it is; chosen folder → `Chosen`.

Commit: `Games list uses the launchers' save rules`

## L11 — `SaveManager` with a folder outside `userdata`

Read `SaveManager.cs` and `SaveManager.Playthroughs.cs` first.

1. Anything that finds the account from `Path.GetDirectoryName(SavePath)`
   (Cloud state, now in `SteamProvider`) must use `GameAccount.Folder`.
2. `Open`: for `Source != Userdata`, `FileOps.RecoverInterruptedReplace(game.SaveFolder)`
   first (try/catch + log); then `SaveFolderSafety.Check` again — not
   `None` → fail with `SaveFolderNoLongerSafe` (`{0}` game, `{1}` reason).
3. `grep -n "userdata\|GetDirectoryName(SavePath" src/Lss.Core`: list the
   results in the notes; fix only what is clearly the same bug.

Tests `ExternalSavesTests.cs`, save folder in a fake Documents: full round
(two playthroughs, switch, saved point, restore, export, import) leaves the
right files and nothing changed in a sibling folder; interrupted replace
recovered by `Open`; Cloud state read from the account folder.

Commit: `SaveManager works with saves outside userdata`

## L12 — Interface (advanced mode)

Read `GameItemViewModel.cs`, `MainWindowViewModel.cs`, `GameViewModel.cs`,
`MainWindow.axaml`, `GameView.axaml`, `IDialogService.cs` first.

1. `IDialogService.PickFolderAsync(string title, string startFolder)` →
   `Task<string?>` (`StorageProvider.OpenFolderPickerAsync`); fake too.
2. `SupportText` for `NotFound`, `Unsure`, `Unsafe`. New
   `SupportSavesElsewhere` and `NoSupportedGames` (they must no longer say
   that only `userdata` works).
3. In *Other games* each game has **Find the save folder…** (opens phase
   M's wizard; until M exists it opens the folder picker directly) and
   **Choose the folder myself…**: picker (from `U` for prefix games, else
   Documents), safety check (problem → message), then the confirmation
   `UseThisFolderText` (Warning, verb `UseThisFolder`), then
   `GameSaveSettings.Save` and reload the list keeping the selection.
   Write the confirm-and-save code once (`SaveFolderChoice` helper in the
   App), phases M, N and P use it.
4. Under the game title: `SaveFolderFrom…` line with the folder, clicking
   opens it. For `Chosen`/`Learned`, in the ⋯ menu,
   **Use the automatic folder** (clears `cartella.ini`, reloads). With
   `HasOtherSaves`: line `SomeSavesElsewhere`.

| Key | English | Italiano |
|---|---|---|
| `SupportNotFound` | No save folder yet: start the game once and save. | Nessuna cartella dei salvataggi: avvia il gioco e salva almeno una volta. |
| `SupportUnsure` | Saves are in several places: LSS can't switch them yet. You can choose the folder yourself. | I salvataggi sono in più posti: LSS non può ancora cambiarli. Puoi scegliere tu la cartella. |
| `SupportUnsafe` | The save folder found can't be used safely: {0} | La cartella dei salvataggi trovata non si può usare in sicurezza: {0} |
| `FindSaveFolder` | Find the save folder… | Trova la cartella dei salvataggi… |
| `ChooseSaveFolder` | Choose the folder myself… | Scelgo io la cartella… |
| `ChooseSaveFolderTitle` | Save folder of {0} | Cartella dei salvataggi di {0} |
| `UseThisFolderText` | When you switch playthrough, LSS replaces everything in this folder with the playthrough's files:\n{0}\nUse it only if it contains just the saves of {1}. | Quando cambi partita, LSS sostituisce tutto il contenuto di questa cartella con i file della partita:\n{0}\nUsala solo se contiene solo i salvataggi di {1}. |
| `UseThisFolder` | Use this folder | Usa questa cartella |
| `UseAutomaticFolder` | Use the automatic folder | Usa la cartella automatica |
| `SaveFolderFromSteam` | Saves: {0} (Steam) | Salvataggi: {0} (Steam) |
| `SaveFolderFromLauncher` | Saves: {0} (from {1}) | Salvataggi: {0} (da {1}) |
| `SaveFolderChosen` | Saves: {0} (chosen by you) | Salvataggi: {0} (scelta da te) |
| `SaveFolderLearned` | Saves: {0} (found by LSS) | Salvataggi: {0} (trovata da LSS) |
| `SomeSavesElsewhere` | This game also keeps saves in other folders: LSS switches only this one. | Questo gioco ha salvataggi anche in altre cartelle: LSS cambia solo questa. |
| `SaveFolderNoLongerSafe` | The save folder of {0} can no longer be used safely: {1} | La cartella dei salvataggi di {0} non si può più usare in sicurezza: {1} |
| `SafetyMissing` | the folder doesn't exist. | la cartella non esiste. |
| `SafetyEmpty` | the folder is empty. | la cartella è vuota. |
| `SafetyProtected` | it is a system, launcher or game folder, not a save folder. | è una cartella di sistema, di un launcher o del gioco, non dei salvataggi. |
| `SafetyTooBig` | the folder is too big for a save folder (over 2 GB or 20,000 files). | la cartella è troppo grande per dei salvataggi (oltre 2 GB o 20.000 file). |
| `SafetyInsideLss` | the folder is inside LSS's own data. | la cartella è dentro i dati di LSS. |

`{1}` of `SaveFolderFromLauncher` = the provider's `DisplayName` (Steam,
Epic Games...). New `NoSupportedGames`: "LSS found no save folder it can
use yet. Under *Other games* you see why for each game, and LSS can help
you find the folder." (Italian with the same meaning.)

Tests: texts per state; unsafe choice → message, nothing saved; confirmed
choice → `cartella.ini` written, list reloaded (FakeDialogService);
`LocalizationTests`, `XamlKeysTests`.

Commit: `Interface for saves outside userdata`

## L13 — 👤 Steam Deck check (desktop mode)

Andrix, AppImage built from the branch:

0. Before the work on the branch starts, if possible: copy the Deck's
   `~/.local/share/Steam/appcache/appinfo.vdf` to the PC so the agent can
   run the same throw-away count as on Windows (how many installed Proton
   games have `savefiles`). Write the numbers in the notes.
1. Which Proton games are supported (from Steam's rules), which are in
   *Other games* and why (list in the notes: this tells how many games will
   need the search).
2. One supported Proton game, Steam Cloud off for it: two playthroughs,
   play and save in each, switch, the game loads the right one; save on
   exit.
3. A game on the SD card, if there is one.
4. Choose a folder by hand: a good one, and the prefix's `Documents`
   itself (must be refused).
5. `log.txt` in the notes if anything is odd.

Then the same steps 1, 2, 4 on Windows with a game that saves in Documents
or AppData.
