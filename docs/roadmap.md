# Roadmap разработки GraNAS

Дорожная карта составлена на базе `docs/brief.md` и `docs/techspec.md`.
Цель: **100+ пользователей в течение 3 месяцев после релиза**, мультиплатформенный
доступ к папкам без хранения файлов на сервере.

Фазы упорядочены по зависимостям, не по календарным датам — каждая следующая
опирается на результаты предыдущих.

---

## Текущее состояние (2026-04-18)

**Реализовано:**

- `auth-service` — регистрация, логин, JWT, refresh-токены (Clean Architecture)
- `metadata-service` — CRUD папок и файлов (Clean Architecture)
- `log-service` — централизованный сбор логов через RabbitMQ + React-дашборд
- Shared: Correlation-Id, Swagger+JWT, ExceptionHandlingMiddleware, Serilog → Elasticsearch
- Docker Compose для dev и prod
- CI/CD (GitHub Actions → GHCR → staging via SSH)

**Плейсхолдеры без реализации:**
`admin-service`, `notification-service`, `search-service`, `sharing-service`, `signaling-service`

**Не начато:** веб-клиент основного UI, Android, Windows Shell Extension,
P2P-транспорт (WebRTC / ICE / DTLS) и инфраструктура STUN/TURN.

---

## Фаза 1. Стабилизация ядра (foundation)

Прежде чем строить фичи, закрыть долги текущих сервисов.

- [ ] **Тесты auth-service** — юнит на AuthService (все Result-ветки) +
      интеграционные на контроллер с testcontainers PostgreSQL
- [ ] **Тесты metadata-service** — аналогично, с каскадным удалением папок
- [ ] **Убрать `tests.txt` с живыми JWT** из репозитория (если ещё где-то остался)
- [ ] **Health-checks** (`/health`, `/health/ready`) во все API
- [ ] **Rate limiting** на auth-эндпоинтах (ASP.NET Core RateLimiter)
- [ ] **Secrets management** — перевести JWT_SECRET и строки подключения
      на Docker secrets / env-файл вне репо
- [ ] **OpenAPI-контракты** зафиксировать как артефакты CI
- [ ] **Kestrel CVE (NU1904)** — обновить транзитивную зависимость

**Критерий готовности:** зелёный CI, покрытие ≥70% на Auth/Metadata,
секреты не попадают в образ.

---

## Фаза 2. Permissions — зарегистрированные пользователи

Из брифа шаг 3 User Journey: владелец назначает `view` / `full` другому
зарегистрированному пользователю.

- [ ] **Таблица `permissions`** в metadata-service (folder_id, user_id, access_level enum)
- [ ] **Доменная модель** `Permission`, `AccessLevel { View, Full }` в `Metadata.Models`
- [ ] **IPermissionRepository** + реализация
- [ ] **PermissionService** — Grant / Revoke / List / Check
- [ ] **Authorization policy** `CanReadFolder` / `CanWriteFolder` —
      проверка владения ИЛИ наличия permission на каждом запросе
- [ ] **Эндпоинты** `POST /folders/{id}/permissions`, `DELETE /folders/{id}/permissions/{userId}`
- [ ] **Интеграция с auth-service** — поиск пользователя по email для выдачи прав
      (через REST, не через общую БД)

**Критерий готовности:** второй пользователь видит чужую папку согласно правам;
попытка записи с `view` → 403.

---

## Фаза 3. sharing-service — ссылки для незарегистрированных

Шаги 4-5, 13 User Journey: уникальные ссылки со сроком действия, отзыв.

- [ ] Скелет `GraNAS.Sharing.{API,Services,Models,DAL}` по Clean Architecture
- [ ] **Таблица `share_links`** (folder_id, token_hash, expires_at, revoked)
- [ ] **Криптостойкая генерация токенов** — `RandomNumberGenerator`, base64url
- [ ] **Хранение только SHA-256 хэшей** (в БД токен в открытом виде не лежит)
- [ ] Эндпоинты:
  - `POST /folders/{id}/share` → возвращает токен один раз
  - `GET /share/{token}` → проверяет expires_at + revoked, отдаёт метаданные
  - `DELETE /share/{token}` → revoke
- [ ] **Фоновая очистка** просроченных ссылок (Hangfire / IHostedService)
- [ ] **Публикация события** `share_revoked` в RabbitMQ для notification-service

**Критерий готовности:** получатель ссылки видит папку до `expires_at`;
после revoke → 410 Gone с понятным сообщением.

---

## Фаза 4. signaling-service + P2P-транспорт (WebRTC / ICE / DTLS)

Без этого фаза sharing остаётся "бумажной": ссылка выдана, папка видна,
а сами байты между клиентами передавать нечем. Файлы на сервере не лежат —
единственный путь идёт **напрямую между клиентами** через WebRTC data channel.
Сервер только знакомит пиров и, при худшем NAT, ретранслирует зашифрованные
пакеты через TURN.

**Стек:** signaling (SignalR) → STUN (публичные) → TURN (свой coturn) → ICE
(host/srflx/relay) → DTLS (шифрование канала) → app-level E2E (шифрование
самого файла на клиенте). C# — `SIPSorcery`; браузер — нативный `RTCPeerConnection`.

- [ ] Скелет `GraNAS.Signaling.{API,Services}` (Clean Architecture,
      без DAL — состояние эфемерное, в Redis)
- [ ] **SignalR hub** `/p2p` с операциями `join`, `offer`, `answer`, `ice-candidate`
- [ ] **Авторизация в хабе** — JWT от auth-service + проверка прав на папку
      (через metadata-service) или валидация share-токена (через sharing-service)
- [ ] **TURN-credentials endpoint** — генерация временных учёток coturn
      через shared secret (RFC 8489 REST API), TTL ≈ 10 мин
- [ ] **coturn в compose** — отдельный сервис в dev/prod, проброс UDP-диапазона,
      конфиг с `use-auth-secret`
- [ ] **Референсная C#-реализация пира** на SIPSorcery
      (`RTCPeerConnection`, data channel) — для Windows-клиента и интеграционных тестов
- [ ] **Прототип передачи файла** через data channel: chunking, backpressure
      (буфер `bufferedAmount`), SHA-256 верификация на приёмнике
- [ ] **Web-клиент P2P-слой** — нативный `RTCPeerConnection` в браузере
- [ ] **App-level encryption** — ECDH handshake внутри data channel до старта
      передачи, AES-GCM для payload; сервер ключи не видит
- [ ] **Метрики:** распределение соединений host/srflx/relay — нужно для
      оценки нагрузки на TURN и бюджета трафика
- [ ] **Документированное ограничение v1:** симметричный NAT без TURN
      → явный fallback "попросите владельца открыть порт / использовать TURN"

**Критерий готовности:** два клиента (web↔web, web↔Windows) за разными NAT
передают файл > 100 МБ; при симметричном NAT — через relay с видимым лейблом;
проверка, что оператор signaling/TURN не может расшифровать содержимое
(снимаем дамп на relay → не читается без клиентского ключа).

**Что отложено:** push из сервера клиенту "владелец онлайн, можно забирать" —
пока клиент сам опрашивает через signaling.

---

## Фаза 5. notification-service — email + webhooks

Шаги 6, 14 User Journey.

- [ ] Скелет `GraNAS.Notifications.{API,Services,Models,DAL}`
- [ ] **Таблица `events`** (user_id, type, data jsonb, delivered, created_at)
- [ ] **RabbitMQ consumer** на события из metadata/sharing:
  - `access_granted`, `access_revoked`, `share_revoked`, `access_lost`
- [ ] **SMTP-адаптер** — интерфейс `IEmailSender`, реализация через MailKit;
      выбор провайдера (SendGrid / Mailgun / SES) — конфигом
- [ ] **Webhook delivery** с повторами и экспоненциальным backoff
- [ ] **Outbox pattern** — запись события в БД и отправка в фоне,
      чтобы переживать падения SMTP
- [ ] Шаблоны писем (минимум: grant, revoke, share-revoked, access-lost)

**Критерий готовности:** grant/revoke прав генерируют письмо + in-app событие;
падение SMTP не теряет уведомления.

---

## Фаза 6. search-service — расширенный поиск

Шаг 8 User Journey.

- [ ] Решение: **PostgreSQL full-text** (`tsvector`) vs **Elasticsearch**.
      Стартуем с PG FTS (Elasticsearch уже есть для логов — переиспользовать
      как альтернативу, если PG не хватит).
- [ ] Индексы по `name`, `type`, `owner_id`
- [ ] Эндпоинт `GET /search?q=&type=&owner=&access=`
- [ ] Фильтрация по правам: выдаём только то, к чему есть доступ
- [ ] Пагинация (cursor-based)

**Критерий готовности:** поиск по имени/типу/владельцу за < 300 мс на 10k записях.

---

## Фаза 7. admin-service

Шаг 12 User Journey.

- [ ] Скелет `GraNAS.Admin.{API,Services,Models}`
- [ ] Authorization policy `RequireAdmin` (по `is_admin`)
- [ ] Эндпоинты: список пользователей, блокировка, просмотр метаданных,
      принудительный revoke прав / ссылок
- [ ] Аудит-лог административных действий (в events или отдельную таблицу)

**Критерий готовности:** админ может заблокировать пользователя и отозвать
все его share-ссылки одним запросом.

---

## Фаза 8. Web-клиент (React)

Фронтенд основного приложения (отдельно от существующего log-dashboard).

- [ ] Стек: **React 19 + Vite + TypeScript**, React Query, React Router
- [ ] UI Kit: Ant Design / shadcn-ui — выбрать один
- [ ] Экраны:
  - Регистрация / логин
  - Мои папки (дерево + upload метаданных)
  - Назначение прав (поиск пользователя по email, выбор access level)
  - Генерация share-ссылки с датой expire
  - Вкладка «Доступные папки» (фильтры: владелец, название, права)
  - Поиск
  - Уведомления (колокольчик + история)
- [ ] HTTPS-only, refresh-токен в httpOnly cookie
- [ ] **P2P-модуль** — обёртка над нативным `RTCPeerConnection`,
      интеграция с signaling-service, UI прогресса передачи с индикатором
      пути (host / srflx / relay)

**Критерий готовности:** полный user journey из брифа проходится в браузере,
включая скачивание файла по ссылке через P2P.

---

## Фаза 9. Windows-клиент + Shell Extension

Шаг 9 User Journey.

- [ ] **Выбор стека:** .NET 10 + WPF/WinUI3 для UI,
      **Cloud Files API** (CF API) — предпочтительнее устаревших Shell Extensions
- [ ] Virtual folder: монтирование пользовательских папок как placeholder-файлов
- [ ] Действия Проводника (copy, rename, delete метаданных) → REST API
- [ ] **Передача содержимого файлов** через SIPSorcery `RTCPeerConnection`
      → signaling-service; hydrate placeholder → тянем байты напрямую с пира
- [ ] Фоновый синк метаданных (**без кэширования чужих данных**)
- [ ] Обработка offline: понятная ошибка вместо «зависания»
- [ ] Auto-update механизм

**Критерий готовности:** пользователь видит свои и доступные папки в Проводнике,
метаданные синхронизируются с сервером, открытие чужого файла триггерит
P2P-скачивание с владельца.

---

## Фаза 10. Android-клиент

- [ ] Стек: **Kotlin + Jetpack Compose**, Retrofit, DataStore
- [ ] Те же экраны, что веб (адаптированные под мобильный UX)
- [ ] **P2P:** библиотека `google-webrtc`, интеграция с signaling-service
- [ ] Push-уведомления — FCM (привязка к notification-service webhooks)
- [ ] Certificate pinning для HTTPS

**Критерий готовности:** на Android доступны все сценарии брифа, push работают,
файлы качаются P2P.

---

## Фаза 11. Production hardening

- [ ] **Observability:** Grafana дашборды поверх Elasticsearch-логов +
      Prometheus метрики через OpenTelemetry
- [ ] **Tracing:** OTEL через Jaeger — корреляция между сервисами
      (Correlation-Id уже есть, добавить span-ы)
- [ ] **Backup PostgreSQL** — pgBackRest + отдельный бакет
- [ ] **Load testing** — k6 / NBomber на 100+ конкурентных пользователей
- [ ] **TURN-нагрузка** — оценить долю relay-соединений на реальном трафике,
      решить по мощности coturn и бюджету исходящего трафика
- [ ] **Security audit:** OWASP ZAP + `dotnet list package --vulnerable` +
      пентест P2P-handshake (подмена SDP, MITM на signaling)
- [ ] **SLA/SLO:** определить и измерять доступность, p95 latency (отдельно
      для REST и для setup-time P2P-канала)
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
4. **API Gateway** (Ocelot / YARP) — нужен ли при 5+ сервисах или оставляем
   клиенту список URL-ов.
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
