-- Migration: Altar of Rites seal unlock state (Season 28) – MySQL/MariaDB
-- Stores which Seals and Potion Powers each game-account has unlocked per season.
-- NHibernate SchemaUpdate will create this table automatically on startup.
-- This script is provided for manual deployments or for reference.

CREATE TABLE IF NOT EXISTS altar_seal_unlocks (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    dbgameaccount   BIGINT UNSIGNED NOT NULL,
    season          INT             NOT NULL DEFAULT 28,
    sealmask        BIGINT          NOT NULL DEFAULT 0,
    potionpowermask INT             NOT NULL DEFAULT 0,
    UNIQUE KEY uq_gameaccount_season (dbgameaccount, season),
    CONSTRAINT fk_altar_seal_unlocks_gameaccount
        FOREIGN KEY (dbgameaccount) REFERENCES game_accounts(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE INDEX idx_altar_seal_unlocks_gameaccount
    ON altar_seal_unlocks (dbgameaccount);
