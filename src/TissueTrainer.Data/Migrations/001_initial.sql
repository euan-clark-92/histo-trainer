-- TissueTrainer MVP schema (v1.0.0)
-- Migration: 001_initial

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS schema_migrations (
    version     INTEGER PRIMARY KEY,
    name        TEXT NOT NULL,
    applied_at  TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS users (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    display_name    TEXT NOT NULL,
    employee_id     TEXT,
    created_at      TEXT NOT NULL DEFAULT (datetime('now')),
    last_active_at  TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_users_display_name ON users(display_name);

CREATE TABLE IF NOT EXISTS sessions (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id         INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    mode            TEXT NOT NULL CHECK (mode IN ('training', 'weak', 'assessment', 'manual')),
    started_at      TEXT NOT NULL DEFAULT (datetime('now')),
    completed_at    TEXT,
    question_count  INTEGER NOT NULL DEFAULT 0,
    correct_count   INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_sessions_user_started ON sessions(user_id, started_at DESC);

CREATE TABLE IF NOT EXISTS attempts (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id         INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    session_id      INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    question_id     TEXT NOT NULL,
    correct         INTEGER NOT NULL CHECK (correct IN (0, 1)),
    response_ms     INTEGER,
    answered_at     TEXT NOT NULL DEFAULT (datetime('now')),
    mode            TEXT NOT NULL CHECK (mode IN ('training', 'weak', 'assessment', 'manual'))
);

CREATE INDEX IF NOT EXISTS idx_attempts_user_answered ON attempts(user_id, answered_at DESC);
CREATE INDEX IF NOT EXISTS idx_attempts_user_question ON attempts(user_id, question_id);
CREATE INDEX IF NOT EXISTS idx_attempts_session ON attempts(session_id);

CREATE TABLE IF NOT EXISTS category_stats (
    user_id         INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    category_id     TEXT NOT NULL,
    attempts        INTEGER NOT NULL DEFAULT 0,
    correct         INTEGER NOT NULL DEFAULT 0,
    accuracy        REAL NOT NULL DEFAULT 0.0,
    last_seen_at    TEXT,
    status          TEXT NOT NULL DEFAULT 'learning' CHECK (status IN ('weak', 'learning', 'mastered')),
    PRIMARY KEY (user_id, category_id)
);

CREATE INDEX IF NOT EXISTS idx_category_stats_user_status ON category_stats(user_id, status);

CREATE TABLE IF NOT EXISTS assessments (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id         INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    session_id      INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    score           REAL NOT NULL,
    passed          INTEGER NOT NULL CHECK (passed IN (0, 1)),
    completed_at    TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_assessments_user_completed ON assessments(user_id, completed_at DESC);

CREATE TABLE IF NOT EXISTS app_settings (
    key             TEXT PRIMARY KEY,
    value           TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS content_meta (
    id              INTEGER PRIMARY KEY CHECK (id = 1),
    version         TEXT NOT NULL,
    loaded_at       TEXT NOT NULL DEFAULT (datetime('now')),
    question_count  INTEGER NOT NULL DEFAULT 0,
    path            TEXT NOT NULL
);

INSERT OR IGNORE INTO schema_migrations (version, name)
VALUES (1, '001_initial');
