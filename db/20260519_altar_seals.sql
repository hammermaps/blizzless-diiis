-- Migration: Altar of Rites seal unlock state (Season 28)
-- Stores which Seals and Potion Powers each game-account has unlocked per season.
-- NHibernate SchemaUpdate will create this table automatically on startup.
-- This script is provided for manual deployments or for reference.

CREATE TABLE IF NOT EXISTS altar_seal_unlocks (
    id                  BIGSERIAL PRIMARY KEY,
    dbgameaccount_id    BIGINT    NOT NULL REFERENCES game_accounts(id) ON DELETE CASCADE,
    season              INTEGER   NOT NULL DEFAULT 28,
    sealmask            BIGINT    NOT NULL DEFAULT 0,
    potionpowermask     INTEGER   NOT NULL DEFAULT 0,
    UNIQUE (dbgameaccount_id, season)
);

CREATE INDEX IF NOT EXISTS idx_altar_seal_unlocks_gameaccount
    ON altar_seal_unlocks (dbgameaccount_id);
