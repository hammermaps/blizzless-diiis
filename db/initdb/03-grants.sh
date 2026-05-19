#!/bin/bash
# DiIiS-NA – Grant the application user access to both databases.
#
# This script runs after 01-dump.sql and 02-dump-worlds.sql have created the
# "diiis" and "worlds" databases.  It grants the value of $MARIADB_USER full
# access to both databases so the application container can connect without
# using root.
#
# Using a shell script (instead of a .sql file) ensures the grant is always
# issued for the user name defined by MARIADB_USER in docker-compose.mysql.yml,
# so changing that variable does not require editing this file.
#
# The '%' wildcard is intentional: within the Docker Compose network the
# connecting IP is a dynamic bridge-network address, so host-based restriction
# is not meaningful here.  Network-level isolation is provided by Docker
# (the 3306 port is bound to 127.0.0.1 in the Compose stack).
#
# Executed automatically by the MariaDB Docker image on first start via
# /docker-entrypoint-initdb.d/.

set -e

mariadb -u root -p"${MARIADB_ROOT_PASSWORD}" <<-EOSQL
    GRANT ALL PRIVILEGES ON diiis.*  TO '${MARIADB_USER}'@'%';
    GRANT ALL PRIVILEGES ON worlds.* TO '${MARIADB_USER}'@'%';
    FLUSH PRIVILEGES;
EOSQL
