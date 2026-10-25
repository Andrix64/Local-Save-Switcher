# Privacy

LSS doesn't collect or send any data. It will not transfer any information to
other networked systems unless specifically requested by the user or the
person installing or operating it.

LSS works only on local files: the save folders of the games in Steam's
`userdata` and its own data in `%LOCALAPPDATA%\LSS` (on Linux
`~/.local/share/LSS`). To start a game it asks the locally installed Steam
client to run it (`steam://rungameid/<AppID>`); it does not connect to the
internet by itself.
