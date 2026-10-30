# Phase P — Players, favourites and the two modes

Goal:

1. **Every playthrough belongs to a player** (Andrea, Clara...). No
   playthrough is ever without one.
2. Each player has **one favourite playthrough per game** (★).
3. **Advanced mode** (today's app): the playthroughs of a game are listed
   **grouped by player**, with the ★ on each player's favourite.
4. **Simple mode**, usable by anyone: pick who's playing, click a game
   cover, play. It always uses **that player's favourite**. A separate menu
   (**My games** on each cover) lets the player switch to another of their
   playthroughs (it becomes the favourite) or start a new one.
5. When the game closes, LSS saves the progress into the playthrough that
   was played, in both modes.
6. Every player is a **user** or an **admin**. Only an admin can open the
   advanced mode, add players and games, set up a game, and allow a game
   whose cloud saves are on. A user can only play: pick a game, switch
   among their own playthroughs, start a new one.

Requires phases K–O (works with any game LSS can manage). Branch:
`players-and-modes`. Rules in [README.md](README.md).

## What the user sees

```
SIMPLE                                                   ADVANCED
┌──────────────────────────────────────────┐   ┌───────────────────────────────────────────┐
│            Who's playing?                │   │ Terraria                                   │
│   (A) Andrea   (C) Clara   (+) Add       │   │ ● Andrea                                   │
└──────────────────────────────────────────┘   │   ★ Main world        IN THE GAME NOW      │
        │ click                                │     Hardmode test                [Play]    │
        ▼                                      │ ● Clara                                    │
┌──────────────────────────────────────────┐   │   ★ Expert, big world            [Play]    │
│ ← Andrea                                 │   └───────────────────────────────────────────┘
│ [Hades ⋯] [Celeste ⋯] [Terraria ⋯] [+]   │
└──────────────────────────────────────────┘
  click cover → favourite of Andrea is put in place → game starts
  ⋯ "My games" → Andrea's playthroughs of that game, ★ on the favourite,
                 pick one = new favourite; "Start a new game"
```

## Words

| Word in the code | Meaning |
|---|---|
| player / `Persona` | someone who plays (Andrea). Not an OS account, not a Steam account. |
| owner | the player a playthrough belongs to (`Giocatore` in the playthrough's info file) |
| favourite / `preferita` | the one playthrough per player and game that the simple mode uses |
| role / `Ruolo` | `utente` (user: can only play) or `admin` (can change things) |

Steam / Epic / GOG accounts: both modes use the same launcher accounts (the
last used per provider). A player is **not** tied to a launcher account
(see "Later" in [Q-release.md](Q-release.md)).

## Rules (every task must keep them)

- Every playthrough has exactly one owner, an existing player. Creating,
  importing, recovering a deleted playthrough always sets one.
- A player with at least one playthrough of a game has exactly one
  favourite for that game. Deleting or moving away the favourite makes the
  player's most recently played remaining playthrough the favourite (none
  left → no favourite).
- There is always at least one player. The last player can't be removed.
- There is always at least one **admin**. The last admin can't be removed
  or made a user. The first player (P1) is an admin.
- An admin **must** have a 4-digit PIN (P4a, made mandatory later): a user needs
  one to become an admin, an admin can't lose it, and an admin without one
  (older data) is asked to set it the first time an admin is needed. Admin
  rights and the PIN are a guard against children and mistakes, not real
  security: anyone who can edit the data folder can remove a PIN.
- Removing a player: their playthroughs are **given to another player**
  (chosen by the user) or **deleted** (to the deleted folder, as today);
  never left without owner.

---

## P1 — Players

New file `src/Lss.Core/People.cs`, file `<data>/persone.ini` (via
`IniFile`, never throws):

```
Ordine=3fa2b1c0,9d01e4aa
3fa2b1c0.Nome=Andrea
3fa2b1c0.Colore=#C2410C
3fa2b1c0.Ruolo=admin
9d01e4aa.Nome=Clara
9d01e4aa.Colore=#1D6FD0
9d01e4aa.Ruolo=utente
```

```csharp
public sealed record Person(string Id, string Name, string Color, bool IsAdmin);

internal sealed class People
{
    public People(string dataRoot);
    public IReadOnlyList<Person> All { get; }
    public Person? Find(string id);
    public void Load();
    public Result<Person> Add(string name);       // colore: il primo libero della tavolozza
    public Result Rename(string id, string name);
    public Result SetColor(string id, string color);
    public Result Move(string id, int newIndex);
    public Result SetAdmin(string id, bool isAdmin);   // rifiuta di togliere l'ultimo admin
    public Result Remove(string id);              // solo il file; le partite le sistema P5
    public IReadOnlyList<Person> Admins { get; }
}
```

New players are users (`Ruolo=utente`); a missing or unknown `Ruolo` reads
as user, except that when the file has **no** admin at all (broken or hand
edited) the first player in `Ordine` is treated as admin and the file is
fixed on the next `Save`.

- `Id` = 8 random lowercase hex characters. Names: 1–30 characters, no
  two equal ignoring case (reuse `SaveManager.IsValidName` rules).
- Palette: eight colours with white text at 4.5:1 contrast at least,
  defined once in `People.Palette` (start from the design canvas: `#C2410C`,
  `#1D6FD0`, `#2F7D3B`, ...; list the final ones in the notes).
- **First player**: when `persone.ini` doesn't exist, `Load` creates one
  player named after the OS user (`Environment.UserName`, first letter upper
  case; empty → localised "Player 1"), **as admin**, so there is always
  one.
- `Remove` refuses the last player (`LastPlayer`) and the last admin
  (`LastAdmin`); `SetAdmin(false)` refuses the last admin.

Tests `PeopleTests.cs`: first player created as admin, add (user), rename,
duplicate name refused, remove, last player refused, last admin can't be
removed or made user, file without admin → first player admin, order,
palette cycles, broken file → first player again.

Commit: `Players`

## P2 — Owner and favourite of a playthrough

Read `SaveManager.Playthroughs.cs` (info files in `Info/<profile>.ini`,
export file `partita.ini`) first. Split in three commits; after each one
all existing tests pass (only calls that gain the new `ownerId` parameter
change).

### P2a — Owner

- The playthrough info file gets `Giocatore=<personId>`.
  `PlaythroughInfo` gets `string Owner` ("" when missing).
- `SaveManager`:

  ```csharp
  public Result SetOwner(string profileName, string personId);
  public IReadOnlyList<string> GetProfilesOf(string personId);
  ```

- `CreateProfile`, `AdoptGameSaves`, `ImportProfile`,
  `RecoverDeletedProfile` get an `ownerId` parameter (required, an existing
  player: unknown id → `Result.Fail`). Existing callers in the App pass the
  first player for now (P5 adds the choice).
- Rename and delete keep the info file as today (the owner goes with it).

Tests `OwnersTests.cs`: create / adopt / import / recover set the owner;
unknown owner refused; rename keeps it; `GetProfilesOf`.

Commit: `Every playthrough has a player`

### P2b — Favourite

- `preferite.ini` in the game's data folder: `<personId>=<profile folder>`
  plus `<personId>.Ultima=<date>` (last play, used to sort the simple
  mode's covers), via `IniFile`.
- `SaveManager`:

  ```csharp
  public string? FavouriteOf(string personId);
  public Result SetFavourite(string personId, string profileName);  // deve essere sua
  ```

- The first playthrough of a player for a game becomes their favourite.
  `RenameProfile`, `DeleteProfile` and `SetOwner` keep `preferite.ini`
  right, with the rule of "Rules" (deleted or given away → the player's
  most recently played remaining playthrough, none → no favourite; the new
  owner gets it as favourite only if they had none). `preferite.ini` is
  written only after the operation succeeded.

Tests: first playthrough is favourite, second isn't; delete favourite →
most recent becomes favourite; last one deleted → none; rename keeps it;
give away the favourite → both players right; `SetFavourite` of another
player's playthrough refused; failed rename leaves `preferite.ini` as it
was.

Commit: `Favourite playthrough of each player`

### P2c — Existing playthroughs and export

- **Existing playthroughs** (made before this phase, without
  `Giocatore`): when a game is opened, every playthrough without an owner
  gets the **first admin** (P1) and, for each player without a favourite,
  the active playthrough (else the most recently saved) becomes the
  favourite. Log it. This runs once per game: `ProprietariOk=1` in
  `stato.ini` after it succeeded; a failure leaves it unset so it runs
  again next time.
- Export writes `Giocatore` and the player's name in `partita.ini`; import
  ignores them (the user picks the owner, P5).

Tests: old playthroughs get the first admin and a favourite, once; a
second open doesn't change anything; export/import round trip with the
owner chosen on import.

Commit: `Players for existing playthroughs`

## P3 — Getting a game ready for a player (simple mode)

New file `src/Lss.Core/SimplePlay.cs`:

```csharp
/// <summary>Domanda che la modalità semplice può fare prima di giocare.</summary>
public interface ISimpleQuestions
{
    /// <summary>
    /// Di chi sono i salvataggi già presenti nel gioco? null = non si sa:
    /// vanno da parte (al giocatore attuale, non preferiti).
    /// </summary>
    Task<Person?> WhoseSavesAsync(string gameName, IReadOnlyList<Person> people);
}

internal static class SimplePlay
{
    /// <summary>Mette al loro posto i salvataggi preferiti del giocatore. Non avvia il gioco.</summary>
    public static Task<Result> PrepareAsync(SaveManager manager, People people, Person player,
        ISimpleQuestions ask);

    /// <summary>Come PrepareAsync, ma con una partita scelta dal menu "Le mie partite", che diventa la preferita.</summary>
    public static Task<Result> PrepareWithAsync(SaveManager manager, Person player, string profileName);

    /// <summary>Nuova partita vuota del giocatore, che diventa la preferita, messa al suo posto.</summary>
    public static Task<Result> PrepareNewAsync(SaveManager manager, Person player);
}
```

`PrepareAsync` steps:

1. Game running → fail `SimpleCloseGame`.
2. The active playthrough has unsaved progress (`HasUnsavedProgress()`) →
   `SaveProgress()` first: `ActivateProfile` only backs up the current
   saves, it doesn't save them into the active playthrough. Failure → stop.
3. The player has a favourite → `ActivateProfile` unless it is already the
   active one.
4. No favourite:
   - the game has **no playthroughs at all** and its folders have saves
     (`!GameSavesAreEmpty()`) → `WhoseSavesAsync`: a player P →
     `AdoptGameSaves(UniqueName(P.Name), P)` (P's favourite); null →
     `AdoptGameSaves(UniqueName(Strings.SavesBeforeLss), player)` (the
     player's, aside: the favourite rule makes it theirs, see step 5).
     Then step 5 always runs for the current player, unless the adopted
     saves were the current player's own: then they are the favourite and
     nothing else is created. Decided on 2026-10-25 by the plan's author, because the
     "Rules" say that a player with a playthrough always has a favourite:
     the new playthrough is then the favourite of the current player
     (`SetFavourite` explicitly), and the "Before LSS" one stays aside;
   - otherwise → step 5.
5. New playthrough: `CreateProfile(UniqueName(player.Name), copyCurrentSaves: false, player)`,
   favourite, `ActivateProfile`.

`PrepareWithAsync`: steps 1–2, `SetFavourite`, activate. `PrepareNewAsync`:
steps 1–2, then step 5 (new playthrough named
`UniqueName(player.Name)` → "Andrea 2").

Every `SaveManager` call keeps its own backups: nothing new to write for
safety. Tests `SimplePlayTests.cs` for every branch with a fake
`ISimpleQuestions`, checking the files of the game folders after each case.

Commit: `Simple mode: get a game ready for a player`

## P4 — Mode setting and start-up

- `lss.ini` gets `Modalita` = `semplice` or `avanzata` (`AppSettings.Mode`).
  Missing → the first-start window (`FirstRunWindow`, G11) asks with two big
  choices (see the design canvas): **Simple** and **Advanced**. Existing
  users (who already have `lss.ini`) get `avanzata` without being asked.
- Advanced → simple: header button **Simple mode**.
  Simple → advanced: ⚙ (small, top right) → **Advanced mode** → the
  window **Who are you?** (`AdminPickerWindow`) shows only the admins (big
  tiles, `AdminOnlyText` above them); picking one opens the advanced mode,
  Cancel goes back. The same window guards every admin action of the simple
  mode (P6, P7) when the current player is not an admin; it returns the
  chosen admin or null. An admin with a PIN is asked for it first (P4a).
- The main window shows `SimpleView` or the current content, by mode; both
  use the same loaded games and providers. Switching mode never changes any
  playthrough or favourite.

Tests: setting round trip, existing `lss.ini` without the key →
`avanzata`.

Commit: `Simple and advanced mode`

## P4a — PIN for admins (optional at first, mandatory now)

- `Person` gets `bool HasPin`. `persone.ini` stores
  `<id>.Pin=<salt base64>:<hash base64>`: PBKDF2-SHA256 with
  `Rfc2898DeriveBytes.Pbkdf2` (in .NET, no package, no `DllImport`), 16-byte
  random salt, 100,000 iterations, 32-byte hash. The PIN itself is never
  written or logged.
- `People`:

  ```csharp
  public Result SetPin(string id, string pin);      // 4 cifre, solo per un admin
  public Result RemovePin(string id);
  public bool CheckPin(string id, string pin);      // confronto a tempo costante
  ```

  A PIN is exactly 4 ASCII digits (`PinFormat` otherwise). Making an admin
  a user removes their PIN.
- **Admin picker** (P4): tapping an admin with a PIN opens a PIN pad in the
  same window: four dots, big digit buttons 0–9 (64 px at least, usable on
  the Steam Deck touch screen), ⌫, **Cancel**; the keyboard digits work too.
  Wrong PIN → the dots shake (or flash, if animations are off) and
  `PinWrong`; after 5 wrong tries in a row → 30 s wait with `PinWait`
  (counter in memory only, reset when LSS restarts). An admin without a
  PIN is picked at once, as before.
- **Players window** (P5), on an admin's row: **Set PIN…** / **Change
  PIN…** / **Remove PIN**. Changing or removing your own PIN asks the
  current one; **another admin** can remove a PIN without knowing it (that's
  the way out of a forgotten PIN). The only admin forgot it → the docs (Q1)
  say to delete the `<id>.Pin=` line from `persone.ini` with LSS closed.
- The advanced mode opened from the simple one stays open until the user
  switches back: no timeout.

Texts (both `resx`):

| Key | English |
|---|---|
| `PinTitle` | PIN of {0} |
| `PinPrompt` | Enter the PIN |
| `PinWrong` | Wrong PIN |
| `PinWait` | Too many tries. Wait {0} seconds. |
| `PinFormat` | The PIN is 4 digits. |
| `SetPin` | Set PIN… |
| `ChangePin` | Change PIN… |
| `RemovePin` | Remove PIN |
| `PinNew` | New PIN |
| `PinRepeat` | Repeat the new PIN |
| `PinMismatch` | The two PINs are different. |
| `PinOptional` | Optional. With a PIN, only you can use your admin rights. |

Tests `PinTests.cs`: set, check right and wrong, wrong format refused,
hash differs for the same PIN of two admins (salt), PIN not present in
`persone.ini` in clear, remove, user can't have a PIN, demoting removes it,
other admin removes it; view model: 5 wrong → wait, cancel returns null,
admin without PIN picked at once.

Commit: `Optional PIN for admins`

## P5 — Advanced mode: playthroughs grouped by player

Read `GameView.axaml`, `GameViewModel.cs`, `PlaythroughViewModel.cs`,
`PlaythroughCardView.axaml`, `NewPlaythroughViewModel.cs` first.

- The **save folders** block of the game (the main folder line, the other
  spots, **Add another folder…**, **Export save configuration…**) is
  **closed by default**: a header `SaveFoldersCount` ("Save folders ({0})" /
  "Cartelle dei salvataggi ({0})", `{0}` = number of spots) that opens it.
  Open or closed is kept for every game while the app runs (a static value,
  not saved). The line about pending suggestions (N11) stays visible below
  it even when it is closed. Asked by Andrix (2026-10-25): the long paths
  took too much space above the playthroughs. `tools/UiScreenshots` opens it
  for `19-game-spots`. Test: closed at start, opening it in one game keeps
  it open in another.
- The list of playthroughs of a game is **grouped by player**, in the
  `People` order: header with the player's coloured circle and name; inside,
  the favourite first, then the others by last save. Players without
  playthroughs of this game are not shown.
- Each card shows ★ (filled for the favourite, outline otherwise); clicking
  the outline ★ → `SetFavourite`. Tooltip `FavouriteTooltip`.
- ⋯ menu of a card: **Give to…** (another player; if it was the favourite,
  the rule applies to both players).
- **New playthrough** window: new field **Player** (list of players, default
  the owner of the active playthrough). **Import** asks the player the same
  way.
- Header button **Players…** → `PlayersWindow`: list of players (rename,
  colour, order, **Admin** checkbox, **Add player**, **Remove**). The
  checkbox of the last admin is disabled with the tooltip `LastAdmin`. Remove → `RemovePlayerWindow`:
  "What about the playthroughs of {0}?" with **Give them to:** (list of the
  other players) or **Delete them** (they go to the deleted playthroughs,
  recoverable), then `People.Remove`.
- The "deleted playthroughs" window shows the owner; recovering asks the
  player when the owner doesn't exist any more.
- The existing Cloud warning of a game (advanced mode) gets a second
  button **Allow in simple mode anyway** (`CloudAllowSimple`): writes
  `CloudConsentito=1` in the game's `stato.ini` (new `AppState` property)
  and shows `CloudAllowedSimple` with an **Undo** link. When the cloud is
  turned off the flag no longer matters (keep it).

Texts (both `resx`; Italian with the same meaning):

| Key | English |
|---|---|
| `PlayersButton` | Players… |
| `PlayersTitle` | Players |
| `AddPlayer` | Add player |
| `RemovePlayer` | Remove |
| `RemovePlayerText` | What about the playthroughs of {0}? |
| `GiveTo` | Give them to: |
| `DeleteThem` | Delete them (you can recover them from the deleted playthroughs) |
| `LastPlayer` | There must be at least one player. |
| `LastAdmin` | There must be at least one admin. |
| `PlayerAdmin` | Admin |
| `PlayerAdminTooltip` | Admins can open the advanced mode, add players and games, and allow games with cloud saves. |
| `CloudAllowSimple` | Allow in simple mode anyway |
| `CloudAllowedSimple` | Allowed in simple mode even with cloud saves on. |
| `PlaythroughPlayer` | Player |
| `GiveToMenu` | Give to… |
| `FavouriteTooltip` | Favourite: the one used in simple mode |
| `MakeFavourite` | Make favourite |

Tests on the view models (FakeDialogService): grouping and order, ★ click,
give to another player, new playthrough with player, remove player giving
the playthroughs away, remove player deleting them, last player refused.

Commit: `Advanced mode: playthroughs grouped by player`

## P5a — Deleted playthroughs: kept until deleted for good

Asked by Andrix (2026-10-25). Read `SaveManager.Playthroughs.cs`
(`GetDeletedProfiles`, `RecoverDeletedProfile`), `SaveManager.cs`
(`DeleteProfile`, `PruneBackups`), `DeletedWindow.axaml(.cs)` and its view
model first.

1. **Not pruned any more.** A deleted playthrough is a backup with the
   `Eliminato_` prefix (`BackupKind.Delete`), so `PruneBackups` deletes it
   with the other old backups after `BackupsToKeep` newer ones, without
   telling anyone. `PruneBackups` must skip every `BackupKind.Delete`
   backup, and they don't count towards `BackupsToKeep`. Test: with
   `BackupsToKeep = 2`, a deleted playthrough and five newer backups → the
   deleted playthrough is still there after the pruning, and two other
   backups are kept.
2. **Delete for good.** `SaveManager`:

   ```csharp
   /// <summary>Elimina per sempre una partita eliminata (non si può annullare).</summary>
   public Result PurgeDeletedProfile(Snapshot deleted);
   ```

   Same checks as `RecoverDeletedProfileCore` (kind `Delete`, inside
   `BackupPath`, exists), then `Directory.Delete` + `DeleteBackupInfo`,
   through `Finish` (log line `Elimina per sempre <name>`). Test: the folder
   and its info file are gone, a folder outside `BackupPath` is refused,
   other backups untouched.
3. **Interface** (`DeletedWindow`, advanced mode, so only admins): each row
   shows the name, the **player** it belonged to (P2: circle and name; "—"
   when the owner doesn't exist any more), when it was deleted and the play
   time; buttons **Recover** (as today) and **Delete for good…** (danger
   style). The confirmation is destructive (`ConfirmAsync(…, destructive:
   true)`, verb `PurgeVerb`): `PurgeText` names the playthrough and says it
   can't be undone. At the bottom, when there are at least two,
   **Delete all for good…** with its own confirmation (`PurgeAllText`, with
   the number). The intro text of the window changes from "kept" to
   `DeletedIntro`. Follow the design canvas (board "Popup · Partite
   eliminate").

| Key | English |
|---|---|
| `DeletedIntro` | Deleted playthroughs stay here until you recover them or delete them for good. |
| `PurgeButton` | Delete for good… |
| `PurgeAllButton` | Delete all for good… |
| `PurgeTitle` | Delete for good? |
| `PurgeText` | "{0}" will be deleted for good. This can't be undone. |
| `PurgeAllText` | {0} deleted playthroughs will be deleted for good. This can't be undone. |
| `PurgeVerb` | Delete for good |
| `Purged` | Playthrough deleted for good. |

Tests on the view model (FakeDialogService): purge asks and deletes; cancel
deletes nothing; purge all with two items; the window shows the owner.

Commit: `Deleted playthroughs: kept until deleted for good`

## P6 — Simple view: players and games

`SimpleView.axaml` + `SimpleViewModel` (one public type per file). Follow
the design canvas.

**Players page**

- Round tiles 140 px with the initial in white on the player's colour, the
  name below, in the `People` order; admins have a small shield mark
  (`aria-label`/tooltip `PlayerAdmin`). Last tile **Add player** → admin
  picker (P4) → name prompt (`TextPromptWindow`); the new player is a user.
- No rename/delete/role change here (advanced mode only).

**Games page** (after a click on a player)

- Header: ← with the player's circle and name, "Change player".
- Grid of covers (`CoverPath`; no cover → generated tile: game initials on
  a colour from the name's hash, `GameTile` control, reused by phase O),
  the launcher's name on the cover, game name below, under it the last play
  of **this player**. Ordered by the player's last play (`preferite.ini`),
  then by name.
- Every cover has a small **⋯** button (44×44 at least) → **My games** menu
  (P7).
- Games shown: every game with `Support == Supported`. Games not ready
  (`NotFound`, `SavesElsewhere`, `Unsure`, `NoSaves`) are shown **faded**
  with `SimpleNeedsSetup`; clicking → admin picker unless the player is an
  admin → the learn wizard (M4). `Unsafe` games are not shown.
- Games with the cloud on and not allowed (`CloudConsentito`) are shown
  with the label `SimpleCloudLabel`; see P7.
- Last tile **Add a game** (+) → admin picker unless the player is an
  admin → `AddGameWindow` (O4).
- A cover is disabled while that game is running, with `SimplePlaying`.

Tests on the view model: players order, games order per player, faded
games, running game disabled.

Commit: `Simple view: players and games`

## P7 — Simple mode: play, and the "My games" menu

**Click on a ready cover:**

1. Cloud check: `GetCloudState` `Enabled` and the game is not allowed
   (`CloudConsentito`):
   - the player is a **user** → the game doesn't start: `InformAsync` with
     `SimpleCloudBlocked` (names the admins) and the button
     **An admin is here** → admin picker → step "admin" below;
   - the player is an **admin** (or an admin was picked) → `ConfirmAsync`
     with `SimpleCloudAdminText` and two verbs: **Allow this game** (writes
     `CloudConsentito=1`, then plays) and **Play only this time**; Cancel
     stops.
   Never ask a user "play anyway".
2. `SimplePlay.PrepareAsync`, with `WhoseSavesAsync` as a dialog of big
   buttons (one per player + `SimpleDontKnow`). Failure → `InformAsync` with
   the result's text.
3. `provider.Launch(game)` if `CanLaunch`; otherwise the playing panel says
   `SimpleStartYourself`.
4. Playing panel: `SimpleHaveFun` and, only when `CanDetectRunning` is
   false, the button **I've finished playing**.
5. End: the existing `GameMonitor` session end, or the button →
   `SaveProgress(onGameExit: true)` **always** in simple mode (whatever the
   advanced "save on exit" setting says), `AddPlayTime`, toast
   `SimpleSaved`, last play date updated, back to the covers. Save failure
   → `InformAsync` with the error and `SimpleAskForHelp`. Then the scan
   after the session of N11 starts (it never asks anything in simple mode).
6. LSS closed while a game started from simple mode still runs: the
   existing close behaviour of the advanced mode applies (read
   `MainWindowViewModel` and write in the notes what happens).

**⋯ "My games" menu** of a cover (a small window, big rows):

- Title `SimpleMyGamesTitle`; one row per playthrough of this player for
  this game: name, last play, ★ on the favourite. Clicking a row →
  `PrepareWithAsync` (it becomes the favourite) and the game starts as above.
- Last row **Start a new game** → `PrepareNewAsync`, then the game starts.
- Nothing here deletes or renames.

Texts (both `resx`, short and friendly; Italian with the same meaning):

| Key | English |
|---|---|
| `SimpleWhoIsPlaying` | Who's playing? |
| `SimpleChangePlayer` | Change player |
| `SimpleWhatToPlay` | What do you play today? |
| `SimpleAddPlayer` | Add player |
| `SimpleAddGame` | Add a game |
| `SimpleNeedsSetup` | Needs setting up |
| `SimplePlaying` | Playing |
| `SimpleMyGames` | My games |
| `SimpleMyGamesTitle` | Your games of {0} |
| `SimpleFavourite` | The one you play |
| `SimpleNewGame` | Start a new game |
| `SimpleHaveFun` | Have fun, {0}! When you close {1}, LSS saves your game. |
| `SimpleStartYourself` | Now start {0} as usual. When you have finished, come back here. |
| `SimpleFinished` | I've finished playing |
| `SimpleSaved` | {0} saved for {1} |
| `SimpleCloseGame` | {0} is already open. Close it first. |
| `SimpleCloudLabel` | Ask an admin |
| `SimpleCloudBlocked` | {0} keeps a copy of the saves online, and it may bring back someone else's game. Ask an admin ({1}) to turn the cloud saves off or to allow this game. |
| `SimpleAdminHere` | An admin is here |
| `SimpleCloudAdminText` | Cloud saves are on for {0}: the launcher may bring back the saves of another player. Turning them off is safer. |
| `SimpleCloudAllow` | Allow this game |
| `SimpleCloudOnce` | Play only this time |
| `SimpleWhoAreYou` | Who are you? |
| `AdminOnlyText` | Only an admin can do this. |
| `SimpleAskAdminSetup` | An admin has to set up this game once. |
| `SimpleWhoseSaves` | There is already a saved game of {0} on this computer. Whose is it? |
| `SimpleDontKnow` | I don't know — keep it aside |
| `SimpleAskForHelp` | Ask whoever set up LSS to look at it. |
| `SimpleAdvanced` | Advanced mode |
| `SwitchToSimple` | Simple mode |
| `SavesBeforeLss` | Before LSS |

Tests (view model with fakes): play → prepare → launch → session end →
saved, toast; cloud on + user → blocked, game not prepared, admin picker
→ allowed; cloud on + admin → allow writes `CloudConsentito`, once doesn't;
cloud allowed → no question; launch not possible → text; manual "finished"
button; save failure message; My games → other playthrough becomes
favourite and is played; My games → new game; add a game / set up a game
as user → admin picker, cancelled → nothing happens.

Commit: `Simple mode: play, save and My games`

## P8 — Screenshots

`tools/UiScreenshots`: players page, games page (covers, generated tiles,
a faded game, a running game), My games menu, playing panel, the "whose
saves" question, advanced list grouped by player with ★, Players window,
remove-player window, first-start choice. Look at every PNG: big enough to
click on a Steam Deck screen (1280×800), nothing cut; compare with the
design canvas. Notes.

Commit: `Screenshots of players and modes`

## P9 — 👤 Check with real people

Andrix: two players, three games (one Steam, one Epic or GOG, one added by
hand), on Windows and on the Steam Deck. In simple mode: play, switch with
My games, new game. In advanced mode: the grouped list, ★, give a
playthrough to the other player, remove a player, make a player admin. As
a user: a cloud game is blocked, an admin allows it; adding a game asks
for an admin. Hand the simple mode to
someone who has never seen LSS and write in the notes where they got
stuck.
