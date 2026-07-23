# Marketplace discovery integration

C-Sweet can optionally connect to a deployed C-Sweet Marketplace for public agent
discovery. The core application remains fully usable when the marketplace is disabled
or unavailable.

## What is implemented

- The marketplace exposes `GET /api/v1/discovery/agents`.
- The endpoint returns only active, published agent listings and supports:
  - free-text search;
  - category, capability, pricing-model, and maximum-price filters;
  - relevance, rating, price, and newest sorting;
  - category and pricing-model facets;
  - cached rolling-six-month ratings and review counts;
  - repository, documentation, and marketplace listing links.
- C-Sweet exposes an authenticated organization-facing proxy at
  `GET /api/marketplace/agents`.
- The **Agent marketplace** page lets a signed-in C-Sweet user browse and filter the
  remote catalog without leaving C-Sweet.
- The Agent Host registers the marketplace as an `IWorkforceCatalogProvider`.
  The Chief of Staff's existing `workforce.search` path can therefore rank marketplace
  agents by required capabilities, budget, currency, and cached rating.
- Marketplace candidates remain suggestions requiring user approval. Remote listing
  metadata is not granted application permissions or execution authority.
- Disabled, timed-out, or unavailable marketplace calls return an explicit offline
  result. They do not prevent local staff and installed agents from being searched.

## Configuration

Marketplace connectivity is disabled by default. Configure the same public base URL for
the C-Sweet API and Agent Host:

```json
{
  "CSweet": {
    "Marketplace": {
      "Enabled": true,
      "BaseUrl": "https://marketplace.example.com/",
      "TimeoutSeconds": 10
    }
  }
}
```

When starting through Aspire, environment variables are convenient:

```powershell
$env:CSweet__Marketplace__Enabled = "true"
$env:CSweet__Marketplace__BaseUrl = "https://marketplace.example.com/"
dotnet run --project src/CSweet.AppHost/CSweet.AppHost.csproj
```

The AppHost passes these settings to both the API and containerized Agent Host.

For two local Aspire applications on Docker Desktop, use an HTTP marketplace endpoint
that is reachable from both the host and containers, commonly:

```powershell
$env:CSweet__Marketplace__BaseUrl = "http://host.docker.internal:<marketplace-port>/"
```

Use the published HTTP port shown for the marketplace `web` resource. Do not use a
container-local `localhost` address from the C-Sweet Agent Host.

## Discovery and hiring flow

1. A user browses the **Agent marketplace** page, or the Chief of Staff determines that
   current staff and installed plugins do not cover the requested capabilities.
2. C-Sweet queries the marketplace's public discovery endpoint.
3. C-Sweet validates required capabilities locally and ranks eligible candidates.
4. The candidate is persisted as an opaque hiring recommendation with its source,
   price, rating, and marketplace listing link.
5. The user reviews the listing and completes acquisition or installation.
6. Once installed, the existing installed-plugin approval and hiring workflow can
   activate the agent.

## Current boundary

Discovery, filtering, ranking, and suggestion are implemented. Purchasing or subscribing
still opens the marketplace listing, and C-Sweet does not yet synchronize marketplace
identity, Stripe entitlements, or downloaded packages automatically. A future scoped
marketplace authorization flow should add entitlement synchronization and a verified
install handoff without sharing either application's database.

