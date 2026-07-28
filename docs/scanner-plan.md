Fase 1 — Data-laag + testfundament
Doel: de scanner kan straks tegen een echte SQLite-database praten; er bestaat een testproject.

Package: voeg Microsoft.Data.Sqlite toe aan src/Arcadium.Core/Arcadium.Core.csproj.
Migraties embedden: link database/migrations/*.sql als EmbeddedResource in het Core-csproj (met wildcard, zodat migratie 002 later vanzelf meegaat; de repo-map blijft source of truth).
Data/SqliteDatabase.cs: connection factory op basis van het db-pad. Bij openen: PRAGMA journal_mode=WAL, foreign_keys=ON, busy_timeout=5000. Twee methodes: OpenReadWrite() en OpenReadOnly() (die laatste voor de Godot-frontend later). Zorg dat de map van het db-pad aangemaakt wordt als die ontbreekt.
Data/MigrationRunner.cs: lees de embedded scripts op nummer, lees de hoogste versie uit schema_version (tabel bestaat mogelijk nog niet → versie 0), pas elk hoger script toe in zijn eigen transactie en insert de versierij.
Models/RomRecord.cs: spiegel de roms-tabel — systeem-id, filename, basename, path, de vijf mediapaden, SizeBytes, ModifiedTimeUtc, ScanState, timestamps.
Data/ScanRepository.cs met precies vijf operaties:
BeginScanRun(mode, systemId, startedAt) → nieuw scan_runs-id
CompleteScanRun(id, totals, finishedAt)
GetRomsBySystem(systemId) → Dictionary<string, RomRecord> gekeyed op path
UpsertRom(rom) → INSERT … ON CONFLICT(system_id, path) DO UPDATE
MarkUnseenAsDeleted(systemId, scanStartedAt) → scan_state='deleted' waar last_seen_at < start
Plus transactiebeheer: begin/commit/rollback exposen zodat de engine één transactie per systeem kan draaien.

Testproject: tests/Arcadium.Core.Tests (xUnit), toevoegen aan Arcadium.sln, referentie naar Core.
Tests schrijven:
MigrationRunner: verse db → alle tabellen bestaan; tweede run → no-op (idempotent).
Repository: insert → update (gewijzigde size/mtime) → unchanged; MarkUnseenAsDeleted raakt alleen niet-geziene rijen; scan_runs rondje begin/complete.
Gebruik tempbestand-databases (WAL werkt niet op :memory: zoals je zou verwachten bij meerdere connecties).
CI: pas .github/workflows/ci.yml aan — dotnet test moet naar het nieuwe testproject wijzen (nu test hij de console-app, wat een no-op is).
Klaar wanneer: dotnet test groen op Windows én Ubuntu (CI-matrix), en TreatWarningsAsErrors geeft geen gedoe.

Fase 2 — Event-contract + scan-engine
Doel: de volledige scanlogica, host-agnostisch, met progress-events — nog zonder UI.

Scanning/ScanEvent.cs: abstracte base-record met Timestamp, [JsonPolymorphic] met discriminator "event", en de zes afgeleiden: ScanStarted, SystemScanStarted, FileProgress(Processed, Total, CurrentFile), ScanWarning, SystemScanCompleted(Stats), ScanCompleted(Summary).
Registreren in ArcadiumJsonContext: [JsonSerializable(typeof(ScanEvent))] — de afgeleiden gaan mee via de polymorfie-attributes. Even smoke-testen dat serialisatie de discriminator bevat.
Scanning/ScanSummary.cs: SystemScanStats(FilesSeen, Added, Updated, Unchanged, Deleted, Warnings) + ScanSummary(ScanRunId, Aborted, Duration, per-systeem-dict, Totals).
Scanning/ScanOptions.cs: Mode, SystemFilter, DryRun, ProgressInterval (default 100 ms).
Scanning/InlineProgress.cs: IProgress<T> die zijn delegate synchroon aanroept — de BCL Progress<T> post naar de SynchronizationContext en herordent events; dit klasje van vijf regels voorkomt dat in beide hosts.
Scanning/MediaIndex.cs: bouwt per systeem éénmalig de basename → path-dictionaries voor wheel/videos/marquee/physical/game (case-insensitive keys, ontbrekende submappen zijn gewoon lege dicts, geen warning per bestand).
Scanning/ScanEngine.cs — het hart, in deze volgorde per enabled systeem (of alleen SystemFilter):
SystemScanStarted emitten, transactie openen, BeginScanRun.
Rom-paden valideren; onbereikbaar → ScanWarning, systeem overslaan zonder abort.
Bestanden verzamelen over álle RomPath-roots (recursief, case-insensitive extensiematch), dan pas totaal bekend → eerste FileProgress(0, Total).
MediaIndex bouwen; bestaande roms laden via GetRomsBySystem.
Per bestand: size + mtime lezen, media matchen op genormaliseerde basename, diffen (unchanged ⟺ path + size + mtime gelijk), upsert bij nieuw/gewijzigd. FileProgress alleen emitten als ProgressInterval verstreken is; altijd een afsluitende Processed == Total.
MarkUnseenAsDeleted, stats verzamelen, CompleteScanRun.
Commit — of rollback bij DryRun (met dezelfde echte cijfers) en bij cancellation.
SystemScanCompleted(Stats) emitten.
Na alle systemen: ScanCompleted(Summary). CancellationToken checken per bestand; ScanMode-verschillen (init/update/verify) volgens docs/scanner.md — verify muteert niet, rapporteert alleen.

Tests (tempmappen + tempdb): nieuw ROM, gewijzigd ROM (mtime), verwijderd ROM → deleted, met/zonder media-match, onbereikbare dir → warning maar scan loopt door, disabled systeem wordt overgeslagen, SystemFilter, dry-run twee keer → identieke cijfers en lege db, cancellation halverwege → eerdere systemen gecommit, event-volgorde correct.
Klaar wanneer: alle scenario-tests groen; JSON-roundtrip van elk eventtype werkt.

Fase 3 — CLI-host
Doel: de eerste zichtbare mijlpaal — de scanner indexeert jouw testset met live progress.

Package: Spectre.Console toevoegen aan src/Arcadium.Scanner (alleen daar, niet in Core).
Argparsing uitbreiden in Program.cs (bestaande hand-rolled loop): --db (fixt meteen de gedocumenteerd-maar-niet-geparste bug), --system, --dry-run, --json, --verbose. Bare -cc accepteren (de args.Length < 2-check).
SpectreScanRenderer: Spectre Progress met één taakregel per systeem (aangemaakt bij SystemScanStarted, total uit eerste FileProgress), warnings via AnsiConsole.MarkupLine erboven, samenvattingstabel uit ScanSummary op het einde.
NdjsonEventWriter: één JsonSerializer.Serialize(evt, ArcadiumJsonContext.Default.ScanEvent) per regel op stdout, per regel flushen; álle menselijke output naar stderr. Dit is het latere Godot-protocol — hier al exact goed krijgen.
Wiring: config laden/valideren (bestaand) → SqliteDatabase openen + MigrationRunner draaien → sink kiezen (--json → NDJSON, anders Spectre) → ScanEngine.RunAsync met InlineProgress → summary naar exitcode: 0 ok, 1 config/startfout, 2 voltooid met errors, 3 geannuleerd.
Ctrl+C: Console.CancelKeyPress → e.Cancel = true; cts.Cancel() — nette abort via hetzelfde cancellation-pad.
Docs: docs/scanner.md bijwerken met de flags, exitcodes en het NDJSON-formaat (dat document wordt het contract voor fase 4).
Klaar wanneer (handmatige verificatie):

dotnet run --project src/Arcadium.Scanner -- scan tegen je testset (C:\Users\sage\Downloads\test\) toont progress en vult roms + scan_runs.
scan --json geeft geldige NDJSON, afgesloten met scanCompleted.
--dry-run twee keer → identieke cijfers, geen db-mutaties.
Ctrl+C halverwege → exit 3, db consistent.