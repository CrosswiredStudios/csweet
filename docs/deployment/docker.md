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

## Expected services

The first Docker distribution should include:

```text
csweet-app      Blazor UI or static web frontend
csweet-api      ASP.NET Core API
csweet-worker   Local worker runtime
postgres        Durable database
```

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

## Required files

```text
docker-compose.yml
.env.example
.dockerignore
docker/api.Dockerfile
docker/app.Dockerfile
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
csweet-worker   GET /api/worker-host/health or process health
csweet-app      HTTP response on public port
postgres        pg_isready
```

Health checks should be used by Compose dependencies where practical so the app does not start before the database is ready.

## First-run Docker flow

```text
docker compose up -d
  → postgres starts
  → API starts and applies or validates migrations
  → SystemConfiguration is seeded
  → app starts
  → user opens http://localhost:8080
  → setup wizard opens
  → user configures LM Studio using host.docker.internal
```

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
