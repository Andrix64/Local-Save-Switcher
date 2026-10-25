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
- [x] L3 — Binary VDF reader (2bf56cb)
- [x] L4 — Steam save rules from `appinfo.vdf` (eefb435)
- [x] L5 — Rules from the provider (b673864)
- [x] L6 — From a rule to real paths (92a9a59; Linux 266 tests on lss-ci)
- [x] L7 — Safety of a save folder (23bd093)
- [x] L8 — Locate the saves of a game (c362845)
- [x] L9 — Folder chosen or learned (f607c24)
- [x] L10 — Every provider uses the locator (8f9c563; Linux 299 tests on lss-ci)
- [x] L11 — `SaveManager` with a folder outside `userdata` (18202c0; Linux 304 tests on lss-ci)
- [x] L12 — Interface (advanced mode) (71f4d18; Linux 311 tests on lss-ci)
- [x] L13 — 👤 Steam Deck and Windows check (2026-10-25, Andrix: checked)

### Phase M — Learn the save folder ([M-learn-saves.md](M-learn-saves.md))

- [x] M1 — Search folders (1d0b994; Linux 327 tests on lss-ci)
- [x] M2 — Changed files (a554071, test fix 5e7d799; Linux 337 tests on lss-ci)
- [x] M3 — Candidates and their score (1ef5152; Windows 345 tests before M3b)
- [x] M3b — Folders that are never saves (ce2ed78; Linux 353 tests on lss-ci)
- [x] M4 — The wizard (ce24576; Windows 373 tests)
- [x] M5 — Learning a game that already has playthroughs (3413df7; Windows 376 tests)
- [x] M6 — Screenshots (a78b9cd; Windows 376 tests)
- [x] M7 — 👤 Real check (2026-10-25, Andrix on the Steam Deck: works after f42f760 and 58c4f89, see the notes)

### Phase N — Several save folders, look after every session ([N-several-folders.md](N-several-folders.md))

- [x] N1 — `SaveSpot` and `GameInfo.Spots` (97d27f2)
- [x] N2 — Pattern spots in `FileOps` and `SaveFolderSafety` (e70bd25)
- [x] N3 — Spots of a game: `posti.ini` (ad21c5e)
- [x] N4 — Snapshots with extra spots (9e0a8b2)
- [x] N5a — `SaveSet` without the journal, unused (36ff969)
- [x] N5b — Journal and recovery (fea1ca7)
- [x] N5c — `SaveManager` switches through `SaveSet` (dffe1fa)
- [x] N5d — `SaveManager` saves through `SaveSet`, and the check (48ecbc7)
- [x] N6 — The locator finds several spots (b5f8fd3)
- [x] N7 — Adding and removing a spot of a game that has playthroughs (3d46a6a)
- [x] N8 — Export and import with spots (e63cf3a)
- [x] N9 — Interface: the save folders of a game (586a6da)
- [x] N9a — Save configuration: the file and the export (30c162c)
- [x] N9b — Save configuration: import (c58138a)
- [x] N9c — Save configuration: interface (4300a43)
- [x] N10 — Look after every session (core) (ac0a8dc)
- [x] N11 — Look after every session (interface) (306d572)
- [x] N12 — Screenshots (faa21c5)
- [x] N13 — Docs for developers (d708e11)
- [x] N14 — 👤 Checks (2026-10-25, Andrix: checked)

### Phase O — Other launchers and any game ([O-launchers.md](O-launchers.md))

- [x] O1 — Epic Games Launcher (Windows) (67d1031)
- [x] O2 — GOG Galaxy and GOG games (Windows) (253fc6c)
- [x] O3 — Heroic Games Launcher (7df1fe1)
- [x] O4 — Any other game, added by hand (ca23683; menu Modifica/Rimuovi e test della vista non fatti, vedi note)
- [x] O5 — Non-Steam games added to Steam (b3bdde7)
- [x] O6 — The same game twice (cb3f550)
- [x] O8 — An icon for every game (d4c8ef4, 9751449)
- [x] O7 — 👤 Checks (2026-10-25, Andrix: checked)

### Phase P — Players, favourites and the two modes ([P-simple-mode.md](P-simple-mode.md))

- [x] P1 — Players (57664e2)
- [x] P2a — Owner (e375941)
- [x] P2b — Favourite (1c15ec6)
- [x] P2c — Existing playthroughs and export (3553e24)
- [x] P3 — Getting a game ready for a player (simple mode) (badb747)
- [x] P4 — Mode setting and start-up (dc7b761)
- [x] P4a — Optional PIN for admins (8e81077)
- [x] P5 — Advanced mode: playthroughs grouped by player (88b01ee)
- [x] P5a — Deleted playthroughs: kept until deleted for good (0d1b427)
- [x] P6 — Simple view: players and games (dc7b761)
- [x] P7 — Simple mode: play, and the "My games" menu (3292e4a)
- [x] P8 — Screenshots (b40ca6d)
- [ ] P9 — 👤 Check with real people

### Phase R — Fixes after the first try ([R-fixes.md](R-fixes.md)), before Q

- [ ] R0 — One file for the 1.0 compatibility (`V1Compat`)
- [ ] R1 — Renaming a playthrough keeps its automatic copies
- [ ] R2 — "Add a game": cancel means cancel
- [ ] R3 — PIN for every player, asked when picked; wrong tries per player
- [ ] R4 — Sidebar: launcher titles apart, no "Games" title, icons in "Other games"
- [ ] R5 — Long game names: "…" and a slow scroll
- [ ] R6 — Simple mode tiles: badge under the chip, icon when there's no cover
- [ ] R7 — A Simple / Advanced switch instead of ⚙
- [ ] R8 — Two settings windows: the game's and LSS's
- [ ] R9 — JSON settings files: the format and the app-wide files
- [ ] R10 — JSON for the per-game files
- [ ] R11 — Old paths with `\` on Linux
- [ ] R12 — Screenshots, docs, Linux check
- [ ] R13 — 👤 Check on Windows and the Steam Deck (copy the data folder first)

### Phase Q — Docs and release ([Q-release.md](Q-release.md))

- [ ] Q1 — Docs
- [ ] Q2 — Version `2.0.0-beta.1`
- [ ] Q3 — 👤 Beta, then 2.0.0

## Notes

Write here anything that didn't match the plan, decisions taken with
Andrix, and results of the 👤 checks. Newest first, with the date.

- 2026-10-25 — Fase P, grafica rifatta come il canvas (Andrix: "la grafica e' molto diversa da
  quella dell'artifact"), commit `d5e81d4`, `54003d1`, `7df1c1d`. Test Windows: 749 verdi. Linux
  (`lss-ci`, completo): tutto a posto.
  - Rifatte: vista semplice (persone, giochi, in gioco), "Le mie partite", "Di chi è?", "Chi sei?" e
    tastierino PIN, primo avvio (scheda scelta + "Continua con …"), avanzata (schede su una riga,
    ★ gialla, cartelle in un riquadro chiuso), Giocatori (tabella, menu ⋯), nuova partita
    (pastiglie del giocatore), partite eliminate (azioni sulla riga).
  - Mancava in P4a: "Imposta PIN…" / "Cambia PIN…" / "Togli il PIN" nella finestra Giocatori.
    Aggiunta la finestra "PIN di …"; cambiarlo chiede quello attuale, toglierlo no.
  - Ora una domanda sola: togliere una persona (dalle a / eliminale) e il cloud per un admin
    (consenti / solo questa volta / annulla), come nel canvas.
  - Schermate nuove: 30 togli persona, 34 tastierino, 35 "PIN di …", 36 cloud admin.
  - Non rifatte: "Aggiungi un gioco" e le finestre delle fasi M/N/O (fuori dalla fase P).

- 2026-10-25 — Fase P, P3 fatta (Haiku, decisione dall'autore del piano, opzione b): commit
  `badb747` (tick `23944ba`). Test Windows: 660 verdi. Nel commit e in `P-simple-mode.md`
  (passo 4) la frase "e **non** preferita" è sostituita dal comportamento deciso.
  Lo stesso per il ramo "i salvataggi sono di un altro giocatore P": P prende la sua come
  preferita, il giocatore attuale una nuova.
- 2026-10-25 — Fase P, P8 (Haiku), commit `b40ca6d` (tick nel commit successivo). Test Windows: 742
  verdi, 0 warning. Linux (`lss-ci`, `--tests-only`, `b40ca6d`): 742 verdi.
  - Schermate: `tools/UiScreenshots` (output in una cartella temporanea, `it`): 33 PNG. Nuove: 23
    persone (modalità semplice), 24 giochi di Andrea, 25 "Le mie partite", 26 pannello in gioco, 27
    "di chi sono i salvataggi", 28 avanzata raggruppata per persona, 29 finestra Giocatori, 30 e 31
    togliere una persona (conferma e "dai le partite a"), 32 prima scelta di modalità, 33 "Chi sei?".
  - Viste e confrontate con il canvas (sono le schede `Main`, `Games`, `MyGames`, `Playing`,
    `WhoseSaves`, `Advanced`, `Players`, `RemovePlayer`, `FirstRun`, `AdminPin`): le schede di
    persone, la scelta "di chi sono", la barra cloud con "Consenti anche in modalità semplice", il
    gruppo per persona con ★ e ☆, la finestra Giocatori, la conferma e la scelta "Dai le partite a"
    corrispondono. Cosa ho corretto guardando le immagini: il nome del launcher ora è "Steam" (non
    "steam"); il simbolo dello scudo è più grande; il pulsante del colore mostra il colore e
    "Rinomina…" non è tagliato; le descrizioni della scelta di modalità non sono in grassetto; "Chi sei?"
    ha il pulsante Annulla; l'intestazione della modalità avanzata aveva troppi pulsanti e copriva il
    titolo: "Giocatori…" e "Modalità semplice" sono ora nelle Impostazioni del gioco, come nel canvas
    (`Settings` ha entrambi i link).
  - Non guardate in questa sessione: 02–07 e 09–22 (finestre di 1.0 e delle fasi L–O). Non le ho
    cambiate in P, tranne la finestra "Nuova partita" (04/05) che ha ora il campo Giocatore quando ci
    sono più persone: lo schermo di prova ha una sola persona, quindi non appare. Da guardare con
    Andrix sul computer vero.
  - Da vedere con Andrix (P9 👤, saltato): la vista semplice su un Deck (schermo 1280×800), i tasti
    grandi, la velocità con molti giochi (ogni tile apre il gioco).
- 2026-10-25 — Fase P, P7 (Haiku), commit `3292e4a` (tick nel commit successivo). Test Windows: 742
  verdi, 0 warning. Cosa c'è: giocare dalla modalità semplice (`SimplePlayFlow`): controllo cloud
  (un utente manda a un admin; l'admin consente il gioco o lo gioca solo questa volta; cloud spento =
  nessuna domanda), preparazione (`SimplePlay`), avvio, pannello "Buon divertimento", fine (segnale del
  launcher, o "Ho finito di giocare" se il launcher non lo sa dire), salvataggio sempre alla chiusura,
  tempo di gioco, poi la ricerca di cartelle dopo la partita (N11, senza domande). Menu "Le mie
  partite" (`MyGamesWindow`): le partite della persona, la preferita per prima; una riga la gioca e la
  rende preferita; "Inizia una nuova partita". Niente si elimina né si rinomina lì.
  - Deviazioni: (1) l'avviso cloud usa la barra del gioco per il consenso, il piano dice "Consenti" nei
    pulsanti dell'avviso. Per "Consenti" e "Solo questa volta" si fanno due domande in sequenza (la
    finestra ha un solo pulsante di conferma). (2) "Ho finito" e l'esito si mostrano nel pannello e in
    `Message`, niente toast. (3) Le scritte "non lo so" sono il pulsante annulla della scelta di persona.
  - **Chiudere LSS mentre gira un gioco avviato dalla modalità semplice**: la finestra si chiude e
    l'app si chiude (come oggi nella modalità avanzata). Il salvataggio alla chiusura del gioco non
    parte in nessuna delle due modalità: i progressi sono quelli salvati l'ultima volta, e il tempo di
    gioco di quella sessione si perde. Da decidere con Andrix (uguale ai 1.0 o da cambiare).
  - Non verificato a schermo: il pannello, il menu ⋯ e "Le mie partite" (P8).
- 2026-10-25 — Fase P, P4 e P6 (Haiku), commit `dc7b761` (tick nel commit successivo). Test
  Windows: 730 verdi, 0 warning. Cosa c'è: modalità in `lss.ini`; intestazione "Modalità semplice";
  vista semplice con le persone (piastrelle da 140 px, scudo per gli admin, "Aggiungi giocatore":
  admin, poi nome) e con i giochi del giocatore (copertina o piastrella generata, launcher, ultima
  partita, avviso cloud, sbiadito con "Da sistemare" se non è pronto, disattivato mentre gira);
  "⚙" torna alla modalità avanzata chiedendo un admin (con PIN se c'è).
  - **Non fatto, spostato in P7**: giocare un gioco pronto dalla modalità semplice (oggi il clic non
    fa niente, `MainWindowViewModel.PlaySimpleAsync`) e il menu ⋯ "Le mie partite" con il pulsante
    della copertina (44 px).
  - Deviazioni: la pagina "Chi gioca?" usa lo stesso `PersonTile` della finestra admin; "Aggiungi un
    gioco" usa la procedura della modalità avanzata (`AddGameAsync`) dopo il controllo admin. Il
    collegamento "Passa alla modalità semplice" delle Impostazioni (canvas `Settings`) non c'è: il
    pulsante dell'intestazione lo sostituisce.
  - `IDialogService` e `People` sono pubblici (la finestra di scelta admin ha bisogno di `People`):
    `People` era `internal`.
  - Costo: scegliere un giocatore apre il gioco di ogni tile (`GameFor`), per avere l'ultima partita
    e lo stato cloud. Con molti giochi la prima apertura sarà lenta: da misurare (P8).
  - Non verificato a schermo: la vista semplice (P8).
- 2026-10-25 — Fase P, P5a (Haiku), commit `0d1b427`. Test Windows: 720 verdi, 0 warning. Linux
  (`lss-ci`, `--tests-only`, `0d1b427`): 720 verdi.
  - **Cambio di comportamento voluto di `PruneBackups`**: prima le partite eliminate entravano nel
    conteggio e venivano eliminate con le copie vecchie; ora si saltano (né contano né si eliminano).
    Test nuovo: `DeletedPlaythroughsTests.PruningKeepsDeletedPlaythroughsAndDoesNotCountThem`. Nessun
    test esistente è cambiato.
  - Il proprietario di una partita eliminata è nel suo file di backup (`Giocatore`): `DeletedOwnerOf`.
    Il recupero (`RecoveryOwnerOf`) lo usa, come da P5.
  - Finestra: "Elimina per sempre…" (la selezionata) ed "Elimina tutte per sempre…" da due partite
    in su; entrambe chiedono conferma distruttiva. Ogni riga mostra la persona (pallino, nome, "—" se
    non esiste più) e il tempo di gioco. Il pulsante di eliminazione è rosso solo dal colore del testo
    (non c'è uno stile "pericolo" nel tema).
  - Non verificato a schermo: la finestra delle partite eliminate (P8).
- 2026-10-25 — Fase P, P5 **completata** (Haiku), commit `b52a089` … `88b01ee` (tick nel commit
  successivo). Test Windows: 712 verdi, 0 warning. Deviazioni dal piano, in ordine di importanza:
  - Recupero di una partita eliminata: **nessuna domanda** (il piano diceva "chiede alla persona
    quando il proprietario non esiste più"). Come da indicazione di Andrix: va alla persona di prima se
    esiste ancora, altrimenti al primo amministratore (`RecoveryOwnerOf`), come le partite di prima di 2.0.
  - Rimozione di una persona: prima una conferma "Elimina le partite" (rossa), poi, se si dice no, la
    scelta "Dai le partite a:". Annullando la scelta non cambia nulla. Il piano non diceva due passi:
    serve perché la finestra di conferma ha due pulsanti.
  - Cloud "Consenti anche in modalità semplice": sulla barra del cloud, non nella finestra di avviso
    (quella ha una sola coppia di pulsanti). `CloudConsentito` in `stato.ini`.
  - Blocco "Cartelle dei salvataggi (n)": lo stato aperto/chiuso è statico: vale per tutti i giochi
    finché l'app è aperta, ma le altre finestre aperte non si aggiornano da sole (si aggiornano alla
    prossima selezione del gioco).
  - Una sola lista di persone per tutti i giochi (`MainWindowViewModel`), così nomi e colori sono gli
    stessi ovunque. Togliere una persona tocca le partite di ogni gioco che si apre; un gioco che non si
    apre viene saltato (nel log).
  - Scelta del giocatore all'importazione e nella nuova partita: con una sola persona non si chiede.
  - Colore: passa al successivo della tavolozza (un pulsante), non un selettore.
  - Non verificato a schermo: le finestre Giocatori, Dai a…, e il blocco delle cartelle (le prove sono
    sui view model; i file XAML compilano). Da vedere con P8.
  - Non fatto in P5 e spostato in P5a: l'intestazione "proprietario" nella finestra delle partite
    eliminate.
- 2026-10-25 — Fase P, P5 **parziale** (Haiku): fatto (non spuntato, mancano sotto):
  - Partite per persona (`GameViewModel.PlayerGroups`, `PlayerGroup`): nell'ordine delle persone,
    la preferita per prima, poi dalla più recente; le persone senza partite del gioco non compaiono.
    Test: `PlayerGroupsTests` (6).
  - ★ sulla scheda: una persona con la partita non preferita vede ☆ e la clicca
    (`GameViewModel.SetFavouriteAsync`); la preferita ha ★ (tooltip `FavouriteTooltip`).
  - `GameViewModel.GiveToAsync(profile, personId)`: dà la partita a un'altra persona
    (`SaveManager.SetOwner`, con le regole della preferita). Test: `GiveToMovesThePlaythroughToAnotherPlayer`.
  - Blocco "Cartelle dei salvataggi (n)": parte chiuso, si apre con l'intestazione; lo stato
    è statico (vale per tutti i giochi finché l'app è aperta, non si salva). I suggerimenti
    restano visibili sotto il blocco.
  - Fix del punto aperto di P2b: una partita recuperata senza persona valida va al primo
    amministratore (vedi il commit `b84b0fe`).
  - **Non fatto**: (1) la voce "Dai a…" nel menu ⋯ della scheda (serve una finestra di scelta della
    persona, la stessa di "Rimuovi giocatore"); (2) la finestra "Giocatori…" (rinomina, colore,
    ordine, Admin, aggiungi, rimuovi): rimuovere una persona tocca le partite di tutti i giochi, cioè
    serve un'operazione a livello di app (`MainWindowViewModel`), non solo di un gioco; (3) il campo
    "Giocatore" nella finestra "Nuova partita" e la scelta dell'autore all'importazione; (4) il
    pulsante "Consenti anche in modalità semplice" con `CloudConsentito` (in `stato.ini`, `AppState`).
- 2026-10-25 — Fase P, P4a (Haiku): PIN facoltativo per gli admin. `People.SetPin` / `RemovePin` /
  `CheckPin`: PBKDF2-SHA256, 100 000 iterazioni, sale di 16 byte, hash di 32, in `persone.ini`
  come `<id>.Pin=<sale>:<hash>` (mai in chiaro). Un utente non ha PIN; diventare utente toglie il
  PIN; togliere una persona toglie il suo. `Person.HasPin`. Confronto a tempo costante
  (`FixedTimeEquals`). Nell'interfaccia: `PinPadViewModel` (cinque errori di fila → 30 s di attesa,
  contatore in memoria, si riparte al riavvio; il tempo è un orologio iniettato per i test) e la
  finestra "Chi sei?" apre il tastierino per un admin con PIN (cifre e tastiera). Test:
  `PinTests` (8), `PinPadViewModelTests` (7). Test Windows: 685 verdi, 0 warning.
  - Non fatto (sono di P5/P6): "Imposta/Cambia/Togli PIN" nella finestra Giocatori (P5); il
    cambio o la rimozione del proprio PIN che chiede il PIN attuale (P5); la domanda "Imposta PIN"
    dei testi del piano non ha ancora una finestra.
  - Dal canvas `AdminPin`: la finestra ha il titolo "Chi sei?", le due scelte, il tastierino 3×4 con
    "⌫", "Annulla". Fatto così; la pallina per la cifra è un testo "●○○○".
- 2026-10-25 — Fase P, P4 **parte 2** (Haiku), commit `ea821d8`: finestra di primo avvio con le
  due scelte (`ModeChoiceWindow`, chiamata in `App.StartAsync` solo se `lss.ini` non c'era; chiusa
  senza scelta = avanzata), e view model + finestra "Chi sei?" (`AdminPickerViewModel`,
  `AdminPickerWindow`: solo gli amministratori, un utente è rifiutato). Test: `AdminPickerViewModelTests`
  (4). Test Windows: 667 verdi, 0 warning.
  - Deviazione: il `FirstRunWindow` del piano (G11) qui è la finestra del primo nome della partita
    di un gioco; la scelta di modalità è quindi una finestra nuova.
  - **Ancora da fare in P4** (P4 non spuntato): il pulsante "Modalità semplice" nell'intestazione e
    la commutazione della finestra principale (la vista semplice è P6); "Chi sei?" non è ancora
    collegata (la usano P5 e P6/P7). Il PIN è P4a.
  - Schede del canvas lette: `FirstRun` (fatta), `Main` (è la vista semplice "Chi gioca?", P6),
    `AdminPin` (P4a), `Settings` (ha "Passa alla modalità semplice": P4, da collegare).
- 2026-10-25 — Fase P, P4 **parziale** (Haiku): committata solo la parte di core
  (`AppSettings.Mode` in `lss.ini`, chiave `Modalita`; `FirstStart` = il file non c'era).
  Test: `AppSettingsTests` (3 nuovi). **Non fatto, P4 non è spuntato**: la finestra di prima
  partenza con le due scelte, il pulsante "Modalità semplice/avanzata" nell'intestazione,
  la finestra "Chi sei?" (`AdminPickerWindow`) e la vista semplice nella finestra principale
  (che dipende da P6). Per queste bisogna leggere le schede `FirstRun`, `Main`, `Advanced`
  e `Settings` del canvas e `P6`/`P7`: fatto a parte, in un turno dedicato.
- 2026-10-25 — **Fase P, P3 ferma (Haiku): da decidere con Andrix.** P3 (`SimplePlay`), passo 4
  del piano: "salvataggi di nessuno (`WhoseSaves` = null) → `AdoptGameSaves(UniqueName(
  Strings.SavesBeforeLss), player)` **e non preferita**". Ma la regola "Rules" dice che una
  persona con almeno una partita di un gioco ha **sempre** una preferita, e in quel ramo la
  partita adottata è l'unica del gioco, quindi del giocatore attuale. Con la regola
  implementata in P2b (dopo ogni operazione la preferita si ricalcola) diventa preferita da
  sola. Le scelte possibili: (a) come scritto nel passo, ma con la preferita cancellata a mano
  (`preferite.ini` non ha un "nessuna" stabile: al prossimo ricalcolo torna); (b) la partita
  "Prima di LSS" diventa preferita del giocatore attuale, come dice la regola (**consigliato**);
  (c) la partita va a un proprietario fittizio "Prima di LSS" (una persona in più, senza ruolo).
  Non implementato P3: sospeso in attesa della scelta. Tutto il resto (P4–P8) dipende da P3 in
  ordine, quindi non iniziato.
- 2026-10-25 — Fase P, P1–P2c (Haiku), commit su `v2`: P1 `57664e2`/`c063eb9`, P2a
  `e375941`/`2559b6c`, P2b `1c15ec6`/`23918aa`, P2c `3553e24`. Test
  Windows: 651 verdi, 0 warning (prima di P: 612). Linux (`lss-ci`, `--tests-only`, `3553e24`): 651 verdi.
  - Deviazioni dal piano e scelte da sapere:
    - P2a: il piano dice "un proprietario esistente". `SaveManager` ha la proprietà interna
      `Players` (l'elenco di `People`), impostata da `Open` e `Initialize`. Senza elenco (test,
      `new SaveManager(…)` diretto) basta un id non vuoto. `DefaultOwnerId` è il primo giocatore
      e dà "" senza elenco: in quel caso la creazione fallisce con `PlayerNotFound`.
    - P2a, test: ogni chiamata di `CreateProfile`/`AdoptGameSaves`/`ImportProfile`/
      `RecoverDeletedProfile` ha ora l'argomento del proprietario (`TestFolder.Owner`), come da
      piano. `TestFolder.CreateManager` carica un `persone.ini` con una sola persona (quella
      `Owner`, amministratore). `ExternalSavesTests` usa `manager.DefaultOwnerId` perché apre il
      gestore con `Open`. Nessuna asserzione cambiata.
    - P2b, preferite: non c'è un caso per caso. Dopo ogni operazione riuscita (creazione, import,
      recupero, rinomina, eliminazione, cambio proprietario) `RepairFavourites` tiene la preferita
      se è ancora valida, altrimenti prende la partita giocata per ultima (`LastSession`, poi nome).
      Una rinomina la segue. "Ultima" (`<persona>.Ultima`) si scrive all'attivazione e a fine
      sessione: il piano non diceva quando.
    - P2b: `SetFavourite` con un'altra persona dà il messaggio nuovo `NotYourPlaythrough`.
    - P2b, rischio: se la partita recuperata non riesce a scrivere le sue informazioni, resta
      senza proprietario (solo nel log). Il recupero non rifà il controllo di P2c, perché
      `ProprietariOk` è già a 1. Da sistemare con P5a/P5.
    - P2c: `ProprietariOk=1` in `stato.ini` si scrive solo quando è vero (i `stato.ini` vecchi non
      cambiano). `AssignExistingPlaythroughs` gira a `Open` e `Initialize`. Senza persone non
      segna nulla, quindi riprova alla prossima apertura.
    - P2c, export: `partita.ini` ha `Giocatore` (id, già scritto da P2a con le informazioni) e
      `NomeGiocatore` (il nome, letto solo da chi guarda il file). L'import li ignora, il
      proprietario è quello scelto.
    - Nessun test del 1.0 è cambiato nelle asserzioni (vedi sopra per le chiamate).
- 2026-10-25 — O8 (Haiku), commit `d4c8ef4` (`ExeIcon`, con test) e `9751449` (`ExecutableOf`,
  `GameIcons`, vista, test). Test Windows: 612 verdi, 0 errori, 0 warning (prima di O8: 584).
  Linux (`lss-ci`, `--tests-only`, commit `9751449`): 612 verdi.
  - Controllato su file veri (sola lettura): il manifest Epic `50089E5FAC31881FE1813CC4DE41CA6F.item`
    ha il campo `LaunchExecutable` (`FEZ.exe`) con `InstallLocation` (`H:\EpicLibrary\Fez`); il
    file `FEZ.exe` esiste (1,1 MB) e ha un'icona. `explorer.exe` dà un'icona e il PNG 64×64 (test).
    Non controllati (launcher non installati su questo PC): il campo `executable` di Heroic
    (`installed.json`) e `exe` del registro GOG; il codice gestisce il percorso assoluto e quello
    relativo alla cartella del gioco.
  - Deviazioni dal piano, in ordine di importanza:
    - Il test di smoke su `Environment.ProcessPath` non passa: `testhost.exe` non ha icona (solo
      info di versione e manifest). Il test usa `explorer.exe` nella cartella di Windows (sola lettura).
    - `AvaloniaTestSetup` chiama `UseSkia()`: senza, `Bitmap.Save` in headless scrive un file vuoto.
    - `scripts/linux-check.sh`, passo dei test: il contenitore installa `libfontconfig1` (la
      dipendenza del pacchetto, usata da SkiaSharp) e gira come root con `chown` alla fine, come il
      passo dei pacchetti. Senza, sulla macchina Linux 47 test falliscono.
    - Il passaggio alla UI è `GameItemViewModel.ShowIcon`, testato a parte. `LoadIconAsync` non ha
      un test: un `await` nei test con Avalonia non torna mai, e xUnit1031 vieta il blocco.
    - Su Linux i `.desktop` si cercano in `~/.local/share/applications` e `/usr/share/applications`;
      `Icon=` può essere un percorso o un nome del tema (il PNG più grande in hicolor o pixmaps).
      Le immagini accanto all'eseguibile (`icon.png`, `<nome exe>.png`, `<cartella>.png`) sono solo
      su Linux, come dice il titolo del punto 3 del piano. Non provato su un desktop Linux vero.
    - Per i collegamenti Steam, `icon` può essere un'immagine o un `.exe`: `IconPath` li accetta entrambi.
    - La cache: un PNG più recente della sua fonte si riusa. Con più fonti, un PNG da una fonte di
      priorità più bassa resta valido finché la fonte prioritaria non è più recente (caso raro).
  - Non visto a schermo: `tools/UiScreenshots` usa dati finti senza giochi con un eseguibile, quindi
    non mostra un'icona estratta. La XAML non è cambiata (immagine 32×32 con sorgente 64 px).
- 2026-10-25 — Fine fase O (Haiku). O1–O6 committati su `v2` (da `67d1031` a `cb3f550`), ogni
  task con il suo tick in un commit `Progress:`. O7 non fatto (👤). Test Windows: 584 verdi, 0
  warning (prima di O: 526). Linux (`lss-ci`, `--tests-only`, a fine fase): 584 verdi.
  - Formati controllati su file veri (sola lettura): solo il catalogo Epic (`catcache.bin`).
    Non controllati (launcher non installati su questo PC): manifest Epic `.item`, registro GOG,
    tutto Heroic (incluso l'indirizzo heroic://, non confermato, quindi `CanLaunch` false), `shortcuts.vdf`.
  - Cambi di test voluti (ognuno nel commit): `SteamProviderTests.ProvidersHaveSteamFirst`
    (O1–O4: da uno a cinque launcher, ora 5 con i giochi a mano); `FakePlatform` con processi,
    registro e programmi avviati (default invariati).
  - Deviazioni da sapere, in ordine di importanza:
    - Heroic non avvia i giochi da LSS (regola del piano per l'indirizzo non confermato).
    - Giochi a mano: il menu Modifica/Rimuovi non c'è in UI (il core sì); "Aggiungi un gioco…"
      usa le finestre esistenti invece di `AddGameWindow`. Vedi la nota di O4.
    - Scelte di O1: `{AppData}` = AppData\Local (confermato dal catalogo reale), e i `..`
      verso locallow/roaming diventano radici LSS.
    - Scorciatoie Steam: appid letto con segno, usato senza segno (O5).
    - Gli avvisi cloud fuori da Steam non usano la guida di Steam (`CloudHelpAsync` generico).
  - Da controllare con Andrix (O7 👤): un gioco Epic e uno GOG su Windows (formati mai visti);
    Heroic sul Deck (formati e `gogSaves`); un gioco a mano su entrambi; uno Steam con
    `shortcuts.vdf` vero; la cartella del pulsante "Aggiungi un gioco…" nell'intestazione
    (l'intestazione è più piena ora).
- 2026-10-25 — O6 (Haiku), commit `cb3f550`. `DuplicateGames.Keep` (cartella d'installazione,
  senza distinguere le maiuscole su Windows): tiene il primo launcher di `Providers.All`, log
  in italiano. Applicato alla costruzione di `MainWindowViewModel` e alla rilettura
  (`WithoutDuplicates`). Stesso launcher con due voci sulla stessa cartella: si tengono
  entrambe (piano: "due giochi di launcher diversi"). Test: `DuplicateGamesTests`,
  `MainWindowViewModelTests.TheSameGameInTwoLaunchersIsShownOnce`.
- 2026-10-25 — O5 (Haiku), commit `b3bdde7`. `shortcuts.vdf` letto con `BinaryVdf` (nessun
  formato non verificato: su questo PC non ci sono collegamenti, quindi il file reale non è
  stato visto; la struttura è quella del piano, provata con un file scritto in test).
  Il `appid` è letto come int32 e usato senza segno (`unchecked((uint)…)`), perché il piano
  diceva "come uint" ma Steam lo scrive con segno. Avvio: `steam://rungameid/` +
  `(appid << 32) | 0x02000000`. Le voci Heroic, EpicGamesLauncher e GalaxyClient sono
  escluse. Lo stato "in esecuzione" usa il registro di Steam con l'ID del collegamento: non
  verificato su un Deck. Test: `SteamShortcutsTests`.
- 2026-10-25 — O4 (Haiku), commit `ca23683`. Fatto: `ManualGames` (gioco.ini in
  `<dati>\manual\local\<id>`), `ManualProvider` (account "Questo computer", avvio con
  l'eseguibile, su Linux niente `.exe`), pulsante "Aggiungi un gioco…" nell'intestazione
  (programma facoltativo, poi nome; poi si apre la ricerca della cartella).
  **Non fatto, da completare**: (a) il menu ⋯ con **Modifica** e **Rimuovi da LSS** (il core
  c'è: `ManualGames.Write`/`Remove`; `Remove` sposta la cartella in `manual\eliminati`, non
  in "partite eliminate" come dice il piano: da decidere con Andrix); (b) il nome del gioco
  preso da `FileVersionInfo.ProductName` (ora: nome del file); (c) test del view model di
  "Aggiungi" (`AddGameAsync` non ha test, serve un `IDialogService` finto); (d) la finestra
  `AddGameWindow` del piano (qui si usano `PickFileAsync` e `TextPromptWindow`); (e) su Linux
  il pulsante non è stato provato a video. Test: `ManualProviderTests`.
- 2026-10-25 — O3 (Haiku), commit `7df1fe1`. **Nessun formato di Heroic verificato**: Heroic
  non è installato su questo PC (`%APPDATA%\heroic` e `~/.config/heroic` assenti). Una ricerca
  web (sola verifica, non un file) non ha confermato né l'indirizzo di avvio né `gogSaves` /
  `installed.json`; il piano è seguito così com'è scritto. Per questo:
  - `CanLaunch` è **false** (regola del piano per l'indirizzo non confermato): i giochi
    Heroic si avviano da Heroic (`StartFromHeroic`).
  - Il nome dei giochi GOG da `store_cache/gog_library.json` e il file `sideload_apps/library.json`
    sono letti in modo tollerante (array, o oggetto con `games`); non verificati.
  - Amazon (Nile): stesso formato di Legendary, non verificato.
  - Modifica di `SavePathResolver`: la radice `Absolute` (L6 la rimandava a questa fase) è ora
    supportata: la regola è la cartella intera, solo se esiste.
  - Decisioni: id del gioco = `runner_appName` (un app name può esistere in più runner);
    prefisso = cartella con `drive_c` (`pfx/drive_c` per Proton, altrimenti la cartella `winePrefix`);
    metadato Epic da `legendaryConfig/legendary/metadata/<app>.json`.
  - `PrefixIsPerGame` del piano non è calcolato (l'interfaccia `PrefixOf` non lo passa; in
    `SaveLookup` è `prefix != ""`).
  - Test cambiato di proposito: `SteamProviderTests.ProvidersHaveSteamFirst` (ora quattro launcher).
  - Linux (`lss-ci`, `--tests-only`, su `7df1fe1`): 569 verdi.
- 2026-10-25 — O2 (Haiku), commit `253fc6c`. Il registro GOG
  (`HKLM\SOFTWARE\WOW6432Node\GOG.com\Games`) **non è stato verificato**: su questo PC la
  chiave non esiste (GOG non installato), quindi il formato (`path`, `gameName`, `exe`) è
  quello del piano. Giochi con cartella mancante saltati. Launch = `StartProgram(exe,"",path)`,
  senza Galaxy; `CanLaunch` solo se c'è l'eseguibile. Nuovi testi `GogExeMissing`,
  `ProgramNotStarted`. Nessuna regola di salvataggio: tutti i giochi GOG passano dalla ricerca
  (test `GogGamesAlwaysUseTheSearchForTheirSaves`).
- 2026-10-25 — O1 (Haiku), commit `67d1031`. Formati controllati su file veri (PC di
  Andrix, sola lettura):
  - `Data\Catalog\catcache.bin`: **verificato**. È testo base64 di un array JSON; ogni voce
    ha `id` e `customAttributes.CloudSaveFolder.value`. Su 145 valori trovati i token sono
    `{AppData}`, `{UserDir}`, `{UserProfile}`, `{UserSavedGames}`, `{InstallDir}`, `{EpicID}`
    e `{EpicId}`, scritti anche in minuscolo (`{appdata}`, `{userdir}`): il riconoscimento
    dei token non distingue le maiuscole. Qui `{AppData}` è **AppData\Local**: i valori
    `{appdata}/../locallow/...` e `{appdata}/../roaming/...` lo confermano (da memoria, non
    verificato: Satisfactory salva in Local). Coincide con il piano.
  - `Data\Manifests\*.item`: **non verificato**. Su questo PC la cartella c'è ma non ha
    file `.item` (solo `Pending`): il formato dei campi è quello del piano.
  - Cosa non è nel piano: `{../locallow}` e `{../roaming}` dopo `{AppData}` diventano
    `WinAppDataLocalLow` / `WinAppDataRoaming` (senza, quei giochi non si trovavano). Un
    altro `..` → nessuna regola, nel log.
  - `DisplayName` = "Epic Games". Il lancio (`com.epicgames.launcher://apps/...`) usa
    l'elenco di `FindGames`: un gioco non più nei manifest non si avvia (messaggio nuovo
    `EpicItemMissing`, `LaunchFailedVia` per i testi non di Steam).
  - Testi del cloud fuori da Steam (`GameViewModel`): barra, conferma e aiuto usano i
    testi generici; l'avviso non compare se il cloud è "non noto" (per Epic vuol dire
    nessuna cartella cloud). Il pulsante "Come si fa" per gli altri launcher mostra il
    testo generico, non la guida di Steam.
  - Test cambiato di proposito: `SteamProviderTests.ProvidersHaveSteamFirst` (ora due
    launcher). `FakePlatform` ha processi, registro e programmi avviati impostabili.
- 2026-10-25 — Review of phase N (Opus), merged from `v2-review`:
  `de476eb` an extra spot whose folder is missing on disk (SD card out,
  folder deleted) is left as it is: before, saving progress emptied its copy
  in the playthrough and the backup before every switch failed; `1a2d26e`
  N6 decision with Andrix: a Steam game managed through `userdata` (as in
  1.0) is no longer made Unsafe by an unsafe folder from Steam's rules: that
  folder is left out with a log line (folders added by hand still make it
  Unsafe); `30c271e` saving progress is refused when spot 1 is empty, as in
  1.0 (before, a settings file in another spot was enough to empty the main
  copy on a save on exit). The rest of the switch code (journal, rollback,
  recovery) checked and kept. Then the open point of the N9c/N11 note:
  "Not saves" on a loose-file suggestion now dismisses that suggestion
  (`Scartata.n` in `proposte.ini`) instead of ignoring the whole install
  folder, and "For every game" is not offered for it.

- 2026-10-25 — Completate le parti semplificate di N9c e N11 (sessione dedicata
  all'interfaccia; nessuna modifica a `SaveSet`, `FileOps`, `SaveLocator`, `SaveLookup`
  né al cambio/salvataggio di `SaveManager`). Commit su `v2`: `8f153af` anteprima con
  Note; `a1bc4ca` Esporta tutte (Impostazioni); `28ecb32` finestra di importazione;
  `330231a` finestra delle proposte; `e34a584` impostazione CercaDopoPartita;
  `d54f7ed` schermate (tool). Test Windows: 517 verdi, 0 warning. Linux
  (`lss-ci@192.168.1.3`, `--tests-only`): 517 verdi.
  Ancora diverso dal piano:
    - N11, motivi: `proposte.ini` ora salva anche i motivi (quinto campo, `tipo:punti:dettaglio`
      separati da `;`). Il piano diceva "si ricalcolano"; senza salvarli la finestra non poteva
      mostrarli. Le righe a quattro campi restano valide (senza motivi).
    - N11, "Non sono salvataggi" per tutti i giochi sulle proposte a file sparsi: la proposta è
      una maschera nella cartella di installazione, ma `IgnoredFolders` ignora cartelle intere,
      quindi per tutti i giochi si ignora **tutta** la cartella di installazione. È il
      comportamento già presente in N11, non cambiato: da decidere con Andrix (ignorare la
      maschera, non la cartella).
    - N11, "Esporta tutte" sta nelle Impostazioni del gioco (la finestra è per gioco) e chiama
      `MainWindowViewModel.ExportAllConfigsAsync`. Considera solo i giochi della colonna
      principale (`Games`), non "Altri giochi".
    - N9b/N9c: con partite, ogni posto importato chiede copia o vuoto come "Aggiungi un'altra
      cartella" (`GameViewModel.AddSpotWithChoiceAsync`): `SaveConfigImport.Apply` è diventato
      `ApplyAsync`. Un gioco del file non presente qui mostra il provider del file (es. `steam`)
      come launcher.
    - N11, impostazione: `GameViewModel` legge l'oggetto `AppSettings` condiviso (proprietà
      interna `Settings`), quindi il cambio vale subito per tutti i giochi.
    - Non coperto da test: "impostazione spenta → nessuna ricerca" (la ricerca dopo la partita è
      un thread privato di `GameViewModel.StartLookAfterSession`, non esposto). Coperto: il
      valore predefinito e il salvataggio in `lss.ini`.
    - Anteprima e importazione sono finestre (`ExportConfigWindow`, `ImportConfigWindow`):
      nessuna prova automatica delle finestre (i test usano i view model); schermate `20`–`22`
      controllate a mano (tool `tools/UiScreenshots`, lingua `it`).
- 2026-10-25 — Fine fase N (Haiku). N1–N13 committati su `v2` (da `97d27f2` a
  `d708e11`), ogni task con il suo tick in un commit `Progress:`. N14 non fatto (👤).
  Test Windows: 498 verdi, 0 warning (prima di N: 378). Linux (`lss-ci`,
  `scripts/linux-check.sh lss-ci@192.168.1.3 --tests-only`): 496 verdi dopo N2, N5b e
  N5d. Dopo N10 un test di N9b (radice `LinuxHome`, che su Linux esiste) falliva e
  c'era un avviso xUnit2031 in N10: corretti in `69a2a7a`; verifica finale 498 verdi.
  - Deviazioni dal piano, da sapere:
    - N1: `ToPortable` sostituisce gli ID Steam dell'account con `{64BitSteamID}` /
      `{Steam3AccountID}` (solo segmenti interi); `FromPortable` li riporta.
      `GameInfo.SpotsOf(folder, source)` crea i posti di una cartella sola.
    - N3: gli id dei posti partono da 2 (il posto 1 è il primario, `SetPrimary`).
      `Ignorate` è `Ignorate.n`/`Aggiunta.n` come in M3b, non la lista `;` del piano
      (si perdeva la data). `cartella.ini` si converte alla prima lettura. Test cambiati
      di proposito perché il file è ora `posti.ini`: `GameSaveSettingsTests`,
      `SaveFolderChoiceTests`, `LearnSavesViewModelTests`. Nuovo: `GameSpotsTests`.
    - N4: il primo tentativo saltava `.lss-posizioni` anche nelle copie tra snapshot
      (backup, profili, esporta): le avrebbe tolte. Corretto in `0292d9a`:
      `CopyFolder`/`CopyFolderVerified` portano i posti extra; `ReplaceFolder` e
      `MirrorFolder` hanno `carryExtraSpots` (default `false`, il caso del gioco).
      Punti che trattano `remotecache.vdf` (N4), in `FileOps`: `CopyFolder`,
      `MirrorFolder` (2), `CopyKeptFromDestination`, `HasNoSaveFiles`, `FoldersAreEqual`
      (`ListNames`), `IsSkipped` (usato da `MatchedFiles` e `HasNoSaveFiles`). Fuori:
      `SteamLibrary.GetSupport` e `SteamProvider` (righe 187, 279-283) leggono
      `remotecache.vdf` nella cartella del gioco, dove `.lss-posizioni` non sta mai.
    - N5a/N5b: `SaveSet.ReplaceGameFrom` scrive `cambio.ini`. I test di `SaveSetTests`
      (scritti in N5a, in questa fase) passano ora un journal e un backup veri invece di
      stringhe vuote: cambio di proposito. Il ripristino riporta dal backup tutti i posti
      di `Posti=` (non solo `Fatti=`: un posto cambiato a metà va rimesso). Se un posto
      del journal non è più noto al gioco, il journal resta.
    - N5c, usi di `SavePath` cambiati in questo commit: `ReplaceGameSaves` (3 punti:
      backup con `CopyGameTo`, `ReplaceGameFrom`, ripristino con
      `ReplaceGameFrom(backup, backup, …)`); `Open` chiama `RecoverInterruptedSwitch`.
    - N5d: `SaveProgress` usa `ReplaceSnapshotFromGame`; il ripristino del profilo dopo
      un errore usa `ReplaceFolder(…, carryExtraSpots: true)`. Usi di `SavePath` rimasti
      in `SaveManager*.cs`: costruttore, proprietà, `SharedFilePath` (file condivisi:
      solo posto 1, per scelta) e `SaveManager.Playthroughs.cs` 529–531 (stesso motivo).
      Nessun testo di log cambiato. Nuovo: `SaveManagerSpotsTests`.
    - N6: il posto 1 scelto (`posti.ini` id 1) sostituisce le cartelle trovate, come in
      L8; il piano diceva "aggiunto dopo". `HasOtherSaves` e `SomeSavesElsewhere` tolti.
      I giochi Steam con userdata prendono anche le cartelle delle regole come posti
      extra (Terraria): se una è insicura, **tutto il gioco diventa Unsafe**. È un rischio
      di regressione per giochi che prima funzionavano: da controllare con Andrix.
      Un posto extra che contiene il primario viene scartato (log). Test L8 cambiati di
      proposito: `TwoFoldersAreUnsure` → `TwoFoldersAreTwoSpots`, `OnlyFilesAreUnsure` →
      `OnlyFilesAreAPatternSpot`; le asserzioni su `HasOtherSaves` diventano su posti.
      Il testo di `SupportUnsure` è nuovo (usato solo per più di 8 posti).
    - N7: `AddSpot`/`RemoveSpot`. `RemoveSpot` tocca solo `posti.ini`: le copie restano.
      `SaveManager.Game` ha ora `private set` e si aggiorna in memoria.
    - N8: formato 4 (`Formato=4`, `posizioni.ini` nello zip). Il primo posto dello zip va
      al primo posto qui, qualunque sia il suo percorso portabile (quello userdata non è
      portabile tra PC); gli altri per percorso portabile, maschera e ricorsione. Senza il
      primo posto l'importazione si rifiuta: una cartella principale vuota svuoterebbe il
      gioco. Test cambiato di proposito: `Export_WritesFormat3…` → `…Format4…`.
    - N9: wizard con caselle (la prima resta la cartella principale); le altre diventano
      posti extra solo se il gioco non ha partite (con partite: nel log, e si aggiungono
      con "Aggiungi un'altra cartella"). Quest'ultima chiede la scelta delle copie con due
      conferme (copia, poi vuoto), non con tre pulsanti. Chiavi del piano presenti in
      entrambi i `resx`. Non fatto: la finestra `ImportConfigWindow` (la conferma è una
      per gioco).
    - N9a–c: file `*.lss-config.json` (`SaveConfig`), lettura non fidata (`SaveConfig.Read`:
      1 MB, 200 giochi, 8 posti, radici, percorsi, token, maschere), abbinamento e
      applicazione (`SaveConfigImport`). Deviazioni: il preview non ha la casella Note
      (vuote); niente finestra di import con caselle; niente "Esporta tutte" in
      Impostazioni; con partite i posti importati ricevono una copia dei file attuali
      senza chiedere; il pulsante di esporta è un link nella vista del gioco.
    - N10: `SessionScan.Run` con soglia 9, posti e cartelle ignorate saltati; i file
      sparsi con estensione .sav/.save/.dat/.sl2/.slot nella cartella di installazione
      diventano una maschera (per estensione se sono tre o più, altrimenti i nomi). Le
      proposte in `proposte.ini`; i motivi non si salvano (come da piano).
    - N11: le proposte si esaminano con i dialoghi in sequenza (Aggiungi / Non sono
      salvataggi, per questo gioco o per tutti / Più tardi), non con
      `SpotSuggestionsWindow`. Il toast ha "Guarda"; la vista del gioco ha la riga "N
      possibili cartelle". Il controllo è `CercaDopoPartita` in `lss.ini` (attivo di
      default), **senza** interruttore in Impostazioni. La modalità semplice (P) non
      esiste ancora: non è gestita.
    - N12: `tools/UiScreenshots` mostra un gioco con tre posti (`19-game-spots`); le righe
      dei posti e quella della cartella principale ora vanno a capo (prima uscivano dalla
      finestra). Immagini generate in `scratchpad\shots` (fuori dal repository); letti
      `16-learn-result` (caselle ok) e `19-game-spots`. Non ci sono immagini del toast né
      delle conferme.
    - N13: `docs/DEVELOPMENT.md`, sezione "Save spots".
  - Da controllare con Andrix (N14 👤): Terraria (userdata + Documents) con due partite;
    un gioco con file sparsi nella cartella di installazione; togliere un posto e
    giocare (la ricerca lo ripropone); spegnere il computer durante un cambio con due
    cartelle; le stesse prove sullo Steam Deck; il punto di N6 sui giochi Steam con una
    cartella extra insicura; la conversione di `cartella.ini` in `posti.ini` su un gioco
    vero.

- 2026-10-25 — M7 so far (Andrix, Steam Deck, Opus fixing): Hollow Knight
  (Proton) found nothing: the search skipped every launcher folder with all
  its content, so the game's prefix, installation and userdata inside
  `~/.local/share/Steam` were never read (fixed in `f42f760`). Sekiro
  (Proton) after the fix: first proposal right,
  `…/pfx/drive_c/users/steamuser/AppData/Roaming/Sekiro` (`S0000.sl2`,
  `GraphicsConfig.xml`, as Steam's own rules); second proposal was DXVK's
  shader cache `AppData/Local/dxvk` (`.dxvk.bin` counted as save-like):
  `dxvk`, `vkd3d-proton`, `vkd3d` are now skipped folder names and
  `.dxvk.bin`, `.dxvk.lut`, `.dxvk-cache` ignored files.

- 2026-10-25 — Fine fase M (Haiku). M1–M6 committati su `v2` (da `1d0b994` a
  `76c8fee`), ogni task con il suo tick in un commit `Progress:`. M7 non fatto (👤).
  Test Windows: 376 verdi, 0 warning (prima di M: 317). Linux (`lss-ci`,
  `scripts/linux-check.sh lss-ci@192.168.1.3 --tests-only`): dopo M1 (corretto, vedi
  sotto) 327 verdi, dopo M2 337 verdi, dopo M3b 353 verdi, a fine fase 376 verdi.
  Nessun test già esistente è cambiato: `git diff ab5c301 -- tests` non toglie righe,
  solo aggiunge. `GameSaveSettingsTests` non è cambiato; la conservazione delle altre
  chiavi di `cartella.ini` è coperta da `IgnoredFoldersTests.SavingTheSaveFolderKeepsTheIgnoredFolders`.
  - Smart App Control: tra le 12:20 e le 12:25 (ora locale) del 2026-10-25 ha bloccato
    `Lss.Tests.dll` e poi `LSS.dll` appena compilate (Code Integrity, eventi 3077 e 3033,
    "did not meet the Enterprise signing level"). Bloccate anche la build Debug e quella
    con `--no-incremental` e con un'altra cartella di output. Poi, senza che io cambiassi
    nulla alle impostazioni di sicurezza, la build successiva è passata. Se torna, vedi
    `docs/DEVELOPMENT.md` → "Smart App Control".
  - M1: `SaveFinder.Roots` ha un terzo parametro opzionale, `userdataFolder` (la
    userdata di Steam del gioco), perché `SaveContext` non la contiene. La fusione tiene
    la cartella esterna con la profondità maggiore; una cartella del gioco dentro una
    cartella non del gioco resta a parte (così il punteggio la riconosce). Le cartelle
    nascoste di Home e i prefissi Wine senza prefisso noto sono come da tabella (le
    prime solo su Linux). Il run Linux dopo il primo M1 ha trovato due test sbagliati
    (`.local/share` e `.wine/drive_c/users` sono dentro cartelle nascoste di Home e la
    fusione le unisce): corretto l'atteso del test, e il commit M1 è stato corretto con
    `--amend` prima del tick (era solo locale, non ancora ticked).
  - M2: il percorso usa `Directory.GetFileSystemEntries` (array, con try/catch) invece di
    `EnumerateFileSystemEntries`: un errore a metà non perde le voci già lette. "windows",
    "Program Files" e "Program Files (x86)" si saltano solo sotto la radice di un disco.
    Un file visto da più radici compare una volta sola (vince la prima, cioè quella del
    gioco). Test Linux corretti in `5e7d799`: su Linux `SetCreationTimeUtc` cambia la data
    di modifica, quindi il test imposta solo la scrittura, usa un'ora di partenza nel
    futuro e controlla la data di creazione solo su Windows.
  - M3: il motivo "il nome contiene" mostra il nome della cartella trovata, non il token
    (più leggibile per l'utente). Il punteggio dei file di impostazioni (−4) richiede
    estensione `.ini/.cfg/.xml/.json` **e** un nome con `config/setting/option/prefs`, come
    scritto nel piano. Le parole da cartella di salvataggi: +2 per una, +3 con due (totale).
    `Rank` ha due parametri opzionali in più, preparati per M3b e N: la lista delle
    cartelle ignorate (già risolta, così non si legge `cartella.ini` per ogni file) e le
    cartelle già note.
  - M3b: `cartella.ini` ha `Ignorate.<n>` e `Aggiunta.<n>` per un gioco solo (percorso
    assoluto: la cartella dati è locale); `cartelle-ignorate.ini` ha `Cartella.<n>` e
    `Aggiunta.<n>` per tutti i giochi (percorso portabile, `{WinAppDataLocal}/Discord`).
    `Folder` e `PortablePath` coincidono. `AddGlobal` riceve solo il percorso portabile;
    `RemoveGlobal` e `RemoveForGame` il percorso salvato.
  - M3b, cambio di comportamento su L: `GameSaveSettings.Save` ora conserva le altre chiavi
    di `cartella.ini` (prima scriveva solo le sue due, e avrebbe perso le cartelle
    ignorate). Con `Folder` vuota elimina il file solo se non ne restano altre chiavi.
    `SavePathResolver.ToPortable` e `FromPortable` sono stati aggiunti in L6 (il piano lo
    prevedeva se N1 non era fatto).
  - M4: che la cartella attuale del gioco non si possa segnare lo decide il ViewModel
    (`IgnoredFolders` non conosce il gioco attuale). `CartelleExtra` (separate da `;`) in
    `cartella.ini`: `GameSaveSettings.ExtraFolders` e `AddExtraFolder`. La lista del
    risultato ha pulsanti radio (la prima scelta) e un solo "Usa questa cartella".
    "Trova la cartella" di Altri giochi apre ora la procedura (prima era la scelta a mano).
    Le cartelle "non di salvataggi" in Impostazioni compaiono in tutte le modalità: non
    c'è ancora l'amministratore (fase P). La finestra mostra quelle di tutti i giochi e
    quelle di questo gioco. Il percorso portabile si vede com'è salvato
    (`{WinAppDataLocal}/Discord`): da rivedere con Andrix, forse un testo più semplice.
  - M5: con partite già fatte e cartella cambiata c'è un solo avviso (`LearnFolderChanged`)
    al posto della conferma normale, non due finestre. "Ha partite" = c'è una
    `Profili\Profilo_*` nella cartella dei dati (senza aprire un `SaveManager`).
  - M6: lo screenshot ha mostrato due difetti, corretti in `a78b9cd`. I pulsanti del
    risultato uscivano dalla finestra (ora il pulsante principale sta da solo sulla riga,
    gli altri vanno a capo). `startedUtc` parte dalla creazione del ViewModel: prima, un
    passo "in gioco" impostato senza `StartAsync` sottraeva da `DateTime.MinValue`.
    Le immagini sono in `%TEMP%\shots-m6` (fuori dal repository, come da
    `docs/DEVELOPMENT.md`). Le ho lette: i quattro passi, il risultato con tre cartelle,
    il "nessuna cartella" e le cartelle non di salvataggi (più la voce in Impostazioni).
  - Da controllare con Andrix (M7, 👤): i pesi di `SaveFinder` (costanti in cima al file)
    contro i giochi reali. Il nome del gioco con parole di 3 lettere può prendere cartelle
    che non c'entrano (il token minimo è 3, come da piano).

- 2026-10-25 — L13 step 0 (Opus, Andrix's Steam Deck `appinfo.vdf`, read
  with the throw-away reader): version 29, 115 games in Steam's cache (the
  library, not only what is installed): 53 with `savefiles` rules, 29 with
  `ufs` but no `savefiles` (Steam Cloud API → `userdata`), 33 without `ufs`.
  The AppImage of `v2` at `4e85d6f` found only Stardew Valley as supported
  outside `userdata`, and that is what phase L is meant to do: almost every
  Steam rule uses a file pattern (`*.dat` Hollow Knight, `*.sav` Portal,
  `*.hg` No Man's Sky, exact names like `SaveData.sav` Hotline Miami),
  `*.*` without `recursive` (Half-Life), or several folders (Half-Life 2:
  four), and L8 leaves all of these `Unsure` until phase N (pattern spots,
  several spots). Most of these games become supported with N1–N6.
  Also seen: `rootoverrides` on `gameinstall` with `os = Windows` (Hotline
  Miami 2, Jotun) and paths starting with `/` or `.` (`/HotlineMiami`,
  `'.'`): L6 rejected them as absolute paths; fixed in the commit after
  this note (leading `/`, `.` and `'.'` are relative to the root, tests
  in `SavePathResolverTests`).

- 2026-10-25 — Fine fase L (Haiku), L1–L12 committati su `v2` (da `91e2f90` a
  `71f4d18`, tick in commit `Progress:`). L13 non fatto (👤). Test Windows: 311
  verdi, 0 warning. Linux (`lss-ci`, `scripts/linux-check.sh lss-ci@192.168.1.3
  --tests-only`, su `71f4d18`): 311 verdi. Nessuna asserzione di un test già
  esistente è cambiata; sono cambiati solo costruttori/helper di test (FakePlatform,
  FakeProvider, FakeDialogService, TestSetup) e l'attributo `[Collection("AppData")]`
  su `MainWindowViewModelTests` (vedi sotto).
  - Deviazioni dal piano e scelte da sapere:
    - L3: `BinaryVdf.ReadCString` è `internal` (L4 la riusa). Il tick di L3 è finito
      nel commit di L4 (`eefb435`), e il tick di L5 in quello di L6 (`92a9a59`): un
      `git add -A` con il PROGRESS modificato. Da qui in poi i tick hanno commit suo.
    - L7: `SafetyProblem` ha in più `Unreadable` (cartella che non si legge, con
      testo `SafetyUnreadable`); nel piano non c'era, e senza il valore un errore
      di lettura sarebbe finito in un'altra voce. `SaveFolderSafety.Describe` dà il
      testo. Il confronto dei percorsi ignora le maiuscole su Windows e quando
      `IgnoreCase` è vero (su Linux: nei prefissi). Radice di disco rifiutata prima
      della normalizzazione (`7a899da`: su Linux la radice diventa "", e il primo
      test Linux ha trovato il problema).
    - L8: `SaveLocator.Locate` riceve la cartella scelta e la sua origine
      (`string chosenFolder, SaveSource chosenSource`) invece di `GameSaveSettings`,
      che è di L9. Per `Unsafe` il `Reason` è il nome di `SafetyProblem` (la UI lo
      traduce con `Describe`). Per `NotFound` resta il `SaveFolder` del gioco.
    - L8/L9: regole con maschera (`*.sav`), anche non ricorsive, danno file, non
      cartelle: `Locate` le tratta come `Unsure` (il piano le dice così).
    - L6: la radice `Absolute` (Heroic, fase O) non è ancora supportata: la regola
      viene saltata e registrata nel log. `WinUserProfile` (Epic) sì.
    - L10/L11: `Apply` non riceve `launcherFolders` né il prefisso come nel piano:
      li chiede al provider. Per questo `IGameProvider` ha due membri in più:
      `PrefixOf(game)` e `LauncherFolders(account)` (aggiunti in L11, con
      `SaveLookup.ContextFor`, perché `SaveManager.Open` deve ripetere il controllo
      di sicurezza e non ha il contesto). `lssDataRoot` è `AppData.Root`.
    - L11: il costruttore di `SaveManager` riceve l'account; prima lo ricavava da
      `Path.GetDirectoryName(SavePath)`, che fuori da `userdata` dà una cartella
      sbagliata. Il grep richiesto: resta solo il ramo di riserva di quel costruttore
      (usato da Control e dai test, senza account) e i riferimenti a userdata in
      `SteamProvider`/`SteamLibrary` (sono Steam). `GameNotSupported` dice ancora
      "userdata": lo mostra `Open` per i giochi non gestiti. Non è nella lista di L12:
      da decidere con Andrix.
    - L11: i test che cambiano `AppData.Root` (`MainWindowViewModelTests`,
      `SaveLookupTests`, `SaveFolderChoiceTests`) hanno `[Collection("AppData")]`,
      perché xUnit esegue le classi in parallelo: senza, un test leggeva il
      `cartella.ini` di un altro. `TestSetup` imposta una `AppData.Root` temporanea,
      così nessun test legge i dati veri.
    - L12: `UserdataNotice` si mostra solo per i giochi con la cartella in userdata
      (il piano non lo dice). Il testo "Trova la cartella" è lo stesso di "Scelgo io
      la cartella" fino alla fase M. Per `Unsafe` in Altri giochi il motivo è tradotto
      da `GameItemViewModel`. Aggiunta la chiave `SaveFolderMenu` (testo del ⋯).
      Non coperto da test: il ricaricamento della lista dopo la scelta (`ReloadGames`)
      e il ritorno alla cartella automatica (`UseAutomaticFolder`): da provare a mano.
      Limite noto: cambiare cartella mentre il gioco è aperto non è controllato;
      la `GameViewModel` si butta solo se non è occupata.
  - Test nuovi: `BinaryVdfTests`, `SteamAppInfoTests`, `SavePathResolverTests`,
    `GlobTests`, `SaveFolderSafetyTests`, `SaveLocatorTests`, `GameSaveSettingsTests`,
    `SaveLookupTests`, `ExternalSavesTests`, `SaveFolderChoiceTests`, più quelli
    aggiunti a `LinuxPlatformTests`, `SteamLibraryTests`, `SteamProviderTests`. Il test dei
    giochi Proton gira solo su Linux (`ProtonSaveFolderIsFoundThroughTheRule`), la
    scelta con Documents solo su Windows.
  - Da fare con Andrix (L13, 👤): numeri di `appinfo.vdf` del Deck (quanti giochi
    Proton hanno regole), la prova di una scelta di cartella e del ⋯ "cartella
    automatica", e la scelta di `GameNotSupported`.

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
