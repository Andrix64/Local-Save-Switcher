# LSS — Local Save Switcher

Multiple playthroughs for your PC games (Windows and Linux).

![Main window](docs/img/main.png)

LSS keeps several playthroughs of the same game apart. Pick the one you want
to play and LSS loads its saves and starts the game. Before every switch it
makes a copy, and you can save points for the hard parts and go back to them
later.

It works with **Steam, Epic Games, GOG and Heroic** games and with any other
game you add by hand, and it finds by itself where each game keeps its saves.
It runs on Windows 10/11 and on Linux (including the Steam Deck), and the
interface is in English or Italian, following the system language.

## Two modes

- **Advanced mode** (the one above) is the full window: games on the left,
  the playthroughs of the chosen game, saved points, history and settings.
- **Simple mode** is made for a family or for the Steam Deck: everyone picks
  their name, then a tile for each game, and LSS does the rest. A player can
  have favourite games and an optional PIN; the admins set the games up and
  manage the players.

  ![Simple mode](docs/img/simple.png)

A **Simple / Advanced** switch in the header moves between them. LSS asks
which one you want the first time, and simple mode always starts full screen.

## Which games work

| Where the game comes from | How LSS finds it |
|---|---|
| **Steam** (Windows, Linux, Steam Deck; native, Flatpak or Snap) | the Steam libraries; games added to Steam as non-Steam games are listed too |
| **Epic Games Launcher** (Windows) | the launcher's own manifests |
| **GOG** and **GOG Galaxy** (Windows) | the installed GOG games |
| **Heroic Games Launcher** (Linux and Windows) | Heroic's own files |
| **Any other game** | added by hand with **Add a game** (a name and, if you like, the program to start) |

**How the saves are found.** For each game LSS looks, in this order, at:

1. Steam's `userdata` folder, when the game keeps its saves there;
2. what the launcher itself knows: Steam Auto-Cloud rules and Epic's cloud
   save folder, for example;
3. the folder you chose, or one LSS learned: **Find the saves** has you play a
   bit and then looks for the folders that changed (it only suggests; nothing
   is used until you confirm);
4. a folder you pick by hand.

LSS never uses a list of games from the internet and never connects to
anything.

A game can keep saves in **several folders, or in loose files** (for example
`*.sav` in a folder): add each one in the game's settings, under *Save
folders*, and they switch together. After every session LSS looks for new save
folders written during the game and offers them to you; the ones you refuse
are remembered.

Games that have no saves yet are shown as *No saves yet: start the game once*.
Not supported yet: saves kept in the Windows registry. Other launchers (EA app,
Ubisoft Connect, Battle.net, itch.io, Lutris) work as "any other game".

If someone already worked out where a game saves, you can share it: in the
game's settings, **Export save configuration** writes a `.lss-config.json`
file and **Import save configuration** reads one (see
[docs/SAVE_CONFIG.md](docs/SAVE_CONFIG.md)).

Games that were tested are listed in [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md).

## Installation — Windows

1. Download `LSS-<version>-win-x64.zip` from the
   [releases page](https://github.com/Andrix64/Local-Save-Switcher/releases)
   and extract `LSS.exe` wherever you like.
2. Run it. Nothing to install: the program already contains everything it
   needs (that's why it's about 50 MB).

Requirements: Windows 10 or 11, 64-bit, and a launcher with games (Steam, Epic Games, GOG or Heroic), or any game you add by hand.

> **The exe is not digitally signed.** Signing it needs a certificate that
> costs money every year, and LSS is a free hobby project. What this means:
>
> - **SmartScreen** may show "Windows protected your PC": choose
>   *More info → Run anyway*. It only asks the first time.
> - **Smart App Control** (Windows 11), if it's on, may refuse to start the
>   program, with no way to allow it for a single app. If that happens, the
>   only options are to turn Smart App Control off (Settings → Privacy &
>   security → Windows Security → App & browser control; it can't be turned
>   back on without reinstalling Windows) or to use LSS on another computer.
> - To check that the zip is the one built on GitHub, compare it with the
>   `.sha256` file next to it on the releases page.
>
> Every release is built by GitHub Actions from the code in this repository:
> anyone can see how.

## Installation — Linux

Linux x64 only. Steam can be installed natively, as Flatpak or as Snap: all
three are supported. Pick the file for your distribution from the
[releases page](https://github.com/Andrix64/Local-Save-Switcher/releases),
where `<version>` is the version in the file name, for example `2.0.0`.

| Distribution | File | Command |
|---|---|---|
| Ubuntu, Debian, Linux Mint | `lss_<version>_amd64.deb` | `sudo apt install ./lss_<version>_amd64.deb` |
| Fedora | `lss-<version>-1.x86_64.rpm` | `sudo dnf install ./lss-<version>-1.x86_64.rpm` |
| Steam Deck (desktop mode), openSUSE, Arch and others | `LSS-<version>-x86_64.AppImage` | `chmod +x LSS-<version>-x86_64.AppImage`, then run it |
| Any other distribution | `LSS-<version>-linux-x64.tar.gz` | extract it, then `./install.sh` (to remove it: `./install.sh --uninstall`) |

The AppImage needs no installation. The `.tar.gz` installs into `~/.local`
and doesn't need root.

### Steam Deck: use LSS in Gaming Mode

1. In **Desktop Mode**, download the AppImage, make it executable
   (`chmod +x`) and run it once to check that it opens.
2. Open Steam in Desktop Mode, choose **Games → Add a Non-Steam Game to My
   Library → Browse…**, pick the AppImage and add it.
3. Back in **Gaming Mode**, open LSS from the *Non-Steam* part of your library.
   It fills the screen, and the games you start from it open in Gaming Mode
   as usual.
4. LSS in simple mode always starts full screen. To use it with the controller
   as a mouse, open the controller settings of the LSS entry and pick the
   *Web Browser* layout.

Turn off Steam Cloud for each game you manage with LSS (see below): the Deck
syncs it like any other Steam machine.

## Before you start: turn off Steam Cloud

With Steam Cloud on, Steam may put back saves from another playthrough when
the game starts. Turn it off for the game: in Steam, open the **Library**,
right-click the game, choose **Properties**, and in **General** switch off
*Keep games saves in the Steam Cloud*. While it's on, LSS shows a yellow bar at
the top.

**Steam must be closed while LSS changes the files.** Steam keeps its own list of the saves
in memory (`remotecache.vdf`) and writes it back, so a game may not find its saves, or say it
could not restore them, if you change playthrough with Steam open. LSS asks you to close
Steam first (and starts it again with the game). On the Steam Deck in Gaming Mode Steam can't
be closed: change playthrough from Desktop Mode, after exiting Steam.

## How to use it

### Playthroughs

Each playthrough has its own saves. The one with the red border and
*IN THE GAME NOW* is the one loaded in the game.

- **Play this**: saves the progress of the current playthrough, loads the
  chosen one and starts the game through Steam. No questions asked: a copy is
  made before every switch.
- **Play**, on the playthrough already in the game: only starts the game.
- **⋯** on every playthrough: *Load without starting*, *Rename*, *Edit note*,
  *Export to a file...* and *Delete playthrough...*. If you load without
  starting, an **Undo** button appears at the bottom to go straight back to
  the previous playthrough.
- **New playthrough**: empty by default, to start again from the beginning; you
  can also copy an existing playthrough or import a file exported on another
  computer.

  ![New playthrough](docs/img/new-playthrough.png)

Each card shows the note, the play time, when it was last saved and the saved
points.

### While you play

While the game is running LSS doesn't touch the saves and says so at the top.
When you close the game, your progress is saved automatically to the
playthrough you were playing and the play time is updated (LSS needs to be
open).

### History and saved points

**Save a point** (on the playthrough in the game) saves the current saves
under a name, for example before a boss: if it goes badly, you go back there.
**History** shows your saved points and the automatic copies made before
every operation; each row has **Go back here**. Before going back, the
current situation is copied, so you can always change your mind.

![History](docs/img/history.png)

### Settings

There are two settings windows: the game's (the gear next to its name) and
LSS's (the gear in the header).

Game settings:

- *“Play this” also starts the game*: if you turn it off, the playthrough is
  only loaded.
- *Save progress when you close the game*.
- *Same game options for all playthroughs*: controls, audio and subtitles stay
  the same when you switch playthrough.
- *Files kept the same*: other files of the game that you want to share
  between all playthroughs, for example a preferences file.
- How many automatic copies to keep (20 by default, 0 = all).
- *Save folders*: where the game keeps its saves (see [Which games work](#which-games-work)),
  and the folders that are not saves.
- *Look for other save folders after playing*.
- *Export / Import save configuration*.

![Settings](docs/img/settings.png)

## Where the files are

Playthroughs, saved points, automatic copies, the players and the operation log
(`log.txt`) are in the LSS data folder, with one folder per launcher, account
and game:

- Windows: `%LOCALAPPDATA%\LSS` (usually `C:\Users\<name>\AppData\Local\LSS`).
- Linux: `~/.local/share/LSS`.

OneDrive doesn't sync the Windows folder, so saves aren't locked,
half-downloaded or duplicated as "conflict copies". Settings has a button to
open the folder.

If more than one Steam account on the computer has saves for a game, LSS asks
which one to use at startup; each account has its own playthroughs.

To uninstall, delete the exe (or remove the package). To delete the data as
well, delete the folder above: that's where your playthroughs are.

## Coming from Control Save Manager

LSS is the new name of Control Save Manager. On first start, the data is moved
automatically from `%LOCALAPPDATA%\ControlSaveManager` to `%LOCALAPPDATA%\LSS`,
and the app tells you. Playthroughs exported with Control Save Manager can
still be imported.

## FAQ

**I loaded the wrong playthrough.** If the game didn't start, press **Undo**
in the notice at the bottom; otherwise play the previous one: its progress was
saved before the switch.

**I lost some progress.** Open **History**: there's a copy before every
operation, with date and description.

**I deleted a playthrough by mistake.** *Deleted playthroughs* appears at the
bottom of the main window: recover it from there, until the cleanup of
automatic copies removes it.

**Saves get mixed up between playthroughs.** It's almost always Steam Cloud:
make sure it's off (see above).

**My game isn't in the list.** Use **Add a game**, then let LSS find the saves
(*Find the saves*) or pick the folder yourself. If the launcher's games are
missing, check that the launcher is installed and has been opened once. To tell us about a game, use the
[game report](https://github.com/Andrix64/Local-Save-Switcher/issues/new/choose)
form.

**I forgot a PIN.** Any player can have one. An admin removes it in LSS's
settings, under *Players* (⋯ → *Remove PIN*). If the only admin forgot theirs,
see [docs/KNOWN_BUGS.md](docs/KNOWN_BUGS.md#forgotten-pin).

**Something went wrong.** Open a bug report from the
[issues page](https://github.com/Andrix64/Local-Save-Switcher/issues/new/choose)
and attach the file `log.txt` from the data folder (see above).

## License

[MIT](LICENSE). What's new in each version: [CHANGELOG](CHANGELOG.md).
LSS collects no data: see [PRIVACY.md](PRIVACY.md). For developers:
[docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

*Steam is a trademark of Valve Corporation. LSS is not affiliated with or
endorsed by Valve. Game names are trademarks of their owners.*
