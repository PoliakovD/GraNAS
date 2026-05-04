# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY services/notification-service/GraNAS.Notifications.API/GraNAS.Notifications.API.csproj ./services/notification-service/GraNAS.Notifications.API/
COPY services/notification-service/GraNAS.Notifications.Models/GraNAS.Notifications.Models.csproj ./services/notification-service/GraNAS.Notifications.Models/
COPY services/notification-service/GraNAS.Notifications.Services/GraNAS.Notifications.Services.csproj ./services/notification-service/GraNAS.Notifications.Services/
COPY services/notification-service/GraNAS.Notifications.DAL/GraNAS.Notifications.DAL.csproj ./services/notification-service/GraNAS.Notifications.DAL/

COPY shared/GraNAS.Shared.Models/GraNAS.Shared.Models.csproj ./shared/GraNAS.Shared.Models/
COPY shared/GraNAS.Shared.Correlation/GraNAS.Shared.Correlation.csproj ./shared/GraNAS.Shared.Correlation/
COPY shared/GraNAS.Shared.Infrastructure/GraNAS.Shared.Infrastructure.csproj ./shared/GraNAS.Shared.Infrastructure/
COPY shared/GraNAS.Shared.LoggingService/GraNAS.Shared.LoggingService.csproj ./shared/GraNAS.Shared.LoggingService/
COPY shared/GraNAS.Shared.Messaging/GraNAS.Shared.Messaging.csproj ./shared/GraNAS.Shared.Messaging/
COPY shared/GraNAS.Shared.Swagger/GraNAS.Shared.Swagger.csproj ./shared/GraNAS.Shared.Swagger/

RUN dotnet restore "services/notification-service/GraNAS.Notifications.API/GraNAS.Notifications.API.csproj"

COPY . .
WORKDIR /src/services/notification-service/GraNAS.Notifications.API

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GraNAS.Notifications.API.csproj" \
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
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet GraNAS.Notifications.API.dll"]
