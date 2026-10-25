# Phase Q — Docs and release of 2.0

Requires phases K–P. Branch: `release-2.0`. Rules in [README.md](README.md).

## Q1 — Docs

- `README.md`: new first section with the two modes (screenshot of the
  simple mode), "Which games work" (Steam, Epic, GOG, Heroic, any game by
  hand; how saves are found: Steam folder, the launcher's own data
  (Steam Auto-Cloud, Epic cloud saves), learned from the disk, chosen by
  hand; several folders and loose files per game; the look after every
  session; what is not supported yet: saves in the
  registry), "Steam Deck" section.
- `docs/COMPATIBILITY.md`: columns "Launcher" and "Save folder"
  (`userdata`, launcher, learned, chosen).
- `docs/DEVELOPMENT.md`: how to add a provider; where each launcher's save
  rules come from (Steam `appinfo.vdf`, Epic `CloudSaveFolder`, Heroic
  `gogSaves`) and the root table of L6; the learn weights (M3).
- `docs/KNOWN_BUGS.md` or the README's help section: how to remove a
  forgotten admin PIN (delete the `<id>.Pin=` line of `persone.ini` with
  LSS closed; where the data folder is on each system).
- New `docs/SAVE_CONFIG.md`: the save configuration format (N9a), the
  rules of the import check (N9b) and how to share a file, so people can
  write or fix one by hand.
- `CHANGELOG.md`, `docs/TODO.md` (move done items, keep "Later" below).
- Product texts: subtitle and descriptions stay launcher-neutral; now they
  can name Steam, Epic and GOG where they list what works (J9 rules).

Commit: `Docs for 2.0`

## Q2 — Version `2.0.0-beta.1`

Same steps as J4 (version in `Lss.App.csproj`, `--version`, metainfo
release entry, Linux check on lss-ci). Commit: `Version 2.0.0-beta.1`

## Q3 — 👤 Beta, then 2.0.0

Andrix: tag, pre-release, feedback round (Steam Deck users first), then
`2.0.0`.

---

## Later (no tasks yet)

- Saves in the Windows registry.
- Lutris (`pga.db` is SQLite: would need a package), EA app, Ubisoft
  Connect, Battle.net, itch.io as real providers (today: add by hand).
- A person tied to a launcher account (families with one Steam account per
  person).
- Desktop shortcut "Play <game> as <person>".
- A shared place for save configurations made by LSS users (for example a
  folder in the GitHub repository with one `.lss-config.json` per game and
  system), still downloaded and imported by hand: LSS itself keeps no
  network access.
- Flathub, AUR, macOS (from J9).
