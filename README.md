# 📁 GraNAS — Peer‑to‑Peer Folder Sharing

> **Безопасный совместный доступ к папкам с разных платформ.**
> Сервер хранит **только метаданные и права** — сами файлы никогда не покидают устройства пользователей.

---

## 🎯 Ключевые возможности

- 🔐 Регистрация и вход по email/паролю, JWT + refresh-токены в httpOnly cookie
- 📂 Создание папок с неограниченной вложенностью, каскадное удаление
- 👥 Назначение прав доступа другим пользователям (`View` / `Full`)
- 🔗 Временные share-ссылки для незарегистрированных пользователей (с TTL и отзывом)
- 🌐 Веб-клиент: все операции через React-SPA, доступ к чужим папкам в отдельной вкладке
- 📡 P2P-передача файлов через WebRTC *(в разработке, Phase 6)*
- 🔍 Поиск по названию/владельцу/правам *(Phase 8)*
- 📧 Email и in-app уведомления *(Phase 7)*
- 🪟 Windows-клиент с интеграцией в Проводник *(Phase 5+)*

---

## 🧱 Архитектура

**Файлы не хранятся на сервере.** Сервер управляет только метаданными и правами; байты передаются напрямую между клиентами по WebRTC.

```
Browser / Desktop App
     │
     ▼
YARP API Gateway :8080
     ├─ /api/auth/**       → auth-service
     ├─ /api/metadata/**   → metadata-service
     ├─ /api/sharing/**    → sharing-service
     └─ /hubs/signaling/** → signaling-service (WebSocket/SignalR)

Logs pipeline:
  [each service] → RabbitMQ logs_exchange → log-service consumer → OpenSearch
```

| Сервис | Стек | Назначение |
|---|---|---|
| `api-gateway` | YARP + ASP.NET Core | Маршрутизация, CORS, Correlation-Id |
| `auth-service` | ASP.NET Core + PostgreSQL + Redis | Регистрация, логин, JWT, refresh-токены |
| `metadata-service` | ASP.NET Core + PostgreSQL | CRUD папок с иерархией, permissions |
| `sharing-service` | ASP.NET Core + PostgreSQL + RabbitMQ | Share-ссылки, токены SHA256 |
| `log-service` | ASP.NET Core + OpenSearch + RabbitMQ | Централизованный сбор логов (consumer → OpenSearch) |
| `signaling-service` | ASP.NET Core + SignalR + Redis | WebRTC signaling, TURN-креды |
| `clients/web` | React 19 + Vite + AntD 6 | Веб-клиент + P2P receiver |
| `clients/desktop` | Avalonia 11 + ReactiveUI | Windows-клиент + P2P sender |

---

## 📦 Структура репозитория

```
services/
  api-gateway/        — YARP reverse proxy
  auth-service/       — GraNAS.Auth.{API,Services,DAL,Models}
  metadata-service/   — GraNAS.Metadata.{API,Services,DAL,Models}
  sharing-service/    — GraNAS.Sharing.{API,Services,DAL,Models}
  log-service/        — GraNAS.LogService
  signaling-service/  — placeholder (Phase 6)
  notification-service/ — placeholder (Phase 7)
  search-service/     — placeholder (Phase 8)
  admin-service/      — placeholder (Phase 9)
clients/
  web/                — React 19 + Vite + AntD 6
shared/               — Correlation-Id, LoggingService, Swagger, Infrastructure
tests/                — GraNAS.WebAPI.Tests (unit + integration, Testcontainers)
infrastructure/
  docker-compose/     — compose.yaml + dev/prod overlays + env-файлы
docs/
  brief.md            — обзор проекта, user journey
  techspec.md         — техническая спецификация
  roadmap.md          — план разработки по фазам
  web-ux-roadmap.md   — UX-улучшения веб-клиента
```

---

## 🚀 Быстрый старт

### Требования

- Docker Desktop
- .NET 10 SDK (для разработки)
- Node.js 20+ (для фронтенда)

### Запуск всего стека

```bash
docker compose -f infrastructure/docker-compose/compose.yaml \
               -f infrastructure/docker-compose/compose.dev.yaml \
               --env-file infrastructure/docker-compose/dev.env up -d
```

API Gateway доступен на `http://localhost:8080`.

### Запуск веб-клиента

```bash
cd clients/web
npm install
npm run dev   # http://localhost:5173
```

### Тесты

```bash
# .NET backend (155 тестов — unit + integration через Testcontainers)
dotnet test tests/GraNAS.WebAPI.Tests/GraNAS.WebAPI.Tests.csproj

# .NET desktop (16 тестов)
dotnet test tests/GraNAS.Desktop.Tests/GraNAS.Desktop.Tests.csproj

# Frontend (10 Vitest-тестов)
cd clients/web && npm test
```

---

## 🗺️ Статус разработки

| Фаза | Описание | Статус |
|---|---|---|
| Phase 1 | Стабилизация: тесты, иерархия папок, health-checks, rate-limiting | ✅ |
| Phase 2 | Permissions: `View`/`Full` для зарегистрированных пользователей | ✅ |
| Phase 3 | Share-ссылки для анонимных пользователей | ✅ |
| Phase 4 | Веб-клиент + API Gateway + UX-полировка | ✅ |
| Phase 5 | Windows-клиент (Avalonia 11 + ReactiveUI + SIPSorcery) | ✅ |
| Phase 6 | P2P/WebRTC: signaling-service + coturn + передача файлов | ✅ |
| Phase 6.5 | Device Sessions: идентичность устройств + управление сессиями | ✅ |
| Phase 7 | Уведомления: email + in-app | 🔜 |
| Phase 8 | Поиск по метаданным | 🔜 |
| Phase 9 | Административная панель | 🔜 |
| Phase 10–12 | Windows Shell Extension, Android, Production hardening | 🔜 |

Подробный план: [`docs/roadmap.md`](./docs/roadmap.md)

---

## 🛡️ Безопасность

- JWT access-токены (15 мин) + refresh-токены в `httpOnly; SameSite=Lax` cookie (7 дней)
- Токены share-ссылок: `base64url(random 32 bytes)` → SHA-256 в БД
- Ownership-guard на каждом запросе: сервер не раскрывает существование чужих ресурсов
- CORS: только разрешённые origin на уровне gateway; upstream-сервисы не доступны из браузера

---

> ✨ **Главное правило проекта**: никакие файлы пользователей никогда не попадают на сервер. Только метаданные и логика доступа.
