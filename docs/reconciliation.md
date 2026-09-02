# Реконсилизация при прекращении обработки ПДн

Обзор процесса реконсилизации — фазы 2 прекращения обработки персональных данных в ЕДИН MPI.

---

## Контекст

Прекращение обработки ПДн реализовано двухфазно:

| Фаза | Метод | Действие |
|------|-------|----------|
| 1 | `CeaseProcessingAsync` | Пометить внешние ключи (`processing_status = 'cessation'`) |
| 2 | `ReconcileAsync` | Удалить помеченные записи и золотые данные (если нет ссылок) |

Такой подход позволяет:
- Сохранять данные лица, пока хотя бы одна организация продолжает обработку
- Атомарно удалять данные только после полного прекращения во всех системах
- Изолировать пометку от фактического удаления

---

## Способы вызова

Реконсилизация доступна двумя способами:

| Способ | Описание | Использование |
|--------|----------|---------------|
| HTTP API | `POST /persons/cessation/reconcile` | Ручной вызов или внешний планировщик |
| Worker | Периодическая фоновая задача | Автоматическое выполнение по cron |

---

## Автоматическая реконсилизация (Worker)

### Архитектура

```
┌─────────────────────┐     ┌─────────────────────┐
│   Api (ASP.NET)     │     │   Worker (Console)  │
│                     │     │                     │
│  POST /cessation    │     │  TaskScheduler      │
│  POST /deferred     │     │    ↓                │
│                     │     │  ReconcileCessations│
└─────────┬───────────┘     │    ↓                │
          │                 │  ProcessDeferred    │
          │                 │    ↓                │
          │                 │  ReconcileAsync     │
          └────────────────►│                     │
                PostgreSQL  └─────────────────────┘
```

Worker — отдельный консольный процесс (side-car), запускаемый рядом с Api. Оба процесса подключаются к одной PostgreSQL.

### Расписание

По умолчанию — каждый час (`0 * * * *`). Настраивается в `appsettings.json`:

```json
{
  "Worker": {
    "Tasks": [
      {
        "Id": "reconcile-cessations",
        "CronExpression": "0 * * * *",
        "Enabled": true,
        "TimeoutSeconds": 300,
        "RetryIntervalMinutes": 5
      }
    ]
  }
}
```

### Цикл выполнения

Каждый цикл планировщика выполняет два шага:

#### Шаг 1: Обработка отложенных отзывов

`ProcessDeferredCessationsAsync()` — преобразует отложенные отзывы в немедленные:

```sql
-- Найти отложенные отзывы с наступившей датой
SELECT * FROM person_deferred_cessations
WHERE status = 'pending' AND scheduled_deletion_date <= NOW()
```

Для каждой записи:
1. Найти `ext_persons` по `source_system_id` + `external_person_id`
2. Создать `ext_person_cessations` с `processing_status = 'cessation'`
3. Пометить `person_deferred_cessations.status = 'completed'`

#### Шаг 2: Реконсилизация

`ReconcileAsync()` — удаляет помеченные записи (подробнее: [Алгоритм ReconcileAsync](#алгоритм-reconcileasync)).

### Время задержки

После наступления `scheduled_deletion_date` данные удаляются в течение **одного цикла планировщика** (до часа по умолчанию). Для уменьшения задержки — уменьшить интервал cron:

```
"*/5 * * * *"  — каждые 5 минут
"*/15 * * * *" — каждые 15 минут
```

### Запуск

```bash
# Локально
dotnet run --project src/Worker

# Docker Compose
docker compose up worker
```

---

## Ручная реконсилизация (HTTP API)

### Эндпоинт

```
POST /persons/cessation/reconcile
```

**Ответ:** `200 OK` с телом — количество обработанных записей.

```json
{
  "value": 3
}
```

---

## Алгоритм ReconcileAsync

### 1. Поиск помеченных записей

```sql
SELECT * FROM ext_person_cessations WHERE processing_status = 'cessation'
```

Если записей нет → возврат 0, завершение.

### 2. Группировка по staging-лицу

Помеченные записи группируются по `person_id` (ссылка на `ext_persons.id`).

### 3. Обработка каждого лица

Для каждого уникального `ext_persons.id`:

#### 3a. Найти золотое лицо

```
ext_person_cessations.person_id → ext_persons.id → ext_persons.person_id (masterId)
```

Если `ext_person` не найден или не привязан к лицу → удалить только cessation-записи, перейти к следующему.

#### 3b. Удалить помеченные cessation-записи

```sql
DELETE FROM ext_person_cessations WHERE id IN (...)
```

#### 3c. Удалить соответствующие внешние ссылки

Для каждой cessation-записи удалить `person_external_ids` с совпадающими `source_system_id` + `external_person_id`.

#### 3d. Проверить оставшиеся ссылки

```sql
SELECT COUNT(*) FROM person_external_ids WHERE person_id = :masterId
```

- Если **остались ссылки** → лицо сохраняется (другие организации продолжают обработку)
- Если **ссылок нет** → перейти к удалению золотых записей

### 4. Удаление золотых записей (если нет ссылок)

Порядок удаления таблиц: [Порядок удаления (FK-безопасность)](#порядок-удаления-fk-безопасность)

### 5. Коммит транзакции

Все операции выполняются в одной транзакции. При ошибке — полный откат.

---

## Диаграмма потока

```
                    ┌─────────────────────┐
                    │ ReconcileAsync      │
                    └──────────┬──────────┘
                               │
                    ┌──────────▼──────────┐
                    │ Найти cessation     │
                    │ processing_status   │
                    │ = 'cessation'       │
                    └──────────┬──────────┘
                               │
                    ┌──────────▼──────────┐
                    │ Группировать по     │
                    │ ext_persons.id      │
                    └──────────┬──────────┘
                               │
              ┌────────────────┼────────────────┐
              │                │                │
    ┌─────────▼─────────┐ ┌───▼───────────┐ ┌──▼──────────────┐
    │ Лицо 1            │ │ Лицо 2        │ │ Лицо N          │
    └─────────┬─────────┘ └───┬───────────┘ └──┬──────────────┘
              │                │                │
    ┌─────────▼─────────┐     │                │
    │ Удалить cessation │     │                │
    │ Удалить ext_ids   │     │                │
    └─────────┬─────────┘     │                │
              │                │                │
    ┌─────────▼─────────┐     │                │
    │ Остались ссылки?  │     │                │
    └─────────┬─────────┘     │                │
              │                │                │
      ┌───────┴───────┐       │                │
      │               │       │                │
    Да(есть)       Нет(0)     │                │
      │               │       │                │
      ▼               ▼       │                │
  Сохранить    Удалить         │                │
  лицо        золотые          │                │
              записи           │                │
              (keys, defects,  │                │
               documents,     │                │
               persons)       │                │
                               │                │
                    ┌──────────▼────────────────▼──┐
                    │ Коммит транзакции            │
                    └─────────────────────────────┘
```

---

## Пример сценария

### Организация A прекращает обработку

1. **Запрос cessation:**
   ```json
   {
     "identifiers": [
       { "sourceSystemId": "ORG_A", "externalPersonId": "emp-001" }
     ]
   }
   ```

2. **Фаза 1:** создана запись `ext_person_cessations` с `processing_status = 'cessation'`

3. **Запрос reconcile:**
   ```
   POST /persons/cessation/reconcile
   ```

4. **Фаза 2:**
   - Удалена cessation-запись
   - Удалена внешняя ссылка `ORG_A/emp-001`
   - Проверены оставшиеся ссылки: `ORG_B/emp-002` существует
   - **Лицо сохранено** — организация B продолжает обработку

### Все организации прекратили обработку

1. Организация A: cessation → reconcile → ссылка удалена, осталась ссылка B → лицо сохранено
2. Организация B: cessation → reconcile → ссылка удалена, ссылок нет → **лицо удалено**

---

## Порядок удаления (FK-безопасность)

Таблицы удаляются строго в порядке, обратном зависимостям FK:

| # | Таблица | FK |
|---|---------|-----|
| 1 | `person_identification_keys` | → `persons(id)` RESTRICT |
| 2 | `person_defects` | → `persons(id)` RESTRICT |
| 3 | `person_documents` | → `persons(id)` RESTRICT |
| 4 | `person_deferred_cessations` | → `persons(id)` RESTRICT |
| 5 | `ext_person_defects` | → `ext_persons(id)` RESTRICT |
| 6 | `ext_persons` | → `persons(id)` SET NULL |
| 7 | `persons` | (корневая таблица) |

`ON DELETE CASCADE` запрещён. Все удаления выполняются приложением явно.

---

## Транзакционность

- Все операции `ReconcileAsync` выполняются в **одной транзакции**
- При ошибке — **полный откат** (данные не удаляются частично)
- Уровень изоляции: `READ_COMMITTED` (по умолчанию PostgreSQL)

---

## Исходный код

| Файл | Метод |
|------|-------|
| `src/Infrastructure/Services/PersonCessationService.cs` | `ReconcileAsync()` |
| `src/Infrastructure/Services/PersonCessationService.cs` | `ProcessDeferredCessationsAsync()` |
| `src/Infrastructure/Services/PersonCessationService.cs` | `DeleteGoldenRecordsAsync()` |
| `src/Api/Endpoints/PersonEndpoints.cs` | `HandleReconcileAsync()` |
| `src/Domain/Interfaces/IPersonCessationService.cs` | `ReconcileAsync()` (интерфейс) |
| `src/Domain/Interfaces/IPersonCessationService.cs` | `ProcessDeferredCessationsAsync()` (интерфейс) |
| `src/Worker/Tasks/ReconcileCessationsTask.cs` | Фоновая задача |
| `src/Worker/Scheduling/WorkerTaskScheduler.cs` | Планировщик |
