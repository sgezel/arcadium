PRAGMA foreign_keys = ON;

-- Metadata over de mame.xml import zelf; machine-namen wijzigen tussen MAME-releases,
-- dus de build-versie moet traceerbaar zijn.
CREATE TABLE IF NOT EXISTS mame_imports (
    id INTEGER PRIMARY KEY,
    build TEXT NOT NULL,
    machine_count INTEGER NOT NULL,
    imported_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Eén rij per <machine>; alle 0..1-kinderen (description, year, driver, input,
-- primaire display, sound) plat in de rij. name == rom-zip basename en is de
-- join-sleutel naar roms.basename.
CREATE TABLE IF NOT EXISTS mame_machines (
    name TEXT PRIMARY KEY,
    sourcefile TEXT,
    description TEXT NOT NULL,
    year TEXT,
    manufacturer TEXT,

    clone_of TEXT,
    rom_of TEXT,
    sample_of TEXT,

    is_bios INTEGER NOT NULL DEFAULT 0,
    is_device INTEGER NOT NULL DEFAULT 0,
    is_mechanical INTEGER NOT NULL DEFAULT 0,
    runnable INTEGER NOT NULL DEFAULT 1,

    driver_status TEXT,
    driver_emulation TEXT,
    driver_savestate TEXT,

    players INTEGER,
    coins INTEGER,

    display_type TEXT,
    display_rotate INTEGER,
    display_width INTEGER,
    display_height INTEGER,
    display_refresh REAL,

    sound_channels INTEGER,

    requires_chd INTEGER NOT NULL DEFAULT 0
) WITHOUT ROWID;

CREATE INDEX IF NOT EXISTS idx_mame_machines_clone_of ON mame_machines(clone_of);
CREATE INDEX IF NOT EXISTS idx_mame_machines_year ON mame_machines(year);
CREATE INDEX IF NOT EXISTS idx_mame_machines_manufacturer ON mame_machines(manufacturer);
CREATE INDEX IF NOT EXISTS idx_mame_machines_playable
    ON mame_machines(is_device, is_bios, runnable);

-- Individuele ROM-dumps per machine, voor audit/verificatie van zip-inhoud.
CREATE TABLE IF NOT EXISTS mame_machine_roms (
    id INTEGER PRIMARY KEY,
    machine_name TEXT NOT NULL REFERENCES mame_machines(name) ON DELETE CASCADE,
    name TEXT NOT NULL,
    size_bytes INTEGER NOT NULL,
    crc TEXT,
    sha1 TEXT,
    merge_name TEXT,
    region TEXT,
    status TEXT NOT NULL DEFAULT 'good',
    is_optional INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_mame_machine_roms_machine ON mame_machine_roms(machine_name);
CREATE INDEX IF NOT EXISTS idx_mame_machine_roms_crc ON mame_machine_roms(crc);

-- Besturing per speler ("alleen 4-way joystick", "trackball", ...).
-- ways is TEXT: MAME gebruikt ook waarden als "half8".
CREATE TABLE IF NOT EXISTS mame_machine_controls (
    id INTEGER PRIMARY KEY,
    machine_name TEXT NOT NULL REFERENCES mame_machines(name) ON DELETE CASCADE,
    type TEXT NOT NULL,
    player INTEGER,
    buttons INTEGER,
    ways TEXT
);

CREATE INDEX IF NOT EXISTS idx_mame_machine_controls_machine ON mame_machine_controls(machine_name);
