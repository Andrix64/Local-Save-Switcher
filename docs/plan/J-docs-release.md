# Phase J — Documentation, beta and release

All documentation in English. The user guide is `README.md`; technical
notes are `docs/DEVELOPMENT.md`.

---

## J1 — 👤 Rename the repository (Andrix)

1. GitHub: *Settings → General → Repository name* → `Local-Save-Switcher`.
   GitHub redirects the old links. GitHub is the only repository of LSS
   (Forgejo at most as a backup copy, not part of this plan).
2. Locally:

   ```bash
   git remote set-url origin https://github.com/Andrix64/Local-Save-Switcher.git
   ```

3. GitHub *About*: description "Multiple playthroughs for your PC games
   (Windows and Linux)", topics `steam`, `savegame`, `save-manager`,
   `windows`, `linux`, `steam-deck`, `avalonia`.

---

## J1a — New icon: three proposals

The current icon is a red "C" (from Control) on a dark rounded square: it
no longer fits LSS. The agent draws three proposals; Andrix chooses (J1b).

Rules for every proposal:

- SVG, `viewBox="0 0 256 256"`, only shapes (`rect`, `circle`, `path`...),
  **no text** and no fonts, no gradients, no filters, no embedded images.
- Same base as today: a rounded square filling the canvas
  (`rx` about 56) in `#16161A`, with a 1-unit `#2E2E33` border.
- Colours only from the app theme: accent `#E0261B`, white `#F2F2F2`,
  grey `#2E2E33` / `#96969C`.
- Readable at **16×16**: few, thick shapes (lines at least 20 units thick at
  256), nothing thinner than that.
- No letter "C", no Steam or Valve logo or shapes that recall them, no
  game controllers.

The three ideas (one file each):

| File | Idea |
|---|---|
| `design/icon/proposal-a.svg` | **Two cards**: two rounded rectangles like the playthrough cards, offset diagonally; the back one grey, the front one with a red border, as the card "in the game". |
| `design/icon/proposal-b.svg` | **Switch**: two thick curved arrows forming a circle (swap), red, around the white dot of today's icon. |
| `design/icon/proposal-c.svg` | **Slots**: three stacked horizontal bars (save slots), the middle one red and slightly longer, the others grey. |

Render each proposal at 256, 48 and 16 pixels into one preview image
`design/icon/preview.png` (three rows, one per proposal, with the three
sizes side by side on a `#0F0F10` background). Tools: `rsvg-convert`
(`librsvg2-bin`) and ImageMagick in a temporary container on the Linux
check host, if the session brief gives one:

```bash
docker run --rm -v "$PWD/design/icon:/icon" debian:13 bash -c "apt-get update -qq && apt-get install -y -qq librsvg2-bin imagemagick >/dev/null && cd /icon && for p in a b c; do for s in 256 48 16; do rsvg-convert -w \$s -h \$s proposal-\$p.svg -o \$p-\$s.png; done; done && ..."
```

(build the preview with ImageMagick `montage` or `convert +append/-append`;
copy the `design/icon` folder there and back with `tar` over `ssh`, as
`scripts/linux-check.sh` does). Open `preview.png` with the Read tool and
check that every proposal is still recognisable at 16 px; if one isn't,
simplify it before showing.

Commit: `Icon proposals` (the SVGs and `preview.png`; the intermediate
PNGs are not committed).

## J1b — 👤 Choose the icon (Andrix)

Andrix looks at `design/icon/preview.png` and chooses one proposal, or
asks for changes (each round of changes is a task `J1a.n`). Write the
choice in `PROGRESS.md`.

## J1c — New icon in the app and the packages

1. Copy the chosen SVG to `src/Lss.App/Assets/app.svg`: it's the only
   source of the icon from now on. Delete `design/icon/` (the proposals stay
   in git history).
2. New script `scripts/make-icons.sh` (bash, comments in Italian like
   `release.sh`), which needs `rsvg-convert` and `icotool` (`icoutils`) and
   says so if they're missing:
   - renders `app.svg` at 16, 24, 32, 48, 64, 256 into
     `packaging/linux/icons/<n>x<n>/io.github.andrix64.LocalSaveSwitcher.png`
     (overwriting the current ones);
   - builds `src/Lss.App/Assets/app.ico` with all six sizes:
     `icotool -c -o app.ico 16.png 24.png 32.png 48.png 64.png --raw=256.png`
     (256 as PNG inside the ICO, as today).
   Run it in a temporary container on the Linux check host
   (`apt-get install -y librsvg2-bin icoutils`), then bring the files back.
3. Check:
   - open the 256 and 16 PNGs with the Read tool;
   - `dotnet build LSS.slnx -c Release` and the tests;
   - `bash scripts/linux-check.sh <host>` (the packages contain the icons);
   - in `docs/DEVELOPMENT.md` code structure table add `app.svg` ("source of
     the icon") and a line on `scripts/make-icons.sh` in the build section.
4. The header of the main window ("LSS" with the red bar) stays as it is.

Commit: `New icon`

---

## J2 — Links and README

1. Replace `Andrix64/Control-Savegame-Switcher` with
   `Andrix64/Local-Save-Switcher` in every file (`grep -rn
   "Control-Savegame-Switcher" --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj .`).
2. Rewrite `README.md`. Keep the tone and the structure of the current one
   (short sentences, "you", no marketing words). Sections:
   1. Title `# LSS — Local Save Switcher`, one-line subtitle, screenshot
      `docs/img/main.png`, one paragraph: what it does, for which games
      (saves in Steam's `userdata` folder), Windows and Linux, English and
      Italian.
   2. **Which games work**: how to tell (the app lists them; "Other games"
      shows why a game isn't supported); games tried in
      `docs/COMPATIBILITY.md`; the note about games that also save
      elsewhere.
   3. **Installation — Windows**: zip, extract `LSS.exe`, requirements,
      the digital signature paragraph (from the current README, with
      `LSS.exe`).
   4. **Installation — Linux**: a table "distribution → file → command"
      (Ubuntu/Debian/Mint: `sudo apt install ./lss_<v>_amd64.deb`; Fedora:
      `sudo dnf install ./lss-<v>-1.x86_64.rpm`; Steam Deck and others:
      AppImage, `chmod +x` and run, or `.tar.gz` + `./install.sh`;
      openSUSE: AppImage). A line about Steam installed as Flatpak or Snap
      (supported).
   5. **Before you start: turn off Steam Cloud** for the game (current
      text, generic).
   6. **How to use it**: games list, playthroughs, while you play, history
      and saved points, settings (add "files kept the same"), as in the
      current README but generic.
   7. **Where the files are**: Windows `%LOCALAPPDATA%\LSS`, Linux
      `~/.local/share/LSS`; one folder per account and game.
   8. **Coming from Control Save Manager**: data moved automatically on
      first start (from `%LOCALAPPDATA%\ControlSaveManager`), old exports
      can be imported.
   9. **FAQ** (current ones, generic) + "My game isn't in the list".
   10. **License** and the disclaimer: *Steam is a trademark of Valve
       Corporation. LSS is not affiliated with or endorsed by Valve. Game
       names are trademarks of their owners.*
3. `CODE_SIGNING.md`: product name, `LSS.exe`, data folder, repository
   link.

Commit: `README for LSS`

---

## J3 — Other documents

1. `docs/DEVELOPMENT.md`: update every section to the new layout:
   projects (`src/Lss.Core`, `src/Lss.Platform`, `src/Lss.App`), the code
   structure table (one row per file of the three projects and tests),
   building (`dotnet build LSS.slnx`, publish profiles `win-x64` /
   `linux-x64`), tests (Windows and Linux), Linux notes (H: Steam folders,
   `registry.vdf`, `/proc`, lock file), packages (I), Smart App Control
   section (keep it; add that the Avalonia native dlls are signed by
   Microsoft and AvaloniaUI). Remove every WinForms-only detail.
2. `docs/COMPATIBILITY.md` (new): table `Game | AppID | Windows | Linux |
   Notes`, with Control (870780, tested on Windows), Terraria (105600:
   only cloud characters and worlds are in `userdata`), and an "How to
   report a game" paragraph pointing to the issue template.
3. `docs/TODO.md`: rewrite: what's left after 1.0 (Flathub, AUR, games
   saving elsewhere, macOS, desktop shortcut, more languages), and the
   manual checks still open.
4. `docs/KNOWN_BUGS.md`: log path for both systems.
5. `.github/ISSUE_TEMPLATE/bug.yml` (GitHub issue form): fields system
   (dropdown Windows/Linux/Steam Deck), distribution, how Steam is
   installed (native/Flatpak/Snap), LSS version, steps, log (textarea, with
   the log path for both systems).
6. `.github/ISSUE_TEMPLATE/game-report.yml`: game name, AppID, system,
   works yes/no/partly, notes.
7. Remove `docs/PIANO_LSS.md` if it is still there (superseded by
   `docs/plan/`).

Commit: `Docs for LSS`

---

## J4 — Version `1.0.0-beta.1`

1. `src/Lss.App/Lss.App.csproj`: `<Version>1.0.0-beta.1</Version>`.
2. `Program.Version` must show the beta: use the informational version,
   without the `+commit` suffix the SDK adds:

   ```csharp
   public static string Version { get; } =
       (typeof(Program).Assembly
           .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "")
       .Split('+')[0];
   ```

   (`using System.Reflection;`). Check the header shows
   `v1.0.0-beta.1`.
3. `CHANGELOG.md`: turn "Unreleased" into `## 1.0.0-beta.1 — <date>` and
   list, in the existing style (short bullets, grouped): new name; every
   Steam game with saves in `userdata`; games list; Linux (packages);
   new interface; files kept the same per game; data moved to `LSS`;
   export format 2; detection of the running game via Steam.
4. Metainfo: `<release version="1.0.0-beta.1" date="<date>" type="development" />`.

Commit: `Version 1.0.0-beta.1`

---

## J5 — 👤 First beta (Andrix)

1. `git switch main`, `git merge --ff-only lss` (or a pull request from
   `lss`), push.
2. `git tag v1.0.0-beta.1`, `git push origin v1.0.0-beta.1`.
3. On GitHub: the draft release has Windows (unsigned: SignPath isn't set
   up yet) and Linux files. Publish it as **pre-release**.

---

## J6 — 👤 SignPath (Andrix) — closed, refused

The application was sent on 2026-10-24 and **refused** on 2026-10-25.
Decision with Andrix: release without code signing. The SignPath steps
were removed from the workflow; the README explains SmartScreen and Smart
App Control to users; `CODE_SIGNING.md` became `PRIVACY.md` (its privacy
part); `docs/DEVELOPMENT.md` → "Code signing" says what's left if signing
is ever wanted (paid only).

---

## J7 — Further betas — skipped

Planned only to check the signed exe; without signing, 1.0.0 follows
beta 1 directly.

---

## J8 — 1.0.0

Release when, all written in `PROGRESS.md`:

- no open bug in `docs/KNOWN_BUGS.md`;
- G13, I7 done; H8 as far as the hardware allows (see `docs/TODO.md`,
  "Known limits of 1.0");
- tried on Windows 11, Ubuntu, Debian, Fedora, Arch (containers) and the
  Steam Deck;
- migration from Control Save Manager tried with real data (F4).

Agent: `<Version>1.0.0</Version>`, CHANGELOG `## 1.0.0 — <date>` (summary of
the betas), metainfo `<release version="1.0.0" date="<date>" />` (remove
the beta release lines), commit `Version 1.0.0`, Linux check.

Andrix (or the agent with `gh`, when Andrix asks): merge to `main`,
`git tag v1.0.0`, push, check the draft (every file and its `.sha256`),
publish it as the latest release. In the old `v0.9.0` release add a line
"Continued as LSS — Local Save Switcher" with the link.

---

## J9 — After 1.0 (separate plan)

Flathub (manifest with `org.freedesktop.Platform`, `--filesystem` for the
Steam folders, `--socket=wayland`, `--socket=fallback-x11`; process
detection works only through `registry.vdf` inside the sandbox), AUR
package `lss-bin`, games that save outside `userdata`, macOS. Write a new
plan for these when 1.0 is out.

**Direction decided with Andrix (2026-10-24): LSS will support N game
launchers**, not only Steam (Epic Games Launcher, GOG Galaxy, EA app,
Ubisoft Connect, games without a launcher...). Until that plan exists,
every change keeps this in mind:

- Product texts stay launcher-neutral where they describe LSS in general
  ("PC games"); they name Steam only where they describe what works
  today (installation, "which games work").
- New code doesn't add Steam assumptions to the generic parts
  (`SaveManager`, `FileOps`, the playthrough/history/session logic, the
  ViewModels): Steam-specific code stays in `SteamLibrary`, in the Steam
  parts of `IPlatform`, and in texts that say "Steam".
- The plan now exists: phases K–Q in this folder (see README.md → "The
  2.0 plan"): launchers in the core, save locations (Steam Deck), learning
  the save folder from the disk, Epic / GOG / Heroic / any game, simple
  mode, release.
- Original sketch, not to do now: an `IGameProvider` per launcher
  (find installed games, save folders, running game, launch URI, cloud
  sync state) with `SteamProvider` as the first one; `GameInfo` gets a
  provider id and a generic game id instead of `AppId`; data folder
  `<data>/<provider>/<account>/<game>/` with a migration from today's
  layout (in `LegacyMigration`); save locations for launchers without a
  fixed folder from a manifest such as Ludusavi's (dropped on 2026-10-24: no third-party list, see phase L); the games list grouped
  by launcher.
