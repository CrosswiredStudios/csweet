# Docker Deployment

## Purpose

Docker Compose should be the default self-hosted distribution path for C-Sweet.

The goal is that a user can clone the repository, copy the example environment file, start the Compose stack, and complete first-run setup in the browser.

```bash
cp .env.example .env
docker compose up -d
```

Then open:

```text
http://localhost:8080
```

## Services

The Docker distribution includes:

| Service | Internal Port | Exposed | Description |
|---------|--------------|---------|-------------|
| `csweet-app` | 8080 | 8080 (configurable via `APP_PORT`) | Blazor WASM frontend served by nginx |
| `csweet-migrator` | - | Internal only | One-shot database migration and setup seed runner |
| `csweet-api` | 8080 | Internal only | API and application services |
| `csweet-worker` | - | Internal only | Local worker runtime |
| `postgres` | 5432 | Internal only | PostgreSQL database |

Only the web app (`csweet-app`) is exposed publicly by default. The API, worker, and database are internal-only services.

Optional services can be added when needed:

```text
minio           S3-compatible object storage for artifacts
qdrant          Semantic retrieval/vector storage
redis           Cache, backplane, or queue support
```

## LM Studio configuration

When running C-Sweet directly on your machine, LM Studio is usually reachable at:

```text
http://localhost:1234/v1
```

When running C-Sweet inside Docker and LM Studio on the host machine, use:

```text
http://host.docker.internal:1234/v1
```

For Linux Docker hosts, the Compose file may need:

```yaml
extra_hosts:
  - "host.docker.internal:host-gateway"
```

The setup wizard should prefer the Docker-safe value when it detects container mode.

## Required files (implemented)

```text
docker-compose.yml
.env.example
.dockerignore
docker/api.Dockerfile
docker/app.Dockerfile
docker/migrator.Dockerfile
docker/worker.Dockerfile
```

## Environment file

The `.env.example` file should include safe placeholders:

```env
CSWEET_PUBLIC_URL=http://localhost:8080

POSTGRES_DB=csweet
POSTGRES_USER=csweet
POSTGRES_PASSWORD=change-me

CSWEET_LLM__DEFAULT_PROVIDER_TYPE=LMStudio
CSWEET_LLM__DEFAULT_BASE_URL=http://host.docker.internal:1234/v1
CSWEET_LLM__DEFAULT_API_KEY=lm-studio
CSWEET_LLM__DEFAULT_CHAT_MODEL=
```

Do not commit a real `.env` file.

## Basic commands

```bash
# Start stack
cp .env.example .env
docker compose up -d

# Show running services
docker compose ps

# View logs
docker compose logs -f

# Rebuild images
docker compose build

# Stop services but keep data
docker compose down

# Stop services and remove local volumes/data
docker compose down -v
```

## Data persistence

Postgres must use a named volume so setup state, organizations, tasks, artifacts, and audit history survive container restarts.

Example:

```yaml
volumes:
  csweet-postgres:
```

Do not store user data only inside disposable containers.

## Health checks

Minimum health checks:

```text
csweet-api      GET /api/health
csweet-worker   process health
csweet-app      HTTP response on public port
postgres        pg_isready
```

Health checks should be used by Compose dependencies where practical so the app does not start before the database is ready.

## First-run Docker flow

```text
docker compose up -d
  → postgres starts and becomes healthy (pg_isready health check)
  → API starts and exposes /api/health
  → app starts after API is healthy (/api/health check)
  → user opens http://localhost:8080
  → setup wizard opens
  → user configures LM Studio using host.docker.internal
```

Database migrations run through the dedicated `csweet-migrator` one-shot service, not inside the API or worker containers.

## Security defaults

- Do not expose Postgres outside the Compose network by default.
- Do not bake secrets into images.
- Do not commit `.env`.
- Do not log API keys.
- Prefer named volumes for persistent data.
- Prefer internal networking between app, API, worker, and database.

## Release image publishing

When the project is ready to publish images, use GitHub Actions to build on PRs and publish on tags.

Suggested image names:

```text
ghcr.io/crosswiredstudios/csweet-app
ghcr.io/crosswiredstudios/csweet-api
ghcr.io/crosswiredstudios/csweet-migrator
ghcr.io/crosswiredstudios/csweet-worker
```

## Acceptance test

A clean-machine Docker acceptance test should verify:

```bash
git clone <repo>
cd csweet
cp .env.example .env
docker compose up -d
```

Then confirm:

- `docker compose ps` reports healthy services.
- `http://localhost:8080` opens the setup wizard.
- LM Studio can be configured using `http://host.docker.internal:1234/v1`.
- Restarting containers preserves setup state.
- `docker compose down -v` resets the local install.
