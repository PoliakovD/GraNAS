# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY services/signaling-service/GraNAS.Signaling.API/GraNAS.Signaling.API.csproj ./services/signaling-service/GraNAS.Signaling.API/
COPY services/signaling-service/GraNAS.Signaling.Models/GraNAS.Signaling.Models.csproj ./services/signaling-service/GraNAS.Signaling.Models/
COPY services/signaling-service/GraNAS.Signaling.DAL/GraNAS.Signaling.DAL.csproj ./services/signaling-service/GraNAS.Signaling.DAL/
COPY services/signaling-service/GraNAS.Signaling.Services/GraNAS.Signaling.Services.csproj ./services/signaling-service/GraNAS.Signaling.Services/

COPY shared/GraNAS.Shared.Models/GraNAS.Shared.Models.csproj ./shared/GraNAS.Shared.Models/
COPY shared/GraNAS.Shared.Correlation/GraNAS.Shared.Correlation.csproj ./shared/GraNAS.Shared.Correlation/
COPY shared/GraNAS.Shared.Infrastructure/GraNAS.Shared.Infrastructure.csproj ./shared/GraNAS.Shared.Infrastructure/
COPY shared/GraNAS.Shared.LoggingService/GraNAS.Shared.LoggingService.csproj ./shared/GraNAS.Shared.LoggingService/
COPY shared/GraNAS.Shared.Swagger/GraNAS.Shared.Swagger.csproj ./shared/GraNAS.Shared.Swagger/

RUN dotnet restore "services/signaling-service/GraNAS.Signaling.API/GraNAS.Signaling.API.csproj"

COPY . .
WORKDIR /src/services/signaling-service/GraNAS.Signaling.API

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GraNAS.Signaling.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:DebugType=none \
    /p:DebugSymbols=false

FROM build AS bundle
RUN dotnet tool install --global dotnet-ef --version 10.0.4
RUN ConnectionStrings__SignalingDb="Host=localhost;Database=build;Username=build;Password=build" \
    /root/.dotnet/tools/dotnet-ef migrations bundle \
    --project ../GraNAS.Signaling.DAL/GraNAS.Signaling.DAL.csproj \
    --self-contained -r linux-musl-x64 \
    -o /app/efbundle && \
    chmod +x /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
ARG APP_UID=1000
RUN addgroup -g $APP_UID appgroup && adduser -D -u $APP_UID -G appgroup appuser

WORKDIR /app
EXPOSE 8080

COPY --from=publish --chown=appuser:appgroup /app/publish .
COPY --from=bundle --chown=appuser:appgroup /app/efbundle ./efbundle

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

USER appuser
ENTRYPOINT ["/bin/sh", "-c", "./efbundle && exec dotnet GraNAS.Signaling.API.dll"]
