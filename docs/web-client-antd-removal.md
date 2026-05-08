# Frontend Plan: Полное удаление Ant Design из веб-клиента

## Контекст

После Phase 8 UI/UX redesign визуальный слой полностью переведён на кастомный CSS.
Ant Design остался только в трёх местах:

| Место | Что использует | Заменить на |
|---|---|---|
| `src/main.tsx` | `import 'antd/dist/reset.css'` | Убрать (наш CSS уже сбрасывает box-sizing/margins) |
| `src/App.tsx` | `<ConfigProvider>`, `<AntApp>` | Убрать, если хуки не нужны |
| `src/features/folders/useFoldersQuery.ts` | `notification.error(...)` | `toast(...)` из `useToast.ts` |
| `src/features/permissions/usePermissionMutations.ts` | `notification.success/error(...)` | `toast(...)` |
| `src/features/shares/useShareMutations.ts` | `notification.error(...)` | `toast(...)` |
| `src/auth/AuthContext.tsx` | (проверить наличие AntD) | — |
| `src/__tests__/test-utils.tsx` | `<AntApp>` wrapper в тестах | Убрать обёртку |

---

## Шаг 1 — Migrate hooks to Toast bus

### useToast.ts (уже реализован)

`clients/web/src/shared/useToast.ts` уже содержит `toast(msg)` и `subscribeToast`.

### Обновить useFoldersQuery.ts

```ts
// Было:
import { notification } from 'antd';
// ...
onError: () => notification.error({ message: 'Не удалось создать папку' }),

// Стало:
import { toast } from '../../shared/useToast';
// ...
onError: () => toast('Не удалось создать папку'),
```

Изменения:
- `useCreateFolder.onError` → `toast('Не удалось создать папку')`
- `useDeleteFolder.onError` → `toast('Не удалось удалить папку')`

### Обновить usePermissionMutations.ts

```ts
// Удалить:
import { notification } from 'antd';

// Заменить:
import { toast } from '../../shared/useToast';

// useGrantPermission:
onSuccess: () => toast('Права выданы'),
onError: () => toast('Не удалось выдать права'),

// useRevokePermission:
onSuccess: () => toast('Права отозваны'),
onError: () => toast('Не удалось отозвать права'),
```

### Обновить useShareMutations.ts

```ts
import { toast } from '../../shared/useToast';
// useCreateShare.onError → toast('Не удалось создать ссылку')
// useRevokeShare.onError → toast('Не удалось отозвать ссылку')
```

---

## Шаг 2 — Удалить AntD из App.tsx и main.tsx

### App.tsx

```tsx
// Удалить:
import { App as AntApp, ConfigProvider } from 'antd';
import ruRU from 'antd/locale/ru_RU';

// Заменить обёртку:
export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <ErrorBoundary>
          <RouterProvider router={router} />
        </ErrorBoundary>
      </AuthProvider>
    </QueryClientProvider>
  );
}
```

Локаль `ru_RU` использовалась только для AntD DatePicker (удалён в Phase 8).
`ConfigProvider.theme.colorPrimary` больше не нужен — цвет определяется CSS-переменной `--brand-primary`.

### main.tsx

```tsx
// Удалить:
import 'antd/dist/reset.css';
// Наш index.css уже содержит box-sizing reset и html/body margin: 0
```

---

## Шаг 3 — Убрать AntApp из тестов

### test-utils.tsx

```tsx
// Удалить:
import { App as AntApp } from 'antd';

// Убрать обёртку:
function Wrapper() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>
  );
}
```

---

## Шаг 4 — Удалить зависимости

```bash
npm uninstall antd @ant-design/icons
```

После удаления проверить через `tsc -b && vite build`:
- Все импорты `from 'antd'` и `from '@ant-design/icons'` должны исчезнуть.

### Поиск оставшихся импортов

```bash
# в PowerShell:
Select-String -Path "src\**\*.tsx","src\**\*.ts" -Pattern "from 'antd|from '@ant-design" -Recurse
```

---

## Шаг 5 — Финальная проверка

После удаления `antd`:
1. `npm run build` — без ошибок TS
2. `npm test` — все 10 тестов зелёные
3. Bundle size: ожидаемое уменьшение с ~800 KB → ~200-250 KB (gzip: 250 → 60-80 KB)

---

## Влияние на ShareCreatedModal

`ShareCreatedModal.tsx` (уже переписан в Phase 8) не использует AntD.  
`App.useApp()` был убран при rewrite — ✅

---

## Порядок реализации

1. `useFoldersQuery.ts` — заменить `notification.*` → `toast`
2. `usePermissionMutations.ts` — заменить `notification.*` → `toast`
3. `useShareMutations.ts` — заменить `notification.*` → `toast`
4. `App.tsx` — убрать `<ConfigProvider>` + `<AntApp>`
5. `main.tsx` — убрать `import 'antd/dist/reset.css'`
6. `test-utils.tsx` — убрать `<AntApp>`
7. `npm uninstall antd @ant-design/icons`
8. `npm run build && npm test` — убедиться в чистоте
9. PR: "chore(web): remove Ant Design dependency"

**Оценка:** ~2-3 часа. Чисто механическая работа без логических изменений.

---

## Что проверить после удаления

| Сценарий | Проверяем |
|---|---|
| Создать папку → успех | Toast "Папка создана" появляется |
| Создать папку → ошибка | Toast "Не удалось создать папку" |
| Выдать права → успех | Toast "Права выданы" |
| Выдать права → ошибка (401) | Toast "Не удалось выдать права" |
| Создать share-link → ошибка | Toast "Не удалось создать ссылку" |
| Войти в систему | /login рендерится без AntD |
| Переход по папкам | Sidebar/Topbar/Inspector без AntD |
| P2P скачивание | FileListPanel без AntD Progress |

---

## Примечание о CSS reset

`antd/dist/reset.css` содержит:
```css
*, *::before, *::after { box-sizing: border-box; }
body { margin: 0; }
```

Наш `src/index.css` уже содержит аналогичные правила в явном виде (добавлены в Phase 8),
поэтому визуальных регрессий при удалении AntD reset не будет.
