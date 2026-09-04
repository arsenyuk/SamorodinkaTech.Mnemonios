# Структура базы данных

Обзор таблиц проекта SamorodinkaTech.Mnemonios (ЕДИН MPI).

---

## Общая схема

```
persons (1) ──── (N) person_identification_keys
     │
     ├──────────── (N) person_external_ids
     │
     ├──────────── (N) person_defects
     │
     ├──────────── (N) person_documents
     │
     ├──────────── (N) person_deferred_cessations
     │
     ├──────────── (N) person_review_queue
     │
     └──────────── (N) person_review_history
```

- У одного лица может быть **любое количество** ключей идентификации
- У одного лица может быть **любое количество** связей с внешними информационными системами
- У одного лица может быть **любое количество** дефектов данных
- У одного лица может быть **любое количество** документов (ДУЛ и др.)
- У одного лица может быть **любое количество** записей отложенной прекращения обработки
- Каждая связь уникальна по паре (source_system_id, external_person_id)

---

## persons

Единая запись физического лица в MPI. Хранит только идентификатор и таймстемпы — **без ПДн**. Персональные данные хранятся в staging-таблицах `ext_*`.

| Колонка | Тип | Обязательно | Описание |
|---------|-----|-------------|----------|
| `id` | uuid | PK | Уникальный идентификатор лица (ИД) |
| `created_at` | timestamp with time zone | NOT NULL | Дата создания |
| `updated_at` | timestamp with time zone | NOT NULL | Дата обновления |

---

## person_identification_keys

HMAC-ключи для детерминированного сопоставления лиц.

| Колонка | Тип | Обязательно | Описание |
|---------|-----|-------------|----------|
| `id` | uuid | PK | Уникальный идентификатор ключа |
| `person_id` | uuid | FK → persons(id) | Ссылка на лицо |
| `key_type` | varchar(50) | NOT NULL | Тип ключа |
| `key_value` | varchar(255) | NOT NULL | HMAC-SHA256 хеш (hex) |
| `organization_unit_key` | varchar(100) | — | Ключ организационной единицы |
| `normalization_version` | integer | NOT NULL, DEFAULT 1 | Версия алгоритма нормализации |
| `created_at` | timestamp with time zone | NOT NULL | Дата создания |

**Типы ключей и алгоритм вычисления:** [docs/hashes.md](hashes.md)

**Индексы:**
- `ux_person_identification_keys_type_value` — уникальный (key_type, key_value)
- `ix_person_identification_keys_person_id`

**Ограничения:** `ON DELETE RESTRICT`

---

## person_external_ids

Связи лица с внешними информационными системами.

| Колонка | Тип | Обязательно | Описание |
|---------|-----|-------------|----------|
| `id` | uuid | PK | Уникальный идентификатор связи |
| `person_id` | uuid | FK → persons(id) | Ссылка на лицо |
| `source_system_id` | varchar(100) | NOT NULL | Идентификатор внешней информационной системы |
| `external_person_id` | varchar(255) | NOT NULL | Идентификатор лица во внешней информационной системе |
| `external_person_type` | varchar(255) | — | Произвольный тип внешнего объекта |
| `created_at` | timestamp with time zone | NOT NULL | Дата создания |
| `updated_at` | timestamp with time zone | NOT NULL | Дата обновления |

**Индексы:**
- `ux_person_external_ids_system_extid` — уникальный (source_system_id, external_person_id)
- `ix_person_external_ids_person_id`
- `ix_person_external_ids_source_system_id`

**Ограничения:** `ON DELETE RESTRICT`

---

## person_defects

Дефекты данных при идентификации. Связь с таблицей `persons`.

| Колонка | Тип | Обязательно | Описание |
|---------|-----|-------------|----------|
| `id` | uuid | PK | Уникальный идентификатор дефекта |
| `person_id` | uuid | FK → persons(id) | Ссылка на лицо |
| `defect_type` | varchar(50) | NOT NULL | Тип дефекта |
| `defect_message` | varchar(500) | NOT NULL | Описание дефекта |
| `field_name` | varchar(100) | — | Поле, вызвавшее дефект |
| `original_value` | varchar(500) | — | Исходное значение |
| `created_at` | timestamp with time zone | NOT NULL | Дата создания |

**Типы дефектов:**

| Тип | Описание |
|-----|----------|
| `invalid_inn` | Некорректный ИНН (неверная контрольная сумма) |
| `invalid_snils` | Некорректный СНИЛС (неверная контрольная сумма) |
| `dul_incomplete` | ДУЛ неполный (серия без номера или наоборот) |

**Индексы:**
- `ix_person_defects_person_id`
- `ix_person_defects_defect_type`

**Ограничения:** `ON DELETE RESTRICT`

---

## person_documents

Документы физического лиц (ДУЛ и др.), создаваемые при наличии данных ДУЛ в запросе resolve.

| Колонка | Тип | Обязательно | Описание |
|---------|-----|-------------|----------|
| `id` | uuid | PK | Уникальный идентификатор документа |
| `person_id` | uuid | FK → persons(id) | Ссылка на лицо |
| `document_type` | varchar(50) | NOT NULL | Тип документа (например, `dul`) |
| `document_series` | varchar(50) | — | Серия документа |
| `document_number` | varchar(50) | — | Номер документа |
| `created_at` | timestamp with time zone | NOT NULL | Дата создания |

**Индексы:**
- `ix_person_documents_person_id`

**Ограничения:** `ON DELETE RESTRICT`

---

## person_deferred_cessations

Записи отложенной прекращения обработки персональных данных.

| Колонка | Тип | Обязательно | Описание |
|---------|-----|-------------|----------|
| `id` | uuid | PK | Уникальный идентификатор записи |
| `person_id` | uuid | FK → persons(id) | Ссылка на лицо |
| `source_system_id` | varchar(100) | NOT NULL | Идентификатор системы-источника |
| `external_person_id` | varchar(255) | NOT NULL | Внешний идентификатор персоны |
| `organization_unit_key` | varchar(100) | NOT NULL | Ключ организационной единицы |
| `scheduled_deletion_date` | timestamp with time zone | NOT NULL | Планируемая дата удаления данных |
| `status` | varchar(20) | NOT NULL, DEFAULT 'pending' | Статус записи |
| `created_at` | timestamp with time zone | NOT NULL | Дата создания |

**Статусы:**

| Статус | Описание |
|--------|----------|
| `pending` | Ожидает выполнения |
| `cancelled` | Отменено |
| `completed` | Выполнено |

**Индексы:**
- `ux_person_deferred_cessations_system_extid` — уникальный (source_system_id, external_person_id) WHERE status = 'pending'
- `ix_person_deferred_cessations_scheduled_date` — WHERE status = 'pending'
- `ix_person_deferred_cessations_person_id`

**Ограничения:** `ON DELETE RESTRICT`

---

## Staging-таблицы (ext_*)

Сырые данные запросов для аудита. Ссылаются на `ext_persons(id)`, а не на `persons(id)`.

### ext_persons

Staging-запись запроса идентификации. Хеширует HMAC-ключи (включая невалидные ИНН/СНИЛС).

| Колонка | Тип | Обязательно | Описание |
|---------|-----|-------------|----------|
| `id` | uuid | PK | Уникальный идентификатор |
| `person_id` | uuid | FK → persons(id) | Ссылка на золотую запись |
| `source_system_id` | varchar(100) | NOT NULL | Идентификатор системы-источника |
| `external_person_id` | varchar(255) | NOT NULL | Внешний идентификатор |
| `external_person_type` | varchar(255) | — | Тип внешнего объекта |
| `key_inn` | varchar(255) | — | HMAC-хеш ключа inn |
| `key_snils` | varchar(255) | — | HMAC-хеш ключа snils |
| `key_dul` | varchar(255) | — | HMAC-хеш ключа dul |
| `key_inn_fio` | varchar(255) | — | HMAC-хеш ключа inn_fio |
| `key_snils_fio` | varchar(255) | — | HMAC-хеш ключа snils_fio |
| `key_dul_fio` | varchar(255) | — | HMAC-хеш ключа dul_fio |
| `source_ip` | varchar(45) | — | IP-адрес источника вызова |
| `created_at` | timestamp with time zone | NOT NULL | Дата создания |

### ext_person_cessations

Записи запросов прекращения обработки ПДн.

| Колонка | Тип | Обязательно | Описание |
|---------|-----|-------------|----------|
| `id` | uuid | PK | Уникальный идентификатор |
| `person_id` | uuid | FK → ext_persons(id), NOT NULL | Ссылка на staging-запись лица |
| `source_system_id` | varchar(100) | NOT NULL | Идентификатор системы-источника |
| `external_person_id` | varchar(255) | NOT NULL | Внешний идентификатор |
| `organization_unit_key` | varchar(100) | NOT NULL | Ключ организации |
| `processing_status` | varchar(20) | NOT NULL, DEFAULT 'pending' | Статус: pending / cessation |
| `source_ip` | varchar(45) | — | IP-адрес источника вызова |
| `created_at` | timestamp with time zone | NOT NULL | Дата создания |

### ext_person_deferred_cessations

Записи запросов отложенного прекращения обработки ПДн.

| Колонка | Тип | Обязательно | Описание |
|---------|-----|-------------|----------|
| `id` | uuid | PK | Уникальный идентификатор |
| `person_id` | uuid | FK → ext_persons(id), NOT NULL | Ссылка на staging-запись лица |
| `source_system_id` | varchar(100) | NOT NULL | Идентификатор системы-источника |
| `external_person_id` | varchar(255) | NOT NULL | Внешний идентификатор |
| `scheduled_deletion_date` | timestamp with time zone | NOT NULL | Планируемая дата удаления |
| `organization_unit_key` | varchar(100) | NOT NULL | Ключ организации |
| `processing_status` | varchar(20) | NOT NULL, DEFAULT 'pending' | Статус обработки |
| `source_ip` | varchar(45) | — | IP-адрес источника вызова |
| `created_at` | timestamp with time zone | NOT NULL | Дата создания |

---

## person_review_queue

Очередь на ручную обработку стюардом (Ambiguous).

| Колонка | Тип | Обязательно | Описание |
|---------|-----|-------------|----------|
| `id` | uuid | PK | Уникальный идентификатор |
| `person_a_id` | uuid | FK → persons(id) | Ссылка на существующую мастер-запись |
| `person_b_id` | uuid | FK → persons(id) | Ссылка на новую мастер-запись |
| `shared_key_type` | varchar(50) | NOT NULL | Тип ключа совпадения |
| `conflict_key_type` | varchar(50) | NOT NULL | Тип ключа конфликта |
| `status` | varchar(20) | NOT NULL, DEFAULT 'pending' | Статус: pending |
| `created_at` | timestamp with time zone | NOT NULL | Дата создания |

---

## person_review_history

История разрешённых конфликтов. Создаётся автоматически при разрешении конфликта внешней ИС.

| Колонка | Тип | Обязательно | Описание |
|---------|-----|-------------|----------|
| `id` | uuid | PK | Уникальный идентификатор |
| `review_id` | uuid | NOT NULL | Ссылка на запись в очереди |
| `person_a_id` | uuid | NOT NULL | Идентификатор мастер-записи A |
| `person_b_id` | uuid | NOT NULL | Идентификатор мастер-записи B |
| `shared_key_type` | varchar(50) | NOT NULL | Тип ключа совпадения |
| `conflict_key_type` | varchar(50) | NOT NULL | Тип ключа конфликта |
| `resolution` | varchar(20) | NOT NULL | Результат: auto_resolved |
| `resolved_by` | varchar(100) | NOT NULL | Кто разобрал (source_system_id) |
| `resolved_at` | timestamp with time zone | NOT NULL | Дата разрешения |
| `resolution_details` | jsonb | — | Детали разрешения |
| `created_at` | timestamp with time zone | NOT NULL | Дата создания |

---

## Нормализация и версионирование

Подробное описание правил нормализации и версионирования ключей: [docs/hashes.md](hashes.md)

---

## SQL-скрипты

| Файл | Назначение |
|------|-----------|
| `tools/db/01_schema.sql` | Каноническая схема (DDL) |
| `tools/db/00_reset_schema.sql` | Сброс и пересоздание |
| `tools/db/02_seed.sql` | Seed-данные (пусто) |
