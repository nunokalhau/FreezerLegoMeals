# Local Docker Infrastructure

This directory contains local Docker infrastructure for Freezer Lego Meals:

- Redis on port `6379`
- RedisInsight on port `5540`
- ChromaDB on port `8001`

Runtime data is stored outside this directory under `../data/`. Deleting containers does not delete repository data because the services use bind mounts instead of Docker named volumes.

## Prerequisites

- Windows with PowerShell 5.1 or newer
- Docker Desktop with the Docker Compose v2 plugin
- Docker Desktop running before commands are executed

Verify Docker is available:

```powershell
docker --version
docker compose version
```

## Install Docker Desktop

1. Download Docker Desktop from <https://www.docker.com/products/docker-desktop/>.
2. Install it with the default WSL 2 backend when prompted.
3. Restart Windows if the installer requests it.
4. Start Docker Desktop and wait until it reports that Docker is running.

## Configuration

Copy the example environment file if you want to customize ports:

```powershell
Copy-Item .env.example .env
```

Default values:

```env
REDIS_PORT=6379
REDISINSIGHT_PORT=5540
CHROMADB_PORT=8001
```

## Start Infrastructure

From any directory:

```powershell
.\docker\docker.ps1 up
```

The script always runs `docker compose` from the `docker/` directory, so relative bind mounts resolve consistently.

Equivalent manual command from this directory:

```powershell
docker compose up -d
```

## Stop Infrastructure

```powershell
.\docker\docker.ps1 down
```

This stops and removes the containers, but it does not delete any data in `data/`.

## Restart Infrastructure

```powershell
.\docker\docker.ps1 restart
```

This stops the containers and starts them again with the current compose configuration.

## Inspect Redis with RedisInsight

1. Start the infrastructure.
2. Open <http://localhost:5540>.
3. Add a Redis database connection using:
   - Host: `redis`
   - Port: `6379`

Use the service name `redis` from RedisInsight because both containers share the same Docker bridge network.

## Persistent Data

Persistent data is stored in the repository `data/` directory:

- Redis: `data/redis/`
- ChromaDB collections, metadata, and indexes: `data/chromadb/`

These project data directories are bind mounts, not Docker named volumes. Bind mounts make the storage location explicit, easy to back up, and independent of container lifecycle. Removing or recreating containers does not remove Redis or ChromaDB data in `data/`.

RedisInsight is treated as a disposable local development tool. Its UI state is not persisted in the repository, so deleting or recreating the RedisInsight container can reset saved UI preferences and connections without affecting project data.

## Update Images

Pull newer images:

```powershell
.\docker\docker.ps1 pull
```

Then restart the stack:

```powershell
.\docker\docker.ps1 restart
```

## Clean Containers Without Deleting Data

```powershell
.\docker\docker.ps1 clean
```

This stops containers and removes orphaned containers created by older compose files. It does not delete `data/redis/` or `data/chromadb/`.

## Reset Infrastructure Data

Use reset only when you want to delete local Redis and ChromaDB data:

```powershell
.\docker\docker.ps1 reset
```

The script asks for confirmation before deleting:

- `data/chromadb/`
- `data/redis/`

RedisInsight state is disposable and is not stored in the repository.

## Validate Services

From the `docker/` directory:

```powershell
docker compose config
docker compose up -d
docker compose ps
```

Check service endpoints:

```powershell
docker exec freezer-redis redis-cli ping
Invoke-WebRequest http://localhost:5540 -UseBasicParsing
Invoke-WebRequest http://localhost:8001/api/v2/heartbeat -UseBasicParsing
```

## Troubleshooting

### Docker Desktop is not running

Start Docker Desktop and wait until it reports that the engine is running. Then retry the command.

### Port is already allocated

Another local process is using one of the configured ports. Either stop that process or create `docker/.env` and change the host port.

### RedisInsight cannot connect to Redis

Inside RedisInsight, use host `redis` and port `6379`. Do not use `localhost` from RedisInsight because `localhost` refers to the RedisInsight container.

### ChromaDB health check is still starting

ChromaDB can take longer than Redis to initialize on first start, especially while Docker is pulling or extracting images. Check status with:

```powershell
docker compose ps
```

View logs with:

```powershell
docker compose logs chromadb
```

### Permission errors under data

Ensure Docker Desktop has access to the repository path. On Windows, keep the repository under a Docker-accessible drive and restart Docker Desktop after changing file sharing settings.

### Need a fresh local database

Run:

```powershell
.\docker\docker.ps1 reset
```

Confirm by typing `RESET` when prompted. This deletes Redis and ChromaDB data from `data/` and recreates empty directories.
