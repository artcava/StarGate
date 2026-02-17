#!/bin/bash

# Stop StarGate development environment
# This script stops all Docker Compose services gracefully

echo "🛑 Stopping StarGate development environment..."
echo ""

docker-compose down

echo ""
echo "✅ Environment stopped successfully."
echo ""
echo "💡 To start again, run:"
echo "   ./scripts/start-dev.sh"
echo ""
echo "⚠️  To completely reset the environment (remove all data), run:"
echo "   ./scripts/reset-dev.sh"
echo ""
