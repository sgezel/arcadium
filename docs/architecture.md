# Architecture

Arcadium separates editable configuration from generated library data.

| Concern | Storage |
|---|---|
| Cabinet settings | `config/cabinet.json` |
| Systems and ROM/media paths | `config/systems.json` |
| Reusable emulator commands | `config/emulator-profiles/*.json` |
| Indexed games, media links and history | SQLite |

`Arcadium.Scanner` reads JSON configuration and writes the SQLite library. `Arcadium.Godot` reads both configuration and library data and starts emulators through a safe argument list, never a shell command.

The Godot UI, scanner and shared models are all C#/.NET. The project supports Windows and Linux; platform-specific defaults belong in emulator profiles and installation documentation.
