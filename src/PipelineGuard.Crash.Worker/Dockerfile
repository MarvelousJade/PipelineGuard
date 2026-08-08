FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY Directory.Build.props global.json ./
COPY src/PipelineGuard.CrashIntelligence/PipelineGuard.CrashIntelligence.csproj src/PipelineGuard.CrashIntelligence/
COPY src/PipelineGuard.Crash.Worker/PipelineGuard.Crash.Worker.csproj src/PipelineGuard.Crash.Worker/
RUN dotnet restore src/PipelineGuard.Crash.Worker/PipelineGuard.Crash.Worker.csproj
COPY src/PipelineGuard.CrashIntelligence/ src/PipelineGuard.CrashIntelligence/
COPY src/PipelineGuard.Crash.Worker/ src/PipelineGuard.Crash.Worker/
RUN dotnet publish src/PipelineGuard.Crash.Worker/PipelineGuard.Crash.Worker.csproj \
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
ENTRYPOINT ["dotnet", "PipelineGuard.Crash.Worker.dll"]
