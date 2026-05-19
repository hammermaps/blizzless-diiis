-- DiIiS-NA – Grant the application user access to both databases.
--
-- This script runs after 01-dump.sql and 02-dump-worlds.sql have created the
-- "diiis" and "worlds" databases.  It gives the MARIADB_USER ("diiis") full
-- access so the application container can connect without using root.
--
-- Executed automatically by the MariaDB Docker image on first start via
-- /docker-entrypoint-initdb.d/.

GRANT ALL PRIVILEGES ON diiis.*  TO 'diiis'@'%';
GRANT ALL PRIVILEGES ON worlds.* TO 'diiis'@'%';
FLUSH PRIVILEGES;
