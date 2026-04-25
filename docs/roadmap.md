# Roadmap разработки GraNAS

Дорожная карта составлена на базе `docs/brief.md` и `docs/techspec.md`.
Цель: **100+ пользователей в течение 3 месяцев после релиза**, мультиплатформенный
доступ к папкам без хранения файлов на сервере.

Фазы упорядочены по зависимостям, не по календарным датам — каждая следующая
опирается на результаты предыдущих.

---

## Текущее состояние (2026-04-25)

**Реализовано:**

- `auth-service` — регистрация, логин, JWT, refresh-токены (Clean Architecture)
- `metadata-service` — CRUD папок с иерархией подпапок + permissions (Phase 2) + InternalFoldersController
- `sharing-service` — share-ссылки для незарегистрированных (Phase 3): токены base64url+SHA256, 5 эндпоинтов, cleanup job, RabbitMQ publish
- `log-service` — централизованный сбор логов через RabbitMQ + React-дашборд
- Shared: Correlation-Id, Swagger+JWT, ExceptionHandlingMiddleware, Serilog → Elasticsearch
- Docker Compose для dev и prod
- CI/CD (GitHub Actions → GHCR → staging via SSH)

**Плейсхолдеры без реализации:**
`admin-service`, `notification-service`, `search-service`, `signaling-service`

**Не начато:** веб-клиент основного UI, Windows-клиент, Android,
P2P-транспорт (WebRTC / ICE / DTLS) и инфраструктура STUN/TURN.

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

## Фаза 5. Минимальный Windows-клиент (Desktop)

Нативное приложение под Windows, покрывающее те же 4 сервиса, что и веб-клиент.
Цель — получить рабочий desktop-клиент до добавления P2P, чтобы потом
интегрировать signaling в оба клиента одновременно.

- [ ] Стек: **.NET 10 + WPF** (WinUI3 — при необходимости, если нужны MSIX)
- [ ] Тот же набор экранов, что и веб-клиент Phase 4 (адаптированный под desktop UX)
- [ ] JWT хранить в `Windows Credential Manager` (не в файле)
- [ ] Фоновый поллинг метаданных папок (без Cloud Files API — это в Фазе 9)
- [ ] Обработка offline: ясная ошибка вместо подвисания

**Критерий готовности:** пользователь может выполнять все операции (папки,
права, share-ссылки) через нативное окно без браузера; токены не хранятся
в plaintext на диске.

---

## Фаза 6. signaling-service + P2P-транспорт (WebRTC / ICE / DTLS)

Соединяем web и desktop клиентов: ссылка выдана, папка видна — теперь можно
и файлы передать. Файлы на сервере не лежат — единственный путь идёт
**напрямую между клиентами** через WebRTC data channel. Сервер только знакомит
пиров и, при худшем NAT, ретранслирует зашифрованные пакеты через TURN.

**Стек:** signaling (SignalR) → STUN (публичные) → TURN (свой coturn) → ICE
(host/srflx/relay) → DTLS (шифрование канала) → app-level E2E (AES-GCM на файле).
C# — `SIPSorcery`; браузер — нативный `RTCPeerConnection`.

- [ ] Скелет `GraNAS.Signaling.{API,Services}` (без DAL — состояние в Redis)
- [ ] **SignalR hub** `/p2p` с операциями `join`, `offer`, `answer`, `ice-candidate`
- [ ] **Авторизация в хабе** — JWT или share-токен; проверка прав через metadata/sharing
- [ ] **TURN-credentials endpoint** — временные учётки coturn через shared secret
      (RFC 8489 REST API), TTL ≈ 10 мин
- [ ] **coturn в compose** — отдельный сервис в dev/prod, проброс UDP-диапазона
- [ ] **Прототип передачи файла** через data channel: chunking, backpressure,
      SHA-256 верификация на приёмнике
- [ ] **App-level encryption** — ECDH handshake внутри data channel, AES-GCM payload
- [ ] **P2P-слой в web-клиенте** (Phase 4) — нативный `RTCPeerConnection`
- [ ] **P2P-слой в desktop-клиенте** (Phase 5) — SIPSorcery `RTCPeerConnection`
- [ ] **Метрики:** доля соединений host/srflx/relay
- [ ] **Документированный fallback** при симметричном NAT

**Критерий готовности:** web↔web и web↔desktop за разными NAT передают файл
> 100 МБ; оператор signaling/TURN не может прочитать содержимое (дамп relay
не расшифровывается без клиентского ключа).

---

## Фаза 7. notification-service — email + webhooks

- [ ] Скелет `GraNAS.Notifications.{API,Services,Models,DAL}`
- [ ] **Таблица `events`** (user_id, type, data jsonb, delivered, created_at)
- [ ] **RabbitMQ consumer** на события: `access_granted`, `access_revoked`,
      `share_revoked`, `access_lost`
- [ ] **SMTP-адаптер** — `IEmailSender` через MailKit; провайдер — конфигом
- [ ] **Webhook delivery** с повторами и экспоненциальным backoff
- [ ] **Outbox pattern** — событие в БД → отправка в фоне
- [ ] Шаблоны писем: grant, revoke, share-revoked, access-lost
- [ ] **In-app уведомления** в web и desktop клиентах (bell + история)

**Критерий готовности:** grant/revoke генерируют письмо + in-app событие;
падение SMTP не теряет уведомления.

---

## Фаза 8. search-service — расширенный поиск

- [ ] **PostgreSQL full-text** (`tsvector`) — старт с PG FTS; Elasticsearch —
      запасной вариант, если PG не хватит
- [ ] Эндпоинт `GET /search?q=&owner=&access=` с пагинацией (cursor-based)
- [ ] Фильтрация по правам: только доступные пользователю папки
- [ ] Экран поиска в web и desktop клиентах

**Критерий готовности:** поиск по имени/владельцу за < 300 мс на 10k записях.

---

## Фаза 9. admin-service

- [ ] Скелет `GraNAS.Admin.{API,Services,Models}`
- [ ] Authorization policy `RequireAdmin` (по `is_admin` в JWT)
- [ ] Эндпоинты: список пользователей, блокировка, принудительный revoke прав / ссылок
- [ ] Аудит-лог административных действий

**Критерий готовности:** админ блокирует пользователя и отзывает все его
share-ссылки одним запросом.

---

## Фаза 10. Windows-клиент — Cloud Files API + Shell Extension

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

## Фаза 11. Android-клиент

- [ ] Стек: **Kotlin + Jetpack Compose**, Retrofit, DataStore
- [ ] Те же экраны, что веб-клиент (адаптированные под мобильный UX)
- [ ] **P2P:** `google-webrtc`, интеграция с signaling-service
- [ ] Push-уведомления — FCM (привязка к notification-service)
- [ ] Certificate pinning для HTTPS

**Критерий готовности:** на Android доступны все сценарии брифа, push работают,
файлы качаются P2P.

---

## Фаза 12. Production hardening

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
