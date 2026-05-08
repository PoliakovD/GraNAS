# Backend Plan: GET /api/share-links — Global Share Links Listing

## Цель

Новый эндпоинт `GET /api/share-links` возвращает все share-ссылки текущего авторизованного пользователя
по всем его папкам. Нужен для страницы `/links` веб-клиента.

## Запрос

```
GET /api/share-links?activeOnly=true&take=200
Authorization: Bearer <jwt>
```

**Query params:**
- `activeOnly` (bool, default `true`) — если true, возвращает только не-отозванные и не-истёкшие ссылки
- `take` (int, default 200, max 500) — лимит на количество результатов

**Response: 200 OK**
```json
[
  {
    "id": "uuid",
    "folderId": "uuid",
    "folderName": "Название папки",
    "path": null,
    "expiresAt": "2026-06-01T00:00:00Z",
    "revoked": false,
    "createdAt": "2026-05-01T12:00:00Z",
    "openCount": 4
  }
]
```

## Новый DTO

Файл: `services/sharing-service/GraNAS.Sharing.Models/DTO/ShareLinkOwnerResponse.cs`

```csharp
public class ShareLinkOwnerResponse
{
    public Guid Id { get; set; }
    public Guid FolderId { get; set; }
    public string FolderName { get; set; } = string.Empty;
    public string? Path { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public DateTime CreatedAt { get; set; }
    public int OpenCount { get; set; }
}
```

## Repository

Файл: `services/sharing-service/GraNAS.Sharing.DAL/Repositories/Contracts/IShareLinkRepository.cs`

Добавить метод:
```csharp
Task<IEnumerable<ShareLink>> ListByOwnerAsync(
    Guid ownerId,
    bool activeOnly,
    int take,
    CancellationToken cancellationToken);
```

Файл: `services/sharing-service/GraNAS.Sharing.DAL/Repositories/Implementation/ShareLinkRepository.cs`

```csharp
public async Task<IEnumerable<ShareLink>> ListByOwnerAsync(
    Guid ownerId, bool activeOnly, int take, CancellationToken ct)
{
    var now = DateTime.UtcNow;
    return await _context.ShareLinks
        .Where(s => s.OwnerId == ownerId)
        .Where(s => !activeOnly || (!s.Revoked && s.ExpiresAt > now))
        .OrderByDescending(s => s.CreatedAt)
        .Take(take)
        .ToListAsync(ct);
}
```

> Примечание: `IX_share_links_owner_id` индекс уже существует — дополнительной миграции не нужно.

## Service

Файл: `services/sharing-service/GraNAS.Sharing.BL/Services/Contracts/IShareService.cs`

Добавить:
```csharp
Task<IEnumerable<ShareLinkOwnerResponse>> ListByOwnerAsync(
    Guid ownerId, bool activeOnly, int take, CancellationToken ct);
```

Файл: `services/sharing-service/GraNAS.Sharing.BL/Services/Implementation/ShareService.cs`

```csharp
public async Task<IEnumerable<ShareLinkOwnerResponse>> ListByOwnerAsync(
    Guid ownerId, bool activeOnly, int take, CancellationToken ct)
{
    var links = (await _repo.ListByOwnerAsync(ownerId, activeOnly, take, ct)).ToList();

    // Получить названия папок через metadata-service (batch)
    var folderIds = links.Select(l => l.FolderId).Distinct().ToList();
    var folderNames = await _metadataClient.GetFolderNamesBatchAsync(folderIds, ct);
    // v1-fallback если batch не реализован: Task.WhenAll параллельных вызовов

    return links.Select(l => new ShareLinkOwnerResponse
    {
        Id = l.Id,
        FolderId = l.FolderId,
        FolderName = folderNames.GetValueOrDefault(l.FolderId, "—"),
        Path = l.Path,
        ExpiresAt = l.ExpiresAt,
        Revoked = l.Revoked,
        CreatedAt = l.CreatedAt,
        OpenCount = l.OpenCount,
    });
}
```

## Controller

Файл: `services/sharing-service/GraNAS.Sharing.API/Controllers/SharingController.cs`

Добавить endpoint (в класс с `[Authorize]` и `[EnableRateLimiting("api")]`):

```csharp
[HttpGet("/api/share-links")]
public async Task<IActionResult> ListMyShares(
    [FromQuery] bool activeOnly = true,
    [FromQuery] int take = 200,
    CancellationToken ct = default)
{
    var ownerId = User.GetUserId(); // existing extension
    take = Math.Min(take, 500);
    var result = await _shareService.ListByOwnerAsync(ownerId, activeOnly, take, ct);
    return Ok(result);
}
```

## IMetadataServiceClient

Если ещё нет batch-метода, добавить в интерфейс:
```csharp
Task<Dictionary<Guid, string>> GetFolderNamesBatchAsync(
    IEnumerable<Guid> folderIds, CancellationToken ct);
```

v1-fallback в сервисе — `Task.WhenAll` с параллельными одиночными вызовами `GetFolderAsync(id)`.

## Опциональные миграции

### open_count (для колонки "Открытий" в UI)

Миграция: `AddOpenCountToShareLinks`  
Таблица: `share_links`  
Добавить: `open_count INT NOT NULL DEFAULT 0`

Инкремент при успешном доступе — в методе `GetByTokenAsync` / `ResolvePublicShareAsync`:
```csharp
shareLink.OpenCount++;
await _context.SaveChangesAsync(ct);
```

### last_accessed_at (для страницы "Недавние" в metadata-service)

Таблица: `folders`  
Добавить: `last_accessed_at TIMESTAMPTZ NULL`  
Обновлять при: просмотре списка файлов через P2P или навигации в папку.  
v1: UI сортирует по `updated_at` — миграция не блокирует запуск.

## Тесты

Добавить в `tests/` (зеркало существующих паттернов в sharing-service.tests):

```
GET /api/share-links
  - 200 OK + непустой массив (у пользователя есть ссылки)
  - 200 OK + пустой массив (нет ссылок)
  - 401 Unauthorized (нет токена)
  - activeOnly=true фильтрует отозванные и истёкшие
  - activeOnly=false возвращает все включая отозванные
  - take=5 ограничивает результаты
```

## Frontend

После реализации эндпоинта фронтенд автоматически начнёт показывать данные:
- `clients/web/src/api/shares.api.ts` — `listAll()` уже добавлен
- `clients/web/src/features/shares/useGlobalSharesQuery.ts` — хук готов
- `clients/web/src/pages/LinksPage.tsx` — покажет ссылки вместо заглушки
- `clients/web/src/pages/HomePage.tsx` — заполнит счётчик "Активные ссылки"
