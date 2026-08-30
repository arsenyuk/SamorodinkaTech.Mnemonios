# Вычисление и хранение хешей идентификации

Обзор алгоритма вычисления HMAC-ключей для детерминированного сопоставления персон в ЕДИН MPI.

---

## Назначение

Хеши идентификации позволяют:
- Сопоставлять записи из разных ИС без передачи ПДн между системами
- Определять, является ли входящая запись уже известным лицом
- Гарантировать детерминированность: одни и те же входные данные всегда дают одинаковый хеш

---

## Поток вычисления

```
ResolveRequest → NormalizationService → IdentificationKeyService → HMAC-SHA256 → person_identification_keys
```

1. **Нормализация** — приведение входных данных к единому формату
2. **Формирование строки-кandidата** — объединение нормализованных полей
3. **Вычисление HMAC** — одностороннее преобразование с секретным ключом
4. **Сохранение** — хеш сохраняется в `person_identification_keys.key_value`

---

## Нормализация

Правила нормализации определены в `NormalizationService`.

### ФИО

| Шаг | Описание | Пример |
|-----|----------|--------|
| 1 | Trim | `"  Иванов  "` → `"Иванов"` |
| 2 | Схлопывание пробелов | `"Иванов  Иван"` → `"Иванов Иван"` |
| 3 | Unicode NFC |兼容ные символы приводятся к NFC |
| 4 | ToUpperInvariant | `"Иванов"` → `"ИВАНОВ"` |

### ИНН

**Валидация:** длина 10 или 12 цифр + проверка контрольной суммы (`InnValidator`). Нормализация: те же правила, что для ФИО.

### СНИЛС

**Валидация:** длина 11 цифр + проверка контрольной суммы (`SnilsValidator`). Нормализация: те же правила, что для ФИО.

### ДУЛ

| Шаг | Описание | Пример |
|-----|----------|--------|
| 1 | Нормализация типа | `"Паспорт"` → `"ПАСПОРТ"` |
| 2 | Нормализация серии | `"45 10"` → `"45 10"` → `"4510"` (нормализация как ФИО) |
| 3 | Нормализация номера | `"123456"` → `"123456"` |
| 4 | Объединение | `"ПАСПОРТ|4510|123456"` |

**Валидация:** серия и номер обязательны. Тип опционален.

---

## Типы ключей

| Тип | Формула | Пример входных данных |
|-----|---------|----------------------|
| `inn` | HMAC(normalized_inn) | ИНН: `"7707083893"` |
| `snils` | HMAC(normalized_snils) | СНИЛС: `"12345678964"` |
| `dul` | HMAC(type\|series\|number) | ДУЛ: тип `"21"`, серия `"4510"`, номер `"123456"` |
| `fio` | HMAC(lastName\|firstName) | ФИО: `"ИВАНОВ"`, `"ИВАН"` |
| `fio_full` | HMAC(lastName\|firstName\|middleName) | Полное ФИО (если есть отчество) |
| `inn_fio` | HMAC(normalized_inn\|fio) | ИНН + ФИО |
| `snils_fio` | HMAC(normalized_snils\|fio) | СНИЛС + ФИО |
| `dul_fio` | HMAC(normalized_dul\|fio) | ДУЛ + ФИО |

**Ключ `fio`** создаётся всегда. Остальные — только при наличии соответствующих данных.

---

## Вычисление HMAC

```csharp
private string ComputeHmacSha256(string value)
{
    using var hmac = new HMACSHA256(_hmacKey);
    var bytes = Encoding.UTF8.GetBytes(value);
    var hash = hmac.ComputeHash(bytes);
    return Convert.ToHexString(hash).ToLowerInvariant();
}
```

- **Алгоритм:** HMAC-SHA256
- **Ключ:** секретный ключ из конфигурации (`HmacSettings.Key`)
- **Выход:** hex-строка 64 символа в нижнем регистре
- **Пример:** `"а3f5b8c1d2e4..."` (64 символа)

### Почему HMAC, а не SHA256

- **Детерминированность:** одинаковые данные → одинаковый хеш
- **Защита от подбора:** знание хеша не позволяет восстановить исходные данные
- **Секретный ключ:** хеш нельзя вычислить без знания ключа
- **Уникальность:** ключ проекта гарантирует, что хеши не пересекаются с другими системами

---

## Хранение

Структура таблицы `person_identification_keys`, индексы и ограничения: [docs/database.md](database.md#person_identification_keys)

### Версионирование

Поле `normalization_version` позволяет менять алгоритм нормализации без потери совместимости:
- Старые ключи остаются в БД и участвуют в поиске
- Новые ключи создаются с увеличенной версией
- При изменении алгоритма — увеличивать `normalization_version`

---

## Поиск совпадений

При идентификации лица (`PersonResolveService.ResolveByMatchingAsync`):

1. Вычислить все доступные ключи из входного запроса
2. Найти все `person_identification_keys`, где `key_value` совпадает с любым из вычисленных
3. Если найдено > 1 уникального `person_id` → **Conflict**
4. Если найдено ровно 1 `person_id` → **Matched**
5. Если ничего не найдено → **Unmatched** (создать нового)

```csharp
var computedKeys = _keyService.ComputeKeys(request, DefaultNormalizationVersion);
var keyValues = computedKeys.Select(k => k.KeyValue);
var matchedPersonIds = await _repository.FindPersonIdsByKeysAsync(keyValues, cancellationToken);
```

---

## Безопасность

- **Секретный ключ** хранится в `.env` / переменных окружения, не в коде
- **HMAC** необратим: знание хеша не позволяет восстановить ФИО/ИНН/СНИЛС
- **ПДн не хранится** в золотых записях (`persons`) — только хеши и таймстемпы
- **Staging-таблицы** (`ext_*`) хранят сырые данные для аудита, ссылаются на `ext_persons`

---

## Исходный код

| Файл | Назначение |
|------|-----------|
| `src/Infrastructure/Services/NormalizationService.cs` | Нормализация полей |
| `src/Infrastructure/Services/IdentificationKeyService.cs` | Вычисление HMAC-ключей |
| `src/Domain/Validation/InnValidator.cs` | Валидация ИНН |
| `src/Domain/Validation/SnilsValidator.cs` | Валидация СНИЛС |
| `src/Infrastructure/Persistence/Configurations/PersonIdentificationKeyConfiguration.cs` | EF-маппинг |
