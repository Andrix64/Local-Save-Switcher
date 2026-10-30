# Known bugs

No open known bugs.

The last code review (0.9.0) found eight bugs, all fixed and covered by tests
where possible without the interface or a running game. Fixed bugs aren't
listed here: their history is in git.

To report a problem, please include, if possible, the log `log.txt` and the
steps to reproduce it. Where the log is:

- Windows: `%LOCALAPPDATA%\LSS\log.txt` (usually
  `C:\Users\<name>\AppData\Local\LSS\log.txt`).
- Linux: `~/.local/share/LSS/log.txt`.

The bug report form on GitHub asks for it. The manual tests to run with the
real game are in [TODO.md](TODO.md).

## Forgotten PIN

Any player can have a PIN. An admin removes it in LSS's settings, under
*Players* (⋯ → *Remove PIN*). If the only admin forgot theirs:

1. Close LSS.
2. Open the data folder: `%LOCALAPPDATA%\LSS` on Windows, `~/.local/share/LSS`
   on Linux.
3. Open `persone.json` with a text editor and delete the `Pin` value of that
   player (leave it as `"Pin": ""`).
4. Start LSS: that player has no PIN any more.

Copy the file first if you are not sure.
