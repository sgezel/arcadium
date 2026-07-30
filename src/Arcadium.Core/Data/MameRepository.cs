using Arcadium.Core.Mame;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Data;

/// <summary>
/// Data access for MAME metadata imports and lookups. The caller owns the connection; the whole
/// import runs in one transaction. Insert commands are prepared once and reused because an import
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
        using SqliteCommand command = CreateCommand();
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

        using SqliteCommand command = CreateCommand();
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

    /// <summary>Loads one machine with its rom dumps and controls, or null when unknown.</summary>
    public MameMachine? GetMachineByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        MameMachine? machine = ReadMachine(name);
        if (machine is null)
        {
            return null;
        }

        ReadMachineRoms(machine);
        ReadMachineControls(machine);

        return machine;
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

    private MameMachine? ReadMachine(string name)
    {
        using SqliteCommand command = CreateCommand();
        command.CommandText = @"
            SELECT name, sourcefile, description, year, manufacturer,
                   clone_of, rom_of, sample_of,
                   is_bios, is_device, is_mechanical, runnable,
                   driver_status, driver_emulation, driver_savestate,
                   players, coins,
                   display_type, display_rotate, display_width, display_height, display_refresh,
                   sound_channels, requires_chd
            FROM mame_machines
            WHERE name = $name;
        ";
        command.Parameters.AddWithValue("$name", name);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new MameMachine
        {
            Name = reader.GetString(0),
            SourceFile = reader.IsDBNull(1) ? null : reader.GetString(1),
            Description = reader.GetString(2),
            Year = reader.IsDBNull(3) ? null : reader.GetString(3),
            Manufacturer = reader.IsDBNull(4) ? null : reader.GetString(4),
            CloneOf = reader.IsDBNull(5) ? null : reader.GetString(5),
            RomOf = reader.IsDBNull(6) ? null : reader.GetString(6),
            SampleOf = reader.IsDBNull(7) ? null : reader.GetString(7),
            IsBios = reader.GetBoolean(8),
            IsDevice = reader.GetBoolean(9),
            IsMechanical = reader.GetBoolean(10),
            Runnable = reader.GetBoolean(11),
            DriverStatus = reader.IsDBNull(12) ? null : reader.GetString(12),
            DriverEmulation = reader.IsDBNull(13) ? null : reader.GetString(13),
            DriverSavestate = reader.IsDBNull(14) ? null : reader.GetString(14),
            Players = reader.IsDBNull(15) ? null : reader.GetInt32(15),
            Coins = reader.IsDBNull(16) ? null : reader.GetInt32(16),
            DisplayType = reader.IsDBNull(17) ? null : reader.GetString(17),
            DisplayRotate = reader.IsDBNull(18) ? null : reader.GetInt32(18),
            DisplayWidth = reader.IsDBNull(19) ? null : reader.GetInt32(19),
            DisplayHeight = reader.IsDBNull(20) ? null : reader.GetInt32(20),
            DisplayRefresh = reader.IsDBNull(21) ? null : reader.GetDouble(21),
            SoundChannels = reader.IsDBNull(22) ? null : reader.GetInt32(22),
            RequiresChd = reader.GetBoolean(23)
        };
    }

    private void ReadMachineRoms(MameMachine machine)
    {
        using SqliteCommand command = CreateCommand();
        command.CommandText = @"
            SELECT name, size_bytes, crc, sha1, merge_name, region, status, is_optional
            FROM mame_machine_roms
            WHERE machine_name = $machineName
            ORDER BY id;
        ";
        command.Parameters.AddWithValue("$machineName", machine.Name);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            machine.Roms.Add(new MameRomDump(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6),
                reader.GetBoolean(7)));
        }
    }

    private void ReadMachineControls(MameMachine machine)
    {
        using SqliteCommand command = CreateCommand();
        command.CommandText = @"
            SELECT type, player, buttons, ways
            FROM mame_machine_controls
            WHERE machine_name = $machineName
            ORDER BY id;
        ";
        command.Parameters.AddWithValue("$machineName", machine.Name);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            machine.Controls.Add(new MameControl(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
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
