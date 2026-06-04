#!/bin/bash
# deploy/scripts/setup.sh
# First-time server setup script
set -e

echo "🚀 Fantasy World Game — Setup"
echo "================================"

# ─── Check prerequisites ─────────────────────────────────────
command -v docker      >/dev/null 2>&1 || { echo "❌ Docker not installed"; exit 1; }
command -v docker compose >/dev/null 2>&1 || { echo "❌ Docker Compose not installed"; exit 1; }

# ─── Create .env if not exists ───────────────────────────────
if [ ! -f .env ]; then
    echo "📝 Creating .env file..."
    cat > .env << 'EOF'
MYSQL_ROOT_PASSWORD=fw_root_secret_change_me
MYSQL_PASSWORD=fw_pass_change_me
REDIS_PASSWORD=fw_redis_change_me
JWT_SECRET=change_this_to_a_random_64_char_string_in_production_now
JWT_REFRESH_SECRET=change_this_refresh_secret_to_64_chars_in_production
EOF
    echo "⚠️  Please edit .env with your secrets before continuing!"
    echo "   nano .env"
    exit 0
fi

# ─── Pull & build ────────────────────────────────────────────
echo "🔨 Building Docker images..."
docker compose -f deploy/docker-compose.yml build --no-cache

# ─── Start services ──────────────────────────────────────────
echo "▶️  Starting services..."
docker compose -f deploy/docker-compose.yml up -d

# ─── Wait for MySQL ──────────────────────────────────────────
echo "⏳ Waiting for MySQL..."
sleep 10
docker compose -f deploy/docker-compose.yml exec mysql \
    mysqladmin ping -h localhost --silent

# ─── Run DB migrations ───────────────────────────────────────
echo "🗄️  Running migrations..."
docker compose -f deploy/docker-compose.yml exec game_server \
    dotnet FantasyWorld.Server.dll migrate || true

echo ""
echo "✅ Setup complete!"
echo "   REST API:    http://localhost:3000/api"
echo "   Health:      http://localhost:3000/health"
echo "   J2ME TCP:    port 7777"
echo ""
echo "📋 Useful commands:"
echo "   docker compose logs -f game_server    # Server logs"
echo "   docker compose restart game_server    # Restart server"
echo "   ./deploy/scripts/backup.sh            # Backup DB"
