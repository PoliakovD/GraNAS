# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy all dependency .csproj files for proper restore
COPY ["services/auth-service/GraNAS.Auth.API/GraNAS.Auth.API.csproj", "services/auth-service/GraNAS.Auth.API/"]
COPY ["services/auth-service/GraNAS.Auth.Models/GraNAS.Auth.Models.csproj", "services/auth-service/GraNAS.Auth.Models/"]
COPY ["services/auth-service/GraNAS.Auth.DAL/GraNAS.Auth.DAL.csproj", "services/auth-service/GraNAS.Auth.DAL/"]
COPY ["services/auth-service/GraNAS.Auth.Services/GraNAS.Auth.Services.csproj", "services/auth-service/GraNAS.Auth.Services/"]
COPY ["shared/GraNAS.Shared.Correlation/GraNAS.Shared.Correlation.csproj", "shared/GraNAS.Shared.Correlation/"]
COPY ["shared/GraNAS.Shared.Infrastructure/GraNAS.Shared.Infrastructure.csproj", "shared/GraNAS.Shared.Infrastructure/"]
COPY ["shared/GraNAS.Shared.LoggingService/GraNAS.Shared.LoggingService.csproj", "shared/GraNAS.Shared.LoggingService/"]
COPY ["shared/GraNAS.Shared.Models/GraNAS.Shared.Models.csproj", "shared/GraNAS.Shared.Models/"]
COPY ["shared/GraNAS.Shared.Swagger/GraNAS.Shared.Swagger.csproj", "shared/GraNAS.Shared.Swagger/"]
RUN dotnet restore "services/auth-service/GraNAS.Auth.API/GraNAS.Auth.API.csproj"

# Copy remaining source and build
COPY . .
WORKDIR "/src/services/auth-service/GraNAS.Auth.API"
RUN dotnet build "GraNAS.Auth.API.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# Publish to optimized output
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GraNAS.Auth.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-build \
    /p:UseAppHost=false \
    /p:DebugType=none \
    /p:DebugSymbols=false

# Runtime stage - keep small and secure
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
ARG APP_UID=1000
RUN addgroup -g $APP_UID appgroup && adduser -D -u $APP_UID -G appgroup appuser

WORKDIR /app
EXPOSE 8080

# Copy published app from publish stage
COPY --from=publish --chown=appuser:appgroup /app/publish .

# Health check for orchestration systems
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

USER appuser
ENTRYPOINT ["dotnet", "GraNAS.Auth.API.dll"]
