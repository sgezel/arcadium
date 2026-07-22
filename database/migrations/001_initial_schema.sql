PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS schema_version (
    version INTEGER PRIMARY KEY,
    applied_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS roms (
    id INTEGER PRIMARY KEY,
    system_id TEXT NOT NULL,
    filename TEXT NOT NULL,
    basename TEXT NOT NULL,
    path TEXT NOT NULL,
    wheel_path TEXT,
    video_path TEXT,
    marquee_path TEXT,
    physical_path TEXT,
    game_image_path TEXT,
    size_bytes INTEGER NOT NULL,
    modified_time_utc TEXT NOT NULL,
    scan_state TEXT NOT NULL DEFAULT 'active',
    first_seen_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_seen_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(system_id, path)
);

CREATE INDEX IF NOT EXISTS idx_roms_system_state ON roms(system_id, scan_state);
CREATE INDEX IF NOT EXISTS idx_roms_system_basename ON roms(system_id, basename);

CREATE TABLE IF NOT EXISTS scan_runs (
    id INTEGER PRIMARY KEY,
    system_id TEXT,
    mode TEXT NOT NULL,
    started_at TEXT NOT NULL,
    finished_at TEXT,
    files_seen INTEGER NOT NULL DEFAULT 0,
    files_added INTEGER NOT NULL DEFAULT 0,
    files_updated INTEGER NOT NULL DEFAULT 0,
    files_deleted INTEGER NOT NULL DEFAULT 0,
    warnings_count INTEGER NOT NULL DEFAULT 0,
    errors_count INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS favorites (
    rom_id INTEGER PRIMARY KEY,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (rom_id) REFERENCES roms(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS game_play_history (
    id INTEGER PRIMARY KEY,
    rom_id INTEGER NOT NULL,
    launched_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    exited_at TEXT,
    exit_code INTEGER,
    FOREIGN KEY (rom_id) REFERENCES roms(id) ON DELETE CASCADE
);
