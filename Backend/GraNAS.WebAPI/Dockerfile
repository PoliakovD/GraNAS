# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore dependencies first for better layer caching
COPY ["Backend/GraNAS.WebAPI/GraNAS.WebAPI.csproj", "Backend/GraNAS.WebAPI/"]
RUN dotnet restore "Backend/GraNAS.WebAPI/GraNAS.WebAPI.csproj"

# Copy remaining source and build
COPY . .
WORKDIR "/src/Backend/GraNAS.WebAPI"
RUN dotnet build "GraNAS.WebAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# Publish to optimized output
FROM build AS publish
ARG BUILD_CONFIGURATION=Debug
RUN dotnet publish "GraNAS.WebAPI.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
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
    CMD curl -f http://localhost:8080/health || exit 1

USER appuser
ENTRYPOINT ["dotnet", "GraNAS.WebAPI.dll"]
