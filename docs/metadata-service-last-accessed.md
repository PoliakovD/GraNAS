# Backend Plan: last_accessed_at — RecentPage sorting by access, not update

## Цель

Страница `/recent` сейчас сортирует папки по `updated_at`. Хотим сортировку по времени
**последнего открытия** — чтобы «Недавние» показывали папки, которые пользователь реально смотрел,
а не те, что были изменены автоматически.

---

## Анализ вариантов

| Вариант | Плюсы | Минусы |
|---|---|---|
| Клиентский localStorage | Нет серверной работы | Только текущий браузер, теряется при очистке |
| `last_accessed_at` в БД | Кросс-девайс, точно | Нужна миграция + touch endpoint |
| Обновлять при P2P WatchFolder | Уже есть хук в signaling | Только для папок с P2P-активностью |

**Рекомендуется: серверный `last_accessed_at` + новый `PATCH /api/metadata/folders/:id/touch`.**

---

## Схема — миграция в metadata-service

**Файл:** `services/metadata-service/GraNAS.Metadata.DAL/Migrations/` → новая миграция `AddLastAccessedAtToFolders`

```sql
ALTER TABLE folders
  ADD COLUMN last_accessed_at TIMESTAMPTZ NULL;

CREATE INDEX IX_folders_last_accessed_at
  ON folders (owner_id, last_accessed_at DESC NULLS LAST);
```

**Entity** (`GraNAS.Metadata.Models/Folder.cs`):
```csharp
public DateTime? LastAccessedAt { get; set; }
```

---

## Endpoint

```
PATCH /api/metadata/folders/{folderId}/touch
Authorization: Bearer <jwt>
```

- Обновляет `last_accessed_at = NOW()` для указанной папки, если текущий пользователь имеет к ней доступ (владелец **или** есть запись в permissions).
- Response: `204 No Content`.
- Вызывается при открытии `FolderDetailPage` на фронтенде.

### Controller (FoldersController.cs)

```csharp
[HttpPatch("{folderId:guid}/touch")]
public async Task<IActionResult> Touch(Guid folderId, CancellationToken ct)
{
    var userId = User.GetUserId();
    var folder = await _folderService.GetByIdForUserAsync(folderId, userId, ct);
    if (folder is null) return NotFound();
    await _folderService.TouchAsync(folderId, ct);
    return NoContent();
}
```

### Service (IFolderService / FolderService.cs)

```csharp
Task TouchAsync(Guid folderId, CancellationToken ct);
// impl:
folder.LastAccessedAt = DateTime.UtcNow;
await _context.SaveChangesAsync(ct);
```

### Repository — обновить `GetUserFoldersAsync`

Уже возвращает owned + shared. Добавить сортировку опционально через query param
`?sortBy=accessed` (default = `updated`):

```csharp
var query = sortBy == "accessed"
    ? dbQuery.OrderByDescending(f => f.LastAccessedAt ?? f.UpdatedAt ?? f.CreatedAt)
    : dbQuery.OrderByDescending(f => f.UpdatedAt ?? f.CreatedAt);
```

### DTO — FolderResponse

Добавить поле (`GraNAS.Metadata.Models/DTO/FolderResponse.cs`):
```csharp
public DateTime? LastAccessedAt { get; set; }
```

---

## Frontend

### API client

`clients/web/src/api/folders.api.ts` — добавить опцию сортировки:
```ts
list: (sortBy?: 'updated' | 'accessed') =>
  api.get<FolderResponse[]>('/api/metadata/folders', { params: sortBy ? { sortBy } : {} }),
```

### FolderResponse type

`clients/web/src/types/folder.ts`:
```ts
export interface FolderResponse {
  // ... existing
  lastAccessedAt: string | null;  // новое поле
}
```

### Touch при открытии папки

`clients/web/src/pages/FolderDetailPage.tsx` — в `useEffect` при mount:
```ts
useEffect(() => {
  if (id) api.patch(`/api/metadata/folders/${id}/touch`).catch(() => {});
}, [id]);
```

Или выделить в хук `useTouchFolder(id)`.

### RecentPage

`clients/web/src/pages/RecentPage.tsx` — использовать `lastAccessedAt`:
```ts
const recent = [...folders]
  .filter(f => f.lastAccessedAt)
  .sort((a, b) => +new Date(b.lastAccessedAt!) - +new Date(a.lastAccessedAt!))
  .slice(0, 12);
// fallback: если lastAccessedAt null — не показываем в Recent, только в Folders
```

---

## Rate limiting / abuse

`PATCH /touch` попадает в существующий rate-limit `api`. Дополнительно:
- Минимальный дебаунс на клиенте: вызывать `touch` не чаще 1 раза в 5 минут на папку
  (через `setTimeout` + `useRef` для last-touch timestamp).

---

## Тесты

`tests/metadata-service.tests/` (зеркало паттернов):
- `PATCH /api/folders/{id}/touch` → 204 владелец
- `PATCH /api/folders/{id}/touch` → 204 пользователь с permission
- `PATCH /api/folders/{id}/touch` → 404 неизвестная папка
- `PATCH /api/folders/{id}/touch` → 401 без токена
- После touch: `GET /api/folders?sortBy=accessed` возвращает тронутую папку первой

---

## Порядок реализации

1. Миграция `AddLastAccessedAtToFolders` + entity update
2. `TouchAsync` в репозитории + сервисе
3. Endpoint `PATCH /touch` в контроллере
4. DTO `lastAccessedAt` в `FolderResponse`
5. Frontend: обновить тип, добавить `useTouchFolder`, обновить `RecentPage`
6. Тесты

**Оценка:** ~1 день (изолированная фича, нет зависимостей от других сервисов).
