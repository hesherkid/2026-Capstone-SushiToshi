# Deploying with Docker (Recommendation)

Sushi Toshi doesn't ship production Docker images. The only Docker setup in the repo is the **development database**: `docker-compose.yml` and `src/maria-db/dockerfile`. This guide is a recommendation for anyone who wants to run the whole app in containers. It covers how to lay it out, and the parts of this codebase that need attention in containers.

Read [Production setup](production-setup.md) first. Every setting in it still applies; this guide only covers what changes when the app runs in containers.

> The example files below are a **starting point that hasn't been tested** against this project. Build and try them on a test server before relying on them.

## Don't reuse the development database setup

`docker-compose.yml` is for development only:

- The passwords (`root` / `app_password`) are committed to git.
- It publishes MariaDB on port 3307 to the whole network.
- `src/maria-db/dockerfile` uses `mariadb:latest`, so a rebuild can jump to a new major version without warning.

Use a separate compose file for production, as below.

## Recommended layout

Four containers on one private Docker network, on one server:

```text
Internet ──443──▶ proxy (Caddy or nginx, HTTPS)
                    ├── /api/*  ──▶ backend  (ASP.NET Core, port 8080) ──▶ db (MariaDB, port 3306)
                    └── /*      ──▶ frontend (Next.js, port 3000)
```

- **Only the proxy publishes ports** (80 and 443). The frontend, backend and database are reachable only on the Docker network. This is what [Behind a reverse proxy](production-setup.md#behind-a-reverse-proxy) asks for.
- **The proxy sends `/api/*` straight to the backend.** Browsers call `/api/...` on the site's own domain, so no CORS setup or `NEXT_PUBLIC_API_URL` is needed (see [The frontend's API rewrite](#1-the-frontends-api-rewrite) for why).
- **Run one backend container.** Rate limits are kept in memory, the nightly guest cleanup runs in the backend, and uploaded files are on local disk. A second copy would split the limits and run the cleanup twice.

## What needs attention in this codebase

### 1. The frontend's API rewrite

`src/front-end/next.config.mjs` forwards `/api/*` to `http://127.0.0.1:5264`. In a container, `127.0.0.1` is the frontend container itself, so that rewrite finds nothing.

Recommended: let the proxy route `/api/*` to the backend, as in the layout above. Browser requests never reach the rewrite, and you don't need to change any code. Data is loaded in the browser, so the server-side fallback in `src/front-end/src/config/api.js` (also `127.0.0.1:5264`) doesn't matter.

The alternative is a separate API domain: set `NEXT_PUBLIC_API_URL` **at build time** and add the frontend's origin to `Cors__AllowedOrigins__0`. That needs a second certificate and CORS, so it's more to get right.

### 2. Real client addresses (rate limits and the Wi-Fi check)

The sign-in limits and the [Wi-Fi check](../admin/admin-manual.md#wi-fi-check) both use the client's IP address. In Docker, requests reach the backend from the proxy container's address on the Docker network, not from loopback. The backend only trusts loopback by default, so you must also tell it to trust the Docker network:

```bash
ForwardedHeaders__Enabled=true
ForwardedHeaders__KnownNetworks__0=172.30.0.0/24   # the subnet of the compose network below
```

Without `KnownNetworks`, the backend ignores `X-Forwarded-For` and sees every request as coming from the proxy container. Every guest then shares one rate limit, and the Wi-Fi check can't tell guests apart. The Wi-Fi check dialog **doesn't warn** about this: it only warns when requests come from loopback, and the proxy's address isn't loopback. Instead, the dialog says "You're connecting from" a Docker address such as `172.30.0.5` and offers to add it. **Don't add it:** every request comes from that address, so the check would let everyone join from anywhere. If you see a `172.30.0.x` address there, fix `KnownNetworks` first.

The proxy must **overwrite** `X-Forwarded-For` with the address it sees, not append to whatever the browser sent. By default, Caddy ignores the incoming header from clients it doesn't trust and sets its own. For nginx, see the lines in [Behind a reverse proxy](production-setup.md#behind-a-reverse-proxy). The proxy should also send `X-Forwarded-Proto` (Caddy does by default). The backend turns on HTTPS redirection in Production, and this header is how it knows the browser's request was already HTTPS.

### 3. Uploaded menu item images

When an admin uploads a menu item image, the **backend** saves it into the **frontend's** folder: `../front-end/public/menu-items/`, relative to the folder the backend runs from (see `MenuItemController.cs`). The frontend then serves it as `/menu-items/<file>`. On one machine those folders sit side by side. In containers they don't, so:

- Run the backend from `/app/back-end` inside its container, so the image folder is `/app/front-end/public/menu-items`.
- Mount one named volume at that path in the backend and at `/app/public/menu-items` in the frontend.

**Test this after deploying:** upload an image to a menu item, then open the customer menu in a private window and check that the image appears. If it doesn't, the simplest fix is a code change: have the backend serve the uploads itself under `/api/...`, so the frontend isn't involved.

### 4. Generated QR codes

QR images are saved in `storage/qrcodes/` under the backend's working folder. Mount a volume there (`/app/back-end/storage/qrcodes`) so the ZIP and PDF downloads still work after the container is recreated. Otherwise, bulk generate again after each deploy.

The WiFi QR images contain the guest WiFi password, so don't back this volume up anywhere public.

### 5. Fonts for QR code labels

QR labels ("North Side Branch — Table 4 — WiFi") are drawn with SkiaSharp, which asks for Arial. The .NET runtime images have no fonts, so install fontconfig and a font in the backend image (see the Dockerfile below). After deploying, preview a QR code and check that its label is readable.

### 6. Startup order and settings

- **The backend migrates the database when it starts, and doesn't retry.** If MariaDB isn't ready yet, the backend logs `An error occurred during database initialization` and keeps running with no migrations applied. Use a health check and `depends_on: condition: service_healthy`, as below. If it happens anyway, restart the backend container.
- **The backend won't start without** `Jwt__Key`, `SendGrid__ApiKey` and `SendGrid__FromEmail`. Put them and the other settings from [Production setup](production-setup.md) in an env file that isn't committed (`backend.env` below).
- **`NEXT_PUBLIC_*` variables are read when the frontend image is built**, not when the container starts. Pass them as build arguments and rebuild the image after changing them.
- Use `ConnectionStrings__MySqlConnection=Server=db;Port=3306;...`, where `db` is the database service's name on the Docker network.

### 7. Database backups

The data lives in the `db_data` volume. Back it up regularly, and always before deploying a version with new migrations:

```bash
docker compose -f compose.prod.yml exec db sh -c 'mariadb-dump -u root -p"$MARIADB_ROOT_PASSWORD" sushi_toshi_db' > backup-$(date +%F).sql
```

Copy the backup off the server. Test restoring it on a spare machine at least once.

## Example files

None of these are in the repo. Copy them in when you're ready to try a Docker deployment.

### Backend: `src/back-end/Dockerfile` and `src/.dockerignore`

```dockerfile
# Build context: src/
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY back-end/back-end.csproj back-end/
RUN dotnet restore back-end/back-end.csproj
COPY back-end/ back-end/
RUN dotnet publish back-end/back-end.csproj -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
# Fonts for the QR code labels (SkiaSharp)
RUN apt-get update \
 && apt-get install -y --no-install-recommends libfontconfig1 fonts-dejavu-core \
 && rm -rf /var/lib/apt/lists/*
# Run from /app/back-end so uploads land in /app/front-end/public/menu-items (see section 3)
WORKDIR /app/back-end
COPY --from=build /out .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "back-end.dll"]
```

The .NET 9 runtime image listens on port 8080 by default.

The build context is `src/`, so also add a `src/.dockerignore`. It stops `COPY back-end/` from copying any local `bin/` and `obj/` folders over the restore the image just did. These folders only exist where someone has built the backend before, not in a fresh clone:

```text
front-end
back-end.Tests
back-end.IntegrationTests
**/bin
**/obj
back-end/storage
```

This file also applies to the development database build in `docker-compose.yml`, which uses the same context. That build only needs `maria-db/`, so it still works.

### Frontend: `src/front-end/Dockerfile` and `src/front-end/.dockerignore`

```dockerfile
FROM node:22-bookworm-slim AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# NEXT_PUBLIC_* values are baked in here
ARG NEXT_PUBLIC_GOOGLE_CLIENT_ID
ENV NEXT_PUBLIC_GOOGLE_CLIENT_ID=$NEXT_PUBLIC_GOOGLE_CLIENT_ID
RUN npm run build

FROM node:22-bookworm-slim
WORKDIR /app
ENV NODE_ENV=production
COPY --from=build /app ./
EXPOSE 3000
CMD ["npm", "run", "start"]
```

```text
node_modules
.next
.env*.local
coverage
playwright-report
test-results
```

This image includes dev dependencies, so it's larger than it needs to be. To shrink it, set `output: "standalone"` in `next.config.mjs` and follow the Next.js Docker example linked from its deploying guide.

### `compose.prod.yml` (repo root)

```yaml
services:
  db:
    image: mariadb:11.4          # pin a version; upgrade on purpose
    restart: unless-stopped
    environment:
      MARIADB_DATABASE: sushi_toshi_db
      MARIADB_USER: sushi_app
      MARIADB_PASSWORD: ${DB_PASSWORD}
      MARIADB_ROOT_PASSWORD: ${DB_ROOT_PASSWORD}
    volumes:
      - db_data:/var/lib/mysql
      - ./src/maria-db/my-custom.cnf:/etc/mysql/conf.d/my-custom.cnf:ro
    healthcheck:
      test: ["CMD", "healthcheck.sh", "--connect", "--innodb_initialized"]
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s
    networks: [app]
    # No "ports": the database is only reachable from the backend

  backend:
    build:
      context: ./src
      dockerfile: back-end/Dockerfile
    restart: unless-stopped
    depends_on:
      db:
        condition: service_healthy
    env_file: backend.env          # Jwt__Key, SendGrid__*, Cors__*, URLs, Bootstrap__*, WiFi ... never commit it
    environment:
      ConnectionStrings__MySqlConnection: "Server=db;Port=3306;Database=sushi_toshi_db;User=sushi_app;Password=${DB_PASSWORD};"
      ForwardedHeaders__Enabled: "true"
      ForwardedHeaders__KnownNetworks__0: "172.30.0.0/24"
    volumes:
      - menu_images:/app/front-end/public/menu-items
      - qr_codes:/app/back-end/storage/qrcodes
    networks: [app]

  frontend:
    build:
      context: ./src/front-end
      args:
        NEXT_PUBLIC_GOOGLE_CLIENT_ID: ${GOOGLE_CLIENT_ID}
    restart: unless-stopped
    volumes:
      - menu_images:/app/public/menu-items:ro
    networks: [app]

  proxy:
    image: caddy:2
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
    depends_on: [frontend, backend]
    networks: [app]

networks:
  app:
    ipam:
      config:
        - subnet: 172.30.0.0/24    # must match ForwardedHeaders__KnownNetworks__0

volumes:
  db_data:
  menu_images:
  qr_codes:
  caddy_data:
```

Put `DB_PASSWORD`, `DB_ROOT_PASSWORD` and `GOOGLE_CLIENT_ID` in a `.env` file next to `compose.prod.yml`, and keep it out of git. Docker Compose reads it automatically.

### `Caddyfile` (repo root)

```text
sushitoshi.example.com {
    handle /api/* {
        reverse_proxy backend:8080
    }
    handle {
        reverse_proxy frontend:3000
    }
}
```

Caddy gets and renews the HTTPS certificate on its own, as long as the domain's DNS points at the server and ports 80 and 443 are open.

### Start it

```bash
docker compose -f compose.prod.yml up -d --build
docker compose -f compose.prod.yml logs -f backend   # watch migrations and the first-admin message
```

Then work through the [pre-launch checklist](production-setup.md#7-pre-launch-checklist). Also check the Docker-specific items:

- [ ] Only ports 80 and 443 are open on the server (`docker compose -f compose.prod.yml ps` shows no other published ports)
- [ ] An uploaded menu item image shows on the customer menu
- [ ] A QR code preview shows a readable label
- [ ] On the restaurant Wi-Fi, the Wi-Fi check dialog shows the restaurant's public address after "You're connecting from", not a `172.30.0.x` Docker address
- [ ] A database backup has been taken and copied off the server
