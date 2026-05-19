-- DiIiS-NA – Grant the application user access to both databases.
--
-- This script runs after 01-dump.sql and 02-dump-worlds.sql have created the
-- "diiis" and "worlds" databases.  It gives the MARIADB_USER ("diiis") full
-- access so the application container can connect without using root.
--
-- The '%' wildcard is intentional: within the Docker Compose network the
-- connecting IP is a dynamic bridge-network address, so host-based restriction
-- is not meaningful here.  Network-level isolation is provided by Docker
-- (the 3306 port is only exposed to localhost by default).
--
-- Executed automatically by the MariaDB Docker image on first start via
-- /docker-entrypoint-initdb.d/.

GRANT ALL PRIVILEGES ON diiis.*  TO 'diiis'@'%';
GRANT ALL PRIVILEGES ON worlds.* TO 'diiis'@'%';
FLUSH PRIVILEGES;
