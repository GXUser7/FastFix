# Сборка из корня репозитория: docker build -f src/ServiceDesk.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/ServiceDesk.Contracts/ServiceDesk.Contracts.csproj src/ServiceDesk.Contracts/
COPY src/ServiceDesk.Api/ServiceDesk.Api.csproj src/ServiceDesk.Api/
RUN dotnet restore src/ServiceDesk.Api/ServiceDesk.Api.csproj
COPY src/ServiceDesk.Contracts/ src/ServiceDesk.Contracts/
COPY src/ServiceDesk.Api/ src/ServiceDesk.Api/
RUN dotnet publish src/ServiceDesk.Api/ServiceDesk.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    TZ=Europe/Moscow \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
# QuestPDF (SkiaSharp) требует fontconfig
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --retries=10 CMD curl -fs http://localhost:8080/api/health || exit 1
ENTRYPOINT ["dotnet", "ServiceDesk.Api.dll"]
