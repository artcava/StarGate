#!/bin/bash

# Reset StarGate development environment
# This script stops all services and removes volumes, effectively resetting the database

echo "⚠️  WARNING: This will delete ALL data in the development environment!"
echo ""
echo "This includes:"
echo "  - All MongoDB data (processes, policies, etc.)"
echo "  - All Redis cache data"
echo "  - All RabbitMQ messages and queues"
echo ""
read -p "Are you sure you want to continue? (type 'yes' to confirm): " -r
echo

if [[ $REPLY != "yes" ]]; then
    echo "❌ Reset cancelled."
    exit 0
fi

echo "🗑️  Stopping and removing containers, networks, and volumes..."
echo ""

docker-compose down -v

echo ""
echo "✅ Development environment reset complete!"
echo ""
echo "💡 To start fresh, run:"
echo "   ./scripts/start-dev.sh"
echo ""
