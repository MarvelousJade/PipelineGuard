FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY Directory.Build.props global.json ./
COPY src/PipelineGuard.CrashIntelligence/PipelineGuard.CrashIntelligence.csproj src/PipelineGuard.CrashIntelligence/
COPY src/PipelineGuard.Crash.Api/PipelineGuard.Crash.Api.csproj src/PipelineGuard.Crash.Api/
RUN dotnet restore src/PipelineGuard.Crash.Api/PipelineGuard.Crash.Api.csproj
COPY src/PipelineGuard.CrashIntelligence/ src/PipelineGuard.CrashIntelligence/
COPY src/PipelineGuard.Crash.Api/ src/PipelineGuard.Crash.Api/
RUN dotnet publish src/PipelineGuard.Crash.Api/PipelineGuard.Crash.Api.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "PipelineGuard.Crash.Api.dll"]
