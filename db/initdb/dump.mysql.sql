-- DiIiS-NA – Account database schema for MariaDB 10.3+ / MySQL 5.7+
-- Generated from the original PostgreSQL dump (db/initdb/dump.sql).
--
-- Usage:
--   mysql -u root -p < db/initdb/dump.mysql.sql
-- Or using Docker:
--   docker exec -i <container> mysql -u root -p"$MYSQL_ROOT_PASSWORD" < db/initdb/dump.mysql.sql
--
-- Notes:
--   * All BOOLEAN columns are stored as TINYINT(1) (MySQL convention).
--   * Binary/BLOB columns use LONGBLOB (equivalent to PostgreSQL BYTEA).
--   * Sequences are replaced by AUTO_INCREMENT primary keys.
--   * The discordid / discordtag columns were added after the original dump
--     and are included here for completeness; NHibernate SchemaUpdate would
--     also add them automatically on first start-up if they are missing.

SET NAMES utf8mb4;
SET CHARACTER SET utf8mb4;
SET collation_connection = utf8mb4_unicode_ci;
SET FOREIGN_KEY_CHECKS = 0;

-- ---------------------------------------------------------------------------
-- Database
-- ---------------------------------------------------------------------------

CREATE DATABASE IF NOT EXISTS diiis
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE diiis;

-- ---------------------------------------------------------------------------
-- Tables
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS accounts (
  id                  BIGINT       NOT NULL AUTO_INCREMENT,
  email               VARCHAR(255),
  banned              TINYINT(1)   NOT NULL DEFAULT 0,
  salt                LONGBLOB,
  passwordverifier    LONGBLOB,
  saltedticket        VARCHAR(255),
  battletagname       VARCHAR(255),
  hashcode            INT,
  referralcode        INT,
  inviteeaccount_id   BIGINT,
  money               BIGINT       DEFAULT 0,
  userlevel           VARCHAR(255),
  lastonline          BIGINT,
  hasrename           TINYINT(1)   NOT NULL DEFAULT 0,
  renamecooldown      BIGINT       DEFAULT 0,
  discordtag          VARCHAR(255),
  discordid           BIGINT       DEFAULT 0,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS account_relations (
  id              BIGINT       NOT NULL AUTO_INCREMENT,
  listowner_id    BIGINT,
  listtarget_id   BIGINT,
  type            VARCHAR(255) NOT NULL DEFAULT 'FRIEND',
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS achievements (
  id                  BIGINT     NOT NULL AUTO_INCREMENT,
  dbgameaccount_id    BIGINT,
  achievementid       BIGINT,
  completetime        INT,
  ishardcore          TINYINT(1) NOT NULL DEFAULT 0,
  quantity            INT        DEFAULT 0,
  criteria            LONGBLOB,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS collection_editions (
  id                  BIGINT     NOT NULL AUTO_INCREMENT,
  setid               INT,
  dbaccount_id        BIGINT,
  claimed             TINYINT(1) NOT NULL DEFAULT 0,
  claimedtoon_id      BIGINT,
  claimedhardcore     TINYINT(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS craft_data (
  id                  BIGINT     NOT NULL AUTO_INCREMENT,
  dbgameaccount_id    BIGINT,
  ishardcore          TINYINT(1),
  isseasoned          TINYINT(1),
  artisan             VARCHAR(255),
  level               INT,
  learnedrecipes      LONGBLOB   NOT NULL,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS game_accounts (
  id                      BIGINT     NOT NULL AUTO_INCREMENT,
  dbaccount_id            BIGINT,
  lastonline              BIGINT,
  flags                   INT,
  banner                  LONGBLOB,
  uiprefs                 LONGBLOB,
  uisettings              LONGBLOB,
  seentutorials           LONGBLOB,
  bossprogress            LONGBLOB,
  stashicons              LONGBLOB,
  paragonlevel            INT,
  paragonlevelhardcore    INT,
  experience              BIGINT,
  experiencehardcore      BIGINT,
  lastplayedhero_id       BIGINT,
  gold                    BIGINT,
  hardcoregold            BIGINT,
  platinum                INT,
  hardplatinum            INT,
  rmtcurrency             BIGINT,
  hardrmtcurrency         BIGINT,
  bloodshards             INT,
  hardcorebloodshards     INT,
  stashsize               INT,
  hardcorestashsize       INT,
  seasonstashsize         INT,
  hardseasonstashsize     INT,
  eliteskilled            BIGINT,
  hardeliteskilled        BIGINT,
  totalkilled             BIGINT,
  hardtotalkilled         BIGINT,
  totalgold               BIGINT,
  hardtotalgold           BIGINT,
  totalbloodshards        INT,
  hardtotalbloodshards    INT,
  totalbounties           INT        NOT NULL DEFAULT 0,
  totalbountieshardcore   INT        NOT NULL DEFAULT 0,
  pvptotalkilled          BIGINT,
  hardpvptotalkilled      BIGINT,
  pvptotalwins            BIGINT,
  hardpvptotalwins        BIGINT,
  pvptotalgold            BIGINT,
  hardpvptotalgold        BIGINT,
  craftitem1              INT,
  hardcraftitem1          INT,
  craftitem2              INT,
  hardcraftitem2          INT,
  craftitem3              INT,
  hardcraftitem3          INT,
  craftitem4              INT,
  hardcraftitem4          INT,
  craftitem5              INT,
  hardcraftitem5          INT,
  bigportalkey            INT,
  hardbigportalkey        INT,
  leorikkey               INT,
  hardleorikkey           INT,
  vialofputridness        INT,
  hardvialofputridness    INT,
  idolofterror            INT,
  hardidolofterror        INT,
  heartoffright           INT,
  hardheartoffright       INT,
  horadrica1              INT,
  hardhoradrica1          INT,
  horadrica2              INT,
  hardhoradrica2          INT,
  horadrica3              INT,
  hardhoradrica3          INT,
  horadrica4              INT,
  hardhoradrica4          INT,
  horadrica5              INT,
  hardhoradrica5          INT,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS global_params (
  id      BIGINT       NOT NULL AUTO_INCREMENT,
  name    VARCHAR(255),
  value   BIGINT,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS guilds (
  id                  BIGINT       NOT NULL AUTO_INCREMENT,
  name                VARCHAR(50),
  tag                 VARCHAR(6),
  description         VARCHAR(255),
  motd                VARCHAR(255),
  category            INT,
  language            INT,
  islfm               TINYINT(1),
  isinviterequired    TINYINT(1),
  rating              INT,
  creator_id          BIGINT,
  ranks               LONGBLOB,
  disbanded           TINYINT(1),
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS guild_members (
  id                  BIGINT       NOT NULL AUTO_INCREMENT,
  dbguild_id          BIGINT,
  dbgameaccount_id    BIGINT,
  note                VARCHAR(50),
  rank                INT,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS guild_news (
  id                  BIGINT  NOT NULL AUTO_INCREMENT,
  dbguild_id          BIGINT,
  dbgameaccount_id    BIGINT,
  type                INT,
  time                BIGINT,
  data                LONGBLOB,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS hireling_data (
  id              BIGINT  NOT NULL AUTO_INCREMENT,
  dbtoon_id       BIGINT,
  class           INT,
  skill1snoid     INT     NOT NULL DEFAULT -1,
  skill2snoid     INT     NOT NULL DEFAULT -1,
  skill3snoid     INT     NOT NULL DEFAULT -1,
  skill4snoid     INT     NOT NULL DEFAULT -1,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS items (
  id                  BIGINT       NOT NULL AUTO_INCREMENT,
  dbgameaccount_id    BIGINT,
  dbtoon_id           BIGINT,
  equipmentslot       INT,
  forsale             TINYINT(1)   NOT NULL DEFAULT 0,
  hirelingid          INT          NOT NULL DEFAULT 0,
  locationx           INT,
  locationy           INT,
  ishardcore          TINYINT(1)   NOT NULL DEFAULT 0,
  unidentified        TINYINT(1)   NOT NULL DEFAULT 0,
  firstgem            INT          NOT NULL DEFAULT -1,
  secondgem           INT          NOT NULL DEFAULT -1,
  thirdgem            INT          NOT NULL DEFAULT -1,
  gbid                INT,
  version             INT          NOT NULL DEFAULT 1,
  count               INT          DEFAULT 1,
  rareitemname        LONGBLOB,
  dyetype             INT          DEFAULT 0,
  quality             INT          DEFAULT 1,
  binding             INT          DEFAULT 0,
  durability          INT          DEFAULT 0,
  rating              INT          DEFAULT 0,
  affixes             VARCHAR(255),
  attributes          VARCHAR(2500),
  transmoggbid        INT          NOT NULL DEFAULT -1,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS mail (
  id          BIGINT       NOT NULL AUTO_INCREMENT,
  dbtoon_id   BIGINT,
  claimed     TINYINT(1),
  title       VARCHAR(255),
  body        VARCHAR(255),
  itemgbid    INT,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS quests (
  id              BIGINT     NOT NULL AUTO_INCREMENT,
  dbtoon_id       BIGINT,
  questid         INT,
  iscompleted     TINYINT(1) NOT NULL DEFAULT 0,
  queststep       INT,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS reports (
  id                  BIGINT       NOT NULL AUTO_INCREMENT,
  type                VARCHAR(255),
  dbgameaccount_id    BIGINT,
  dbtoon_id           BIGINT,
  sender_id           BIGINT,
  note                VARCHAR(255),
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS skills (
  id          BIGINT  NOT NULL AUTO_INCREMENT,
  dbtoon_id   BIGINT,
  rune0       INT,
  skill0      INT,
  rune1       INT,
  skill1      INT,
  rune2       INT,
  skill2      INT,
  rune3       INT,
  skill3      INT,
  rune4       INT,
  skill4      INT,
  rune5       INT,
  skill5      INT,
  passive0    INT,
  passive1    INT,
  passive2    INT,
  passive3    INT     NOT NULL DEFAULT -1,
  potiongbid  INT     NOT NULL DEFAULT -1,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS toons (
  id                  BIGINT       NOT NULL AUTO_INCREMENT,
  class               VARCHAR(255),
  dbgameaccount_id    BIGINT,
  deleted             TINYINT(1),
  ishardcore          TINYINT(1),
  isseasoned          TINYINT(1),
  dead                TINYINT(1),
  timedeadharcode     INT,
  stoneofportal       TINYINT(1),
  createdseason       INT,
  experience          INT,
  paragonbonuses      LONGBLOB,
  pverating           INT          NOT NULL DEFAULT 0,
  chestsopened        INT          NOT NULL DEFAULT 0,
  eventscompleted     INT          NOT NULL DEFAULT 0,
  kills               INT          NOT NULL DEFAULT 0,
  deaths              INT          NOT NULL DEFAULT 0,
  eliteskilled        INT          NOT NULL DEFAULT 0,
  goldgained          INT          NOT NULL DEFAULT 0,
  activehireling      INT,
  currentact          INT,
  currentquestid      INT,
  currentqueststepid  INT,
  currentdifficulty   INT,
  flags               VARCHAR(255),
  level               SMALLINT,
  stats               VARCHAR(255) NOT NULL DEFAULT '0;0;0;0;0;0',
  name                VARCHAR(255),
  timeplayed          INT,
  lore                LONGBLOB,
  archieved           TINYINT(1)   NOT NULL DEFAULT 0,
  wingsactive         INT          NOT NULL DEFAULT -1,
  cosmetic1           INT,
  cosmetic2           INT,
  cosmetic3           INT,
  cosmetic4           INT,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ---------------------------------------------------------------------------
-- Foreign key constraints
-- ---------------------------------------------------------------------------

ALTER TABLE accounts
  ADD CONSTRAINT fk_accounts_invitee
    FOREIGN KEY (inviteeaccount_id) REFERENCES accounts (id);

ALTER TABLE account_relations
  ADD CONSTRAINT fk_account_relations_owner
    FOREIGN KEY (listowner_id) REFERENCES accounts (id),
  ADD CONSTRAINT fk_account_relations_target
    FOREIGN KEY (listtarget_id) REFERENCES accounts (id);

ALTER TABLE achievements
  ADD CONSTRAINT fk_achievements_gameaccount
    FOREIGN KEY (dbgameaccount_id) REFERENCES game_accounts (id);

ALTER TABLE collection_editions
  ADD CONSTRAINT fk_collection_editions_account
    FOREIGN KEY (dbaccount_id) REFERENCES accounts (id),
  ADD CONSTRAINT fk_collection_editions_toon
    FOREIGN KEY (claimedtoon_id) REFERENCES toons (id);

ALTER TABLE craft_data
  ADD CONSTRAINT fk_craft_data_gameaccount
    FOREIGN KEY (dbgameaccount_id) REFERENCES game_accounts (id);

ALTER TABLE game_accounts
  ADD CONSTRAINT fk_game_accounts_account
    FOREIGN KEY (dbaccount_id) REFERENCES accounts (id),
  ADD CONSTRAINT fk_game_accounts_lasthero
    FOREIGN KEY (lastplayedhero_id) REFERENCES toons (id);

ALTER TABLE guild_members
  ADD CONSTRAINT fk_guild_members_guild
    FOREIGN KEY (dbguild_id) REFERENCES guilds (id),
  ADD CONSTRAINT fk_guild_members_gameaccount
    FOREIGN KEY (dbgameaccount_id) REFERENCES game_accounts (id);

ALTER TABLE guild_news
  ADD CONSTRAINT fk_guild_news_guild
    FOREIGN KEY (dbguild_id) REFERENCES guilds (id),
  ADD CONSTRAINT fk_guild_news_gameaccount
    FOREIGN KEY (dbgameaccount_id) REFERENCES game_accounts (id);

ALTER TABLE guilds
  ADD CONSTRAINT fk_guilds_creator
    FOREIGN KEY (creator_id) REFERENCES game_accounts (id);

ALTER TABLE hireling_data
  ADD CONSTRAINT fk_hireling_data_toon
    FOREIGN KEY (dbtoon_id) REFERENCES toons (id);

ALTER TABLE items
  ADD CONSTRAINT fk_items_gameaccount
    FOREIGN KEY (dbgameaccount_id) REFERENCES game_accounts (id),
  ADD CONSTRAINT fk_items_toon
    FOREIGN KEY (dbtoon_id) REFERENCES toons (id);

ALTER TABLE mail
  ADD CONSTRAINT fk_mail_toon
    FOREIGN KEY (dbtoon_id) REFERENCES toons (id);

ALTER TABLE quests
  ADD CONSTRAINT fk_quests_toon
    FOREIGN KEY (dbtoon_id) REFERENCES toons (id);

ALTER TABLE reports
  ADD CONSTRAINT fk_reports_gameaccount
    FOREIGN KEY (dbgameaccount_id) REFERENCES game_accounts (id),
  ADD CONSTRAINT fk_reports_toon
    FOREIGN KEY (dbtoon_id) REFERENCES toons (id),
  ADD CONSTRAINT fk_reports_sender
    FOREIGN KEY (sender_id) REFERENCES game_accounts (id);

ALTER TABLE skills
  ADD CONSTRAINT fk_skills_toon
    FOREIGN KEY (dbtoon_id) REFERENCES toons (id);

ALTER TABLE toons
  ADD CONSTRAINT fk_toons_gameaccount
    FOREIGN KEY (dbgameaccount_id) REFERENCES game_accounts (id);

SET FOREIGN_KEY_CHECKS = 1;
