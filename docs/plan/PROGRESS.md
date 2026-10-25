# Progress

Tick a task (`- [x]`) when its commit is done and write the short commit
hash after it. 👤 = done by Andrix, skip it. Rules in [README.md](README.md).

## Done before this plan

- [x] A — Rename to LSS (`552e3c9`, `f5aeaa5`)
- [x] B — Avalonia test app in `spike/` (`71840cb`): builds, ~48 MB single
  exe for Windows and Linux, native dlls already signed, starts with Smart
  App Control on, window checked by Andrix on 2026-10-24.

## Phase C — Core library ([C-core.md](C-core.md))

- [x] C1 — Solution file (bafaacf)
- [x] C2 — `IPlatform`, still inside the WinForms project (035d396)
- [x] C3 — Card texts out of the WinForms control (b80bb70)
- [x] C4 — Projects `Lss.Core` and `Lss.Platform` (af1f6f1)
- [x] C5 — Tests on plain `net10.0` (98d083e)
- [x] C6 — Tests on Linux in GitHub Actions (79683d9)
- [x] C7 — 👤 Check the WinForms app, push, note failing Linux tests
  (2026-10-24: app works; CI run on `lss` by hand: Windows 92/92, Ubuntu
  90/92, the two failures are planned in H7)

## Phase D — Game catalogue ([D-catalog.md](D-catalog.md))

- [x] D1 — `GameInfo` and `SupportState` (c563816)
- [x] D2 — Library folders (e345f9b)
- [x] D3 — Manifests (91efc49)
- [x] D4 — Support state (745b9f8)
- [x] D5 — Images (2220175)
- [x] D6 — List the games of an account (401f983)
- [x] D7 — Docs (2195cef)

## Phase E — Any game ([E-per-game.md](E-per-game.md))

- [x] E1 — `SaveManager` gets a `GameInfo` (79efd2d)
- [x] E2 — Texts with the game name (765edb8)
- [x] E3 — `LaunchGame`, `IsGameRunning`, `GameStartTime` per game (e5f65bc)
- [x] E4 — Shared files per game (c1a2827)
- [x] E5 — Accounts and recovery for every game (f1cc69b)
- [x] E6 — 👤 Check with Control (2026-10-24, Andrix: game bar while Control runs, play time and save on exit when it closes)

## Phase F — Data folder ([F-data.md](F-data.md))

- [x] F1 — `AppSettings` (`lss.ini`) (eaedc98)
- [x] F2 — `SaveManager.Open` (9ecef98)
- [x] F3 — Migration from Control Save Manager (d5d2bba)
- [x] F4 — 👤 Migration with real data (2026-10-24: ControlSaveManager moved to LSS; both playthroughs, notes, saved points and 29 copies under `405015118/870780`)
- [x] F5 — Export format 2 (320fb77)
- [x] F6 — Docs for the data folder (62a5ef5)

## Phase G — Avalonia interface ([G-avalonia.md](G-avalonia.md))

- [x] G1 — Project skeleton (f1a0faa)
- [x] G2 — Theme and texts in XAML (24aa28d)
- [x] G3 — Services (db28682)
- [x] G4 — `MessageWindow` (3e4f19a)
- [x] G5 — `SessionTracker` (a4ce61a)
- [x] G6 — `GameViewModel` (36c5ebd)
- [x] G7 — Playthrough card (d5560fa)
- [x] G8 — Main window, games and monitor (21a0d0e)
- [x] G9 — New playthrough, rename, note (2741435)
- [x] G10 — History (f74f110)
- [x] G11 — Settings and first start (39c8f98)
- [x] G12 — Accounts and deleted playthroughs (396633a)
- [x] G13 — 👤 Full manual check (2026-10-24, Andrix: done; first round of visual fixes in `617b509`)
- [x] G14 — Remove WinForms, `Lss.App` becomes `LSS` (50eeb41)

## Phase H — Linux ([H-linux.md](H-linux.md))

- [x] H1 — Steam folders (241528a)
- [x] H2 — `registry.vdf` (bdb46ca)
- [x] H3 — Game processes (5870599)
- [x] H4 — Launching and single instance (35fd677)
- [x] H5 — Case of names (34b0517)
- [x] H6 — Paths and permissions (f77a0cb)
- [x] H7 — Linux tests required in CI (7217d15)
- [ ] H8 — 👤 Real Linux check (2026-10-24, Steam Deck: Steam found, games list right, all games save outside userdata so no switch could be tried; the app now opens anyway, `27146a0`. Still open: a switch and a game run on Linux with a supported game, Flatpak/Snap Steam)

## Phase I — Packages ([I-packaging.md](I-packaging.md))

- [x] I1 — `--version` (4891315)
- [x] I2 — Linux desktop files and icons (9158360)
- [x] I3 — `scripts/release.sh` (88519f5)
- [x] I4 — `nfpm.yaml` (deb, rpm) (b88596e)
- [x] I5 — AppImage (e5e9f9e)
- [x] I6 — Workflow (951350d)
- [x] I7 — 👤 First CI run (2026-10-24: locally with
  `scripts/linux-check.sh` on Andrix's Debian server: 176 tests on Linux, the
  four packages built, installed and started on Ubuntu 24.04/22.04, Debian 13,
  Fedora 42, Arch, AppImage. GitHub run #10 at `29f8be2`: test, windows and
  linux green, release skipped as expected without a tag)

## Phase J — Docs and release ([J-docs-release.md](J-docs-release.md))

- [x] J1 — 👤 Rename the repository (2026-10-24: renamed to
  `Andrix64/Local-Save-Switcher`, `origin` updated; description and topics
  set with `gh repo edit`)
- [x] J1a — New icon: three proposals (8f095a9)
  - A (`proposal-a.svg`): two cards, a grey one behind and one with a red border in front.
  - B (`proposal-b.svg`): two red arrows in a circle around the white dot of the current icon.
  - C (`proposal-c.svg`): three horizontal save slots, the middle one red and longer.
- [x] J1b — 👤 Choose the icon (Andrix chose B, 2026-10-24)
- [x] J1c — New icon in the app and the packages (8c9d40c)
- [x] J2 — Links and README (f1ec978)
- [x] J3 — Other documents (5e5c0ac)
- [x] J4 — Version `1.0.0-beta.1` (bd813d5; Linux check on lss-ci: 176 tests, six installs report `LSS 1.0.0-beta.1`)
- [x] J5 — 👤 First beta (2026-10-24: tag `v1.0.0-beta.1` at `4805f83`, all five CI jobs green, published as pre-release with `gh`; Windows exe not signed yet)
- [x] J6 — 👤 SignPath: closed (application sent 2026-10-24, refused 2026-10-25; decision: release without code signing, `9395e93`)
- [x] J7 — Further betas: skipped (they were only for the signed exe)
- [x] J8 — 1.0.0 (2026-10-25: tag `v1.0.0` at `85a86fe`, all five CI jobs green, published as latest release "LSS 1.0.0" without code signing; the 0.9.0 release is no longer on GitHub, so no "Continued as LSS" line)

## The 2.0 plan — phases K–Q

Order and reasons in [README.md](README.md) → "The 2.0 plan". One branch
per phase (the phase file names it).

### Phase K — Launchers in the core ([K-launchers-core.md](K-launchers-core.md))

- [x] K1 — `GameAccount` instead of `SteamAccount` (53e8d82)
- [x] K2 — `GameInfo` with a provider (89bcfc0)
- [x] K3 — Generic platform functions (5bbbfc9; Linux tests 184 passed on lss-ci)
- [x] K4a — The interface, unused (a82f1bc)
- [x] K4b — Accounts and games through the provider (e71c853)
- [x] K4c — Running, launch and cloud through the provider (1562655)
- [x] K4d — Clean-up and check (69fd782)
- [x] K5 — Data folder per provider (24931b1)
- [x] K6 — Export format 3 (9a4d94a)
- [x] K7 — Start-up and games list for several providers (8cd4ede)
- [x] K8 — 👤 Check (2026-10-25, Andrix: tested, works)

### Phase L — Save folders known by the launchers ([L-save-locations.md](L-save-locations.md))

- [x] L1 — Known folders (91e2f90; Linux 208 tests on lss-ci)
- [x] L2 — Installed Steam games without a `userdata` folder (6e58581)
- [ ] L3 — Binary VDF reader
- [ ] L4 — Steam save rules from `appinfo.vdf`
- [ ] L5 — Rules from the provider
- [ ] L6 — From a rule to real paths
- [ ] L7 — Safety of a save folder
- [ ] L8 — Locate the saves of a game
- [ ] L9 — Folder chosen or learned
- [ ] L10 — Every provider uses the locator
- [ ] L11 — `SaveManager` with a folder outside `userdata`
- [ ] L12 — Interface (advanced mode)
- [ ] L13 — 👤 Steam Deck and Windows check

### Phase M — Learn the save folder ([M-learn-saves.md](M-learn-saves.md))

- [ ] M1 — Search folders
- [ ] M2 — Changed files
- [ ] M3 — Candidates and their score
- [ ] M3b — Folders that are never saves
- [ ] M4 — The wizard
- [ ] M5 — Learning a game that already has playthroughs
- [ ] M6 — Screenshots
- [ ] M7 — 👤 Real check

### Phase N — Several save folders, look after every session ([N-several-folders.md](N-several-folders.md))

- [ ] N1 — `SaveSpot` and `GameInfo.Spots`
- [ ] N2 — Pattern spots in `FileOps` and `SaveFolderSafety`
- [ ] N3 — Spots of a game: `posti.ini`
- [ ] N4 — Snapshots with extra spots
- [ ] N5a — `SaveSet` without the journal, unused
- [ ] N5b — Journal and recovery
- [ ] N5c — `SaveManager` switches through `SaveSet`
- [ ] N5d — `SaveManager` saves through `SaveSet`, and the check
- [ ] N6 — The locator finds several spots
- [ ] N7 — Adding and removing a spot of a game that has playthroughs
- [ ] N8 — Export and import with spots
- [ ] N9 — Interface: the save folders of a game
- [ ] N9a — Save configuration: the file and the export
- [ ] N9b — Save configuration: import
- [ ] N9c — Save configuration: interface
- [ ] N10 — Look after every session (core)
- [ ] N11 — Look after every session (interface)
- [ ] N12 — Screenshots
- [ ] N13 — Docs for developers
- [ ] N14 — 👤 Checks

### Phase O — Other launchers and any game ([O-launchers.md](O-launchers.md))

- [ ] O1 — Epic Games Launcher (Windows)
- [ ] O2 — GOG Galaxy and GOG games (Windows)
- [ ] O3 — Heroic Games Launcher
- [ ] O4 — Any other game, added by hand
- [ ] O5 — Non-Steam games added to Steam
- [ ] O6 — The same game twice
- [ ] O7 — 👤 Checks

### Phase P — Players, favourites and the two modes ([P-simple-mode.md](P-simple-mode.md))

- [ ] P1 — Players
- [ ] P2a — Owner
- [ ] P2b — Favourite
- [ ] P2c — Existing playthroughs and export
- [ ] P3 — Getting a game ready for a player (simple mode)
- [ ] P4 — Mode setting and start-up
- [ ] P4a — Optional PIN for admins
- [ ] P5 — Advanced mode: playthroughs grouped by player
- [ ] P6 — Simple view: players and games
- [ ] P7 — Simple mode: play, and the "My games" menu
- [ ] P8 — Screenshots
- [ ] P9 — 👤 Check with real people

### Phase Q — Docs and release ([Q-release.md](Q-release.md))

- [ ] Q1 — Docs
- [ ] Q2 — Version `2.0.0-beta.1`
- [ ] Q3 — 👤 Beta, then 2.0.0

## Notes

Write here anything that didn't match the plan, decisions taken with
Andrix, and results of the 👤 checks. Newest first, with the date.

- 2026-10-25 — Fine fase K (Haiku). K1–K7 committati su `v2` con i tick. Test
  Windows: 204 verdi, 0 warning. Linux (`lss-ci`, `scripts/linux-check.sh
  lss-ci@192.168.1.3 --tests-only`, su `0c18b5d`): 204 verdi. Non provato a
  video: l'avvio con più launcher e la finestra principale con le intestazioni
  vanno controllati da Andrix (K8). Test cambiati, tutti voluti: K5
  `Open_UsesAccountAndGameFolder` (cartella `steam`); K6
  `Export_WritesFormat2WithGame` diventa `Export_WritesFormat3WithGameAndProvider`
  (`Formato=3`, `Provider=steam`); nessun altro test cambiato in K7. K8 saltato
  (👤).

- 2026-10-25 — K6 (Haiku), approved by the coordinating session: export
  `Formato=3` with `Provider`; import accepts formats 1 and 2 as Steam and
  refuses a zip of another launcher or game. Test changes (only the ones
  K6 makes on purpose): `Export_WritesFormat2WithGame` is now
  `Export_WritesFormat3WithGameAndProvider` (`Formato=3`, `Provider=steam`);
  new tests `Import_Format2IsSteam`, `Import_OtherLauncherIsRefused`.
  Commit `9a4d94a`, tick `59ffc7d`.
- 2026-10-25 — K7, comportamento scelto PRIMA di scrivere il codice (Haiku):
  - Avvio: App chiede a ogni provider (`Providers.All`) i suoi account.
    I controlli di Steam (cartella e userdata non trovate, `RecoverAllGameFolders`)
    restano come oggi, perché sono di Steam; resteranno finché c'è Steam solo.
  - Per ogni provider con account: se ne ha uno, si usa senza domande; se ne ha
    più, si apre `AccountWindow` di quel provider, con il nome del launcher nel
    titolo. Annullare salta quel launcher. Se alla fine non si è scelto nulla:
    se c'era stato un annullo si esce in silenzio (come oggi), altrimenti
    `NoAccountWithGames`.
  - Ultimo account: `lss.ini` come oggi, con `AppSettings.Refers` per provider.
  - Giochi: `provider.FindGames(account)` per ogni account scelto, nell'ordine
    dei provider.
  - Finestra principale: un solo elenco. Con più di un launcher, un'intestazione
    col nome del launcher sopra il primo gioco di ciascun launcher, nell'elenco
    dei giochi gestiti e in quello degli altri giochi. Con un solo launcher
    nessuna intestazione: come oggi.
  - Deviazione dal piano: "Altri giochi" resta un solo riquadro, non uno per
    launcher; le voci portano l'intestazione del launcher.
  - Intestazione della finestra: i nomi degli account scelti, separati da " · ".
  - Testi con "Steam" (`Strings.resx`), esito:
    - Cambiati (il nome del launcher è un parametro): `CaptionSteamAccount` →
      `CaptionLauncherAccount`; `AccountIntroAny` con `{0}`; `SettingAbout`
      "Steam account: {0}" → "Account: {0}".
    - Tenuti (descrivono un meccanismo di Steam): `SteamNotFound`,
      `UserDataNotFound`, `NoAccountWithGames`, `LaunchFailed`, `CloudBarText`,
      `CloudBarTitle`, `CloudConfirmHeading`, `CloudConfirmMaybe`, `CloudConfirmOn`,
      `CloudHelpHeading`, `CloudHelpText`, `FirstRunAccount`, `FirstRunCloudOff`,
      `FirstRunCloudOn`, `FirstRunCloudUnknown`, `GameNotSupported`,
      `SupportSavesElsewhere`, `NoSupportedGames`, `OnlyUserdataNotice`.
      `AccountIntro` non è usato da nessuno: lo lascio com'è.
  - Test: `MainWindowViewModelTests` (intestazioni solo con più launcher, account
    di ogni gioco). Test cambiati: nessuno, salvo quelli che controllano il testo
    cambiato, da dire nel commit.

- 2026-10-25 — Fase K: stop su K6 (Haiku). K5 committato (`24931b1`, tick
  `103da6f`) con la modifica di una sola asserzione, come concordato
  (`Open_UsesAccountAndGameFolder` ora si aspetta `steam\111\123450\Profili`).
  K6 non è iniziato nel codice: il test `PlaythroughTests` (riga 167)
  controlla `Formato=2` nel `partita.ini` esportato, e K6 lo porta a 3 con il
  launcher registrato. Il test non passa invariato, quindi mi fermo come
  per K5, ma senza la conferma scritta della modifica. Proposta: asserire
  `Formato=3` e `Provider=steam` (in più, accanto ad `AppId`), e l'import
  accetta anche il formato 2 come `steam`. K7 non iniziato: il comportamento di
  avvio va scritto qui prima di codificarlo. Fine fase (controllo Linux) non
  eseguito: la fase non è finita.

- 2026-10-25 — Fase K: stop su K5 (Haiku). K1–K4d sono committati su `v2`
  (ticks sopra). K5 non è committato: il test
  `SaveManagerTests.Open_UsesAccountAndGameFolder` si aspetta
  `<dati>\111\123450\Profili`, ma K5 vuole `<dati>\steam\111\123450\...`.
  Il test non può passare invariato, quindi per la regola 4 mi fermo.
  Codice di K5 nel worktree, non committato: `src/Lss.Core/DataLayoutMigration.cs`
  (nuovo), `AppSettings.cs` (`Refers`), `GameAccount.cs` e `GameInfo.cs` (`Key`),
  `SaveManager.cs` (percorsi e `LastAccount`), `App.axaml.cs`,
  `AccountViewModel.cs`, `MainWindowViewModel.cs`; test nuovi in
  `DataLayoutMigrationTests.cs` e in `AppSettingsTests.cs`. Risultato: 198 passano,
  1 fallisce (quello sopra). Da decidere con Andrix: accettare che quell'asserzione
  diventi `Path.Combine(folder.Data, "steam", "111", "123450", "Profili")` (cambia
  cosa controlla il test, quindi serve un sì). Scelte di K5 da confermare:
  `lss.ini` non è normalizzato in lettura (il test di andata e ritorno scrive
  "111"); i valori senza `/` sono Steam solo dove si confrontano (`AppSettings.Refers`);
  `DataLayoutMigration` prova `Directory.Move`, su disco diverso copia, verifica e
  poi elimina; se la destinazione è già identica elimina l'originale (spostamento
  interrotto), se è diversa non tocca nulla. K6 e K7 non iniziati (K7 va scritto
  qui prima di codificarlo).
- 2026-10-25 — K1–K4d (Haiku). Scelte fuori piano o da sapere:
  - K1: `"steam"` come primo argomento delle chiamate `GameAccount(...)` nei test
    e in `tools/UiScreenshots`: forzato dalla nuova firma. Confermato nella
    sessione di coordinamento. Baseline prima della fase: 177 test (il piano
    diceva 176).
  - K2: `InstallPath` è dopo `LibraryPath`. Chiamate di test e UiScreenshots con
    `"steam"` e `InstallPath` vuoto.
  - K3: su Linux la cache per processo è tolta (eseguibile e `environ`): ogni
    chiamata rilegge `/proc`. Su Windows la cache resta. `IsProcessRunning` non
    filtra per utente, come prima (il commento lo dice). Lo script va chiamato
    come `bash scripts/linux-check.sh <host> --tests-only` (il brief metteva il
    flag prima dell'host). Copia solo HEAD: committare prima. Linux: 184 test
    verdi su lss-ci a `5bbbfc9`.
  - K4a: `SteamProvider.FindAccounts()` fa quello che faceva App (account con
    giochi gestibili, altrimenti con qualsiasi gioco). Fino a K4c `GetCloudState`
    e `Launch` in `SteamProvider` sono copie della logica di `SaveManager`.
  - K4b–K4d: App prende la radice di Steam da `SteamProvider.FindRoot()` (il
    controllo «Steam non trovato» è di Steam); `SaveManager.Initialize` passa
    anch'esso da `SteamProvider`. `SaveManager` ha `Account` e un `IGameProvider`
    opzionale (default `SteamProvider`); `IsSteamRunning` resta statico.
  - K4d, grep: su `SaveManager*.cs`, `steam://|userdata|sharedconfig` dà solo un
    commento. `FindSteamRoots()` è chiamato solo da `SteamProvider` (in `src` e test).
  - Test: 177 → 184 (K3) → 191 (K4a) → 192 (K4c, K4d).

- 2026-10-24 — 2.0 plan written (Opus), phases K–Q, after Andrix's
  request: LSS lists the games of Steam, Epic and GOG (Heroic on Linux /
  Steam Deck) and any game added by hand; it finds the saves by itself
  (Steam `userdata` and Steam's own Auto-Cloud rules in `appinfo.vdf`, Epic's
  cloud-save folder, Heroic's GOG cloud locations), and every other game by
  watching what changed on the disk after "start, save, quit"; a simple mode
  (who's playing → click a game) next to today's advanced mode; every
  playthrough has a player, one favourite per player and game, the simple
  mode uses only the favourite (with a "My games" menu to switch), the
  advanced mode lists playthroughs grouped by player. Decisions
  with Andrix: 1.0 ships as it is and this plan is 2.0; no AI (rules and
  scores only, with the reasons shown); **no Ludusavi manifest or other
  third-party list** (a draft used it; removed); automatic detection only
  for Steam, Epic and GOG games, normal or through Proton/Wine; a look after
  **every** session for folders not detected yet; games with **several save
  folders** fully supported (phase N, extra spots in `.lss-posizioni/<id>/`
  inside each snapshot, so games with one folder don't change). Other
  choices: the data-folder move to `<data>/<provider>/...` goes in a new
  `DataLayoutMigration`, not in `LegacyMigration` (that file is only for
  Control Save Manager); the disk is scanned by timestamps after the game
  closes, no file watcher. GOG on Windows has no readable save data
  (Galaxy uses SQLite): GOG games there always use the search.
  Players are users or admins (the first is admin, at least one always);
  only admins open the advanced mode, add players and games, set up a game
  or allow a game with cloud saves (a user is never asked "play anyway");
  an admin may set an optional 4-digit PIN (P4a). K4, N5 and P2 are split in
  smaller commits. `appinfo.vdf` was read on Andrix's PC: format confirmed
  (v29), the four installed games that save outside `userdata` all have
  Steam rules; the Steam Deck file is still to read (L13, step 0).
  Save configurations (N9a–N9c): an admin exports where a game saves
  (portable roots, no user name, no Steam ids, no saves) to a
  `.lss-config.json` file and shares it by hand; importing checks it as
  untrusted and every folder still passes `SaveFolderSafety`. A folder
  found by LSS can be marked "not a save folder" for one game or for every
  game (M3b, stored in portable form so it also covers Proton prefixes);
  Settings lists them to undo.

- 2026-10-24 — Direction: LSS will support several game launchers after
  1.0 (Epic Games Launcher and others). Rules and a sketch are in
  `J-docs-release.md` → J9. The SignPath application describes LSS as a
  tool for PC games, with Steam as the launcher supported today.

- 2026-10-24 — Decision with Andrix: maintainer of the Linux packages is
  Andrix's GitHub noreply address
  (`44907638+Andrix64@users.noreply.github.com`) in `packaging/linux/nfpm.yaml`.
  The I4 question below is closed.

- 2026-10-24 — I5–I6 (Haiku). Scelte fuori piano:
  - I5: appimagetool fissato a `1.9.1` con l'impronta SHA-256 dell'asset di GitHub
    (`ed4ce84f…`, verificata scaricandolo), controllata dallo script dopo il download.
    AppImage provato solo fino alla chiamata di appimagetool: su Windows
    `Exec format error`, come previsto.
  - I6: il trasferimento dei test Windows (`.trx`) è un artifact a parte,
    `test-results-windows`, così `lss-windows` contiene solo zip e impronta.
    Il download in `release` prende `lss-*` (solo `lss-windows` e `lss-linux`).
    nfpm `2.47.0` è verificato con `checksums.txt` di quella release.
  - I6: il file del workflow è stato letto con `js-yaml` (Node): struttura e
    `needs` corretti; i nomi dei passi con `: ` vanno tra virgolette.
  - Da controllare in CI (I7): il profilo `linux-x64` ha `PublishDir` con `\`; su
    Windows funziona, su Ubuntu non è stato provato. `release.sh` su Ubuntu
    deve trovare `nfpm` (installato dal passo `Install nfpm`).
- 2026-10-24 — I1–I4 (Haiku). Da decidere con Andrix: `MAINTAINER_EMAIL` in
  `packaging/linux/nfpm.yaml` è ancora il segnaposto (I4). Scelte fuori piano:
  - I2: `Icon.ToBitmap()` fallisce sulle voci PNG di `app.ico`. Le sei voci
    (16–256) sono PNG: estratte direttamente dai byte dell'`.ico`, dimensioni
    verificate. Nessuna ricampionatura.
  - I2: nuovo `.gitattributes`: `*.sh`, `packaging/linux/*.desktop|xml|yaml` con
    fine riga LF (su questo PC `core.autocrlf=true` metterebbe CRLF negli script),
    `*.png` binari.
  - I4: nfpm `v2.47.0`: `v2.48.0` è uscita il 2026-10-12, meno di due settimane fa.
  - I3/I4: deb e rpm con nome esplicito (`--target dist/lss_<v>_amd64.deb`, come
    da tabella di I-packaging) invece di `--target dist/`, così il nome è noto
    per `.sha256`. `release.sh` controlla che `nfpm` esista prima di pubblicare.
  - Non eseguiti su questo PC (niente nfpm, Docker, WSL): `release.sh` provato su
    Git Bash con `--skip-tests --allow-dirty` (publish `linux-x64`, tar.gz, `install.sh`
    in una HOME temporanea, disinstallazione) e con un `nfpm` finto nel PATH.
    Il pacchetto vero e la sua installazione vanno provati in CI (I6/I7).

- 2026-10-24 — CI on `lss` at `693134f` (end of phase H): all green, Ubuntu
  required: the seven Linux-only tests of phase H pass. Actions updated to
  the current majors (checkout v7, setup-dotnet v6, upload-artifact v7,
  action-gh-release v3; I6 uses download-artifact v8) to remove the
  "Node.js 20 is deprecated" warnings.
- 2026-10-24 — G14 (Haiku): WinForms, `spike/` e `ZipFilter` eliminati; `LSS.exe`
  da `src/Lss.App`. Build 0 warning, 176 test verdi, 11 PNG con `tools/UiScreenshots`
  (01-main letto: titolo LSS, v0.9.0), `release.ps1 -AllowDirty -SkipTests` crea
  `dist\LSS-0.9.0-win-x64.zip` con `LSS.exe` e nessun `.pdb`; nessun blocco di Smart App Control.
  Scelte fuori piano o da controllare:
  - `linux-x64.pubxml` creato come chiede il passo 4 (il piano lo prevede già); lo usa I3.
  - La `Description` dell'exe è stata spostata com'era: dice ancora "(per ora solo Control)".
  - Restano commenti che citano le vecchie finestre WinForms (`GameInfo.cs`, `Theme.axaml`,
    `InfoKind.cs`, i `ViewModels`, `Views/DeletedWindow.axaml.cs`, `PlaythroughCardView.axaml.cs`):
    solo commenti, non toccati.
  - `DEVELOPMENT.md`: aggiornate tabella del codice, comandi di build/pubblicazione, versione
    in `Lss.App.csproj`, nome dell'artefatto (`lss`). Il resto del file non è stato riletto.
  - `bin/` e `obj/` in radice (ignorati da git) sono resti del vecchio progetto: non usati.
- 2026-10-24 — Fase H, H1–H7 (Haiku), 176 test verdi e 0 warning su Windows
  (`dotnet test`: 157 prima, 19 nuovi). Non eseguiti: i test Linux (nessun WSL
  né Docker sul PC), cioè `SteamLinkIsNotCountedTwice`, `NativeComesBeforeFlatpak`,
  `FolderWithoutUserdataIsSkipped`, `SecondLockFailsUntilTheFirstIsReleased`,
  `CopyKeepsUnixPermissions` e le varianti Linux di `Move_LeavesOldFolderIntactWhenItFails`
  e `Migrate_StaysInOldRootWhenFirstStepFails`: li esegue l'Ubuntu della CI
  (ora obbligatoria). Le funzioni che leggono /proc e il registro di Steam non hanno
  test: si verificano con H8.
  - H1–H4: `LinuxPlatform` non ha l'attributo `SupportedOSPlatform("linux")` del
    piano: con l'attributo il CA1416 scatta nei test delle funzioni pure, che girano su
    ogni sistema. La classe si crea solo su Linux (`Program.cs`).
  - H5: `ProfileExists` confronta i nomi senza maiuscole su ogni sistema; creazione e
    rinomina lo usano (prima `Directory.Exists`, che su Linux distingueva `Mario` da `mario`).
  - H6: la ricerca dei `\` in Core e Platform non ha trovato path da correggere (i casi
    rimasti sono elencati nel commit). Il test dei permessi 0o755 è stato scritto senza
    cambiare `File.Copy`: .NET su Linux di solito conserva il modo del file; se fallisce
    in CI, va aggiunto `File.SetUnixFileMode` dopo le copie in `FileOps`.
  - Fuori piano: `tools/UiScreenshots` aveva un warning (`Bitmap.Save` obsoleto),
    corretto in `3bcdd6b` perché il build deve avere 0 warning.
  - Durante il lavoro un `Lss.App.exe` avviato dal bin Release del repo (alle 12:28)
    bloccava la copia delle dll: build e test di prova con `-p:BaseOutputPath`, poi a
    app chiusa l'ultima verifica con l'output normale.
  - H8 (👤) non fatto.
- 2026-10-24 — G13, first round (Andrix + Opus): decisions: in
  confirmations the **verb is the default button** (Enter = confirm, Esc =
  cancel, Cancel on the left); the **Open folder** button stays in the
  deleted playthroughs window. Fixed after looking at every window with the
  new `tools/UiScreenshots`: text centred in all buttons, ⋯ button same
  height as the others with drawn dots, darker red for destructive buttons,
  hover on secondary/danger, app colours for list selection and for the
  Fluent accent (checkboxes, radio buttons), italic note no longer cut,
  History's Delete button styled. The rest of G13 (every function by hand)
  is still to do.
- 2026-10-24 — Phase G review (Opus): the main window was never shown
  (assigned to `desktop.MainWindow` after startup without `Show()`), and an
  exception in `StartAsync` was lost; both fixed in `01344b5`, with texts
  using `MaxWidth` left-aligned. Window seen with real data: games list
  (Control Ultimate Edition, Terraria), cards and buttons OK.
- 2026-10-24 — Fase G (G1–G12, Haiku): visual checks not done (no window was looked at).
  Andrix's G13 list: the dialogs' button order (see the G4 note below), hover colours of
  `Button.primary` (`/template/ ContentPresenter`, may not show with the Fluent template),
  the Italian texts (`!Key` would show a missing key), the game-card buttons, the Settings
  header button, the deleted-playthroughs link, the wait cursor while busy, the toast colours.
  The app was started only for 10 s after G1 (empty window): it was never run with real
  data, because the startup calls `LegacyMigration.Run()` on the real folders.
- G4/G3 — Deviation from the plan (kept WinForms): in confirmations the default button is
  **Cancel** (Enter and Esc cancel), the verb is secondary or danger. The plan said verb
  primary and Enter = verb; for deletions the WinForms choice is safer. `DialogService.Inform`
  / `Confirm` were a placeholder in G3 and became real in G4 (same commit series).
  WinForms' sound of the message box (`SystemSounds`) is not ported.
- G6 — Test setup deviation: `Avalonia.Headless.XUnit` 12.1.4 brings xunit.v3 and conflicts
  with xunit 2.9.2 of the test project (CS0433 on every `[Fact]`). Used `Avalonia.Headless`
  12.1.4 with `TestApp.Ensure()` (`SetupWithoutStarting`) in the constructor of the tests that
  need the dispatcher, and plain `[Fact]` instead of `[AvaloniaFact]`. That counts as the
  one failure the plan allows for the headless setup; the workaround passed on its first run.
- G6/G7 — `PlaythroughViewModel` is written in G6 (GameViewModel needs its type); G7 holds
  the card view and its tests. The commands Rename, Edit note, Save a point and History were
  placeholders in G7 (`NotImplementedException`) and are real since G9/G10.
- G8 — The header Settings button, the New playthrough button and the deleted link were
  added in G9, G11 and G12 (the plan puts them there). `OfferFirstRunAsync` and the list of
  ignored folders run when the window opens (first game) or at the first selection of each game
  after that (`MainWindowViewModel.OnOpenedAsync`).
- G8/G12 — Several accounts: G8 used the last one or the first one; G12 added AccountWindow
  and the startup now shows it when there is more than one account.
- Keys added that the plan did not list: `AppSubtitle` (the WinForms header wrote
  "LOCAL SAVE SWITCHER" as a literal), `AccountIntroAny` (the plan's `AccountIntro` names
  Control; the account window must not), `SharedFilesCaption`, `AddSharedFile`,
  `RemoveSharedFile`, `SharedFileOutside` (G11 list), `GamesCaption` etc. (G8 list).
- G3/G11 — `IDialogService.PickFileAsync` added for the shared-files picker (G11); the plan
  did not list it.
- G12 — The "Open folder" button in the deleted-playthroughs window is there because the
  plan lists it; the WinForms `DeletedForm` has no such button. Check with Andrix.
- G4–G12 — `CannotOpenFolder` receives the folder path, not the exception message (the
  `DialogService.OpenFolderAsync` returns only true/false).
- G2 — Colours not in the plan's table (`ErrorBack`, `ErrorBorder`, `DangerBorder`,
  `DangerText`) were added as the plan allows. Font: Inter (`WithInterFont`), not Segoe UI.
- G9 — Name box: the WinForms rule kept as is (rename with the same name in other case is
  allowed, the same name exactly is not).
- 2026-10-24 — Decision with Andrix: nobody has used Control Save Manager
  yet, so everything for its compatibility (data migration, its mutex,
  format 1 exports, the "data moved" message) is now in
  `src/Lss.Core/LegacyMigration.cs`, tests in `LegacyMigrationTests.cs`
  (was `AppDataTests.cs`), removable with the checklist in
  `docs/DEVELOPMENT.md` → "Compatibility with Control Save Manager". Rule
  added to README.md.
- 2026-10-24 — F3 review (Opus): (b) fixed: when ControlSaveManager → LSS
  finds LSS already with data the result is `BothExist`, even if Documents
  → ControlSaveManager moved first. (c)+(d): when a move fails the app stays
  in the old folder, and `RestructureAccounts` now runs there too, so the
  playthroughs are found under `IDAccount\870780` (before, the app would
  have seen none). (a), (e), (f) are fine as they are. New test
  `Migrate_FromDocumentsButLssHasData_IsBothExist`; 129 tests.
- 2026-10-24 — F3, to check with Andrix before F4: (a) `AppData.MigrateToLss`
  is `internal` with three roots and returns four values (result, detail,
  root in use, source of the move): the public wrapper needs the last two to
  set `Root` and `LastMoveSource`, and the tests need the roots as
  parameters. (b) Step 4 is applied as written, which gives `Moved` also when
  Documents → ControlSaveManager moved and ControlSaveManager → LSS found LSS
  already with data (`BothExist`): then ControlSaveManager's data stays out of
  use and the message says it was moved to LSS. Maybe it should say
  `BothExist` instead. (c) If the first step fails, `ReportDataMove` still names
  LSS as the destination. (d) `RestructureAccounts` also runs when the result
  is `BothExist` (only inside LSS). (e) `account.txt` is deleted only when
  `lss.ini` was written; if `lss.ini` already exists, `account.txt` is left.
  (f) `Move` already builds `newRoot + ".spostamento"`: unchanged.
  `SaveManager.IsNumeric` became `internal` so `AppData` can use it.
- 2026-10-24 — F2/F5: `ImportOtherGame` and `GameNotSupported` texts added as
  the plan says. Zip entries: the export check for `\` is `Any` instead of the
  plan's `All(!…)`, same meaning. Tests: 113 → 128.
- 2026-10-24 — E3: the check `grep -rn "870780\|Control_DX\|\"Control\""
  src/` lists, besides `GameInfo.cs` and `SteamLibrary.cs`, the literal
  `"Control"` of `SavesNotFound` in `SaveManager.Initialize` (E2 keeps it on
  purpose: that entry point is Control-only until phase G). `AccountForm.cs`
  (outside `src/`) has the same literal. `MinorBugTests.GameLauncherIsRecognized`
  is deleted with `IsGameLauncher` (its 5 cases: 110 → 106 tests, plus
  `LaunchGame_UsesTheGameAppId`).
- 2026-10-24 — E2: `AccountIntro` ("…with Control.") names the game too but
  is not in the table: changed the same way to `{0}`. Its call site
  (`AccountForm`, no `SaveManager` there) and `SavesNotFound` in `Initialize`
  pass `"Control"`. Until E3, the static `LaunchGame` also passes `"Control"`
  to `GameLaunching` and `LaunchFailed`.
- 2026-10-24 — E1: until E3, the static `LaunchGame` uses
  `GameInfo.ControlAppId` (it can't use `Game.AppId`: no instance yet).

- 2026-10-24 — CI run on `lss` at `683f447` (end of phase D): on Ubuntu
  only the two known failures of H7; every `SteamLibrary` test passes on
  Linux too.
- 2026-10-24 — CI is triggered automatically only by pushes to `main` and
  `v*` tags: on `lss` Andrix starts it by hand (*Actions → Build → Run
  workflow*). `lss` is merged into `main` only at the end.
- 2026-10-24 — C7, Ubuntu: `AppDataTests.Move_LeavesOldFolderIntactWhenItFails`
  (an open file doesn't block a move on Linux) and
  `LocalizationTests.StoredDatesAreShownInTheCurrentCulture` (ICU puts
  U+202F before PM). Fixes written in H7.
- 2026-10-24 — C4: the check "grep for Microsoft.Win32 / System.Windows.Forms /
  Process.Start in src/Lss.Core" also matches the standard resx header lines
  (`ResXResourceReader, System.Windows.Forms`) in `Strings.resx` and
  `Strings.it.resx`. Restricted to `*.cs` the grep prints nothing: the code
  is clean and the build has 0 warnings.
- 2026-10-24 — Plan written. Open questions for Andrix: maintainer email
  for the Linux packages (I4).
