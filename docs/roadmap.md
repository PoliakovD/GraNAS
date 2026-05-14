# Roadmap разработки GraNAS

Дорожная карта составлена на базе `docs/brief.md` и `docs/techspec.md`.
Цель: **100+ пользователей в течение 3 месяцев после релиза**, мультиплатформенный
доступ к папкам без хранения файлов на сервере.

Фазы упорядочены по зависимостям, не по календарным датам — каждая следующая
опирается на результаты предыдущих.

---

## Текущее состояние (2026-05-14)

**Реализовано:**

- `api-gateway` (`services/api-gateway/`) — YARP, порт 8080, CORS, Correlation-Id (Phase 4)
- `auth-service` — регистрация, логин, JWT, refresh-токены в httpOnly cookie (Phase 1/4); `GET /api/auth/me`, `GET /api/internal/users/{id}`, `GET /api/internal/users/batch` (Phase 5 / Phase 8.3); **Phase 9:** `table_user_settings` (JSONB notification_prefs), `GET/PUT /api/auth/me/settings`, `GET /api/internal/users/{id}/settings`, email-consent при регистрации; **Phase 9.5:** `avatar BYTEA + avatar_content_type + avatar_updated_at` в `table_users`, `POST/GET/DELETE /api/auth/me/avatar` (≤256 KB, PNG/JPEG/WebP, IAvatarService)
- `metadata-service` — CRUD папок с иерархией + permissions (Phase 2) + InternalFoldersController; `GET /api/folders/{id}/permissions` (Phase 5); `PATCH /api/folders/{id}/touch` + `last_accessed_at` + `ownerEmail` в FolderResponse (Phase 8.2/8.3)
- `sharing-service` — share-ссылки для незарегистрированных (Phase 3): токены base64url+SHA256, 5 эндпоинтов, cleanup job, RabbitMQ publish; **Phase 8:** токены хранятся в зашифрованном виде (AES-256-GCM, `token_encrypted`), `GET /api/share-links` — глобальный листинг с полным `shareUrl`, `shareUrl` в per-folder листинге
- `notification-service` — RabbitMQ consumer (`access_granted/revoked`, `share_revoked`, `access_lost`) + SMTP email (MailKit) + SignalR in-app bell + история уведомлений; web bell + desktop client (Phase 7); **Phase 9:** per-type/per-channel consent gate, Web Push (VAPID, WebPush 1.0.13, Service Worker), push-subscriptions API, share.revoked фикс (owner-fallback)
- `log-service` — централизованный сбор логов: RabbitMQ consumer → OpenSearch; IndexTemplate при старте; `GET /api/logs` с фильтрацией по `service`/`level`/`correlationId`
- `clients/web/` — React 19 + Vite + TypeScript веб-клиент; **Phase 8 UI/UX:** полностью кастомный CSS design system (tokens.css + app.css), шрифт Inter Tight, кастомный Icon компонент (40+ SVG), Sidebar/Topbar/Inspector, 5 основных страниц, Toast bus; **AntD полностью удалён**; ErrorPage + ErrorBoundary (2026-05-11); **Phase 9:** `/settings/{account,notifications}`, чекбокс email-consent при регистрации, Service Worker (sw.js), pushSubscribe утилита, manifest.webmanifest; **Phase 9.5:** вкладка «Устройства» (`/settings/devices`) — inline-rename, раскрытие папок с кнопкой «Отвязать», кнопка «Принудительно отключить»; AccountTab — аватар upload/delete, initials-fallback; `useAvatarUrl` хук + `avatar.api.ts`; **Тёмная тема (2026-05-14):** `ThemeProvider` + `useTheme()` hook, переключатель `sun/moon` в Topbar, `[data-theme="dark"]` блок в tokens.css (инвертированная ink-шкала, dark shadows, dark status-soft), замена hardcoded rgba() → CSS var(), иконки `sun`/`moon` в Icon.tsx, удалён мёртвый App.css (Vite-шаблон); **35 Vitest тестов**
- `clients/desktop/` — Avalonia 11 + ReactiveUI + Semi.Avalonia Windows-клиент (Phase 5+6): 6 экранов + P2P sender (SIPSorcery 8.0.0 + SignalR.Client), FolderShareRegistry, ECDH+AES-GCM, online-toggle, DeviceIdentity (Phase 6.5), **двухуровневая проверка device-folder binding перед P2P** (2026-05-11); **Phase 9.5:** `Expander` «Привязки на этом устройстве» в MyFoldersView + `ReleaseBindingCommand` + `ReloadLocalBindingsAsync`; **Экран настроек (2026-05-14):** `SettingsView` + `SettingsViewModel` (полный профиль: email, userId, deviceId, platform, редактируемое имя устройства); `IDeviceIdentity.SetDeviceName()` → Credential Manager (`GraNAS:deviceName`), fallback `Environment.MachineName`; `ApiBase.PatchAsync<T>`; `ISignalingApi.RenameDeviceAsync` → `PATCH /api/signaling/devices/{id}`; кнопка «Настройки» в ShellWindow; **29 desktop-тестов**
- `services/signaling-service/` — SignalR hub + TURN credentials + Redis session store (Phase 6) + device sessions в signalingdb PostgreSQL (Phase 6.5); **явная привязка папок к устройствам** (`table_device_folders`, 3 эндпоинта): `GET /devices/folder-devices`, `POST /devices/{id}/folders/{folderId}` (с 409-конфликтом + `?force=true`), `DELETE /devices/{id}/folders/{folderId}`; **server-side binding check в `RequestSession`** + **hub-метод `DenyPeerRequest`** (2026-05-11); **Phase 9.5:** `PATCH /devices/{id}` (переименование, 409 при дубле имени) + `GET /devices/{id}/folders` (список папок устройства → `DeviceFolderResponse[]`)
- **coturn** в docker compose (Phase 6)
- Shared: Correlation-Id, Swagger+JWT, ExceptionHandlingMiddleware, `GraNAS.Shared.LoggingService` — `UseGraNasCentralLogging` + `RabbitMqLogSink` + `SensitiveDataEnricher` + `MvcLoggingActionFilter`; логи идут через RabbitMQ → log-service → OpenSearch; `GraNAS.Shared.Messaging` — паблишеры событий во всех сервисах
- Docker Compose для dev и prod (gateway на порту 8080, signaling на 8085)
- CI/CD (GitHub Actions → GHCR → staging via SSH)
- **101 unit .NET + integration тестов + 29 Desktop-тестов + 35 Vitest тестов — все зелёные**

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
- CORS на сервисах (AllowAnyOrigin) — оставить до prod-выкатки, когда весь браузерный трафик идёт через gateway. Tightening — Phase 14.

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

**Остаток для Phase 12 / Phase 6:**
- WebRTC/P2P (Phase 6)
- Shell Extension / Cloud Files API (Phase 12)

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

### 8.4 signaling-service: явная привязка папок к устройствам
- [x] **`table_device_folders`** в signalingdb — `PK = folder_id` (один девайс на папку), FK → table_devices CASCADE
- [x] `IDeviceFolderRepository` / `DeviceFolderRepository` — `ClaimAsync` (PostgreSQL ON CONFLICT DO UPDATE), `ReleaseAsync`, `GetByFolderIdsAsync`
- [x] `IDeviceService` — `TryClaimFolderAsync` (конфликт-логика: null=OK, existing=409), `ReleaseFolderAsync`, `GetFolderDevicesAsync`
- [x] Три новых REST-эндпоинта в `DevicesController`
- [x] Desktop: `ISignalingApi.ClaimFolderAsync/ReleaseFolderAsync`; `BindLocalFolderAsync` вызывает claim с диалогом переназначения; `IDialogService.ShowConfirmAsync`
- [x] Web: `FolderDeviceResponse` тип, `useFolderDevices` хук, badge с именем устройства + онлайн-точка на карточках папок; Inspector.Свойства → строка «Устройство»

### 8.5 P2P binding guard: проверка device-folder binding перед WebRTC ✅ (2026-05-11)

Закрывает race-условие: если несколько устройств одного пользователя вызвали `JoinAsOwner` для одной папки, раньше любое из них могло ответить на `IncomingPeerRequest`. Теперь отвечает только то, за которым числится папка в `table_device_folders`.

**Двойная защита:**
- [x] **Desktop guard** (быстрый отказ + UX): `P2PHost.HandleIncomingPeerRequestCoreAsync` вызывает `GET /api/signaling/devices/folder-devices` для проверки binding. Если `DeviceId != DeviceIdentity.DeviceId` — вызывает новый hub-метод `DenyPeerRequest`; хаб форвардит `AccessDenied("folder_bound_to_another_device")` receiver-у. Результат кэшируется в сессии (`ConcurrentDictionary<Guid, Guid?>`), инвалидируется на `IFolderShareRegistry.MappingChanged`.
- [x] **Server guard** (защита от не-обновлённого desktop): `RequestSession` в хабе вызывает `IDeviceService.GetBoundDeviceIdAsync`. Если JoinAsOwner-device ≠ bound device — перенаправляет `IncomingPeerRequest` на bound device (или `OwnerOffline` если тот offline).
- [x] `IDeviceService.GetBoundDeviceIdAsync(folderId)` — новый метод, делегирует в `IDeviceFolderRepository.GetByFolderIdAsync`
- [x] Hub-метод `DenyPeerRequest(receiverConnId, folderId, reason)` — owner явно отказывает, хаб валидирует через `AssertValidSessionAsync`
- [x] `IFolderShareRegistry.MappingChanged` event — `FolderShareRegistry` поднимает при `SetLocalPath`/`RemoveMapping`; `P2PHost` инвалидирует cache
- [x] `ISignalingApi.GetFolderDevicesAsync(folderIds)` + `FolderDeviceBinding` DTO — новые в desktop API-клиенте
- [x] Test seam: `P2PHost` стал не-`sealed`; `SendDenyAsync` + `StartWebRtcSessionAsync` — `protected internal virtual`; `InternalsVisibleTo("GraNAS.Desktop.Tests")` в csproj
- [x] +6 desktop unit-тестов (`P2PHostTests`), +2 backend unit-тестов (`DeviceServiceTests`); итого **185 .NET + 22 Desktop** ✅

---

## Фаза 9. Расширенные уведомления — consent + web push ✅ (2026-05-12)

Пользователь получил контроль над каналами и типами уведомлений. Добавлен web push.

- [x] **auth-service: `table_user_settings`** — JSONB `notification_prefs`, EF миграция `AddUserSettings`, 1:1 FK→users
- [x] **auth-service: settings API** — `GET/PUT /api/auth/me/settings`, `GET /api/internal/users/{id}/settings` (для notification-service), lazy-create с defaults
- [x] **Consent при регистрации** — `RegisterRequest.EmailNotificationsConsent` (bool, default true); `AuthService.RegisterAsync` сохраняет в user_settings
- [x] **notification-service: consent gate** — `NotificationIngestionService` проверяет prefs перед созданием outbox-строк; если все каналы выключены — не создаёт `NotificationEvent`
- [x] **share.revoked фикс** — `ExtractUserId` fallback на `OwnerId` при `share.revoked` (self-confirmation owner-у)
- [x] **`DeliveryTarget.WebPush`** — новое значение enum, `table_push_subscriptions`, EF миграция `AddPushSubscriptions`
- [x] **WebPushSender** — NuGet `WebPush 1.0.13`, VAPID, 410/404 = expired sub (удалить)
- [x] **WebPushDeliveryWorker** — 5s timer, batch 20, backoff как у EmailDeliveryWorker
- [x] **PushPayloadRenderer** — RU тексты, url=`WebClient:BaseUrl/folders/{id}`
- [x] **PushSubscriptionsController** — `GET /push/vapid-public-key` (anon), `POST/DELETE /push-subscriptions` (auth)
- [x] **`IUserSettingsResolver`** — HTTP-клиент к auth-service, кэш 5 мин, fail-open
- [x] **Frontend: `/settings/{account,notifications}`** — NotificationsTab с матрицей чекбоксов (4 типа × 3 канала), кнопка «Включить push»
- [x] **Frontend: Service Worker** (`public/sw.js`, `public/manifest.webmanifest`) — push handler + notificationclick
- [x] **Frontend: `lib/pushSubscribe.ts`** — enablePush/disablePush/isPushEnabled + urlBase64ToUint8Array
- [x] +11 .NET тестов (unit consent gate, integration settings API, share.revoked unit), +4 Vitest тестов (NotificationsTab + consent checkbox); итого **196 .NET + 22 Desktop + 22 Vitest** ✅

**Критерий готовности:** пользователь управляет per-type/per-channel префами; браузер получает push при закрытой вкладке; email не отправляется при отказе от consent.

---

## Фаза 9.5. Профиль пользователя и управление устройствами ✅ (2026-05-13)

- [x] **auth-service: аватар** — `byte[] Avatar + string? AvatarContentType + DateTime? AvatarUpdatedAt` в `table_users`; миграция `AddUserAvatar`; `IAvatarService` / `AvatarService`; `POST/GET/DELETE /api/auth/me/avatar` (multipart, ≤256 KB, PNG/JPEG/WebP); `IUserRepository.SaveAvatarAsync` через `ExecuteUpdateAsync`
- [x] **signaling-service: PATCH /devices/{id}** — переименование устройства; валидация `[StringLength(100,1)]`; 200=DeviceResponse | 403=чужое | 409=дубль имени `(user_id, device_name)`; `IDeviceRepository.RenameAsync` → tracked entity + SaveChanges + catch DbUpdateException
- [x] **signaling-service: GET /devices/{id}/folders** — список папок конкретного устройства (`DeviceFolderResponse[]`: `{FolderId, ClaimedAt}`); имена папок frontend берёт из кэша FOLDERS_KEY; `IDeviceFolderRepository.GetByDeviceIdAsync`; `IDeviceService.GetFoldersByDeviceAsync`
- [x] **web: DevicesTab** (`/settings/devices`) — таблица устройств: иконка платформы, inline-rename (Enter/blur/Escape/409 toast), online-статус + relTime, кнопка «Отключить»; раскрытие строки → список папок + кнопка «Отвязать» (releaseFolder → invalidate кэши); TanStack Query stale 30s
- [x] **web: AccountTab аватар** — загрузка (file input, accept PNG/JPEG/WebP ≤256KB) + удаление + fallback инициалы (`colorFromString + initials`); хук `useAvatarUrl(avatarKey?)` через axios blob fetch + objectURL; Guard: `useAuth().loading||!user` → spinner (избегает исключения `useCurrentUser()`)
- [x] **web: SettingsPage** — добавлена вкладка «Устройства» (3 таба: Аккаунт / Устройства / Уведомления)
- [x] **desktop: LocalBindings Expander** — `Expander` «Привязки на этом устройстве» в `MyFoldersView.axaml` (Grid Row 2, Row 3 для индикатора); `LocalBindingRow record`, `ObservableCollection<LocalBindingRow>`, `ReloadLocalBindingsAsync` (join server bindings + FolderShareRegistry), `ReleaseBindingCommand` (ReleaseFolderAsync + RemoveMapping + Remove из коллекции)
- [x] **desktop: ISignalingApi** — добавлен `GetDeviceFoldersAsync(Guid, ct) → List<DeviceFolderInfo>` + record `DeviceFolderInfo(FolderId, ClaimedAt)`
- [x] **Тесты:** +6 integration DevicesController (PATCH rename/403/409/400, GET folders 200/403), +6 integration AvatarController, +2 unit DeviceService (RenameAsync null, GetFoldersByDevice), +2 desktop (LocalBindings populated, ReleaseBinding), +9 Vitest (settings-devices + account-avatar); **итого 101 unit .NET + 24 desktop + 31 Vitest**

**Критерий готовности:** пользователь видит/переименовывает/отключает устройства и отвязывает папки через web /settings/devices; загружает свой аватар или видит инициалы; desktop показывает Expander с привязками. ✅

---

## Фаза 10. search-service — расширенный поиск

- [ ] **PostgreSQL full-text** (`tsvector`) — старт с PG FTS; Elasticsearch —
      запасной вариант, если PG не хватит
- [ ] Эндпоинт `GET /search?q=&owner=&access=` с пагинацией (cursor-based)
- [ ] Фильтрация по правам: только доступные пользователю папки
- [ ] Экран поиска в web и desktop клиентах

**Критерий готовности:** поиск по имени/владельцу за < 300 мс на 10k записях.

---

## Фаза 11. admin-service

- [ ] Скелет `GraNAS.Admin.{API,Services,Models}`
- [ ] Authorization policy `RequireAdmin` (по `is_admin` в JWT)
- [ ] Эндпоинты: список пользователей, блокировка, принудительный revoke прав / ссылок
- [ ] Аудит-лог административных действий

**Критерий готовности:** админ блокирует пользователя и отзывает все его
share-ссылки одним запросом.

---

## Фаза 12. Windows-клиент — Cloud Files API + Shell Extension

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

## Фаза 13. Android-клиент

- [ ] Стек: **Kotlin + Jetpack Compose**, Retrofit, DataStore
- [ ] Те же экраны, что веб-клиент (адаптированные под мобильный UX)
- [ ] **P2P:** `google-webrtc`, интеграция с signaling-service
- [ ] Push-уведомления — FCM (привязка к notification-service)
- [ ] Certificate pinning для HTTPS

**Критерий готовности:** на Android доступны все сценарии брифа, push работают,
файлы качаются P2P.

---

## Фаза 14. Production hardening

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
