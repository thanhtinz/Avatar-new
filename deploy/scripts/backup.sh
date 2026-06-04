#!/bin/bash
# deploy/scripts/backup.sh
set -e

BACKUP_DIR="./backups/$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR"

echo "💾 Backing up MySQL..."
docker compose -f deploy/docker-compose.yml exec -T mysql \
    mysqldump -u fw_user -p"${MYSQL_PASSWORD}" fantasy_game \
    > "$BACKUP_DIR/fantasy_game.sql"

echo "✅ Backup saved to $BACKUP_DIR"

# Keep last 7 days
find ./backups -name "*.sql" -mtime +7 -delete
echo "🧹 Old backups cleaned"
