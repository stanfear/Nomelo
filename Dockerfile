# syntax=docker/dockerfile:1.7

# 1) React build
FROM node:20-alpine AS client-build
WORKDIR /src/client
COPY src/Nomelo.Client/package.json src/Nomelo.Client/package-lock.json* ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY src/Nomelo.Client/ ./
RUN npm run build -- --emptyOutDir --outDir /out/wwwroot

# 2) .NET build + publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS server-build
WORKDIR /src
COPY Nomelo.sln ./
COPY src/Nomelo.Server/Nomelo.Server.csproj src/Nomelo.Server/
COPY src/Nomelo.Shared/Nomelo.Shared.csproj src/Nomelo.Shared/
COPY src/Nomelo.Client/Nomelo.Client.csproj src/Nomelo.Client/
RUN dotnet restore src/Nomelo.Server/Nomelo.Server.csproj
COPY src/Nomelo.Server/ src/Nomelo.Server/
COPY src/Nomelo.Shared/ src/Nomelo.Shared/
# Pull in React build output produced by stage 1
COPY --from=client-build /out/wwwroot src/Nomelo.Server/wwwroot
RUN dotnet publish src/Nomelo.Server/Nomelo.Server.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# 3) Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=server-build /app/publish ./
COPY ops/healthcheck.sh /usr/local/bin/healthcheck.sh
RUN chmod +x /usr/local/bin/healthcheck.sh && \
    apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=false
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 \
  CMD /usr/local/bin/healthcheck.sh
ENTRYPOINT ["dotnet", "Nomelo.Server.dll"]
