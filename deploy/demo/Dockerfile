# Demo image. Builds the API from source with the exact SDK pinned in global.json, so the visitor's host
# needs only Docker (no .NET SDK, Python or PowerShell). Locked restore keeps NuGet audit enabled.
FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
WORKDIR /src
COPY global.json NuGet.Config Directory.Build.props ./
COPY src/ src/
RUN dotnet restore src/ContactCenterAI.Api/ContactCenterAI.Api.csproj --locked-mode --configfile NuGet.Config
RUN dotnet publish src/ContactCenterAI.Api/ContactCenterAI.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /app
COPY --from=build /app ./
COPY deploy/demo/init-config.sh deploy/demo/start-app.sh /app/demo/
# Named volumes inherit this ownership on first use, so the non-root app user can persist data and config.
RUN mkdir -p /app/.local /config && chown app:app /app/.local /config
USER app
EXPOSE 7452
ENTRYPOINT ["/bin/sh", "/app/demo/start-app.sh"]
