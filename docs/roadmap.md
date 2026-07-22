# Arcadium: Godot/.NET arcade-cabinet launcher

## Doel

Arcadium wordt een fullscreen arcade-frontend voor een fysieke kast, met één C#/.NET-codebase voor Windows en Linux.

```text
Computer start op
→ Arcadium start automatisch fullscreen
→ speler kiest systeem en game met arcadecontrols
→ Arcadium start de emulator
→ speler verlaat de emulator via een vaste exit-combinatie
→ Arcadium komt weer op de voorgrond
```

De runtime gebruikt geen TOML. Leesbare JSON-bestanden bevatten alle beheerbare configuratie; SQLite bevat alleen de geïndexeerde bibliotheek en runtimegegevens.

## Uitgangssituatie

Deze repository is bewust leeg op `plan_final.md` na: dit is een schone Godot/.NET-herbouw. De oude Rust-, Tauri-, JavaScript- en Python-code staat in een andere repository en wordt hier niet gemigreerd of verwijderd.

Gebruik die oude repository uitsluitend als functionele en visuele referentie. Bekende referentiepunten zijn ROM-scanning, mediafolders (`wheel`, `videos`, `marquee`, `physical`, `game`), de neonstijl, het Arcadium-logo, het Press Start 2P-font en keyboard-first navigatie. Ripgrep mag als ontwikkelhulpmiddel blijven bestaan, maar is geen runtimeonderdeel: scannen gebeurt met .NET-bestands-API's.

## Doelarchitectuur

```text
arcadium-godot/
├── Arcadium.sln
├── src/
│   ├── Arcadium.Shared/       # domeinmodellen, repositories, launchercontracten
│   ├── Arcadium.Scanner/      # C# CLI voor ROM- en mediascans
│   └── Arcadium.Godot/        # Godot 4 .NET interface
├── database/migrations/       # genummerde SQLite-migraties
├── data/                      # lokale runtime-data, niet versioneren
│   └── arcadium.db
└── docs/
```

| Onderdeel | Verantwoordelijkheid |
|---|---|
| `Arcadium.Shared` | modellen, databasecontracten, scan- en launchlogica |
| `Arcadium.Scanner` | ROMs/media indexeren en SQLite bijwerken |
| `Arcadium.Godot` | cabinet-UI, input, onderhoudsmodus, emulator launching |
| `config/*.json` | systemen, emulatorprofielen en cabinetinstellingen |
| `arcadium.db` | gescande ROMs, mediakoppelingen, favorieten, historie en scanlogs |

Gebruik Godot 4 .NET. Alle eigen scripts zijn C#; scenes en themes blijven Godot-bestanden (`.tscn` en `.tres`).

## Fase 0 — Referentie en veiligheidsnet

1. Leg vanuit de oude repository het gewenste gedrag, schema en de MAME-instellingen vast.
2. Maak screenshots van de oude frontend als ontwerpverwijzing.
3. Maak een onafhankelijke testset met ROMs met artwork, zonder artwork en in submappen.
4. Test de beoogde arcadehardware op Windows én Linux.

**Klaar wanneer:** de functionele referenties en de onafhankelijke testset bekend zijn.

## Fase 1 — .NET solution

1. Maak `Arcadium.sln`.
2. Maak `Arcadium.Shared` als class library.
3. Maak `Arcadium.Scanner` als console-app.
4. Maak `Arcadium.Godot` als Godot 4 .NET-project.
5. Laat scanner en Godot verwijzen naar Shared.
6. Voeg minimaal deze packages toe:

```text
Microsoft.Data.Sqlite
Microsoft.Extensions.Logging
System.CommandLine of Spectre.Console
xUnit
```

7. Negeer gegenereerde en lokale data:

```text
.godot/
bin/
obj/
data/*.db
*.db-shm
*.db-wal
```

**Klaar wanneer:** `dotnet build` slaagt en Godot C#-scripts compileert.

## Fase 2 — JSON-configuratie en SQLite-bibliotheek

Gebruik JSON voor beheerbare configuratie:

```text
config/
├── cabinet.json
├── systems.json
└── emulator-profiles/
    ├── mame.json
    ├── retroarch.json
    ├── dolphin.json
    └── pcsx2.json
```

De onderhoudsmodus bewerkt deze bestanden via een wizard; gevorderde beheerders mogen ze ook rechtstreeks aanpassen, back-uppen en tussen kasten kopiëren. Sla wijzigingen atomair op: schrijf, valideer en vervang pas daarna het bestaande bestand.

`cabinet.json` bevat cabinetgedrag. `systems.json` bevat ROM- en mediapaden, extensies en een verwijzing naar een emulatorprofiel. Een profiel bevat per platform executable-zoeklocaties, argumenttemplates en launchgedrag.

Voorbeeld van `emulator-profiles/mame.json`:

```json
{
  "id": "mame",
  "name": "MAME",
  "platforms": {
    "windows": {
      "defaultExecutables": ["C:\\Emulators\\MAME\\mame.exe"]
    },
    "linux": {
      "defaultExecutables": ["/usr/games/mame", "/usr/bin/mame"]
    }
  },
  "arguments": ["{rom}"],
  "launchMode": "wait"
}
```

Voorbeeldrecord in `systems.json`:

```json
{
  "id": "mame",
  "name": "MAME",
  "romPath": "D:\\Arcadium\\ROMs\\mame",
  "mediaPath": "D:\\Arcadium\\Media\\mame",
  "extensions": [".zip", ".7z"],
  "emulatorProfile": "mame",
  "enabled": true,
  "sortOrder": 1
}
```

Maak in SQLite alleen bibliotheek- en runtime-tabellen:

```text
roms
scan_runs
favorites
game_play_history
schema_version
```

### systems.json

Bevat per systeem: id, naam, ROM-pad, mediapad, extensies, verwijzing naar emulatorprofiel, enabled en sortering. Bevat geen automatisch gegenereerde scaninformatie.

Voorbeeld MAME-record:

```text
id: mame
name: MAME
romPath: D:\Arcadium\ROMs\mame
mediaPath: D:\Arcadium\Media\mame
emulatorProfile: mame
extensions: [".zip", ".7z"]
enabled: true
sortOrder: 1
```

Op Linux zijn dezelfde waarden bijvoorbeeld:

```text
romPath: /home/arcadium/ROMs/mame
mediaPath: /home/arcadium/Media/mame
```

### roms

Bewaar minimaal id, system_id, filename, basename, path, alle vijf mediapaden, size_bytes, modified_time, scan_state, first_seen_at en last_seen_at.

Gebruik `UNIQUE(system_id, path)`, niet alleen basename: dezelfde naam kan in verschillende submappen bestaan.

### cabinet.json

Bevat cabinetgedrag, bijvoorbeeld:

```text
fullscreen                         true
hide_cursor                        true
idle_attract_mode_seconds          120
maintenance_combo                  P1_START+P2_START+SERVICE
return_to_library_after_game       true
```

### Migraties

Gebruik genummerde SQL-bestanden:

```text
001_initial_schema.sql
002_scan_runs.sql
003_rom_path_uniqueness.sql
004_favorites_and_history.sql
```

De scanner voert migraties uit bij start. Maak voor elke schemawijziging een databaseback-up.

## Fase 3 — Eerste JSON-configuratie

1. Maak `cabinet.json`, `systems.json` en `emulator-profiles/mame.json` handmatig vanuit de vastgelegde referentie.
2. Vul de lokale ROM-, media- en emulatorpaden in via de onderhoudswizard.
3. Valideer alle bestanden bij opstarten en toon concrete fouten voor ontbrekende velden of paden.
4. Controleer het MAME-profiel met de ingebouwde test-launch.

**Klaar wanneer:** een geldige MAME-configuratie volledig in leesbare JSON-bestanden staat.

## Fase 4 — C# scanner

Maak deze CLI:

```bash
arcadium-scanner scan init --db data/arcadium.db
arcadium-scanner scan update --db data/arcadium.db
arcadium-scanner scan verify --db data/arcadium.db
```

Optioneel:

```text
--system mame
--dry-run
--verbose
--json
--with-checksum
```

Per actief systeem:

1. Lees systeemgegevens en emulatorprofielen uit JSON.
2. Valideer ROM-pad, media-pad en extensies.
3. Scan met `Directory.EnumerateFiles`.
4. Scan recursief tenzij anders ingesteld.
5. Vergelijk extensies exact en case-insensitive.
6. Lees grootte en modified time.
7. Indexeer iedere mediafolder vooraf.
8. Koppel media op een genormaliseerde basename.
9. Upsert alleen nieuwe of gewijzigde ROMs.
10. Markeer niet-gezien ROMs als `deleted`.
11. Schrijf statistieken naar `scan_runs`.

Mediafolders blijven:

```text
<ui_path>/wheel/
<ui_path>/videos/
<ui_path>/marquee/
<ui_path>/physical/
<ui_path>/game/
```

Bouw per mediatype een dictionary `basename → pad`; doorzoek media niet opnieuw per ROM.

Een ROM is ongewijzigd als path, grootte en modified time gelijk zijn. Ontoegankelijke bestanden of mappen geven waarschuwingen, maar stoppen de scan niet.

**Klaar wanneer:** C# de testset volledig en correct indexeert.

## Fase 5 — Functionele validatie

1. Scan de onafhankelijke testset.
2. Vergelijk de resultaten met de vooraf vastgelegde verwachtingen en, waar nuttig, met de oude repository.
3. Test handmatig meerdere MAME-ROMs, ontbrekende artwork en ontbrekende mappen.
4. Documenteer verschillen als expliciete bugs of gewenste verbeteringen.

De oude repository blijft onafhankelijk bestaan en wordt niet vanuit dit project gewijzigd.

## Fase 6 — Godot cabinet-project

Start Godot fullscreen, borderless en met verborgen cursor. Laad de database bij start en plaats focus op de systeemkiezer.

Gebruik deze scene-structuur:

```text
Main.tscn
├── Background
├── Header
│   ├── Logo
│   ├── SystemSelector
│   └── Status
├── Content
│   ├── LibraryScreen
│   ├── GameDetailsScreen
│   ├── SettingsScreen
│   └── MaintenanceScreen
├── OverlayLayer
│   ├── SearchOverlay
│   ├── CommandPalette
│   ├── ConfirmLaunchDialog
│   ├── ErrorDialog
│   └── HelpOverlay
└── ToastLayer
```

Maak minimaal deze C# services:

```text
AppState.cs
InputRouter.cs
NavigationService.cs
LibraryRepository.cs
SettingsRepository.cs
EmulatorLauncher.cs
AttractModeService.cs
MaintenanceAccessService.cs
PlatformService.cs
```

Gebruik Godot Autoload voor `AppState`, `LibraryRepository` en `NavigationService`.

## Fase 7 — Visueel ontwerp

Migreer deze bestaande assets:

```text
PressStart2P-Regular.ttf
logo.png
neongrid.png
```

Maak één Godot-theme met styles voor buttons, geselecteerde buttons, focus, panels, cards, modals, tekstvelden, scrollbars, foutmeldingen en toastmeldingen.

Ontwerpregels:

- leesbaar op cabinetafstand;
- hoge contrasten;
- geen hover-afhankelijke bediening;
- grote selecteerbare zones;
- korte, niet-blokkerende animaties;
- zichtbare selectie voor controller en keyboard.

## Fase 8 — Arcadecontrols en focus

Maak Godot Input Actions:

```text
navigate_up
navigate_down
navigate_left
navigate_right
accept
back
next_system
previous_system
open_search
open_menu
open_help
maintenance
```

Aanbevolen mappings:

| Actie | Arcadecontrol |
|---|---|
| Navigeren | joystick of D-pad |
| Accepteren/starten | Player 1 Button 1 |
| Terug | Player 1 Button 2 |
| Systeem wisselen | Player 1 Button 5/6 |
| Menu | Player 1 Start |
| Onderhoud | serviceknop of verborgen combinatie |

Inputprioriteit:

```text
actieve modal
→ actief scherm
→ globale cabinetacties
```

Bij een overlay: sla focus op, geef focus aan de overlay, blokkeer onderliggende bediening en herstel focus bij sluiten.

## Fase 9 — Bibliotheek

### Systeemselectie

Toon alleen systemen waarvoor `enabled = true` én minstens één actieve ROM bestaat. Sorteer op `sort_order`.

### LibraryScreen

Toon systeemnaam, gamegrid/lijst, geselecteerde game, artwork, wheel-logo, gameaantal en bedieningshint. Gebruik lazy loading en een beperkte texture-cache. Ontbrekende artwork krijgt een nette placeholder.

### GameDetailsScreen

Toon game-art, titel, systeem, bestandsnaam, grootte, beschikbare media, Start game, favoriet en terug.

### Zoekfunctie

Zoek minimaal op basename en filename. Voeg SQLite FTS pas toe als een grote bibliotheek dit nodig maakt.

## Fase 10 — Emulatoren starten

De `EmulatorLauncher` moet:

1. ROM en emulator valideren.
2. Alleen bekende placeholders zoals `{rom}` vervangen.
3. Argumenten als losse waarden opbouwen.
4. Geen shell gebruiken.
5. In cabinetmodus wachten tot de emulator stopt.
6. Arcadium opnieuw fullscreen/focus geven.
7. Optioneel speelgeschiedenis bijwerken.

Gebruik bijvoorbeeld:

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = system.EmulatorCommand,
    WorkingDirectory = system.EmulatorWorkingDirectory,
    UseShellExecute = false,
};

startInfo.ArgumentList.Add(game.RomPath);
using var process = Process.Start(startInfo);
await process.WaitForExitAsync();
```

| Mode | Gedrag |
|---|---|
| `wait` | wacht op emulator-exit; standaard voor de kast |
| `background` | emulator naast Arcadium; alleen voor debug |
| `replace` | Arcadium sluit; niet in de eerste versie |

Toon in Godot duidelijke fouten voor ontbrekende ROMs, ontbrekende/niet-uitvoerbare emulatoren, ongeldige argumenttemplates en onverwachte exitcodes.

## Fase 11 — Kiosk en onderhoud

### Spelersmodus

- fullscreen en cursor verborgen;
- geen toegang tot desktop, paden of shell;
- Arcadium neemt focus terug na emulator-exit;
- geen gewone afsluitknop.

Configureer in iedere emulator een consistente exit-combinatie, bijvoorbeeld `P1 Start + P2 Start`, of gebruik een fysieke serviceknop. Kies een combinatie die niet per ongeluk tijdens normaal spelen ontstaat.

### Onderhoudsmodus

Beschikbaar uitsluitend via serviceknop of geheime combinatie. Bied:

- systemen en emulatorprofielen toevoegen, wijzigen en uitschakelen;
- ROM-, media- en emulatorpaden kiezen;
- extensies instellen;
- emulator testen;
- bibliotheek scannen;
- scanlogs bekijken;
- controller testen;
- instellingen wijzigen;
- databaseback-up;
- veilig afsluiten.

### Attract mode

Na een configureerbare periode zonder input: toon artwork of previews en een startmelding. Verlaat de modus bij elke input. Implementeer dit pas nadat navigatie en launching stabiel zijn.

## Fase 12 — Windows en Linux

Kerncode blijft gedeeld. Alleen OS-integratie varieert. Maak daarvoor:

```text
IPlatformService
├── GetDefaultLibraryRoot()
├── NormalizePath()
├── OpenFolderPicker()
├── OpenFilePicker()
├── GetUserDataDirectory()
└── ConfigureAutostart()
```

Gebruik voor runtime-data:

| Platform | Aanbevolen locatie |
|---|---|
| Windows | `%LocalAppData%\Arcadium\arcadium.db` |
| Linux | `~/.local/share/Arcadium/arcadium.db` |
| Godot | `user://arcadium.db` |

ROMs en media mogen op een andere schijf staan; hun lokale paden worden in `systems.json` opgeslagen.

Autostart:

| Platform | Aanpak |
|---|---|
| Windows | Task Scheduler, Startup-folder of kiosk-account |
| Linux | systemd user service of desktop-autostart |

Gebruik op beide platformen een dedicated arcade-gebruiker zonder beheerdersrechten.

## Fase 13 — Tests

Unit-tests:

- database-migraties;
- extensiefiltering;
- media-basename matching;
- incrementele scanlogica;
- placeholdervervanging;
- padverwerking voor Windows/Linux;
- instellingenvalidatie.

Integratietests met tijdelijke mappen:

1. nieuwe ROM;
2. gewijzigde ROM;
3. verwijderde ROM;
4. ROM met en zonder media;
5. ontoegankelijke map;
6. databasemigratie;
7. uitgeschakeld systeem.

Test op echte cabinet-hardware op beide systemen:

1. fullscreen start;
2. controllerinput;
3. paden met spaties;
4. tweede schijf;
5. MAME starten;
6. emulator-exit en focusherstel;
7. databaseherbouw;
8. autostart na reboot;
9. onderhoudsmodus.

## Fase 14 — Build en release

Publiceer de scanner per platform:

```bash
dotnet publish src/Arcadium.Scanner --configuration Release --runtime win-x64 --self-contained true
dotnet publish src/Arcadium.Scanner --configuration Release --runtime linux-x64 --self-contained true
```

Maak aparte Godot-exports voor Windows en Linux.

Bundel Godot-exportbestanden, C# assemblies, scanner-binary, assets en database-migraties. Bundel geen ROMs, productie-database, lokale media of persoonlijke instellingen.

## Implementatievolgorde

1. Referentiegegevens en testset maken.
2. .NET solution, Shared-project en SQLite-migraties maken.
3. Eerste JSON-configuratie en MAME-profiel maken.
4. C# scanner bouwen.
5. Scanner op de testset valideren.
6. Godot .NET project, theme en fullscreen cabinetbasis maken.
7. Library vanuit SQLite tonen.
8. Controllerinput en focusbeheer toevoegen.
9. Systeem- en gameselectie afronden.
10. MAME launching op Windows en Linux maken.
11. Terugkeer na emulator-exit verifiëren.
12. Onderhoudsmodus toevoegen.
13. Autostart per platform instellen.
14. Zoeken, favorieten, historie en attract mode toevoegen.

## Eerste mijlpaal

Bouw eerst een C# scanner die systemen en emulatorprofielen uit JSON leest en de onafhankelijke testset correct indexeert. Pas als die basis stabiel is, bouw je de Godot cabinet-interface op de SQLite-bibliotheek.
