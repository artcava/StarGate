#!/bin/bash

# Start StarGate development environment
# This script starts the infrastructure services (MongoDB, Redis, RabbitMQ)
# and displays connection information

echo "🚀 Starting StarGate development environment..."
echo ""

# Start infrastructure services
echo "Starting infrastructure services..."
docker-compose up -d mongodb redis rabbitmq

echo ""
echo "⏳ Waiting for services to be healthy..."
echo ""

# Wait for services to be healthy
max_attempts=30
attempt=0

while [ $attempt -lt $max_attempts ]; do
    healthy=$(docker-compose ps --format json | jq -r '.[] | select(.Health == "healthy") | .Name' | wc -l)
    total=$(docker-compose ps --format json | jq -r '.[] | select(.Service == "mongodb" or .Service == "redis" or .Service == "rabbitmq") | .Name' | wc -l)
    
    if [ "$healthy" -eq "$total" ] && [ "$total" -eq 3 ]; then
        echo "✅ All services are healthy!"
        break
    fi
    
    echo "Waiting... ($healthy/$total services healthy)"
    sleep 2
    attempt=$((attempt + 1))
done

if [ $attempt -eq $max_attempts ]; then
    echo "⚠️  Timeout waiting for services to be healthy. Check docker-compose logs."
    echo ""
    echo "Run: docker-compose logs"
    exit 1
fi

echo ""
echo "📊 Service Status:"
docker-compose ps

echo ""
echo "✅ Infrastructure services started successfully!"
echo ""
echo "📝 Connection Information:"
echo "  🍃 MongoDB:"
echo "     URI: mongodb://localhost:27017"
echo "     Username: stargate_admin"
echo "     Password: stargate_password_dev"
echo "     Database: stargate"
echo ""
echo "  🔴 Redis:"
echo "     Host: localhost:6379"
echo "     Password: stargate_redis_password_dev"
echo ""
echo "  🐰 RabbitMQ:"
echo "     AMQP: amqp://localhost:5672"
echo "     Management UI: http://localhost:15672"
echo "     Username: stargate"
echo "     Password: stargate_rabbitmq_password_dev"
echo ""
echo "💡 To start the application services, run:"
echo "   docker-compose up -d stargate-api stargate-server"
echo ""
echo "📋 To view logs:"
echo "   docker-compose logs -f [service-name]"
echo ""
echo "🛑 To stop all services:"
echo "   ./scripts/stop-dev.sh"
echo ""
