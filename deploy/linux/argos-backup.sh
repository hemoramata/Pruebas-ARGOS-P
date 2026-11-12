#!/usr/bin/env bash
set -euo pipefail

# Installed as root-owned /usr/local/lib/argos/argos-backup.sh.
# Secrets remain in root-readable files and are never written to logs.  Do not
# source the EnvironmentFile: a PostgreSQL connection string contains semicolons.
readonly backup_root=/var/lib/argos/backups
readonly key_file=/etc/argos/backup.key
readonly stamp="$(date -u +%Y%m%dT%H%M%SZ)"
readonly temporary="$backup_root/.argos-$stamp.dump"
readonly destination="$backup_root/argos-$stamp.argosbackup"
readonly connection="$(sed -n 's/^ConnectionStrings__DefaultConnection=//p' /etc/argos/argos.env | tail -n 1)"

install -d -m 0700 -o argos -g argos "$backup_root"
umask 077

cleanup() { rm -f "$temporary"; }
trap cleanup EXIT

if [[ ! -s "$key_file" ]]; then
  echo "La clave de respaldo protegida no está disponible." >&2
  exit 1
fi

connection_value() {
  local key="$1"
  printf '%s' "$connection" | tr ';' '\n' | sed -n "s/^[[:space:]]*$key[[:space:]]*=[[:space:]]*//Ip" | tail -n 1
}
readonly db_host="$(connection_value Host)"
readonly db_port="$(connection_value Port)"
readonly db_name="$(connection_value Database)"
readonly db_user="$(connection_value Username)"
db_password="$(connection_value Password)"
if [[ -z "$db_host" || -z "$db_name" || -z "$db_user" || -z "$db_password" ]]; then
  echo "La conexión PostgreSQL protegida no está completa." >&2
  exit 1
fi
export PGPASSWORD="$db_password"
pg_dump --format=custom --no-owner --no-privileges --file="$temporary" -h "$db_host" -p "${db_port:-5432}" -U "$db_user" -d "$db_name"
unset PGPASSWORD db_password
test -s "$temporary"

# PBKDF2 with a fresh salt makes every archive independently encrypted.
openssl enc -aes-256-cbc -pbkdf2 -iter 600000 -salt \
  -in "$temporary" -out "$destination" -pass "file:$key_file"
chown argos:argos "$destination"
chmod 0600 "$destination"

# Keep 30 daily encrypted copies. The current backup is never removed.
find "$backup_root" -maxdepth 1 -type f -name 'argos-*.argosbackup' -printf '%T@ %p\n' \
  | sort -nr | tail -n +31 | cut -d' ' -f2- | xargs -r rm -f
