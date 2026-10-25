# To do

What's missing, in order of priority. Bugs are in
[KNOWN_BUGS.md](KNOWN_BUGS.md); what's already done is in the
[CHANGELOG](../CHANGELOG.md).

## Known limits of 1.0

- **The Windows exe is not code-signed.** SignPath Foundation refused the free
  signing (2026-10-25) and paid signing is not planned. SmartScreen warns the
  first time; with Smart App Control on, Windows may refuse to start LSS. See
  the README and [DEVELOPMENT.md](DEVELOPMENT.md#code-signing).
- **Linux with a real Steam library.** The packages install and start on
  Ubuntu, Debian, Fedora, Arch and the Steam Deck, and the Deck's games list
  is right. Not checked yet: a playthrough switch, a game started and stopped
  from LSS, and Steam as Flatpak or Snap. On the Deck every game saves outside
  `userdata`, so LSS can't manage any of them there yet (see "After 1.0").

## Manual checks with the real game

Done in 0.9.0 (on 2026-10-22 and 2026-10-23): moving the data from Documents,
moving data between two drives, starting the game from the app, recording the
play time, and the data staying intact.

Still to do, because they need actions inside the game or a choice on Steam:

- [ ] **Save on exit with real progress.** Play until a save, close the game
  and check that the playthrough is updated (so far tested only without
  progress and with automated tests).
- [ ] **Files kept the same.** `preferences_data` of Control is a 764-byte
  binary (it contains `default` and an identifier) and the two playthroughs
  have different versions of it. Change an option in the game (subtitles, for
  example), switch playthrough with the setting on and check that it stays.
- [ ] **Export and import between two computers.**
- [ ] **Steam Cloud on.** Deliberately not tested: turning it back on might
  download old saves again. The app warns and recommends turning it off.
- [ ] **Full round.** Two playthroughs, a saved point before a boss, going
  back to the point, switching playthrough and back: each playthrough must
  find its own saved points.

## After 1.0

- [ ] **Flathub**: manifest with `org.freedesktop.Platform`, `--filesystem`
  for the Steam folders, `--socket=wayland`, `--socket=fallback-x11`. Process
  detection works only through `registry.vdf` inside the sandbox.
- [ ] **AUR** package `lss-bin`.
- [ ] **Games that save outside `userdata`** (for example in Documents, in the
  game folder or, on Linux and the Steam Deck, inside the Proton prefix):
  today LSS lists them as *Other games* and doesn't manage them. This is the
  most important change for 2.0, with other launchers.
- [ ] **macOS.**
- [ ] **Desktop shortcut "Play <playthrough>".** One click loads the playthrough
  and starts the game without opening the app. To be checked with Smart App
  Control.
- [ ] **More languages.** Just a `Strings.<code>.resx` with the same keys (see
  [DEVELOPMENT.md](DEVELOPMENT.md#languages)).
