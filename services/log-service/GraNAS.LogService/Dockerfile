# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Копируем все .csproj файлы, необходимые для восстановления зависимостей
COPY services/log-service/GraNAS.LogService/GraNAS.LogService.csproj ./services/log-service/GraNAS.LogService/
COPY shared/GraNAS.Shared.Models/GraNAS.Shared.Models.csproj ./shared/GraNAS.Shared.Models/

# Восстанавливаем зависимости — теперь dotnet знает о всех проектах
RUN dotnet restore "services/log-service/GraNAS.LogService/GraNAS.LogService.csproj"

# Копируем всё остальное
COPY . .

WORKDIR /src/services/log-service/GraNAS.LogService

# Собираем (без повторного restore)
RUN dotnet build "GraNAS.LogService.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# Этап публикации
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GraNAS.LogService.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-build \
    /p:UseAppHost=false \
    /p:DebugType=none \
    /p:DebugSymbols=false

# Финальный образ
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
ARG APP_UID=1000
RUN addgroup -g $APP_UID appgroup && adduser -D -u $APP_UID -G appgroup appuser

WORKDIR /app
EXPOSE 8080

COPY --from=publish --chown=appuser:appgroup /app/publish .

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

USER appuser
ENTRYPOINT ["dotnet", "GraNAS.LogService.dll"]
