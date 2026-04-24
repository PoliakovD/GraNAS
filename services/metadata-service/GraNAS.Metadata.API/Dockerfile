# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# === Копируем ВСЕ .csproj в правильные подпапки ===
COPY services/metadata-service/GraNAS.Metadata.API/GraNAS.Metadata.API.csproj ./services/metadata-service/GraNAS.Metadata.API/
COPY services/metadata-service/GraNAS.Metadata.Models/GraNAS.Metadata.Models.csproj ./services/metadata-service/GraNAS.Metadata.Models/
COPY services/metadata-service/GraNAS.Metadata.Services/GraNAS.Metadata.Services.csproj ./services/metadata-service/GraNAS.Metadata.Services/
COPY services/metadata-service/GraNAS.Metadata.DAL/GraNAS.Metadata.DAL.csproj ./services/metadata-service/GraNAS.Metadata.DAL/

COPY shared/GraNAS.Shared.Models/GraNAS.Shared.Models.csproj ./shared/GraNAS.Shared.Models/
COPY shared/GraNAS.Shared.Correlation/GraNAS.Shared.Correlation.csproj ./shared/GraNAS.Shared.Correlation/
COPY shared/GraNAS.Shared.Infrastructure/GraNAS.Shared.Infrastructure.csproj ./shared/GraNAS.Shared.Infrastructure/
COPY shared/GraNAS.Shared.LoggingService/GraNAS.Shared.LoggingService.csproj ./shared/GraNAS.Shared.LoggingService/
COPY shared/GraNAS.Shared.Swagger/GraNAS.Shared.Swagger.csproj ./shared/GraNAS.Shared.Swagger/

# === Восстановление зависимостей ===
RUN dotnet restore "services/metadata-service/GraNAS.Metadata.API/GraNAS.Metadata.API.csproj"

# === Копируем всё остальное ===
COPY . .

# === Сборка ===
WORKDIR /src/services/metadata-service/GraNAS.Metadata.API
RUN dotnet build "GraNAS.Metadata.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

# === Публикация ===
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GraNAS.Metadata.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-build \
    /p:UseAppHost=false \
    /p:DebugType=none \
    /p:DebugSymbols=false

# === Финальный образ ===
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
ARG APP_UID=1000
RUN addgroup -g $APP_UID appgroup && adduser -D -u $APP_UID -G appgroup appuser

WORKDIR /app
EXPOSE 8080

COPY --from=publish --chown=appuser:appgroup /app/publish .

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

USER appuser
ENTRYPOINT ["dotnet", "GraNAS.Metadata.API.dll"]
