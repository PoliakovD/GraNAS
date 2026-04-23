# Структура проекта GraNAS

GraNAS — платформа peer-to-peer обмена папками. Бэкенд построен на .NET 10 как набор микросервисов. Ключевой архитектурный принцип — **Clean Architecture** с чёткими границами между слоями и направлением зависимостей от внешних слоёв к доменному.

## Корень репозитория

```
GraNAS/
├── GraNAS.slnx                  # Rider solution file (все проекты)
├── CLAUDE.md                    # Инструкции для Claude Code (локально)
├── structure.md                 # Этот файл
├── .dockerignore
├── .gitignore
│
├── infrastructure/              # Инфраструктура развёртывания
│   ├── docker-compose/
│   │   ├── compose.yaml         # Базовый compose
│   │   ├── compose.dev.yaml     # Dev-оверлей (pgAdmin, биндинг портов)
│   │   ├── dev.env              # Переменные окружения для dev
│   │   └── prod.env             # Переменные окружения для prod
│   └── scripts/                 # .ps1/.bat скрипты запуска
│
├── services/                    # Микросервисы (см. ниже)
├── shared/                      # Разделяемые библиотеки
├── tests/                       # Юнит- и интеграционные тесты
└── docs/                        # Brief, тех. спецификация
```

## Сервисы

### auth-service (реализован)

Отвечает за регистрацию, аутентификацию, JWT-токены и refresh-токены.
БД: `authdb` (PostgreSQL) + Redis для кэша.

```
services/auth-service/
├── GraNAS.Auth.API/             # Presentation: ASP.NET Core Web API
│   ├── Controllers/
│   │   └── AuthController.cs    # Тонкий HTTP-адаптер, зависит от IAuthService
│   ├── Program.cs               # Composition Root (AddAuthDal + AddAuthApplication)
│   ├── Dockerfile
│   └── appsettings*.json
│
├── GraNAS.Auth.Services/        # Application: бизнес-логика
│   ├── Interfaces/
│   │   ├── IAuthService.cs      # + RegisterResult/LogoutResult records + Error enums
│   │   └── ITokenService.cs
│   ├── Implementations/
│   │   ├── AuthService.cs       # Register/Login/Refresh/Logout
│   │   ├── JwtTokenService.cs
│   │   └── BCryptPasswordHasher.cs
│   └── Extensions/
│       └── ServicesServiceCollectionExtensions.cs   # AddAuthApplication()
│
├── GraNAS.Auth.Models/          # Domain: сущности + DTO + интерфейсы репозиториев
│   ├── User.cs                  # POCO, без EF-атрибутов
│   ├── RefreshToken.cs          # POCO
│   ├── DTO/                     # Login/Register/Refresh/Logout Request/Response
│   └── Repositories/            # Интерфейсы принадлежат Domain (DIP)
│       ├── IUserRepository.cs
│       └── IRefreshTokenRepository.cs
│
└── GraNAS.Auth.DAL/             # Infrastructure: EF Core + PostgreSQL
    ├── AppDbContext.cs          # Использует ApplyConfigurationsFromAssembly
    ├── Configurations/          # Fluent API маппинг (IEntityTypeConfiguration<T>)
    │   ├── UserConfiguration.cs
    │   └── RefreshTokenConfiguration.cs
    ├── Repositories/Implementation/
    │   ├── UserRepository.cs
    │   └── RefreshTokenRepository.cs
    ├── Migrations/
    │   └── 20260418102543_InitialCreate.cs
    └── Extensions/
        └── DalServiceCollectionExtensions.cs        # AddAuthDal()
```

**Направление зависимостей:** `API → Services → Models ← DAL`
Models ни от чего не зависит. DAL реализует интерфейсы из Models.

### metadata-service (реализован)

CRUD метаданных папок и файлов. Сами файлы не хранятся — только метаданные и права доступа.
БД: отдельная PostgreSQL для метаданных.

```
services/metadata-service/
├── GraNAS.Metadata.API/
│   ├── Controllers/FoldersController.cs    # Зависит только от IFolderService
│   ├── Program.cs                          # AddMetadataDal + AddMetadataApplication
│   └── Dockerfile
│
├── GraNAS.Metadata.Services/
│   ├── Interfaces/IFolderService.cs        # + DeleteFolderResult / DeleteFolderError
│   ├── Implementations/FolderService.cs
│   └── Extensions/ServicesServiceCollectionExtensions.cs
│
├── GraNAS.Metadata.Models/
│   ├── Folder.cs                           # POCO
│   ├── File.cs                             # POCO
│   ├── DTO/
│   │   ├── CreateFolderRequest.cs
│   │   └── FolderResponse.cs
│   └── Repositories/
│       └── IFolderRepository.cs
│
└── GraNAS.Metadata.DAL/
    ├── MetadataDbContext.cs                # ApplyConfigurationsFromAssembly
    ├── Configurations/
    │   ├── FolderConfiguration.cs
    │   └── FileConfiguration.cs
    ├── Repositories/Implementation/FolderRepository.cs
    ├── Migrations/
    │   ├── 20260418102602_InitialCreate.cs         # Таблицы folders/files
    │   └── 20260418102622_AddUpdatedAtTriggers.cs  # Триггеры update_updated_at_column
    └── Extensions/DalServiceCollectionExtensions.cs
```

### log-service (реализован)

Централизованный сбор логов: подписывается на RabbitMQ (`logs_exchange` → `logs_queue`),
пишет в `logsdb`, предоставляет API запросов + React-дашборд.

```
services/log-service/
├── GraNAS.LogService/           # API + RabbitMQ consumer
├── frontend/                    # React 19 dashboard
└── compose.yaml
```

### Сервисы-заглушки

Пустые плейсхолдеры, реализации нет:

- `services/admin-service/`
- `services/notification-service/`
- `services/search-service/`
- `services/sharing-service/`

## Разделяемые библиотеки (shared/)

```
shared/
├── GraNAS.Shared.Correlation/       # Middleware X-Correlation-Id
├── GraNAS.Shared.Infrastructure/    # ExceptionHandlingMiddleware, PostgreSQL extensions
├── GraNAS.Shared.LoggingService/    # Публикация логов в RabbitMQ
├── GraNAS.Shared.Models/            # Только ErrorResponse (общий DTO ошибок)
└── GraNAS.Shared.Swagger/           # Swagger + JWT bearer конфиг
```

**Shared.Models сознательно минимален** — ранее был свалкой (Folder/File тянулись отовсюду),
теперь доменные сущности живут в границах своего bounded context.

## Правила зависимостей

1. **API → Services → Models** — презентационный слой не знает про DAL.
2. **DAL → Models** — инфраструктура реализует интерфейсы, объявленные в Domain.
3. **Models ни от чего не зависит** (кроме базовых `System.*` типов). Никаких ссылок на EF Core, ASP.NET, Serilog.
4. **Между сервисами прямых ссылок нет** — только через HTTP/RabbitMQ.
5. **Shared.* подключается только к тому, что действительно нужно** — например, Models в Auth не тянет EF Core из Shared.

## Composition Root

Регистрация DI идёт через extension methods каждого слоя:

```csharp
// Program.cs (Auth.API)
builder.AddPostgreSql<AppDbContext>();
builder.Services.AddAuthDal();           // регистрирует репозитории
builder.Services.AddAuthApplication();   // регистрирует AuthService, JwtTokenService, ...
```

Это защищает внешние слои от знания деталей внутренней регистрации.

## Миграции

- **Auth:** EF Core code-first, одна актуальная миграция `InitialCreate`.
- **Metadata:** EF Core code-first, `InitialCreate` + `AddUpdatedAtTriggers` (сырой SQL
  для функции `update_updated_at_column()` и триггеров `BEFORE UPDATE` на `table_folders` / `table_files`).
- **Log:** сырой SQL при старте, `/db-migrations/create_db.sql`.

## Тесты

```
tests/GraNAS.WebAPI.Tests/
```

Запуск: `dotnet test tests/GraNAS.WebAPI.Tests/GraNAS.WebAPI.Tests.csproj --configuration Release`.

## Порты (dev)

| Сервис | Порт |
|---|---|
| Auth API | 5001 |
| Metadata API | (конфигурируется) |
| Log API | 5002 |
| Log Frontend | 3000 |
| pgAdmin | 5050 |
| RabbitMQ Management | 15672 |
