# Nuxt storefront SSR image. BUILD CONTEXT = the `web/` workspace root (not web/store),
# because the store is an npm workspace that hoists deps to web/node_modules and depends on
# the local @tfoodies/design-tokens package.
#
#   docker build -f web/store/Dockerfile -t store web
#
# Produces the Nitro node-server output and runs it on port 3000 (Container Apps targetPort).

# ---------- build ----------
FROM node:24-alpine AS build
WORKDIR /app

# Manifests first for a cached install layer. Workspaces = packages/*, store, admin.
COPY package.json package-lock.json ./
COPY packages ./packages
COPY store/package.json ./store/package.json
COPY admin/package.json ./admin/package.json
RUN npm ci

# Source + build the store workspace.
COPY . .
RUN npm --workspace store run build

# ---------- runtime ----------
FROM node:24-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV NITRO_PORT=3000
ENV NITRO_HOST=0.0.0.0

# The Nitro node-server bundle is fully self-contained (deps inlined).
COPY --from=build /app/store/.output ./.output

EXPOSE 3000
CMD ["node", ".output/server/index.mjs"]
