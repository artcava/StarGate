# Local Development Setup

This guide explains how to set up and run StarGate in your local development environment using Docker Compose.

## Prerequisites

Before you begin, ensure you have the following installed:

- **Docker** (version 20.10 or higher)
- **Docker Compose** (version 2.0 or higher)
- **.NET 8 SDK** (for running tests and local debugging outside containers)
- **Git** (for version control)

Verify installations:

```bash
docker --version
docker-compose --version
dotnet --version
```

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/artcava/StarGate.git
cd StarGate
```

### 2. Start Infrastructure Services

The easiest way to start the development environment is using the provided script:

```bash
./scripts/start-dev.sh
```

This script will:
- Start MongoDB, Redis, and RabbitMQ containers
- Wait for all services to be healthy
- Display connection information

Alternatively, you can start services manually:

```bash
docker-compose up -d mongodb redis rabbitmq
```

### 3. Verify Services Are Running

```bash
docker-compose ps
```

All services should show as "healthy" or "running".

### 4. (Optional) Start Application Services

To run the API and background server in containers:

```bash
docker-compose up -d stargate-api stargate-server
```

Or run them locally with .NET CLI for easier debugging:

```bash
# Terminal 1 - API
cd src/StarGate.Api
dotnet run

# Terminal 2 - Background Server
cd src/StarGate.Server
dotnet run
```

## Service Details

### MongoDB

**Connection Information:**
- **Host:** `localhost:27017`
- **Username:** `stargate_admin`
- **Password:** `stargate_password_dev`
- **Database:** `stargate`
- **Connection String:** `mongodb://stargate_admin:stargate_password_dev@localhost:27017/stargate?authSource=admin`

**Management:**
```bash
# Connect with mongosh
docker exec -it stargate-mongodb mongosh -u stargate_admin -p stargate_password_dev --authenticationDatabase admin

# View databases
show dbs

# Use stargate database
use stargate

# Show collections
show collections

# Query processes
db.processes.find().pretty()
```

**Collections:**
- `processes` - Process execution data
- `process_type_policies` - Global policies per process type
- `client_policy_overrides` - Client-specific policy overrides

### Redis

**Connection Information:**
- **Host:** `localhost:6379`
- **Password:** `stargate_redis_password_dev`
- **Connection String:** `localhost:6379,password=stargate_redis_password_dev`

**Management:**
```bash
# Connect with redis-cli
docker exec -it stargate-redis redis-cli -a stargate_redis_password_dev

# Test connection
PING

# List all keys
KEYS *

# Get a value
GET key_name

# Clear all data (use with caution!)
FLUSHALL
```

### RabbitMQ

**Connection Information:**
- **AMQP Port:** `localhost:5672`
- **Management UI:** `http://localhost:15672`
- **Username:** `stargate`
- **Password:** `stargate_rabbitmq_password_dev`
- **Virtual Host:** `stargate`
- **Connection String:** `amqp://stargate:stargate_rabbitmq_password_dev@localhost:5672/stargate`

**Management UI:**

Access the RabbitMQ Management interface at [http://localhost:15672](http://localhost:15672)

**Queues:**
- `stargate.processes` - Quorum queue for process messages (max 100,000 messages)

**CLI Management:**
```bash
# List queues
docker exec stargate-rabbitmq rabbitmqctl list_queues -p stargate

# List exchanges
docker exec stargate-rabbitmq rabbitmqctl list_exchanges -p stargate

# Purge a queue (development only!)
docker exec stargate-rabbitmq rabbitmqctl purge_queue stargate.processes -p stargate
```

## Development Workflows

### Running Tests

Run all tests:
```bash
dotnet test
```

Run specific test category:
```bash
# Unit tests only
dotnet test --filter "Category=Unit"

# Integration tests only
dotnet test --filter "Category=Integration"
```

### Viewing Logs

View logs for all services:
```bash
docker-compose logs -f
```

View logs for specific service:
```bash
docker-compose logs -f mongodb
docker-compose logs -f redis
docker-compose logs -f rabbitmq
docker-compose logs -f stargate-api
docker-compose logs -f stargate-server
```

### Rebuilding Application Containers

After code changes, rebuild and restart:
```bash
docker-compose up -d --build stargate-api stargate-server
```

### Stopping Services

Stop all services (data is preserved):
```bash
./scripts/stop-dev.sh
```

Or manually:
```bash
docker-compose down
```

### Resetting the Environment

**WARNING:** This will delete all data!

```bash
./scripts/reset-dev.sh
```

Or manually:
```bash
docker-compose down -v
```

## Troubleshooting

### Services Won't Start

**Check if ports are already in use:**
```bash
# Check MongoDB port
lsof -i :27017

# Check Redis port
lsof -i :6379

# Check RabbitMQ ports
lsof -i :5672
lsof -i :15672

# Check API ports
lsof -i :5000
lsof -i :5001
```

**Solution:** Stop conflicting services or change ports in `docker-compose.yml`.

### Containers Are Unhealthy

Check container logs:
```bash
docker-compose logs [service-name]
```

Restart the problematic service:
```bash
docker-compose restart [service-name]
```

### MongoDB Authentication Fails

Ensure you're using the correct credentials:
- Username: `stargate_admin`
- Password: `stargate_password_dev`
- Auth Database: `admin`

If issues persist, reset MongoDB:
```bash
docker-compose down mongodb
docker volume rm stargate_mongodb_data
docker-compose up -d mongodb
```

### RabbitMQ Queue Not Found

RabbitMQ definitions might not have loaded. Restart RabbitMQ:
```bash
docker-compose restart rabbitmq
```

Or recreate from scratch:
```bash
docker-compose down rabbitmq
docker volume rm stargate_rabbitmq_data
docker-compose up -d rabbitmq
```

### Out of Disk Space

Clean up Docker resources:
```bash
# Remove stopped containers
docker container prune

# Remove unused volumes
docker volume prune

# Remove unused images
docker image prune

# Remove everything (use with caution!)
docker system prune -a --volumes
```

## Advanced Configuration

### Using Custom Environment Variables

1. Copy the example environment file:
   ```bash
   cp .env.example .env
   ```

2. Edit `.env` with your custom values

3. Restart services:
   ```bash
   docker-compose down
   docker-compose up -d
   ```

### Connecting from External Tools

#### MongoDB Compass

Connection String:
```
mongodb://stargate_admin:stargate_password_dev@localhost:27017/stargate?authSource=admin
```

#### Redis Desktop Manager / RedisInsight

- Host: `localhost`
- Port: `6379`
- Password: `stargate_redis_password_dev`

#### Postman / curl

API Endpoint:
```
http://localhost:5000
```

Example request:
```bash
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{
    "clientProcessId": "order-12345",
    "processType": "order",
    "clientId": "client-001",
    "data": { "orderId": "12345" }
  }'
```

## Network Architecture

All services run on a shared Docker network called `stargate-network` with these internal hostnames:

- `mongodb` → MongoDB service
- `redis` → Redis service
- `rabbitmq` → RabbitMQ service
- `stargate-api` → API Gateway
- `stargate-server` → Background Server

Services can communicate with each other using these hostnames (e.g., `mongodb:27017`).

## Data Persistence

Data is stored in Docker volumes:

- `stargate_mongodb_data` - MongoDB database files
- `stargate_redis_data` - Redis persistence files
- `stargate_rabbitmq_data` - RabbitMQ data

These volumes persist between container restarts but are removed with `docker-compose down -v`.

## Next Steps

- Read [CODING-CONVENTIONS.md](CODING-CONVENTIONS.md) for coding standards
- Read [GIT-FLOW.md](GIT-FLOW.md) for branching strategy
- Read [PULL-REQUEST-PROCESS.md](PULL-REQUEST-PROCESS.md) for PR guidelines
- Explore the API documentation (coming soon)
- Set up your IDE for debugging (coming soon)

## Support

If you encounter issues:

1. Check the [Troubleshooting](#troubleshooting) section
2. Review Docker logs: `docker-compose logs`
3. Open an issue on GitHub

## Security Note

⚠️ **Important:** The credentials in this setup are for **local development only**.

**NEVER** use these credentials in production or commit actual `.env` files to version control!
