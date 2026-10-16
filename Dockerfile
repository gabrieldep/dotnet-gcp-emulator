# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first for better layer caching (only the project file changes invalidate it).
COPY src/GcpStorageEmulator/GcpStorageEmulator.csproj src/GcpStorageEmulator/
RUN dotnet restore src/GcpStorageEmulator/GcpStorageEmulator.csproj

COPY src/ src/
RUN dotnet publish src/GcpStorageEmulator/GcpStorageEmulator.csproj \
    -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Defaults; override at runtime with -e Emulator__Port=... / -e Emulator__StorageRoot=...
ENV Emulator__Port=8080 \
    Emulator__StorageRoot=/data

EXPOSE 8080
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "GcpStorageEmulator.dll"]
