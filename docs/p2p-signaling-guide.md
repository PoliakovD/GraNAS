# GraNAS P2P & Signaling: полный технический гайд

> **Для кого:** junior backend-разработчик, знакомый с ASP.NET Core, HTTP и базовыми
> понятиями сетевого взаимодействия. Опыт с WebRTC не требуется — все понятия
> объясняются по ходу текста.

---

## Содержание

1. [Зачем P2P и почему это сложно](#1-зачем-p2p-и-почему-это-сложно)
2. [Архитектура: общая картина](#2-архитектура-общая-картина)
3. [WebRTC за 5 минут](#3-webrtc-за-5-минут)
4. [Signaling-service: точка встречи пиров](#4-signaling-service-точка-встречи-пиров)
5. [Redis: зачем он здесь](#5-redis-зачем-он-здесь)
6. [ICE, STUN и TURN: как пробить NAT](#6-ice-stun-и-turn-как-пробить-nat)
7. [TURN credentials: временные ключи к relay](#7-turn-credentials-временные-ключи-к-relay)
8. [Полный flow P2P-сессии: шаг за шагом](#8-полный-flow-p2p-сессии-шаг-за-шагом)
9. [Desktop-клиент: роль owner/sender](#9-desktop-клиент-роль-ownersender)
10. [Web-клиент: роль receiver](#10-web-клиент-роль-receiver)
11. [Data channel протокол: что летит по каналу](#11-data-channel-протокол-что-летит-по-каналу)
12. [Шифрование: ECDH + AES-GCM](#12-шифрование-ecdh--aes-gcm)
13. [Безопасность: что сервер не может прочитать](#13-безопасность-что-сервер-не-может-прочитать)
14. [Debugging & мониторинг](#14-debugging--мониторинг)

---

## 1. Зачем P2P и почему это сложно

### Проблема хранения на сервере

Традиционный файловый сервис работает так:

```
Пользователь A → загружает файл на сервер → Пользователь B скачивает файл с сервера
```

У этого подхода есть очевидные минусы:
- **Двойной трафик**: файл проходит через сервер дважды — при загрузке и скачивании.
- **Хранилище**: сервер должен хранить копии всех файлов.
- **Приватность**: оператор сервиса видит содержимое файлов.
- **Стоимость**: исходящий трафик у большинства cloud-провайдеров платный.

GraNAS решает это иначе: **файлы никогда не покидают компьютер владельца через сервер**.
Байты передаются напрямую между клиентами через **WebRTC Data Channel**.
Сервер знает только метаданные (имена папок, права доступа) и занимается тем, чтобы
клиенты «нашли друг друга» в интернете.

### Почему это сложно: NAT

В идеальном мире у каждого компьютера был бы публичный IP-адрес, и любые два клиента
могли бы соединиться напрямую. В реальности большинство устройств прячется за
**NAT** (Network Address Translation) — роутер/провайдер раздаёт внутренние IP-адреса
(192.168.x.x, 10.x.x.x) и транслирует их в один внешний.

```
Компьютер A (192.168.1.10) → Роутер A (публичный IP: 1.2.3.4) → Интернет
Компьютер B (10.0.0.5)     → Роутер B (публичный IP: 5.6.7.8) → Интернет
```

Компьютер A не знает адрес компьютера B, а B не знает адрес A. Оба видят только
IP своего роутера. Напрямую не соединиться без посредника.

Именно здесь и нужен signaling-service — он помогает клиентам обменяться адресами
и установить прямое соединение.

---

## 2. Архитектура: общая картина

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              DOCKER COMPOSE                                  │
│                                                                              │
│  ┌───────────────┐   ┌──────────────────┐   ┌──────────────────────────┐   │
│  │  api-gateway  │   │ signaling-service │   │         coturn           │   │
│  │  YARP :8080   │──▶│  SignalR :8085    │   │  TURN relay :3478/udp   │   │
│  └───────┬───────┘   └────────┬─────────┘   └──────────────────────────┘   │
│          │                    │                        ▲                     │
│          │                    ▼                        │ UDP relay           │
│          │              ┌────────┐                     │ (зашифрованный)    │
│          │              │ Redis  │                     │                     │
│          │              └────────┘                     │                     │
└──────────┼──────────────────────────────────────────── ┼ ───────────────────┘
           │ HTTP (REST)                                  │
           │ WS (/hubs/signaling)                         │
    ┌──────┴──────┐                                ┌──────┴──────┐
    │ Web Browser │◀──────── WebRTC P2P ──────────▶│  Desktop    │
    │ (receiver)  │     (Data Channel, E2E шифр.)  │  (sender)   │
    └─────────────┘                                └─────────────┘
```

**Роли компонентов:**

| Компонент | Роль |
|---|---|
| `api-gateway` | Единая точка входа; проксирует REST и WebSocket к signaling-service |
| `signaling-service` | Сводит пиров: помогает обменяться SDP и ICE кандидатами |
| `coturn` | TURN-сервер; ретранслирует пакеты когда прямое соединение невозможно |
| `Redis` | Хранит состояние сессий signaling-service (кто owner какой папки) |
| Desktop (owner) | Хранит файлы на диске; отдаёт их по WebRTC Data Channel |
| Web browser (receiver) | Скачивает файлы через Data Channel; сам файлы не хранит |

---

## 3. WebRTC за 5 минут

**WebRTC** (Web Real-Time Communication) — это открытый стандарт, позволяющий браузерам
и приложениям устанавливать прямое P2P-соединение для передачи аудио, видео и произвольных
данных. Поддерживается нативно во всех современных браузерах.

### Ключевые понятия

**RTCPeerConnection** — главный объект WebRTC. Управляет установкой соединения,
шифрованием транспортного уровня (DTLS), сбором ICE-кандидатов.

**RTCDataChannel** — канал внутри RTCPeerConnection для передачи произвольных
бинарных и текстовых данных. Именно через него GraNAS передаёт файлы. Аналог
TCP-стрима, но P2P.

**SDP (Session Description Protocol)** — текстовый формат для описания параметров
соединения: кодеки, IP-адреса, порты, возможности шифрования. Два пира обмениваются
SDP-офером (предложением) и SDP-ответом (answer) для согласования параметров.

**ICE (Interactive Connectivity Establishment)** — протокол для нахождения маршрута
между двумя пирами через NAT. Собирает список кандидатов (возможных IP:port пар)
и проверяет какой работает.

**DTLS (Datagram Transport Layer Security)** — шифрование транспортного уровня
WebRTC. Аналог TLS, но для UDP. Устанавливается автоматически — это обязательная
часть WebRTC.

### Почему нужен сигнализирующий сервер

WebRTC умеет устанавливать P2P-соединение, но сам не знает как **найти** второго
пира. SDP-оффер надо как-то доставить до удалённой стороны. Для этого нужен
внешний канал связи — **signaling channel**. Им может быть что угодно: HTTP,
WebSocket, Email, голубиная почта. В GraNAS — это SignalR хаб.

```
Пир A создаёт SDP-оффер → отправляет через SignalR → Пир B
Пир B создаёт SDP-ответ  → отправляет через SignalR → Пир A
Оба начинают ICE         → через SignalR меняются кандидатами
WebRTC соединение установлено → SignalR больше не нужен
```

Signaling-server видит только SDP-строки и ICE-кандидаты — **не содержимое файлов**.

---

## 4. Signaling-service: точка встречи пиров

### Технологический стек

Сервис расположен в `services/signaling-service/` и состоит из трёх проектов:
`GraNAS.Signaling.{API, Services, Models}`.

- **ASP.NET Core 10** — хост
- **SignalR** — WebSocket-фреймворк для realtime-коммуникации
- **StackExchange.Redis** — клиент Redis для хранения состояния
- **JWT Bearer** — аутентификация

### Почему SignalR, а не чистый WebSocket?

SignalR — это библиотека поверх WebSocket (и других транспортов), которая добавляет:
- **Типизированные вызовы** (`hub.invoke("MethodName", arg1, arg2)`) вместо ручного парсинга JSON
- **Автоматический reconnect** при обрыве соединения
- **Groups** — механизм для рассылки сообщений группе клиентов (в GraNAS: всем, кто смотрит папку)
- **Backplane через Redis** — когда несколько инстансов сервиса, они синхронизируются через Redis

### Как JWT передаётся через WebSocket

Обычный HTTP-запрос несёт `Authorization: Bearer <token>` в заголовке. WebSocket
не поддерживает кастомные заголовки при начальном handshake в браузере. Поэтому
SignalR принял конвенцию: **токен передаётся как query-параметр** `?access_token=<jwt>`.

В `Program.cs` это обрабатывается через событие JwtBearer:

```csharp
// services/signaling-service/GraNAS.Signaling.API/Program.cs
options.Events = new JwtBearerEvents
{
    OnMessageReceived = ctx =>
    {
        var accessToken = ctx.Request.Query["access_token"];
        var path = ctx.HttpContext.Request.Path;
        // Читаем токен из query string только для хаба
        if (!string.IsNullOrEmpty(accessToken) &&
            path.StartsWithSegments("/hubs/signaling"))
            ctx.Token = accessToken;
        return Task.CompletedTask;
    }
};
```

Когда браузер/desktop открывает WebSocket:
```
ws://localhost:8080/hubs/signaling?access_token=eyJhbGci...
```

Middleware JwtBearer вытаскивает токен, валидирует подпись, и заполняет
`HttpContext.User` — ровно так же, как для обычного HTTP-запроса.

**Важно:** query string виден в логах сервера и proxy. В production это допустимо,
потому что соединение уже зашифровано TLS (WSS). Никогда не передавайте токены
в query string по незашифрованному каналу.

### Анонимный доступ через share-token

Пользователь без аккаунта открывает share-ссылку `/s/<token>` в браузере. Он не
авторизован JWT, но всё равно может скачать файлы из папки. В этом случае:

1. Браузер подключается к хабу **без** `?access_token=` — соединение принимается,
   `Context.User.Identity.IsAuthenticated == false`
2. При вызове `RequestSession(folderId, shareToken)` передаётся оригинальный токен
   из URL
3. Хаб вызывает `AccessChecker.CheckShareTokenAsync(folderId, shareToken)` — который
   **хэширует токен SHA-256** и ищет его в sharing-service по хэшу. В БД токены
   хранятся только в виде хэшей (как пароли)
4. Если токен валиден, не отозван и не просрочен — P2P сессия разрешается

```csharp
// services/signaling-service/GraNAS.Signaling.Services/Implementations/AccessChecker.cs
public async Task<FolderAccessResult?> CheckShareTokenAsync(Guid folderId, string shareToken, ...)
{
    // SHA-256 токена перед запросом к sharing-service
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shareToken)))
                      .ToLowerInvariant();
    var info = await _sharing.GetShareByTokenHashAsync(hash, ct);
    if (info is null || info.Revoked || info.ExpiresAt < DateTime.UtcNow) return null;
    if (info.FolderId != folderId) return null; // токен от другой папки — отказ
    return new FolderAccessResult(info.FolderId, info.OwnerId, info.ScopePath);
}
```

### Хаб: SignalingHub

Расположен в `Hubs/SignalingHub.cs`. Наследует `Hub` — базовый класс SignalR.
Каждый подключившийся клиент получает уникальный `Context.ConnectionId` (UUID-строка).

#### Метод `JoinAsOwner(folderId)`

Вызывается **desktop-клиентом (owner)** при старте. Выполняет три вещи:

1. **Проверяет права**: вызывает `IAccessChecker.CheckJwtAccessAsync` → тот HTTP-запросом
   спрашивает metadata-service `GET /api/internal/folders/{id}/access?userId={uid}`.
   Если пользователь не является владельцем папки — выбрасывает `HubException`.

2. **Регистрирует в Redis**: `ISessionStore.RegisterOwnerAsync(folderId, connectionId)` →
   Redis: `SET signaling:owner:{folderId} {connectionId} EX 86400`

3. **Рассылает статус**: добавляет соединение в SignalR Group `folder:{folderId}` и
   рассылает всем в ней событие `OwnerOnlineStatusChanged(folderId, true)`. Все браузеры,
   которые сейчас смотрят эту папку, получат зелёный индикатор.

```csharp
public async Task JoinAsOwner(Guid folderId)
{
    // 1. Проверка прав
    var userId = GetUserId();
    var access = await _access.CheckJwtAccessAsync(folderId, userId);
    if (access is null || access.OwnerId != userId)
        throw new HubException("Not authorized as owner of this folder.");

    // 2. Регистрация в Redis
    await _sessions.RegisterOwnerAsync(folderId, Context.ConnectionId);
    TrackOwnerFolder(folderId); // in-memory HashSet в Context.Items для OnDisconnected

    // 3. Рассылка статуса в SignalR Group
    await Groups.AddToGroupAsync(Context.ConnectionId, $"folder:{folderId}");
    await Clients.Group($"folder:{folderId}")
        .SendAsync("OwnerOnlineStatusChanged", folderId, true);
}
```

#### Метод `WatchFolder(folderId)`

Вызывается **браузером** при открытии папки. Он не инициирует P2P, просто:
- Добавляет соединение в Group `folder:{folderId}`, чтобы получать обновления статуса
- Немедленно присылает текущий статус (есть ли owner сейчас онлайн)

Благодаря этому, если owner подключится позже, браузер автоматически получит событие
`OwnerOnlineStatusChanged` без polling.

#### Метод `RequestSession(folderId, shareToken?)`

Ключевой метод. Вызывается браузером когда пользователь нажал «Показать файлы».
Алгоритм:

```
1. Определить кто я: JWT-пользователь или анонимный с share-токеном
2. Проверить доступ через AccessChecker
3. Найти live-owner в Redis: GetOwnerDeviceIdAsync(folderId)
4. Если owner не онлайн → отправить Caller "OwnerOffline"
5. (Phase 8.5) Server-side binding guard:
   a. GetBoundDeviceIdAsync(folderId) — читает table_device_folders
   b. Если bound device != redis-owner:
      - bound device online → перенаправить IncomingPeerRequest на него
      - bound device offline → отправить "OwnerOffline"
6. RegisterSessionPair в Redis + отправить owner "IncomingPeerRequest"
```

`RegisterSessionPairAsync` создаёт двусторонние записи в Redis Sets:
```
SADD signaling:pair:{receiverConnId} {ownerConnId}  EX 3600
SADD signaling:pair:{ownerConnId}    {receiverConnId} EX 3600
```

Это позволяет в методах `SendOffer`/`SendAnswer`/`SendIceCandidate` проверять:
«а вправе ли этот connectionId общаться с тем connectionId?» через `IsValidSessionPairAsync`.
Без этой проверки любой подключённый клиент мог бы relay-ить пакеты кому угодно.

#### Метод `DenyPeerRequest(receiverConnectionId, folderId, reason)`

Вызывается **desktop owner-ом** когда он решает не открывать P2P-сессию
(например, обнаружил что папка привязана к другому устройству в `table_device_folders`).
Хаб проверяет валидность сессионной пары через `AssertValidSessionAsync` и форвардит
`AccessDenied(folderId, reason)` receiver-у. Owner вызывает этот метод вместо `SendOffer`.

Типичное значение `reason`: `"folder_bound_to_another_device"`.

#### Relay-методы: SendOffer / SendAnswer / SendIceCandidate

После `RequestSession` начинается WebRTC negotiation. Все SDP и ICE пакеты
проходят через хаб как relay (ретранслятор):

```
Owner → SendOffer(receiverConnId, sdp) → hub → Clients.Client(receiverConnId).Send("Offer")
Browser → SendAnswer(ownerConnId, sdp) → hub → Clients.Client(ownerConnId).Send("Answer")
```

Хаб не интерпретирует содержимое SDP — это для него просто строки. Он только
проверяет валидность пары через Redis перед relay:

```csharp
private async Task AssertValidSessionAsync(string targetConnectionId)
{
    if (!await _sessions.IsValidSessionPairAsync(Context.ConnectionId, targetConnectionId))
        throw new HubException("Invalid or expired session.");
}
```

#### OnDisconnectedAsync

Когда desktop-клиент закрывается или теряет сеть, SignalR вызывает `OnDisconnectedAsync`.
Хаб читает из `Context.Items` (in-memory хранилище, привязанное к конкретному
connection) список папок, для которых этот клиент был owner, удаляет их из Redis
и рассылает `OwnerOnlineStatusChanged(folderId, false)` всем браузерам.

```csharp
public override async Task OnDisconnectedAsync(Exception? exception)
{
    foreach (var folderId in GetOwnerFolders())
    {
        await _sessions.RemoveOwnerAsync(folderId, Context.ConnectionId);
        await Clients.Group($"folder:{folderId}")
            .SendAsync("OwnerOnlineStatusChanged", folderId, false);
    }
    await _sessions.RemoveConnectionAsync(Context.ConnectionId); // очистка из пар сессий
    await base.OnDisconnectedAsync(exception);
}
```

---

## 5. Redis: зачем он здесь

Redis используется в двух ролях:

### 5.1 Хранение состояния сессий

Signaling-service — stateless сервис с состоянием вынесенным в Redis. Это позволяет
запустить несколько инстансов сервиса (горизонтальное масштабирование) — они все
будут видеть один и тот же Redis и корректно работать.

**Схема ключей:**

```
signaling:owner:{folderId}       → STRING: connectionId owner-а           TTL: 24ч
signaling:pair:{connectionId}    → SET: connectionId-пиров               TTL: 1ч
```

`RedisSessionStore` (`Services/Implementations/RedisSessionStore.cs`) — тонкая
обёртка над StackExchange.Redis, реализующая `ISessionStore`. Для хранения
пар сессий используются **Redis Sets** — это правильнее чем String, потому что
один owner может быть в паре с несколькими receivers одновременно:

```csharp
// Регистрация пары: добавляем connId в Set друг друга
var batch = _db.CreateBatch();
_ = batch.SetAddAsync(PairKey(receiverConnId), ownerConnId);
_ = batch.SetAddAsync(PairKey(ownerConnId), receiverConnId);
batch.Execute();
```

Проверка: `SISMEMBER signaling:pair:{connA} {connB}` — O(1) операция.

### 5.2 SignalR Redis Backplane

SignalR Groups работают в памяти конкретного инстанса. Если у вас два инстанса
signaling-service, и браузер подключён к инстансу 1, а owner к инстансу 2 — без
backplane они не смогут общаться через Groups.

Redis Backplane решает это: SignalR публикует все сообщения в Redis pub/sub канал,
все инстансы подписаны на него и доставляют сообщения своим клиентам.

Настройка в `Program.cs`:
```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnectionString, opts =>
        opts.Configuration.ChannelPrefix = RedisChannel.Literal("signaling"));
```

Ключи backplane имеют префикс `signaling:`, что отделяет их от ключей сессий.

---

## 6. ICE, STUN и TURN: как пробить NAT

После обмена SDP оба пира начинают **сбор ICE-кандидатов** — список возможных
адресов через которые они доступны. Существует три типа кандидатов:

### host-кандидат

Прямой IP-адрес и порт сетевого интерфейса. Работает только если оба клиента
в одной сети (например, корпоративный LAN).

```
candidate:1 1 UDP 2122252543 192.168.1.10 54321 typ host
```

### srflx-кандидат (Server Reflexive)

Внешний IP и порт, которые видит STUN-сервер. Позволяет пирам за NAT узнать
свой публичный адрес. Работает через STUN-сервер — он бесплатный и лёгкий.

```
candidate:2 1 UDP 1686052607 1.2.3.4 54321 typ srflx raddr 192.168.1.10 rport 54321
```

Процесс: клиент шлёт UDP пакет на STUN-сервер (`stun:stun.l.google.com:19302`),
тот отвечает «ваш внешний адрес X.X.X.X:PORT». Клиент добавляет это как srflx-кандидат.

**Работает** при большинстве home NAT (cone NAT). **Не работает** при symmetric NAT
(корпоративные сети, CG-NAT у операторов).

### relay-кандидат

Адрес на TURN-сервере. TURN получает пакеты и пересылает их нужному клиенту.
Работает при любом NAT, но добавляет latency и нагружает TURN.

```
candidate:3 1 UDP 33562367 5.6.7.8 49200 typ relay raddr 5.6.7.8 rport 49200
```

### Приоритеты и выбор

ICE проверяет все пары кандидатов и выбирает лучшую работающую. Приоритет:
`host > srflx > relay`. В логах signaling-service тип кандидата записывается:

```csharp
// SignalingHub.cs
private void LogIceCandidateType(string candidate)
{
    var typIdx = candidate.IndexOf(" typ ", StringComparison.Ordinal);
    if (typIdx >= 0) {
        var typ = candidate[(typIdx + 5)..].Split(' ')[0]; // "host", "srflx" или "relay"
        _logger.LogInformation("ICE candidate type={IceCandidateType}", typ);
    }
}
```

Это позволяет в логах видеть долю relay-соединений — индикатор качества NAT у пользователей.

### Trickle ICE

В GraNAS используется **Trickle ICE** — кандидаты отправляются по мере нахождения,
не дожидаясь полного сбора. Это ускоряет установку соединения. Каждый новый
кандидат сразу летит через SignalR хаб методом `SendIceCandidate`.

---

## 7. TURN credentials: временные ключи к relay

TURN сервер (coturn) не может быть публично открытым — иначе им воспользуются
для relay чужого трафика. Поэтому требуется аутентификация.

GraNAS использует **RFC 8489 TURN REST API** — механизм временных учётных данных
через HMAC-SHA1:

### Алгоритм генерации credentials

```csharp
// TurnCredentialService.cs
public TurnCredentials Generate(string userId)
{
    // 1. Время истечения = сейчас + TTL (600 секунд = 10 минут)
    var expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _ttl;

    // 2. Username = "{unix_timestamp}:{userId}"
    var username = $"{expiry}:{userId}";

    // 3. Credential = base64(HMAC-SHA1(secret, username))
    var credential = Convert.ToBase64String(
        HMACSHA1.HashData(_secret, Encoding.UTF8.GetBytes(username)));

    return new TurnCredentials(username, credential, _uris, _ttl);
}
```

Пример результата:
```json
{
  "username": "1714329600:550e8400-e29b-41d4-a716-446655440000",
  "credential": "base64encodedHMAC...",
  "uris": ["turn:granas.local:3478?transport=udp"],
  "ttl": 600
}
```

### Как coturn это проверяет

Coturn знает тот же `TURN_SECRET` (из env-переменной). Когда клиент подключается:
1. Coturn берёт `username`, вычисляет `HMAC-SHA1(secret, username)`
2. Сравнивает с `credential` из запроса клиента
3. Парсит timestamp из username, проверяет что не истёк
4. Только тогда разрешает relay

Credentials действительны **10 минут**. Desktop-клиент получает свежие credentials
при каждом подключении к хабу и при reconnect.

### Endpoint

```
GET /api/signaling/turn/credentials
Authorization: Bearer <jwt>
→ 200 TurnCredentialsResponse
→ 401 если нет JWT
```

Через YARP Gateway маршрут `/api/signaling/{**remainder}` → signaling-service
с трансформацией пути в `/api/{**remainder}` (без `/signaling/`).

---

## 8. Полный flow P2P-сессии: шаг за шагом

Рассмотрим полный сценарий: **user A** (desktop, owner) делится файлами с **user B** (браузер, receiver).

```
Desktop Owner (A)                SignalR Hub              Web Browser (B)
      │                               │                          │
      │──ConnectAsync()──────────────▶│                          │
      │  WS: /hubs/signaling?token=…  │                          │
      │◀─Connection established───────│                          │
      │                               │                          │
      │──JoinAsOwner(folderId)────────▶│                         │
      │  [hub: Redis SET owner]        │                         │
      │  [hub: Group.Add(folder:id)]   │                         │
      │◀──OK──────────────────────────│                          │
      │                               │                          │
      │                               │◀─WatchFolder(folderId)───│
      │                               │  [hub: Group.Add]        │
      │◀─OwnerOnlineStatusChanged(✓)──│──────────────────────────▶
      │  (owner тоже в Group!)        │  true → green indicator   │
      │                               │                          │
      │                               │◀─RequestSession(id,null)──│
      │                               │  [hub: проверяет доступ] │
      │                               │  [hub: Redis GET owner]  │
      │                               │  [hub: RegisterPair]     │
      │◀─IncomingPeerRequest──────────│                          │
      │  (receiverConnId, folderId)   │                          │
      │                               │                          │
      │  [owner: создаёт RTCPeerConnection]                       │
      │  [owner: createDataChannel("files")]                      │
      │  [owner: createOffer()]                                   │
      │  [owner: setLocalDescription]                             │
      │──SendOffer(receiverConnId, sdp)─▶│                       │
      │                               │──Offer(ownerConnId,sdp)──▶│
      │                               │                          │
      │                               │  [browser: new RTCPeerConnection]
      │                               │  [browser: setRemoteDescription(offer)]
      │                               │  [browser: createAnswer()]
      │                               │  [browser: setLocalDescription(answer)]
      │                               │◀─SendAnswer(ownerConnId, sdp)──│
      │◀─Answer(receiverConnId, sdp)──│                          │
      │  [owner: setRemoteDescription]│                          │
      │                               │                          │
      │  ════════ ICE Trickle (параллельно) ════════════════════ │
      │──SendIceCandidate(rcvr, host)─▶│──IceCandidate──────────▶│
      │◀─IceCandidate(srvflx)─────────│◀─SendIceCandidate────────│
      │  [оба: addIceCandidate()]     │                          │
      │                               │                          │
      │  ═══════════ WebRTC Established (прямое соединение) ════ │
      │◀═══════════════════════════════════════════════════════════▶
      │              (SignalR больше не нужен для данных)         │
      │                               │                          │
      │  [data channel "files" opens]                             │
      │◀───ecdh_offer{publicKey}══════════════════════════════════│
      │────ecdh_answer{publicKey}══════════════════════════════════▶
      │  [обе стороны derive AES-GCM key]                        │
      │◀───list_request══════════════════════════════════════════ │
      │────list_response{files:[…]}══════════════════════════════▶│
      │                               │                          │
      │◀───file_request{path}════════════════════════════════════ │
      │────file_header{sha256}════════════════════════════════════▶│
      │────[binary chunk 1]═══════════════════════════════════════▶│
      │────[binary chunk 2]═══════════════════════════════════════▶│
      │────...══════════════════════════════════════════════════ ▶│
      │────file_complete══════════════════════════════════════════▶│
      │                               │         [browser: verify SHA-256]
      │                               │         [browser: download blob]
```

**Важный момент:** после установки WebRTC-соединения, данные (файлы) передаются
**напрямую** между клиентами. SignalR хаб больше не участвует. TURN-сервер
участвует только если прямое P2P соединение невозможно (симметричный NAT).

---

## 9. Desktop-клиент: роль owner/sender

Desktop-клиент (Avalonia, `clients/desktop/`) реализован на .NET 10 с
**SIPSorcery** для WebRTC — единственной полноценной WebRTC-библиотекой на C#.

### P2PHost: сердце P2P на стороне owner

`Services/P2P/P2PHost.cs` — singleton-сервис, управляющий всем P2P.

#### Подключение к хабу (ConnectAsync)

```csharp
public async Task ConnectAsync(CancellationToken ct = default)
{
    // Получаем TURN credentials заранее
    _turnCredentials = await _signalingApi.GetTurnCredentialsAsync(ct);

    _hub = new HubConnectionBuilder()
        .WithUrl(_hubUrl, opts => {
            // Lambda: каждый раз берёт актуальный access-token (после refresh)
            opts.AccessTokenProvider = () =>
                Task.FromResult<string?>(_session.AccessToken);
            opts.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        })
        .WithAutomaticReconnect() // автоматически переподключается при обрыве
        .Build();

    // Подписываемся на события ДО StartAsync
    _hub.On<string, Guid, string?>("IncomingPeerRequest", HandleIncomingPeerRequestAsync);
    _hub.On<string, string>("Answer", HandleAnswerAsync);
    _hub.On<string, string, string?, int?>("IceCandidate", HandleIceCandidateAsync);

    // После reconnect: обновляем TURN и перевходим в папки
    _hub.Reconnected += async _ => {
        _turnCredentials = await _signalingApi.GetTurnCredentialsAsync();
        await JoinAllFoldersAsync();
    };

    await _hub.StartAsync(ct);
    _isOnline = true;
    await JoinAllFoldersAsync(ct); // JoinAsOwner для каждой привязанной папки
}
```

#### Обработка IncomingPeerRequest

Когда браузер вызвал `RequestSession`, хаб шлёт owner-у `IncomingPeerRequest`.
`HandleIncomingPeerRequestAsync` (async-void обёртка над `HandleIncomingPeerRequestCoreAsync`) выполняет:

1. **Локальная проверка** — `GetLocalPath(folderId)` + `Directory.Exists` (папка физически есть на этом компьютере).
2. **Desktop binding guard (Phase 8.5)** — запрос к `ISignalingApi.GetFolderDevicesAsync([folderId])`. Результат кэшируется в `_bindingCache` (очищается при `DisconnectAsync` и по событию `IFolderShareRegistry.MappingChanged`). Если binding существует и `DeviceId != DeviceIdentity.DeviceId` — вызывает `DenyPeerRequest` через хаб и выходит. Если binding отсутствует — разрешает (обратная совместимость с папками до Phase 6.5).
3. **WebRTC negotiation** — через виртуальный метод `StartWebRtcSessionAsync`: создаёт `PeerSession`, `RTCPeerConnection`, data channel `"files"`, генерирует SDP-offer и отправляет через хаб.

```csharp
// Phase 8.5: Desktop binding guard
private async Task<Guid?> ResolveBoundDeviceIdAsync(Guid folderId)
{
    if (_bindingCache.TryGetValue(folderId, out var cached)) return cached;
    var resp = await _signalingApi.GetFolderDevicesAsync(new[] { folderId });
    var deviceId = resp.FirstOrDefault(r => r.FolderId == folderId)?.DeviceId;
    _bindingCache[folderId] = deviceId; // null = «нет binding» — тоже кэшируем
    return deviceId;
}
```

Методы `SendDenyAsync` и `StartWebRtcSessionAsync` объявлены `protected internal virtual` — это позволяет unit-тестам переопределить их в subclass без реального SignalR/SIPSorcery.

**SIPSorcery API** отличается от браузерного: `createOffer()` и
`setLocalDescription()` — синхронные (не `async`/`await`).
`createDataChannel()` — асинхронный, возвращает `Task<RTCDataChannel>`.

#### FolderShareRegistry

`FolderShareRegistry` хранит маппинг `folderId → localPath` в JSON-файле
`%LocalAppData%/GraNAS/folder-mappings.json`. Пользователь через UI нажимает
«📁 P2P» и выбирает папку через системный диалог `StorageProvider.OpenFolderPickerAsync()`.

При `JoinAllFoldersAsync()` — хаб вызывается только для папок, у которых есть
привязанная локальная директория.

#### Toggle online/offline

В ShellWindow есть кнопка «🟢 Online / ⚪ Offline». Она вызывает
`OnlineToggleCommand` в `ShellViewModel`:

```csharp
private async Task ToggleOnlineAsync()
{
    if (_p2pHost.IsOnline) {
        _p2pHost.ShouldBeOnline = false;
        await _p2pHost.DisconnectAsync(); // LeaveAsOwner + StopAsync
        IsOnline = false;
    } else {
        _p2pHost.ShouldBeOnline = true;
        await ConnectP2PAsync();
        IsOnline = _p2pHost.IsOnline;
    }
}
```

При `DisconnectAsync()` хаб отключается, `OnDisconnectedAsync` на сервере
автоматически удаляет owner из Redis и рассылает offline-статус браузерам.

---

## 10. Web-клиент: роль receiver

Web-клиент (React + TypeScript, `clients/web/`) использует нативный браузерный
`RTCPeerConnection` и `@microsoft/signalr`.

### P2PSession factory

Вместо класса с `private` полями (запрещено `erasableSyntaxOnly` в TypeScript 6)
используется **factory function** — функция, которая возвращает объект с методами,
замыкая (closure) приватное состояние:

```typescript
// clients/web/src/p2p/P2PSession.ts
export function createP2PSession(
  folderId: string,
  shareToken: string | undefined,
  turnCredentials: TurnCredentials | null,
  callbacks: P2PSessionCallbacks,
): P2PSession {
  // Приватное состояние в closure
  const hub = createHubConnection();
  let pc: RTCPeerConnection | null = null;
  let dc: RTCDataChannel | null = null;
  let ecdhKeyPair: CryptoKeyPair | null = null;
  let aesKey: CryptoKey | null = null;
  let downloadState: DownloadState | null = null;

  // ... приватные функции ...

  return { connect, requestFiles, downloadFile, disconnect };
}
```

### Подключение к хабу (connect)

```typescript
async function connect() {
  callbacks.onStatusChange('connecting');

  // Подписываемся на события от хаба
  hub.on('Offer', (senderConnId, sdp) => void handleOffer(senderConnId, sdp));
  hub.on('IceCandidate', (_, candidate, sdpMid, sdpMLineIndex) =>
    handleIceCandidate(candidate, sdpMid, sdpMLineIndex));

  await hub.start(); // WebSocket соединение установлено
  await hub.invoke('WatchFolder', folderId);    // подписка на статус owner-а
  await requestSession();                        // запрос P2P сессии
}
```

### Обработка SDP offer (handleOffer)

```typescript
async function handleOffer(senderConnId: string, sdp: string) {
  // ICE servers: STUN (бесплатный) + TURN (с нашими credentials)
  const iceServers: RTCIceServer[] = [{ urls: 'stun:stun.l.google.com:19302' }];
  if (turnCredentials) {
    for (const uri of turnCredentials.uris) {
      iceServers.push({
        urls: uri,
        username: turnCredentials.username,
        credential: turnCredentials.credential,
      });
    }
  }

  pc = new RTCPeerConnection({ iceServers });

  // Свои ICE кандидаты → через хаб к owner-у
  pc.onicecandidate = async (e) => {
    if (!e.candidate) return;
    await hub.invoke('SendIceCandidate',
      senderConnId, e.candidate.candidate, e.candidate.sdpMid, e.candidate.sdpMLineIndex);
  };

  // Data channel создан owner-ом, мы получаем его через событие
  pc.ondatachannel = (e) => { attachDataChannel(e.channel); };

  // Стандартный WebRTC handshake: setRemote → createAnswer → setLocal → sendAnswer
  await pc.setRemoteDescription({ type: 'offer', sdp });
  const answer = await pc.createAnswer();
  await pc.setLocalDescription(answer);
  await hub.invoke('SendAnswer', senderConnId, answer.sdp ?? '');
}
```

### Жизненный цикл Data Channel (attachDataChannel)

Owner создаёт `dc = pc.createDataChannel("files")`. Browser получает его через
`pc.ondatachannel`. После этого SignalR хаб больше не нужен для передачи данных.

```typescript
function attachDataChannel(channel: RTCDataChannel) {
  dc = channel;
  dc.binaryType = 'arraybuffer'; // получаем бинарные данные как ArrayBuffer
  dc.onopen = () => void initiateEcdh(); // канал открылся → начинаем ECDH
  dc.onmessage = (e) => {
    if (typeof e.data === 'string')
      handleTextMessage(e.data);   // JSON управляющие сообщения
    else
      void handleBinaryChunk(new Uint8Array(e.data)); // зашифрованные чанки
  };
}
```

### useOwnerOnlineStatus hook

В React-компонентах статус владельца отображается через хук:

```typescript
// clients/web/src/features/p2p/useOwnerOnlineStatus.ts
export function useOwnerOnlineStatus(folderId: string | undefined): OwnerStatus {
  const [status, setStatus] = useState<OwnerStatus>('unknown');

  useEffect(() => {
    if (!folderId) return;
    const hub = createHubConnection();
    hub.on('OwnerOnlineStatusChanged', (id, isOnline) => {
      if (id === folderId) setStatus(isOnline ? 'online' : 'offline');
    });
    hub.start()
      .then(() => hub.invoke('WatchFolder', folderId))
      .catch(() => setStatus('unknown'));
    return () => { void hub.stop(); }; // cleanup при unmount
  }, [folderId]);

  return status;
}
```

Каждый компонент, использующий этот хук, поддерживает **независимое** SignalR
соединение. Это дублирование, но упрощает код. В продакшне оптимизация —
вынести соединение в React Context и переиспользовать.

---

## 11. Data channel протокол: что летит по каналу

После установки WebRTC соединения оба клиента общаются через Data Channel.
Протокол — JSON для управляющих сообщений, бинарные фреймы для данных.

### Этапы протокола

```
┌─────────────────────────────────────────────────────────────────────┐
│ Фаза 1: ECDH Key Exchange                                           │
│                                                                     │
│  Browser → { "type": "ecdh_offer", "publicKey": "<spki base64>" }  │
│  Desktop ← { "type": "ecdh_answer", "publicKey": "<spki base64>" } │
│  [обе стороны вычисляют общий AES-GCM ключ]                        │
├─────────────────────────────────────────────────────────────────────┤
│ Фаза 2: Список файлов                                               │
│                                                                     │
│  Browser → { "type": "list_request" }                               │
│  Desktop ← { "type": "list_response",                               │
│               "files": [{ "path": "doc.pdf", "size": 1024,          │
│                           "modifiedAt": "2026-01-01T..." }] }       │
├─────────────────────────────────────────────────────────────────────┤
│ Фаза 3: Скачивание файла                                            │
│                                                                     │
│  Browser → { "type": "file_request", "path": "doc.pdf" }           │
│  Desktop ← { "type": "file_header",                                 │
│               "path": "doc.pdf", "size": 1024,                      │
│               "sha256": "<hex>", "iv": "" }                         │
│  Desktop ← [binary: nonce(12)+ciphertext+tag(16)]  ← чанк 1       │
│  Desktop ← [binary: nonce(12)+ciphertext+tag(16)]  ← чанк 2       │
│  ...                                                                │
│  Desktop ← { "type": "file_complete", "path": "doc.pdf" }          │
│  [Browser проверяет SHA-256 всего файла]                            │
└─────────────────────────────────────────────────────────────────────┘
```

### Формат бинарного чанка

Каждый чанк самодостаточен: содержит свой nonce (IV) для AES-GCM.
Это критично: **нельзя использовать один IV для нескольких блоков шифрования**
с одним ключом. Каждый чанк получает новый случайный nonce:

```
Байты 0..11   (12 bytes) → nonce (криптографически случайный, 96-bit)
Байты 12..N-17            → зашифрованный ciphertext
Байты N-16..N (16 bytes) → authentication tag (GCM-тег целостности)
```

### Path traversal защита

Desktop-клиент валидирует запрошенный путь перед чтением файла:

```csharp
// P2PHost.cs
var safePath = Path.GetFullPath(Path.Combine(session.LocalPath, relativePath));
if (!safePath.StartsWith(session.LocalPath, StringComparison.OrdinalIgnoreCase))
{
    SendText(session, ProtocolSerializer.Serialize(
        new DataChannelErrorMessage(ProtocolMessageType.Error, "FORBIDDEN",
            "Path traversal denied.")));
    return;
}
```

`Path.GetFullPath()` разворачивает `../../../etc/passwd` в абсолютный путь.
Если он не начинается с `session.LocalPath` — запрос отклоняется.

### ScopePath: гранулярный доступ

`FolderAccessResult.ScopePath` — необязательная строка из permission или share_link.
Если задана, desktop фильтрует список файлов и разрешает скачивать только
файлы внутри `localPath/{scopePath}/`.

Пример: user A дал user B доступ только к подпапке `reports/q1/`:
```
ScopePath = "reports/q1"
searchRoot = /home/user/SharedFolder/reports/q1/
```

---

## 12. Шифрование: ECDH + AES-GCM

GraNAS использует **двухуровневое шифрование**:

```
Уровень 1: DTLS (автоматически WebRTC)
Уровень 2: App-level ECDH + AES-GCM (наш код)
```

Даже если TURN-сервер будет скомпрометирован и дампнет все пакеты, содержимое
файлов будет зашифровано ключом, который сервер никогда не видел.

### ECDH (Elliptic-Curve Diffie-Hellman)

Протокол для безопасного согласования общего секрета по открытому каналу.
Используется кривая **P-256** (также NIST P-256, secp256r1).

**Идея**: у каждой стороны своя пара ключей (приватный + публичный).
Обмениваются публичными ключами открыто. Каждая сторона независимо вычисляет
один и тот же **общий секрет** (shared secret) из своего приватного и чужого публичного.
Подслушивающий видит только публичные ключи — вычислить shared secret без приватного
математически невозможно (ECDLP).

**Браузер** (ecdhUtils.ts):
```typescript
// Генерация пары ключей
const keyPair = await crypto.subtle.generateKey(
  { name: 'ECDH', namedCurve: 'P-256' },
  false,          // приватный ключ не экспортируется из WebCrypto
  ['deriveBits'],
);

// Экспорт публичного ключа в формате SPKI → base64 → через Data Channel
const spki = await crypto.subtle.exportKey('spki', keyPair.publicKey);
const pubKeyB64 = btoa(String.fromCharCode(...new Uint8Array(spki)));
```

**Desktop** (EcdhSession.cs):
```csharp
// System.Security.Cryptography.ECDiffieHellman — встроено в .NET
_ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

// Экспорт публичного ключа в формате SPKI (совместим с WebCrypto)
var spki = _ecdh.PublicKey.ExportSubjectPublicKeyInfo();
return Convert.ToBase64String(spki);

// Импорт публичного ключа от browser и derive shared secret
using var peerEcdh = ECDiffieHellman.Create();
peerEcdh.ImportSubjectPublicKeyInfo(peerKeyBytes, out _);
var sharedSecret = _ecdh.DeriveKeyMaterial(peerEcdh.PublicKey);
```

**SPKI** (Subject Public Key Info) — стандартный ASN.1 формат для публичных ключей,
понятный и браузеру (`WebCrypto`), и .NET (`ExportSubjectPublicKeyInfo`).

### HKDF: из shared secret в AES-ключ

`DeriveKeyMaterial()` возвращает 256-bit (32 байта) сырого shared secret.
Прямо использовать его как AES-ключ — плохая практика. Применяется **HKDF**
(HMAC-based Key Derivation Function, RFC 5869) для получения криптографически
стойкого ключа:

```typescript
// Браузер: WebCrypto HKDF
const ikm = await crypto.subtle.importKey('raw', sharedBits, 'HKDF', false, ['deriveKey']);
const aesKey = await crypto.subtle.deriveKey(
  { name: 'HKDF', hash: 'SHA-256', salt: new Uint8Array(0), info: new Uint8Array(0) },
  ikm,
  { name: 'AES-GCM', length: 256 },
  false,       // ключ не экспортируется из WebCrypto
  ['decrypt'], // браузер только дешифрует
);
```

```csharp
// Desktop: System.Security.Cryptography.HKDF (.NET 5+)
var aesKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret,
    outputLength: 32,     // 256-bit AES
    salt: null,
    info: null);
_aesGcm = new AesGcm(aesKey, tagSizeInBytes: 16);
```

### AES-GCM: шифрование чанков

**AES-GCM** (Galois/Counter Mode) — режим шифрования, который обеспечивает
одновременно **конфиденциальность** (ciphertext непонятен без ключа) и
**аутентичность** (если чанк изменён — дешифрование упадёт, authentication tag
не совпадёт). Это так называемое **AEAD** шифрование.

Desktop шифрует каждый чанк:
```csharp
public byte[] Encrypt(byte[] plaintext)
{
    var nonce = new byte[12];              // 96-bit nonce
    RandomNumberGenerator.Fill(nonce);     // криптографически случайный!
    var ciphertext = new byte[plaintext.Length];
    var tag = new byte[16];               // 128-bit authentication tag

    _aesGcm!.Encrypt(nonce, plaintext, ciphertext, tag);

    // Упаковываем всё в один массив: nonce + ciphertext + tag
    var result = new byte[12 + plaintext.Length + 16];
    nonce.CopyTo(result, 0);
    ciphertext.CopyTo(result, 12);
    tag.CopyTo(result, 12 + plaintext.Length);
    return result;
}
```

Browser дешифрует:
```typescript
async function decryptChunk(aesKey: CryptoKey, packed: Uint8Array): Promise<Uint8Array> {
  const nonce = packed.slice(0, 12);
  // SubtleCrypto ждёт ciphertext||tag вместе (tag — последние 16 байт)
  const ciphertextWithTag = packed.slice(12);
  const plaintext = await crypto.subtle.decrypt(
    { name: 'AES-GCM', iv: nonce, tagLength: 128 }, // 128-bit tag
    aesKey,
    ciphertextWithTag,
  );
  return new Uint8Array(plaintext);
}
```

### Почему разные nonce для каждого чанка?

AES-GCM **критически требует** уникального nonce для каждого блока шифрования
с одним ключом. Повторное использование nonce полностью компрометирует
конфиденциальность — атакующий может восстановить ключ. Поэтому для каждого
64KB чанка генерируется новый случайный 96-bit nonce.

96 bits = 12 bytes. Вероятность коллизии при 2^32 чанках ≈ 2^-33 — приемлемо.

### Итоговая SHA-256 верификация

После получения всех чанков браузер собирает файл и проверяет его целостность:

```typescript
const actualHash = await sha256Hex(combined);
if (actualHash !== state.sha256) {
  callbacks.onError(`SHA-256 mismatch for ${state.path}. File may be corrupted.`);
}
```

SHA-256 вычислена **от оригинального расшифрованного контента** и передана в
`file_header` ещё до шифрования. Это защищает от:
- Случайного повреждения данных в сети
- Атаки на data channel (подмена чанков)

---

## 13. Безопасность: что сервер не может прочитать

### Что видит signaling-service

| Данные | Видит ли сервер |
|---|---|
| Имена папок, права доступа | ✅ Да (это его работа) |
| SDP-оффер/ответ | ✅ Да (relay, но это только параметры соединения) |
| ICE-кандидаты (IP-адреса клиентов) | ✅ Да |
| Содержимое файлов | ❌ Нет |
| ECDH приватные ключи | ❌ Нет (генерируются на клиентах) |
| AES-GCM ключ шифрования | ❌ Нет (derive происходит локально) |

### Что видит TURN-сервер

При relay через coturn, пакеты проходят через сервер. Coturn видит:

| Данные | Видит ли TURN |
|---|---|
| IP-адреса клиентов | ✅ Да |
| Объём трафика | ✅ Да |
| Зашифрованные DTLS пакеты | ✅ Да (но расшифровать не может) |
| Содержимое файлов | ❌ Нет (DTLS + AES-GCM) |

DTLS шифрует транспортный уровень. Поверх DTLS ещё AES-GCM. У TURN-сервера
нет DTLS-ключей (они генерируются пирами и никогда не покидают их).

### Что видит оператор браузера/ISP

Всё зашифровано DTLS. Пакеты неразличимы от обычного HTTPS-трафика.

### Эфемерные ключи

ECDH ключи генерируются заново для каждой сессии. Если AES-GCM ключ будет
скомпрометирован в будущем — прошлые сессии не расшифровываются (Perfect
Forward Secrecy — PFS). Это стандарт для современных протоколов (TLS 1.3,
Signal Protocol).

---

## 14. Debugging & мониторинг

### Логи signaling-service

Ключевые строки в логах:

```
[INF] Owner {userId} joined for folder {folderId} (conn {connId})
[INF] Session requested: receiver {rcvr} ↔ owner {owner} for folder {folder}
[INF] ICE candidate type=host|srflx|relay from {connId}
[INF] Connection {connId} disconnected
```

Тип ICE кандидата (host/srflx/relay) показывает качество P2P соединений.
Высокая доля `relay` = клиенты за жёстким NAT.

### Проверка TURN

```bash
# Генерируем временные credentials вручную (bash + openssl)
SECRET="dev_only_change_me_min_32_chars_turn_secret_value"
EXPIRY=$(( $(date +%s) + 600 ))
USERNAME="${EXPIRY}:test-user"
CREDENTIAL=$(echo -n "$USERNAME" | openssl dgst -sha1 -hmac "$SECRET" -binary | base64)

# Проверяем relay через turnutils (если установлен)
turnutils_uclient -v -t \
    -u "$USERNAME" -w "$CREDENTIAL" \
    localhost
```

### Проверка хаба (wscat)

```bash
npm install -g wscat

# Получаем JWT (через API gateway)
TOKEN=$(curl -s -X POST http://localhost:8080/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"test@test.com","password":"password"}' \
    | jq -r '.access_token')

# Подключаемся к хабу
wscat -c "ws://localhost:8080/hubs/signaling?access_token=$TOKEN"
# После подключения видим: Connected (press CTRL+C to quit)
```

### Redis: ручная проверка состояния

```bash
redis-cli -n 2  # база данных 2 — сигнализация

# Посмотреть кто owner конкретной папки
GET signaling:owner:550e8400-e29b-41d4-a716-446655440000

# Посмотреть все owner-ы
KEYS signaling:owner:*

# Посмотреть пары сессий
KEYS signaling:pair:*

# Сколько участников в группе папки (backplane)
# (ключи SignalR backplane имеют префикс signaling:)
KEYS signaling:*
```

### Chrome DevTools для P2P

В браузере откройте `chrome://webrtc-internals/` — детальная статистика всех
RTCPeerConnection: ICE candidates, DTLS state, Data Channel stats, bandwidth.

Видно конкретный тип соединения (host/srflx/relay) и throughput.

### Типичные проблемы

| Симптом | Причина | Решение |
|---|---|---|
| `OwnerOffline` сразу | Desktop не подключён к хабу | Проверить `IsOnline` в UI, логи P2PHost |
| Соединение не устанавливается | Нет TURN relay | Проверить coturn запущен, credentials валидны |
| Все соединения type=relay | Жёсткий NAT | Норма, но latency выше |
| SHA-256 mismatch | Corrupted данные | Редко; переподключение и retry |
| `HubException: Invalid session` | TTL сессии истёк (1ч) | Переподключение к хабу |
| `AccessDenied: folder_bound_to_another_device` | Desktop guard: папка привязана к другому устройству в `table_device_folders` | Открыть правильное устройство или перепривязать через Bind Local Folder с `?force=true` |
| `AccessDenied` (другие причины) | Права изменились после подключения | Reload страницы |

---

## Приложение: быстрая шпаргалка

```
SIGNALING FLOW (упрощённо):
  Desktop: ConnectAsync() → JoinAsOwner(folderId) → ждём IncomingPeerRequest
  Browser: WatchFolder() → RequestSession() → ждём Offer
  После обмена SDP/ICE → WebRTC established → SignalR не нужен

DATA CHANNEL FLOW:
  Browser (receiver): открыт dc → ecdh_offer → ждём ecdh_answer
  Desktop (owner): ecdh_answer → ждём list_request
  Browser: list_request → ждём list_response → можно скачивать
  Browser: file_request → ждём file_header → бинарные чанки → file_complete
  Browser: SHA-256 verify → если ok → download blob

ШИФРОВАНИЕ:
  ECDH P-256: обмен публичными ключами → shared secret
  HKDF SHA-256: shared secret → 256-bit AES-GCM key
  AES-GCM: каждый 64KB чанк → nonce(12) + ciphertext + tag(16)
  Receiver: split → decrypt → verify SHA-256 всего файла

КЛЮЧЕВЫЕ ФАЙЛЫ:
  signaling-service/GraNAS.Signaling.API/Hubs/SignalingHub.cs  (hub logic)
  signaling-service/GraNAS.Signaling.Services/Implementations/ (AccessChecker, Redis, TURN)
  clients/desktop/.../Services/P2P/P2PHost.cs                  (SIPSorcery, sender)
  clients/desktop/.../Services/P2P/EcdhSession.cs              (.NET ECDH + AES-GCM)
  clients/web/src/p2p/P2PSession.ts                            (browser receiver)
  clients/web/src/p2p/ecdhUtils.ts                             (WebCrypto ECDH + AES-GCM)
```

---

*Документ обновлён для GraNAS Phase 8.5 (device-folder binding guard). Версия от 2026-05-11.*
