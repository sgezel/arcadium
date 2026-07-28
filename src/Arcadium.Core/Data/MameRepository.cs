using Arcadium.Core.Mame;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Data;

/// <summary>
/// Data access for MAME metadata imports. The caller owns the connection; the whole import
/// runs in one transaction. Insert commands are prepared once and reused because an import
/// writes hundreds of thousands of rows.
/// </summary>
public sealed class MameRepository : IDisposable
{
    private readonly SqliteConnection _connection;
    private SqliteTransaction? _transaction;

    private SqliteCommand? _insertMachine;
    private SqliteCommand? _insertRom;
    private SqliteCommand? _insertControl;

    public bool HasActiveTransaction => _transaction is not null;

    public MameRepository(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    public void BeginTransaction()
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active.");
        }

        _transaction = _connection.BeginTransaction();
    }

    public void Commit()
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No active transaction to commit.");
        }

        DisposeCachedCommands();
        _transaction.Commit();
        _transaction.Dispose();
        _transaction = null;
    }

    public void Rollback()
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No active transaction to roll back.");
        }

        DisposeCachedCommands();
        _transaction.Rollback();
        _transaction.Dispose();
        _transaction = null;
    }

    /// <summary>Removes all machines; rom and control rows follow via ON DELETE CASCADE.</summary>
    public void ClearMachines()
    {
        using var command = CreateCommand();
        command.CommandText = "DELETE FROM mame_machines;";
        command.ExecuteNonQuery();
    }

    public void InsertMachine(MameMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);

        SqliteCommand command = GetInsertMachineCommand();
        command.Parameters["$name"].Value = machine.Name;
        command.Parameters["$sourcefile"].Value = (object?)machine.SourceFile ?? DBNull.Value;
        command.Parameters["$description"].Value = machine.Description;
        command.Parameters["$year"].Value = (object?)machine.Year ?? DBNull.Value;
        command.Parameters["$manufacturer"].Value = (object?)machine.Manufacturer ?? DBNull.Value;
        command.Parameters["$cloneOf"].Value = (object?)machine.CloneOf ?? DBNull.Value;
        command.Parameters["$romOf"].Value = (object?)machine.RomOf ?? DBNull.Value;
        command.Parameters["$sampleOf"].Value = (object?)machine.SampleOf ?? DBNull.Value;
        command.Parameters["$isBios"].Value = machine.IsBios ? 1 : 0;
        command.Parameters["$isDevice"].Value = machine.IsDevice ? 1 : 0;
        command.Parameters["$isMechanical"].Value = machine.IsMechanical ? 1 : 0;
        command.Parameters["$runnable"].Value = machine.Runnable ? 1 : 0;
        command.Parameters["$driverStatus"].Value = (object?)machine.DriverStatus ?? DBNull.Value;
        command.Parameters["$driverEmulation"].Value = (object?)machine.DriverEmulation ?? DBNull.Value;
        command.Parameters["$driverSavestate"].Value = (object?)machine.DriverSavestate ?? DBNull.Value;
        command.Parameters["$players"].Value = (object?)machine.Players ?? DBNull.Value;
        command.Parameters["$coins"].Value = (object?)machine.Coins ?? DBNull.Value;
        command.Parameters["$displayType"].Value = (object?)machine.DisplayType ?? DBNull.Value;
        command.Parameters["$displayRotate"].Value = (object?)machine.DisplayRotate ?? DBNull.Value;
        command.Parameters["$displayWidth"].Value = (object?)machine.DisplayWidth ?? DBNull.Value;
        command.Parameters["$displayHeight"].Value = (object?)machine.DisplayHeight ?? DBNull.Value;
        command.Parameters["$displayRefresh"].Value = (object?)machine.DisplayRefresh ?? DBNull.Value;
        command.Parameters["$soundChannels"].Value = (object?)machine.SoundChannels ?? DBNull.Value;
        command.Parameters["$requiresChd"].Value = machine.RequiresChd ? 1 : 0;
        command.ExecuteNonQuery();

        foreach (MameRomDump rom in machine.Roms)
        {
            InsertRom(machine.Name, rom);
        }

        foreach (MameControl control in machine.Controls)
        {
            InsertControl(machine.Name, control);
        }
    }

    public long RecordImport(string build, int machineCount, DateTime importedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(build);

        using var command = CreateCommand();
        command.CommandText = @"
            INSERT INTO mame_imports (build, machine_count, imported_at)
            VALUES ($build, $machineCount, $importedAt);
            SELECT last_insert_rowid();
        ";
        command.Parameters.AddWithValue("$build", build);
        command.Parameters.AddWithValue("$machineCount", machineCount);
        command.Parameters.AddWithValue("$importedAt", importedAtUtc);

        return (long)command.ExecuteScalar()!;
    }

    public void Dispose()
    {
        DisposeCachedCommands();

        if (_transaction is not null)
        {
            _transaction.Rollback();
            _transaction.Dispose();
            _transaction = null;
        }
    }

    private void InsertRom(string machineName, MameRomDump rom)
    {
        SqliteCommand command = GetInsertRomCommand();
        command.Parameters["$machineName"].Value = machineName;
        command.Parameters["$name"].Value = rom.Name;
        command.Parameters["$sizeBytes"].Value = rom.SizeBytes;
        command.Parameters["$crc"].Value = (object?)rom.Crc ?? DBNull.Value;
        command.Parameters["$sha1"].Value = (object?)rom.Sha1 ?? DBNull.Value;
        command.Parameters["$mergeName"].Value = (object?)rom.MergeName ?? DBNull.Value;
        command.Parameters["$region"].Value = (object?)rom.Region ?? DBNull.Value;
        command.Parameters["$status"].Value = rom.Status;
        command.Parameters["$isOptional"].Value = rom.IsOptional ? 1 : 0;
        command.ExecuteNonQuery();
    }

    private void InsertControl(string machineName, MameControl control)
    {
        SqliteCommand command = GetInsertControlCommand();
        command.Parameters["$machineName"].Value = machineName;
        command.Parameters["$type"].Value = control.Type;
        command.Parameters["$player"].Value = (object?)control.Player ?? DBNull.Value;
        command.Parameters["$buttons"].Value = (object?)control.Buttons ?? DBNull.Value;
        command.Parameters["$ways"].Value = (object?)control.Ways ?? DBNull.Value;
        command.ExecuteNonQuery();
    }

    private SqliteCommand GetInsertMachineCommand()
    {
        if (_insertMachine is null)
        {
            _insertMachine = CreateCommand();
            _insertMachine.CommandText = @"
                INSERT INTO mame_machines (
                    name, sourcefile, description, year, manufacturer,
                    clone_of, rom_of, sample_of,
                    is_bios, is_device, is_mechanical, runnable,
                    driver_status, driver_emulation, driver_savestate,
                    players, coins,
                    display_type, display_rotate, display_width, display_height, display_refresh,
                    sound_channels, requires_chd)
                VALUES (
                    $name, $sourcefile, $description, $year, $manufacturer,
                    $cloneOf, $romOf, $sampleOf,
                    $isBios, $isDevice, $isMechanical, $runnable,
                    $driverStatus, $driverEmulation, $driverSavestate,
                    $players, $coins,
                    $displayType, $displayRotate, $displayWidth, $displayHeight, $displayRefresh,
                    $soundChannels, $requiresChd);
            ";
            AddParameters(_insertMachine,
                "$name", "$sourcefile", "$description", "$year", "$manufacturer",
                "$cloneOf", "$romOf", "$sampleOf",
                "$isBios", "$isDevice", "$isMechanical", "$runnable",
                "$driverStatus", "$driverEmulation", "$driverSavestate",
                "$players", "$coins",
                "$displayType", "$displayRotate", "$displayWidth", "$displayHeight", "$displayRefresh",
                "$soundChannels", "$requiresChd");
        }

        _insertMachine.Transaction = _transaction;
        return _insertMachine;
    }

    private SqliteCommand GetInsertRomCommand()
    {
        if (_insertRom is null)
        {
            _insertRom = CreateCommand();
            _insertRom.CommandText = @"
                INSERT INTO mame_machine_roms (
                    machine_name, name, size_bytes, crc, sha1, merge_name, region, status, is_optional)
                VALUES (
                    $machineName, $name, $sizeBytes, $crc, $sha1, $mergeName, $region, $status, $isOptional);
            ";
            AddParameters(_insertRom,
                "$machineName", "$name", "$sizeBytes", "$crc", "$sha1",
                "$mergeName", "$region", "$status", "$isOptional");
        }

        _insertRom.Transaction = _transaction;
        return _insertRom;
    }

    private SqliteCommand GetInsertControlCommand()
    {
        if (_insertControl is null)
        {
            _insertControl = CreateCommand();
            _insertControl.CommandText = @"
                INSERT INTO mame_machine_controls (machine_name, type, player, buttons, ways)
                VALUES ($machineName, $type, $player, $buttons, $ways);
            ";
            AddParameters(_insertControl, "$machineName", "$type", "$player", "$buttons", "$ways");
        }

        _insertControl.Transaction = _transaction;
        return _insertControl;
    }

    private static void AddParameters(SqliteCommand command, params string[] names)
    {
        foreach (string name in names)
        {
            // No explicit SqliteType: the type is inferred from the value on each execute.
            command.Parameters.Add(new SqliteParameter { ParameterName = name });
        }
    }

    private void DisposeCachedCommands()
    {
        _insertMachine?.Dispose();
        _insertMachine = null;
        _insertRom?.Dispose();
        _insertRom = null;
        _insertControl?.Dispose();
        _insertControl = null;
    }

    private SqliteCommand CreateCommand()
    {
        SqliteCommand command = _connection.CreateCommand();
        command.Transaction = _transaction;

        return command;
    }
}
