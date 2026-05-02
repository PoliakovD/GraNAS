# Скрипт для запуска dev-окружения в PowerShell
# Использует dev.env для конфигурации

Write-Host "Запуск dev-окружения..." -ForegroundColor Green

# Переходим в папку с compose-файлами
$ScriptDir = Split-Path $MyInvocation.MyCommand.Path
$ComposeDir = Join-Path $ScriptDir "..\docker-compose"
Set-Location $ComposeDir

Write-Host "Запуск контейнеров в режиме разработки..." -ForegroundColor Yellow

# compose.dev.yaml накладывается поверх compose.yaml: debug-логи + hot-reload volumes
docker compose --env-file dev.env -f compose.yaml -f compose.dev.yaml up --build -d

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Dev-окружение успешно запущено!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Доступные сервисы:" -ForegroundColor Cyan
    Write-Host "  Auth Service: http://localhost:8081"
    Write-Host "  Metadata Service: http://localhost:8082"
    Write-Host "  RabbitMQ: http://localhost:15672 (логин: guest, пароль: guest)"
    Write-Host "  PostgreSQL Auth: localhost:5433, БД: auth_dev"
    Write-Host "  PostgreSQL Metadata: localhost:5434, БД: metadata_dev"
    Write-Host "  Elasticsearch: http://localhost:9200"
} else {
    Write-Host "❌ Ошибка при запуске dev-окружения" -ForegroundColor Red
    exit $LASTEXITCODE
}

Pause
