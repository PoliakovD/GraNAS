# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY services/api-gateway/GraNAS.Gateway/GraNAS.Gateway.csproj ./services/api-gateway/GraNAS.Gateway/
COPY shared/GraNAS.Shared.Correlation/GraNAS.Shared.Correlation.csproj ./shared/GraNAS.Shared.Correlation/

RUN dotnet restore "services/api-gateway/GraNAS.Gateway/GraNAS.Gateway.csproj"

COPY . .
WORKDIR /src/services/api-gateway/GraNAS.Gateway

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GraNAS.Gateway.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:DebugType=none \
    /p:DebugSymbols=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
ARG APP_UID=1000
RUN addgroup -g $APP_UID appgroup && adduser -D -u $APP_UID -G appgroup appuser

WORKDIR /app
EXPOSE 8080

COPY --from=publish --chown=appuser:appgroup /app/publish .

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

USER appuser
ENTRYPOINT ["dotnet", "GraNAS.Gateway.dll"]
