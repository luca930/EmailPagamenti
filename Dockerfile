# syntax=docker/dockerfile:1

# ---- Compilazione ----
FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
WORKDIR /origine

# Prima solo i file di progetto: cosi' il ripristino dei pacchetti resta in cache
# finche' non cambiano le dipendenze, e le ricompilazioni sono molto piu' rapide.
COPY Directory.Build.props Directory.Packages.props EmailPagamenti.sln ./
COPY src/EmailPagamenti.Domain/*.csproj src/EmailPagamenti.Domain/
COPY src/EmailPagamenti.Application/*.csproj src/EmailPagamenti.Application/
COPY src/EmailPagamenti.Infrastructure/*.csproj src/EmailPagamenti.Infrastructure/
COPY src/EmailPagamenti.Worker/*.csproj src/EmailPagamenti.Worker/
COPY src/EmailPagamenti.Web/*.csproj src/EmailPagamenti.Web/
COPY tests/EmailPagamenti.Tests/*.csproj tests/EmailPagamenti.Tests/
RUN dotnet restore src/EmailPagamenti.Web/EmailPagamenti.Web.csproj

COPY . .
RUN dotnet publish src/EmailPagamenti.Web/EmailPagamenti.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /pubblicato

# ---- Esecuzione ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS finale
WORKDIR /app

# curl serve solo al controllo di salute: l'immagine di runtime non lo include.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# L'immagine porta gia' l'utente non privilegiato "app": il servizio non gira da root.
COPY --from=build --chown=app:app /pubblicato .

# La cartella dei dati e' il punto di montaggio del volume: qui vive il file SQLite.
RUN mkdir -p /app/dati && chown app:app /app/dati
VOLUME ["/app/dati"]

USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=4s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["/app/EmailPagamenti.Web"]
