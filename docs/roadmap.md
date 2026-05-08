# Roadmap разработки GraNAS

Дорожная карта составлена на базе `docs/brief.md` и `docs/techspec.md`.
Цель: **100+ пользователей в течение 3 месяцев после релиза**, мультиплатформенный
доступ к папкам без хранения файлов на сервере.

Фазы упорядочены по зависимостям, не по календарным датам — каждая следующая
опирается на результаты предыдущих.

---

## Текущее состояние (2026-05-08)

**Реализовано:**

- `api-gateway` (`services/api-gateway/`) — YARP, порт 8080, CORS, Correlation-Id (Phase 4)
- `auth-service` — регистрация, логин, JWT, refresh-токены в httpOnly cookie (Phase 1/4); `GET /api/auth/me`, `GET /api/internal/users/{id}`, `GET /api/internal/users/batch` (Phase 5 / Phase 8.3)
- `metadata-service` — CRUD папок с иерархией + permissions (Phase 2) + InternalFoldersController; `GET /api/folders/{id}/permissions` (Phase 5); `PATCH /api/folders/{id}/touch` + `last_accessed_at` + `ownerEmail` в FolderResponse (Phase 8.2/8.3)
- `sharing-service` — share-ссылки для незарегистрированных (Phase 3): токены base64url+SHA256, 5 эндпоинтов, cleanup job, RabbitMQ publish; **Phase 8:** токены хранятся в зашифрованном виде (AES-256-GCM, `token_encrypted`), `GET /api/share-links` — глобальный листинг с полным `shareUrl`, `shareUrl` в per-folder листинге
- `notification-service` — RabbitMQ consumer (`access_granted/revoked`, `share_revoked`, `access_lost`) + SMTP email (MailKit) + SignalR in-app bell + история уведомлений; web bell + desktop client (Phase 7)
- `log-service` — централизованный сбор логов: RabbitMQ consumer → OpenSearch; IndexTemplate при старте; `GET /api/logs` с фильтрацией по `service`/`level`/`correlationId`
- `clients/web/` — React 19 + Vite + TypeScript веб-клиент; **Phase 8 UI/UX:** полностью кастомный CSS design system (tokens.css + app.css), шрифт Inter Tight, кастомный Icon компонент (40+ SVG), Sidebar/Topbar/Inspector, 5 основных страниц, Toast bus; **AntD полностью удалён**; 10 Vitest тестов
- `clients/desktop/` — Avalonia 11 + ReactiveUI + Semi.Avalonia Windows-клиент (Phase 5+6): 6 экранов + P2P sender (SIPSorcery 8.0.0 + SignalR.Client), FolderShareRegistry, ECDH+AES-GCM, online-toggle, DeviceIdentity (Phase 6.5), 16 desktop-тестов
- `services/signaling-service/` — SignalR hub + TURN credentials + Redis session store (Phase 6) + device sessions в signalingdb PostgreSQL (Phase 6.5)
- **coturn** в docker compose (Phase 6)
- Shared: Correlation-Id, Swagger+JWT, ExceptionHandlingMiddleware, `GraNAS.Shared.LoggingService` — `UseGraNasCentralLogging` + `RabbitMqLogSink` + `SensitiveDataEnricher` + `MvcLoggingActionFilter`; логи идут через RabbitMQ → log-service → OpenSearch; `GraNAS.Shared.Messaging` — паблишеры событий во всех сервисах
- Docker Compose для dev и prod (gateway на порту 8080, signaling на 8085)
- CI/CD (GitHub Actions → GHCR → staging via SSH)
- **176 .NET тестов + 16 Desktop-тестов + 10 Vitest тестов — все зелёные**

**Плейсхолдеры без реализации:**
`admin-service`, `search-service`

**Не начато:** Android.

---

## Фаза 1. Стабилизация ядра (foundation)

Прежде чем строить фичи, закрыть долги текущих сервисов.

- [x] **Тесты auth-service** — юнит на AuthService (все Result-ветки) +
      интеграционные на контроллер с testcontainers PostgreSQL
- [x] **Тесты metadata-service** — юнит на FolderService (создание корня и подпапки, валидация родителя) + интеграционные на FoldersController через Testcontainers PostgreSQL; проверка ON DELETE CASCADE рекурсивно (root → child → grandchild)
- [x] **Миграция AddFolderHierarchy** — parent_folder_id UUID NULL, self-FK ON DELETE CASCADE, индекс IX_folders_parent_folder_id
- [x] **Убрать `tests.txt` с живыми JWT** из репозитория (если ещё где-то остался)
- [x] **Health-checks** (`/health`, `/health/ready`) во все API
- [x] **Rate limiting** на auth-эндпоинтах (ASP.NET Core RateLimiter)
- [x] **Secrets management** — перевести JWT_SECRET и строки подключения
      на Docker secrets / env-файл вне репо
- [x] **OpenAPI-контракты** зафиксировать как артефакты CI
- [x] **Kestrel CVE (NU1904)** — обновить транзитивную зависимость

**Критерий готовности:** зелёный CI, покрытие ≥70% на Auth/Metadata,
секреты не попадают в образ.

---

## Фаза 2. Permissions — зарегистрированные пользователи

Из брифа шаг 3 User Journey: владелец назначает `view` / `full` другому
зарегистрированному пользователю.

- [x] **Таблица `permissions`** в metadata-service (folder_id, user_id, access_level enum, **path VARCHAR NULL**)
- [x] **Доменная модель** `Permission`, `AccessLevel { View, Full }` в `Metadata.Models`
- [x] **IPermissionRepository** + реализация
- [x] **PermissionService** — Grant / Revoke / List / Check
- [x] **Authorization policy** `CanReadFolder` / `CanWriteFolder` —
      проверка владения ИЛИ наличия permission на каждом запросе
- [x] **Эндпоинты** `POST /folders/{id}/permissions` (принимает опциональный `path` для гранулярного доступа), `DELETE /folders/{id}/permissions/{userId}`
- [x] **Интеграция с auth-service** — поиск пользователя по email для выдачи прав
      (через REST, не через общую БД)
- [x] **path-hint** — `permissions.path VARCHAR NULL`: null = вся папка, иначе подпапка или файл; сервер не валидирует путь, владелец применяет scope при P2P-handshake

**Критерий готовности:** второй пользователь видит чужую папку согласно правам;
попытка записи с `view` → 403.

---

## Фаза 3. sharing-service — ссылки для незарегистрированных ✅ (2026-04-25)


- [x] **Скелет `GraNAS.Sharing.{API,Services,Models,DAL}`** по Clean Architecture
- [x] **Таблица `share_links`** (id,folder_id, token_hash, expires_at, revoked, path TEXT NULL)
- [x] path-hint — share_links.path VARCHAR NULL
  - Непрозрачная для сервера строка относительного пути
  - null = доступ ко всей папке, иначе — к конкретной подпапке или файлу
  - Сервер не валидирует существование пути; владелец применяет scope при P2P‑handshake
- [x] **Криптостойкая генерация токенов** — `RandomNumberGenerator`, кодирование `base64url`
- [x] **Хранение только SHA-256 хэшей** (в БД токен в открытом виде не лежит)
- [x] Эндпоинты:
  - **POST** `/folders/{id}/share` (только владелец)
    - Принимает опциональный path (подпапка/файл, null = вся папка)
    - Генерирует токен, сохраняет SHA-256(токен) → token_hash, возвращает полный токен владельцу один раз

  - **GET** `/share/{token}` (публичный)
    - Хэширует полученный токен, ищет по token_hash
    - Проверяет expires_at и revoked → отдаёт метаданные папки/области (или 410 Gone при отзыве / 404 при истечении)

  - **DELETE** `/share/{token}` (владелец, опционально)
    - Отзыв по токену, если владелец сохранил его локально

  - **GET** `/folders/{id}/shares` (только владелец)
    - Возвращает список всех ссылок на папку: `id, folder_id, path, expires_at, revoked, created_at`
    - Исходные токены не возвращаются — клиент идентифицирует ссылки по path и дате создания

  - **DELETE** /share-links/{id} (только владелец)
    - Отзыв ссылки по её внутреннему id — используется, когда исходный токен утерян

- [x] Фоновая очистка просроченных ссылок (`BackgroundService` + `PeriodicTimer`)
- [x] Для ссылок без локального токена разрешён только просмотр метаданных и отзыв по `DELETE /share-links/{id}`
- [x] Публикация события share_revoked в RabbitMQ для `notification‑service`

**Критерий готовности:** Получатель ссылки видит содержимое папки до истечения срока; при отзыве получает 410 Gone.
Владелец видит все свои выданные ссылки (метаданные), может отозвать любую из них по ID, а при наличии локально сохранённого токена — скопировать полный URL.

---

## Фаза 4. Минимальный Web-клиент ✅ (2026-04-25)

Сделать существующий бэкенд (auth + metadata + sharing) пригодным для
реального использования через браузер. P2P-передача файлов не входит в эту
фазу — она появится в Фазе 6 после signaling-service.

- [x] **API Gateway** — YARP (`services/api-gateway/GraNAS.Gateway`), порт 8080, CORS WithOrigins+AllowCredentials для фронтенда
- [x] Стек: **React 19 + Vite + TypeScript**, React Query (TanStack v5), React Router v7
- [x] UI Kit: **Ant Design 6** (выбрано), фронтенд в `clients/web/`
- [x] **HTTPS-only** (в prod), refresh-токен в httpOnly cookie (SameSite=Lax, Path=/api/auth)
- [x] Экраны:
  - Регистрация / Логин / Выход
  - Мои папки — дерево с иерархией, создание, удаление
  - Папка: управление правами (выдать/отозвать по email, access level)
  - Папка: управление share-ссылками (создать с expire, список, отозвать)
  - Доступные папки — чужие папки, к которым выдан доступ
  - Просмотр папки по share-ссылке (публичная страница для получателя)
- [x] Silent refresh при F5 (cookie жива → сессия восстанавливается)
- [x] MSW тесты фронтенда: 10/10 зелёных

**Открытые долги Phase 4:**
- Бэкенд не имеет `GET /api/folders/{id}/permissions` — список прав сбрасывается при F5 (данные в TanStack Query кэше). Решение: добавить эндпоинт в Phase 4.5 follow-up.
- CORS на сервисах (AllowAnyOrigin) — оставить до prod-выкатки, когда весь браузерный трафик идёт через gateway. Tightening — Phase 12.

**Критерий готовности:** все сценарии Phase 1–3 проходятся через UI браузера
без Swagger/curl; незарегистрированный пользователь открывает share-ссылку
и видит метаданные папки.

---

## Фаза 5. Минимальный Windows-клиент (Desktop) ✅ (2026-04-26)

Нативное приложение под Windows, покрывающее те же 4 сервиса, что и веб-клиент.
Цель — получить рабочий desktop-клиент до добавления P2P, чтобы потом
интегрировать signaling в оба клиента одновременно.

> **Примечание:** В roadmap был зафиксирован WPF, но после обсуждения выбран **Avalonia 11** ради кросс-платформенного потенциала. Phase 5 всё равно таргетирует только Windows (Credential Manager — Windows API).

- [x] Стек: **.NET 10 + Avalonia 11 + ReactiveUI + Semi.Avalonia** (`clients/desktop/GraNAS.Desktop.App/`)
- [x] Тот же набор экранов, что и веб-клиент Phase 4 (6 экранов: Login/Register, MyFolders с TreeView, FolderDetail с вкладками Права/Shares, SharedWithMe, PublicShare)
- [x] JWT хранить в `Windows Credential Manager` (через `Meziantou.Framework.Win32.CredentialManager`), access-token — в памяти процесса
- [x] Silent refresh on startup: читает refresh-token из Credential Manager → POST /api/auth/refresh
- [x] Inflight dedup refresh (SemaphoreSlim) + _retry flag (порт client.ts) → 401 storm безопасен
- [x] Backend-долги закрыты: `GET /api/auth/me` (email resolution после login) + `GET /api/folders/{id}/permissions` (список прав с email)
- [x] DI: Microsoft.Extensions.DependencyInjection, circular dep решена через `Func<IAuthSession>` в handlers
- [x] 13 desktop-тестов: FolderTreeBuilder, JwtTokenReader, LoginViewModel
- [x] Modal-диалоги: `CreateFolderDialog`, `GrantPermissionDialog`, `CreateShareDialog`, `ShareCreatedDialog` (одноразовый токен + Copy в буфер); `IDialogService / DialogService`
- [x] Toast-уведомления: `INotificationService` / `NotificationService` поверх Avalonia `WindowNotificationManager` (BottomRight, 4 с); все `ErrorMessage` TextBlock-и заменены на тосты
- [x] Polly retry: `Microsoft.Extensions.Http.Polly`, 2 попытки (1с/3с) на транзиентных ошибках (5xx, timeout); добавлено на все 4 HttpClient-а
- [x] `CorrelationIdDelegatingHandler` реализован локально (без зависимости от `GraNAS.Shared.Correlation` / ASP.NET)

**Критерий готовности:** пользователь может выполнять все операции (папки,
права, share-ссылки) через нативное окно без браузера; токены не хранятся
в plaintext на диске. ✅

**Остаток для Phase 10 / Phase 6:**
- WebRTC/P2P (Phase 6)
- Shell Extension / Cloud Files API (Phase 10)

---

## Фаза 6. signaling-service + P2P-транспорт (WebRTC / ICE / DTLS) ✅ (2026-04-27)

Соединяем web и desktop клиентов: ссылка выдана, папка видна — теперь можно
и файлы передать. Файлы на сервере не лежат — единственный путь идёт
**напрямую между клиентами** через WebRTC data channel. Сервер только знакомит
пиров и, при худшем NAT, ретранслирует зашифрованные пакеты через TURN.

**Стек:** signaling (SignalR) → STUN (публичные) → TURN (свой coturn) → ICE
(host/srflx/relay) → DTLS (шифрование канала) → app-level E2E (AES-GCM на файле).
C# — `SIPSorcery 8.0.0`; браузер — нативный `RTCPeerConnection` + SubtleCrypto.

- [x] Скелет `GraNAS.Signaling.{API,Services}` (без DAL — состояние в Redis)
- [x] **SignalR hub** `/hubs/signaling` с операциями: `JoinAsOwner`, `LeaveAsOwner`,
      `WatchFolder`, `RequestSession`, `SendOffer`, `SendAnswer`, `SendIceCandidate`
- [x] **Авторизация в хабе** — JWT (query string `?access_token=`) для зарегистрированных;
      share-токен передаётся параметром в `RequestSession` для анонимных
- [x] **TURN-credentials endpoint** `GET /api/turn/credentials` — HMAC-SHA1 RFC 8489,
      TTL 10 мин; синхронизирован с coturn shared secret
- [x] **coturn в compose** — command-line конфигурация, UDP 49152–49252, relay для NAT
- [x] **Передача файла** через data channel: 64 KB чанки, backpressure-aware,
      SHA-256 верификация на приёмнике
- [x] **App-level encryption** — ECDH P-256 handshake → HKDF → AES-GCM;
      каждый чанк: nonce(12)+ciphertext+tag(16) packed
- [x] **P2P-слой в web-клиенте** — нативный `RTCPeerConnection` + SubtleCrypto;
      web = только receiver; вкладка «Файлы» в FolderDetailPage + PublicSharePage
- [x] **P2P-слой в desktop-клиенте** — SIPSorcery `RTCPeerConnection`; desktop = owner/sender;
      `FolderShareRegistry` (JSON), `FileChunker`, `EcdhSession`; toggle online/offline в UI
- [x] **Owner-online индикатор** — зелёная/серая точка через `OwnerOnlineStatusChanged` event;
      `useOwnerOnlineStatus` hook + `OwnerStatusBadge` component
- [x] **Метрики:** логирование типа ICE кандидата (host/srflx/relay) в хабе
- [x] **Internal endpoints** — `GET /api/internal/folders/{id}/access?userId=` (metadata-service),
      `GET /api/internal/shares/by-token-hash/{hash}` (sharing-service)
- [x] **Тесты:** 141 backend (18 новых для signaling), 13 desktop, 10 web — все ✅

## Фаза 6.5. Device Sessions — идентичность устройств + управление сессиями ✅ (2026-04-27)

**Проблема:** владельцы трекались по ephemeral `connectionId`, нельзя показать пользователю активные сессии или различить desktop 1 vs desktop 2.

**Решение:**
- [x] `GraNAS.Signaling.DAL` — новый EF-проект, `signalingdb` PostgreSQL, таблица `table_devices`
- [x] `postgres-signaling` контейнер (порт 5437 dev), миграция через efbundle при старте
- [x] `IDeviceRepository` / `DeviceRepository` — upsert по clientgenerated UUID, unique(user_id, device_name)
- [x] `IDeviceService` / `DeviceService` — регистрация/обновление + `isOnline` из Redis
- [x] Обновлённый `ISessionStore` / `RedisSessionStore` — device↔connection mapping, folder-owners теперь Set<deviceId>
- [x] Обновлённый `SignalingHub` — новый метод `RegisterDevice(deviceId)`, hub.JoinAsOwner читает deviceId из Context.Items
- [x] `DevicesController` — `POST/GET /api/signaling/devices`
- [x] `SessionsController` — `GET /api/signaling/sessions`, `DELETE /api/signaling/sessions/{deviceId}` + `ForceDisconnect` event
- [x] Desktop: `IDeviceIdentity` / `DeviceIdentity` — генерирует/сохраняет UUID в Credential Manager; `P2PHost` вызывает REST + `RegisterDevice` перед JoinAsOwner; обрабатывает `ForceDisconnect`
- [x] **Тесты:** 155 backend (5 новых DeviceService unit + 9 новых integration), 16 desktop — все ✅

**Критерий готовности:** web↔desktop за разными NAT передают файл > 100 МБ;
SHA-256 получателя совпадает; дамп TURN relay не расшифровывается без ключа сессии;
owner offline — receiver видит индикатор и не зависает.

---

## Фаза 7. notification-service — email + in-app ✅ (2026-05-02)

- [x] Скелет `GraNAS.Notifications.{API,Services,Models,DAL}` (4 проекта)
- [x] **Таблица `notifications`** (user_id, type, data jsonb, read, created_at)
- [x] **RabbitMQ consumer** на события: `access_granted`, `access_revoked`, `share_revoked`, `access_lost`
- [x] **SMTP-адаптер** — `IEmailSender` через MailKit; провайдер — конфигом
- [x] **In-app уведомления** через SignalR: bell + история, web + desktop клиенты
- [x] **GET /api/notifications** — история, **PATCH /api/notifications/{id}/read** — прочитать
- [x] 90 .NET тестов (в т.ч. 13 новых notification-service тестов)

**Критерий готовности:** grant/revoke генерируют письмо + in-app событие; bell в web/desktop. ✅

---

## Фаза 8. UI/UX redesign + sharing improvements ✅ (2026-05-08)

Полный рефактор визуального слоя веб-клиента + улучшения sharing-service.

### 8.1 UI/UX redesign
- [x] **Кастомный CSS design system** — `tokens.css` (CSS-переменные, бренд `#6938EF`) + `app.css` (все компоненты)
- [x] **Шрифт** Inter Tight + JetBrains Mono из Google Fonts
- [x] **Кастомный `<Icon>` компонент** — 40+ именованных SVG-иконок
- [x] **Sidebar**: brand mark, search pill, nav (Home/Folders/Shared/Links/Recent), TreeNode, storage gauge
- [x] **Topbar**: breadcrumbs + NotifPopover (из useNotificationsList) + AccountMenu
- [x] **Inspector**: правая панель 340px на `/folders/:id`. Tabs: Доступ/Ссылки/Свойства
- [x] **Новые страницы**: HomePage (stat-карточки + недавние), LinksPage, RecentPage
- [x] **Переписаны**: FolderDetailPage, FoldersPage, SharedPage, LoginPage, RegisterPage, PublicSharePage
- [x] **Toast bus** (`subscribeToast / toast()`) вместо AntD notification
- [x] **AntD полностью удалён** из зависимостей (`npm uninstall antd @ant-design/icons`)
- [x] 10/10 Vitest тестов зелёные

### 8.2 sharing-service: хранение токена + глобальный листинг
- [x] **AES-256-GCM шифрование токенов**: `token_encrypted varchar(512)` в `table_share_links`, `ITokenEncryptionService`
- [x] **Миграция** `20260507000000_AddTokenEncryptedToShareLinks`
- [x] **`GET /api/share-links`** — глобальный листинг share-ссылок с `shareUrl`
- [x] **`shareUrl`** в per-folder `ShareLinkResponse` (Inspector и LinksPage — кнопка «Копировать» всегда активна)
- [x] 168 → 176 .NET тестов (12 новых integration-тестов sharing-service)

### 8.3 metadata-service: last_accessed_at + email владельца
- [x] **`last_accessed_at`** в `table_folders` + миграция `20260507100000_AddLastAccessedAtToFolders`
- [x] **`PATCH /api/metadata/folders/{id}/touch`** — обновляет `last_accessed_at`, доступен владельцу + пользователям с permission
- [x] **`ownerEmail`** в `FolderResponse` — metadata-service батч-запрашивает email-ы у auth-service
- [x] **`GET /api/internal/users/batch`** в auth-service (InternalUsersController)
- [x] Frontend: `useTouchFolder` (5-мин дебаунс), touch при открытии FolderDetailPage; RecentPage сортирует по `lastAccessedAt`; SharedPage/Inspector/FolderDetailPage показывают email владельца

**Критерий готовности:** веб-клиент без AntD, кнопка «Копировать ссылку» работает всегда, /recent сортирует по реальным открытиям. ✅

---

## Фаза 9. search-service — расширенный поиск

- [ ] **PostgreSQL full-text** (`tsvector`) — старт с PG FTS; Elasticsearch —
      запасной вариант, если PG не хватит
- [ ] Эндпоинт `GET /search?q=&owner=&access=` с пагинацией (cursor-based)
- [ ] Фильтрация по правам: только доступные пользователю папки
- [ ] Экран поиска в web и desktop клиентах

**Критерий готовности:** поиск по имени/владельцу за < 300 мс на 10k записях.

---

## Фаза 10. admin-service

- [ ] Скелет `GraNAS.Admin.{API,Services,Models}`
- [ ] Authorization policy `RequireAdmin` (по `is_admin` в JWT)
- [ ] Эндпоинты: список пользователей, блокировка, принудительный revoke прав / ссылок
- [ ] Аудит-лог административных действий

**Критерий готовности:** админ блокирует пользователя и отзывает все его
share-ссылки одним запросом.

---

## Фаза 11. Windows-клиент — Cloud Files API + Shell Extension

Расширение desktop-клиента Phase 5: интеграция в Проводник Windows.

- [ ] **Cloud Files API** (CF API) — виртуальные placeholder-файлы
- [ ] Монтирование папок GraNAS как виртуального диска в Проводнике
- [ ] Действия Проводника (copy, rename, delete) → REST API
- [ ] Hydrate placeholder → P2P-скачивание через signaling-service
- [ ] Фоновый синк метаданных без кэширования чужих данных
- [ ] Auto-update механизм

**Критерий готовности:** папки GraNAS видны в Проводнике; открытие чужого файла
триггерит P2P-загрузку с владельца.

---

## Фаза 12. Android-клиент

- [ ] Стек: **Kotlin + Jetpack Compose**, Retrofit, DataStore
- [ ] Те же экраны, что веб-клиент (адаптированные под мобильный UX)
- [ ] **P2P:** `google-webrtc`, интеграция с signaling-service
- [ ] Push-уведомления — FCM (привязка к notification-service)
- [ ] Certificate pinning для HTTPS

**Критерий готовности:** на Android доступны все сценарии брифа, push работают,
файлы качаются P2P.

---

## Фаза 13. Production hardening

- [ ] **Observability:** Grafana дашборды + Prometheus метрики через OpenTelemetry
- [ ] **Tracing:** OTEL через Jaeger (Correlation-Id уже есть, добавить span-ы)
- [ ] **Backup PostgreSQL** — pgBackRest + отдельный бакет
- [ ] **Load testing** — k6 / NBomber на 100+ конкурентных пользователей
- [ ] **Security audit:** OWASP ZAP + `dotnet list package --vulnerable` +
      пентест P2P-handshake
- [ ] **SLA/SLO:** определить и измерять p95 latency (REST и P2P setup-time)
- [ ] **Документация оператора:** runbook инцидентов, playbook деплоя

**Критерий готовности:** система проходит нагрузку 100 пользователей
с p95 < 500 мс, есть runbook для on-call.

---

## Out of scope (зафиксировано брифом — не делаем)

- Хранение файлов на сервере
- Версионирование файлов
- OAuth / сторонние провайдеры (только email+пароль)
- Real-time синхронизация

---

## Кросс-функциональные требования (держим в голове всегда)

- **Clean Architecture** по всем новым сервисам (API → Services → Models ← DAL)
- **Все межсервисные события** — через RabbitMQ, не через прямые вызовы
- **Correlation-Id** во всех request-ах между сервисами
- **Проверка прав на каждом запросе** — никакого доверия клиенту
- **Никакого кэширования чужих данных** — ни на сервере, ни на клиенте
- **HTTPS-only** в prod, HSTS включён
- **Файл никогда не проходит через прикладные сервера** — только P2P
  через WebRTC data channel; сервер сводит пиры и максимум ретранслирует
  зашифрованные пакеты через TURN
- **Двойное шифрование:** DTLS на транспорте + app-level AES-GCM на файле;
  ключ E2E обменивается по ECDH между пирами и серверу неизвестен

---

## Точки решения (требуют обсуждения до реализации)

1. **PostgreSQL FTS vs Elasticsearch** для search — зависит от объёма и бюджета.
2. **Windows Shell Extension vs Cloud Files API** — CF API современнее,
   но требует Windows 10+.
3. **Хранить permissions в metadata-service или вынести в отдельный access-service** —
   пока разумно оставить в metadata, при росте сложности выделить.
4. **API Gateway** ✅ — **Принято: YARP**, реализован в Phase 4. Единая точка входа для браузера (`services/api-gateway/`). CORS, Correlation-Id, health-check. JWT-валидация и rate-limiting остаются на сервисах.
5. **Схема миграции на permissions** — если прод уже запущен, требуется
   plan миграции существующих папок.
6. **C#-стек для WebRTC:** SIPSorcery — фактически единственный
   живой вариант на .NET; альтернативы (самописный wrapper над libwebrtc,
   Pion через gRPC-мост) значительно дороже по поддержке.
7. **TURN:** свой coturn в инфре vs managed (Twilio / Xirsys / Metered) —
   trade-off цены исходящего трафика и приватности логов.
8. **App-level E2E ключи:** эфемерные (каждая сессия — новый ECDH)
   vs долгоживущие (для re-connect без повторного handshake) —
   старт с эфемерных, долгоживущие под большие файлы и докачку.
9. **Поддержка симметричных NAT в v1:** требовать TURN с первого релиза
   vs честный fallback "попросите открыть порт" — зависит от целевой аудитории
   (корпоративные сети часто за симметричным NAT).
10. **Гранулярность шаринга (принято):** path-hint на сервере (Вариант C1) — колонка
    `path VARCHAR NULL` в `share_links` и `permissions`, непрозрачная строка, сервер
    не индексирует и не валидирует существование пути. Альтернатива (путь только на
    клиенте владельца, Вариант C2) отвергнута: кросс-девайс управление шарами
    становится невозможным, state теряется при переустановке клиента, UI получателя
    некорректен пока владелец оффлайн.
