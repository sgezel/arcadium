# Scripts

Standalone .NET file-based scripts for quickly looking things up or experimenting against
`Arcadium.Core` and the real database. No csproj needed: each script references the Core
project itself via a `#:project` line at the top.

Run from the repo root (required to find `config/cabinet.json`):

```bash
dotnet run tools/scripts/lookup-rom.cs mame zookeep
dotnet run tools/scripts/lookup-rom.cs mame zookeep /path/to/other.db
```

| Script | Purpose |
|---|---|
| `lookup-rom.cs` | Looks up a rom (system + basename) and prints the rom, machine, controls and rom dumps. |

These are throwaway helpers, not tests: they guard nothing and may be modified freely.
