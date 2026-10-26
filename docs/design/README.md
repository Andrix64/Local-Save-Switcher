# LSS 2.0 design boards

The reference design of the LSS 2.0 interface (simple and advanced mode,
every window and popup). It was drawn as an online design canvas; the
boards are copied here so anyone working on the plan (`docs/plan/`) can
read them without access to that canvas. Copied on 2026-10-25, identical to
the published version.

## Files

`boards/canvas.json` lists every board with its title (Italian) and size.
One file per board:

| Board | What |
|---|---|
| `Main.dc.html` | Simple mode — "Who's playing?" (shield = admin) |
| `Games.dc.html` | Simple mode — a player's games |
| `MyGames.dc.html` | Simple mode — ⋯ "My games" |
| `Playing.dc.html` | Simple mode — playing |
| `WhoseSaves.dc.html` | Simple mode — "Whose saves are these?" |
| `AdminPin.dc.html` | "Who are you?" and the PIN pad |
| `CloudBlocked.dc.html` | Cloud saves: a user is blocked; the admin's choice |
| `AddGame.dc.html` | Add a game |
| `FirstRun.dc.html` | First start: simple or advanced |
| `Advanced.dc.html` | Advanced mode: sidebar, save folders box, playthroughs by player |
| `Learn.dc.html`, `LearnSteps.dc.html` | Find the save folder from the disk |
| `Suggestions.dc.html` | Folders found after a session |
| `ImportConfig.dc.html`, `ExportConfig.dc.html` | Save configuration import / export |
| `Settings.dc.html` | Settings (before phase R split them) |
| `Players.dc.html` | Players table |
| `NewPlaythrough.dc.html` | New playthrough |
| `RemovePlayer.dc.html` | Remove a player |
| `SetPin.dc.html` | Set a PIN |
| `IgnoredFolders.dc.html` | Folders marked "not saves" |
| `Deleted.dc.html` | Deleted playthroughs |

## How to read a board

The files need the canvas runtime to render, so read them as source; they
are short.

- The markup between `<x-dc>` and `</x-dc>` is plain HTML with **inline
  CSS**: sizes, colours, radii, spacing and font weights are exact.
- `{{name}}` is a value from the `renderVals()` function in the
  `<script type="text/x-dc">` block at the bottom (sample data and
  per-row colours).
- `<sc-for list="{{items}}" as="x">` repeats its content for each item;
  `<sc-if value="{{cond}}">` shows it only when the value is true.
- Links (`href="Other.dc.html"`) say which board a click leads to.

Common values: background `#0F0F10`, header `#0A0A0B`, surfaces `#19191B`
and `#232326`, borders `#2E2E33`, accent `#E0261B`, warning/star `#F5B83D`,
info `#1B2230` / `#7FB0F0`, font Inter.

The app's own screenshots (`tools/UiScreenshots`, see `docs/DEVELOPMENT.md`)
show the implemented windows to compare with the boards.

Where a task of the plan says something different from a board, the plan
wins (for example phase R: R6, R7, R8).
