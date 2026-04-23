# Скрипт для запуска production-окружения в PowerShell
# Использует prod.env для конфигурации

Write-Host "Запуск production-окружения..." -ForegroundColor Green

# Проверяем наличие docker-compose
if (!(Get-Command docker-compose -ErrorAction SilentlyContinue)) {
    Write-Host "Ошибка: docker-compose не найден в системе" -ForegroundColor Red
    Write-Host "Установите Docker Desktop или Docker Compose" -ForegroundColor Red
    Pause
    exit 1
}

# Переходим в папку с docker-compose.yml
$ScriptDir = Split-Path $MyInvocation.MyCommand.Path
$ComposeDir = Join-Path $ScriptDir "..\docker-compose"
Set-Location $ComposeDir

Write-Host "Запуск контейнеров в production режиме..." -ForegroundColor Yellow

# Запускаем docker-compose с переменными из prod.env
docker-compose --env-file prod.env up --build -d

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Production-окружение успешно запущено!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Доступные сервисы:" -ForegroundColor Cyan
    Write-Host "  Auth Service: http://localhost:80"
    Write-Host "  Metadata Service: http://localhost:80"
    Write-Host "  RabbitMQ: http://localhost:15672"
    Write-Host "  PostgreSQL Auth: localhost:5432, БД: auth_prod"
    Write-Host "  PostgreSQL Metadata: localhost:5432, БД: metadata_prod"
    Write-Host "  Elasticsearch: http://localhost:9200"
} else {
    Write-Host "❌ Ошибка при запуске production-окружения" -ForegroundColor Red
    exit $LASTEXITCODE
}

Pause
