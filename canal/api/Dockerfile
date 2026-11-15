# Imagen del canal digital: la PWA (canal/web) embebida en el binario Go (canal/api).
# Contexto de construcción: la carpeta canal/  →  docker build -f canal/api/Dockerfile canal

# 1. PWA
FROM node:24-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npm run build

# 2. Servicio Go con la PWA embebida
FROM golang:1.26-alpine AS api
WORKDIR /src
COPY api/go.mod api/go.sum* ./
RUN go mod download
COPY api/ ./
COPY --from=web /web/dist ./web/dist
RUN CGO_ENABLED=0 GOOS=linux go build -tags embebido -trimpath -ldflags="-s -w" -o /canal ./cmd/canal

# 3. Imagen final mínima, sin shell y sin root
FROM gcr.io/distroless/static-debian12:nonroot
COPY --from=api /canal /canal
USER nonroot:nonroot
EXPOSE 8080
ENTRYPOINT ["/canal"]
