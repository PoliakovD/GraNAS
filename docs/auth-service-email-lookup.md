# Backend Plan: Owner Email Lookup — отображение email в SharedPage / Inspector / HomePage

## Проблема

`FolderResponse.ownerId` — это UUID. SharedPage, Inspector (вкладка «О папке») и HomePage
показывают обрезанный ID (`a1b2c3d4…`) вместо email, что неудобно пользователям.

Текущее состояние:
- `PermissionResponse` уже хранит `email` (он передаётся при grant и сохраняется в кэше React Query) ✅
- `FolderResponse` — только `ownerId`, email нет ❌
- auth-service хранит `email` в таблице `users`

---

## Рекомендуемый подход: batch-lookup в auth-service + denormalization в metadata-service

### Вариант A (рекомендуется): обогатить FolderResponse на уровне metadata-service

metadata-service при формировании `FolderResponse[]` запрашивает у auth-service email-ы владельцев
батчем. Клиент получает готовые данные.

**Плюсы:** клиент ничего не меняет, данные всегда актуальны.  
**Минусы:** межсервисный запрос на каждый `GET /api/folders`.

### Вариант B: frontend сам запрашивает batch lookup

Клиент делает `GET /api/auth/users/batch?ids=uuid1,uuid2` и собирает map.

**Плюсы:** metadata-service не трогаем.  
**Минусы:** ещё один RTT, сложнее кэш.

**Выбор: Вариант A** — единая точка ответственности.

---

## Реализация Варианта A

### 1. auth-service — новый batch endpoint

**Файл:** `services/auth-service/GraNAS.Auth.API/Controllers/UsersController.cs` (создать)

```csharp
[ApiController]
[Route("api/auth/users")]
public class UsersController : ControllerBase
{
    [HttpGet("batch")]
    [Authorize]
    public async Task<IActionResult> GetBatch(
        [FromQuery] IEnumerable<Guid> ids,
        CancellationToken ct)
    {
        if (!ids.Any()) return Ok(Array.Empty<object>());
        var users = await _userRepo.GetByIdsAsync(ids.Distinct().Take(200), ct);
        return Ok(users.Select(u => new { u.Id, u.Email }));
    }
}
```

**Response:**
```json
[
  { "id": "uuid", "email": "user@example.com" }
]
```

**Repository** (`IUserRepository` / `UserRepository.cs`):
```csharp
Task<IEnumerable<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);
// impl: WHERE id = ANY(@ids)
```

Endpoint закрыт `[Authorize]`. Rate-limit: попадает в существующую политику `api`.

---

### 2. IAuthServiceClient в metadata-service

**Файл:** `services/metadata-service/GraNAS.Metadata.BL/Clients/IAuthServiceClient.cs`
(уже существует для Phase 2 permissions)

Добавить метод:
```csharp
Task<Dictionary<Guid, string>> GetUserEmailsAsync(
    IEnumerable<Guid> userIds, CancellationToken ct);
```

**Реализация** (`AuthServiceClient.cs`):
```csharp
public async Task<Dictionary<Guid, string>> GetUserEmailsAsync(
    IEnumerable<Guid> userIds, CancellationToken ct)
{
    var ids = userIds.Distinct().ToList();
    if (!ids.Any()) return new();
    var query = string.Join("&", ids.Select(id => $"ids={id}"));
    var resp = await _httpClient.GetFromJsonAsync<UserEmailDto[]>(
        $"/api/auth/users/batch?{query}", ct);
    return resp?.ToDictionary(u => u.Id, u => u.Email) ?? new();
}

record UserEmailDto(Guid Id, string Email);
```

---

### 3. FolderResponse — добавить ownerEmail

**DTO** (`GraNAS.Metadata.Models/DTO/FolderResponse.cs`):
```csharp
public string? OwnerEmail { get; set; }
```

**FolderService.GetUserFoldersAsync**:
```csharp
var folders = await _folderRepo.GetUserFoldersAsync(userId, ct);
// batch-fetch owner emails
var ownerIds = folders.Select(f => f.OwnerId).Distinct();
var emails = await _authClient.GetUserEmailsAsync(ownerIds, ct);
return folders.Select(f => new FolderResponse
{
    // ...existing mapping...
    OwnerEmail = emails.GetValueOrDefault(f.OwnerId),
});
```

---

### 4. Frontend

**Type** (`clients/web/src/types/folder.ts`):
```ts
export interface FolderResponse {
  // ...existing
  ownerEmail: string | null;  // новое поле
}
```

**SharedPage** (`clients/web/src/pages/SharedPage.tsx`):
```ts
// вместо truncated ownerId:
<div>{f.ownerEmail ?? f.ownerId.slice(0, 8) + '…'}</div>
```

**Inspector** — вкладка «Свойства», поле "Владелец":
```ts
<div style={{ display: 'flex', justifyContent: 'space-between' }}>
  <span style={{ color: 'var(--ink-500)' }}>Владелец</span>
  <span>{folder.ownerEmail ?? folder.ownerId.slice(0, 8) + '…'}</span>
</div>
```

**FolderDetailPage** — page-sub строка:
```ts
{isOwner ? 'Владелец: вы' : `Владелец: ${folder.ownerEmail ?? folder.ownerId.slice(0, 8) + '…'}`}
```

**HomePage** — stat-карточки группировки не требуют email, но SharedPage с email лучше.

---

## Кэширование и производительность

- auth-service добавить `ResponseCache` для `/api/auth/users/batch` на 5 минут (email меняется редко).
- Или metadata-service кэшировать `ownerEmails` в `IMemoryCache` с TTL 5 мин.
- Лимит: `Take(200)` уникальных ID в одном batch-запросе.

---

## Тесты

**auth-service.tests:**
- `GET /api/auth/users/batch?ids=...` → 200 + массив `{id, email}`
- Неизвестные id → игнорируются (нет 404 для отдельных)
- `ids=` empty → 200 + пустой массив
- 401 без токена

**metadata-service.tests:**
- `GET /api/metadata/folders` → поле `ownerEmail` не null для owned folders
- Мок `IAuthServiceClient.GetUserEmailsAsync` возвращает email

---

## Порядок реализации

1. auth-service: `GetByIdsAsync` в репозитории + `GET /api/auth/users/batch` в контроллере
2. metadata-service: метод в `IAuthServiceClient` + `GetUserEmailsAsync`
3. metadata-service: поле `OwnerEmail` в DTO + маппинг в FolderService
4. Frontend: обновить тип + 3 места отображения (SharedPage, Inspector, FolderDetailPage)
5. Тесты обоих сервисов

**Оценка:** ~2 дня (межсервисная интеграция, но auth-client уже настроен в Phase 2).
