# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY services/sharing-service/GraNAS.Sharing.API/GraNAS.Sharing.API.csproj ./services/sharing-service/GraNAS.Sharing.API/
COPY services/sharing-service/GraNAS.Sharing.Models/GraNAS.Sharing.Models.csproj ./services/sharing-service/GraNAS.Sharing.Models/
COPY services/sharing-service/GraNAS.Sharing.Services/GraNAS.Sharing.Services.csproj ./services/sharing-service/GraNAS.Sharing.Services/
COPY services/sharing-service/GraNAS.Sharing.DAL/GraNAS.Sharing.DAL.csproj ./services/sharing-service/GraNAS.Sharing.DAL/

COPY shared/GraNAS.Shared.Models/GraNAS.Shared.Models.csproj ./shared/GraNAS.Shared.Models/
COPY shared/GraNAS.Shared.Correlation/GraNAS.Shared.Correlation.csproj ./shared/GraNAS.Shared.Correlation/
COPY shared/GraNAS.Shared.Infrastructure/GraNAS.Shared.Infrastructure.csproj ./shared/GraNAS.Shared.Infrastructure/
COPY shared/GraNAS.Shared.LoggingService/GraNAS.Shared.LoggingService.csproj ./shared/GraNAS.Shared.LoggingService/
COPY shared/GraNAS.Shared.Swagger/GraNAS.Shared.Swagger.csproj ./shared/GraNAS.Shared.Swagger/

RUN dotnet restore "services/sharing-service/GraNAS.Sharing.API/GraNAS.Sharing.API.csproj"

COPY . .
WORKDIR /src/services/sharing-service/GraNAS.Sharing.API

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GraNAS.Sharing.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:DebugType=none \
    /p:DebugSymbols=false

FROM build AS bundle
RUN dotnet tool install --global dotnet-ef --version 10.0.4
RUN /root/.dotnet/tools/dotnet-ef migrations bundle \
    --project ../GraNAS.Sharing.DAL/GraNAS.Sharing.DAL.csproj \
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

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

USER appuser
ENTRYPOINT ["/bin/sh", "-c", "./efbundle && exec dotnet GraNAS.Sharing.API.dll"]
