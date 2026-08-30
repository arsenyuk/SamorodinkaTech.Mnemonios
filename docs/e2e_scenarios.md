# E2E сценарии ЕДИН

Пошаговые сценарии использования сервиса ЕДИН с примерами запросов и ответов.

---

## Поэтапное добавление данных сотрудника

Сотрудник с паспортом иностранного гражданина поэтапно получает ИНН и СНИЛС. На каждом этапе возвращается **один и тот же MasterId**. В `ext_persons` **создаётся новая запись** при каждом вызове (аудит-лог). Золотая запись **обогащается** новыми данными (ИНН, СНИЛС, ДУЛ).

### Шаг 1: Регистрация в HR-системе (без ИНН и СНИЛС)

**Запрос:**
```json
POST /persons/resolve
{
  "lastName": "Петров",
  "firstName": "Иван",
  "evidence": {
    "dulType": "10",
    "dulSeries": "МР",
    "dulNumber": "123456"
  },
  "identifiers": [
    {
      "sourceSystemId": "HR",
      "externalMasterId": "ext-emp-001"
    }
  ]
}
```

**Ответ:**
```json
{
  "status": "Unmatched",
  "masterId": "7dcc6537-bb5a-4fcc-a2f7-e77520f6f46c"
}
```

**Состояние БД:**
- `ext_persons`: 1 запись (`HR/ext-emp-001`, status=processed)
- `persons`: 1 запись (ФИО + ДУЛ, без ИНН и СНИЛС)
- `person_identification_keys`: ключи на основе ФИО + ДУЛ

### Шаг 2: Сотрудник получает ИНН

**Запрос:**
```json
POST /persons/resolve
{
  "lastName": "Петров",
  "firstName": "Иван",
  "evidence": {
    "inn": "123456789012",
    "dulType": "10",
    "dulSeries": "МР",
    "dulNumber": "123456"
  },
  "identifiers": [
    {
      "sourceSystemId": "HR",
      "externalMasterId": "ext-emp-001"
    }
  ]
}
```

**Ответ:**
```json
{
  "status": "Matched",
  "masterId": "7dcc6537-bb5a-4fcc-a2f7-e77520f6f46c"
}
```

**Состояние БД:**
- `ext_persons`: **2 записи** (новая `HR/ext-emp-001`, status=processed)
- `persons`: **обогащена** — добавлен `inn`, обновлён `updated_at`
- `person_identification_keys`: добавлены новые ключи на основе ИНН

### Шаг 3: Бухгалтер оформляет СНИЛС

**Запрос:**
```json
POST /persons/resolve
{
  "lastName": "Петров",
  "firstName": "Иван",
  "evidence": {
    "inn": "123456789012",
    "snils": "12345678901",
    "dulType": "10",
    "dulSeries": "МР",
    "dulNumber": "123456"
  },
  "identifiers": [
    {
      "sourceSystemId": "HR",
      "externalMasterId": "ext-emp-001"
    }
  ]
}
```

**Ответ:**
```json
{
  "status": "Matched",
  "masterId": "7dcc6537-bb5a-4fcc-a2f7-e77520f6f46c"
}
```

**Состояние БД:**
- `ext_persons`: **3 записи** (новая `HR/ext-emp-001`, status=processed)
- `persons`: **обогащена** — добавлен `snils`, обновлён `updated_at`
- `person_identification_keys`: добавлены новые ключи на основе СНИЛС

### Результат

- Все три вызова вернули один и тот же `masterId`
- В `ext_persons` **3 записи** для ключа `HR/ext-emp-001` (по одной на каждый вызов — аудит-лог)
- Золотая запись **обогащается** данными по мере поступления (ИНН, СНИЛС добавляются, если отсутствуют)

---

## Прекращение обработки персональных данных

### Мгновенный отзыв

Внешняя система запрашивает немедленное удаление данных персоны. Можно указать несколько идентификаторов в одном вызове.

> **Важно:** Нельзя указывать одновременно `identifiers` и `organizationUnitKey` — используйте либо то, либо другое.

**Запрос (один ключ):**
```json
POST /persons/cessation
{
  "identifiers": [
    {
      "sourceSystemId": "CRM",
      "externalMasterId": "ext-12345"
    }
  ]
}
```

**Запрос (несколько ключей):**
```json
POST /persons/cessation
{
  "identifiers": [
    { "sourceSystemId": "HR", "externalMasterId": "ext-emp-001" },
    { "sourceSystemId": "CRM", "externalMasterId": "ext-001-crm" }
  ]
}
```

**Запрос (вся организация по ключу):**
```json
POST /persons/cessation
{
  "organizationUnitKey": "ORG-001"
}
```

**Ответ:**
```json
{
  "masterId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "deletedKeys": 5,
  "deletedExternalIds": 3,
  "deletedDefects": 1
}
```

> Если после удаления указанных связей у персоны остаются другие связи — золотая запись **сохраняется**.

### Отложенный отзыв

Внешняя система запрашивает удаление данных на указанную дату в будущем. Можно указать несколько идентификаторов.

> **Важно:** Нельзя указывать одновременно `identifiers` и `organizationUnitKey` — используйте либо то, либо другое.

**Запрос (по ключам):**
```json
POST /persons/cessation/deferred
{
  "identifiers": [
    { "sourceSystemId": "CRM", "externalMasterId": "ext-12345" },
    { "sourceSystemId": "HR", "externalMasterId": "ext-emp-001" }
  ],
  "scheduledDeletionDate": "2026-12-31T23:59:59Z"
}
```

**Запрос (по организации):**
```json
POST /persons/cessation/deferred
{
  "organizationUnitKey": "ORG-001",
  "scheduledDeletionDate": "2026-12-31T23:59:59Z"
}
```

**Ответ:**
```json
{
  "masterId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "scheduledDeletionDate": "2026-12-31T23:59:59Z"
}
```

---

## Обогащение записи через RequestedMasterId

Система A создаёт персону и получает MasterId. Система B передаёт данные той же персоны с указанием MasterId — ЕДИН возвращает тот же идентификатор.

### Шаг 1: Система A создаёт персону

```json
POST /persons/resolve
{
  "lastName": "Петров",
  "firstName": "Иван",
  "evidence": {
    "dulType": "10",
    "dulSeries": "МР",
    "dulNumber": "123456"
  },
  "identifiers": [
    {
      "sourceSystemId": "HR",
      "externalMasterId": "ext-emp-001"
    }
  ]
}
```

**Ответ:**
```json
{
  "status": "Unmatched",
  "masterId": "7dcc6537-bb5a-4fcc-a2f7-e77520f6f46c"
}
```

### Шаг 2: Система B обогащает запись

```json
POST /persons/resolve
{
  "lastName": "Петров",
  "firstName": "Иван",
  "identifiers": [
    {
      "sourceSystemId": "CRM",
      "externalMasterId": "ext-001-crm"
    }
  ],
  "requestedMasterId": "7dcc6537-bb5a-4fcc-a2f7-e77520f6f46c"
}
```

**Ответ:**
```json
{
  "status": "Matched",
  "masterId": "7dcc6537-bb5a-4fcc-a2f7-e77520f6f46c"
}
```

### Результат

Система B получила тот же `masterId`. Запись обогащена — связана с внешней системой CRM.

---

## Увольнение и повторный приём

Сотрудник увольняется → устанавливается отложенное прекращение обработки ПДн → сотрудник устраивается обратно → отменяется отложенное прекращение.

### Шаг 1: Приём сотрудника

```json
POST /persons/resolve
{
  "lastName": "Казаков",
  "firstName": "Пётр",
  "evidence": {
    "dulType": "21",
    "dulSeries": "4510",
    "dulNumber": "123456"
  },
  "identifiers": [
    {
      "sourceSystemId": "HR",
      "externalMasterId": "ext-emp-100"
    }
  ]
}
```

**Ответ:**
```json
{
  "status": "Unmatched",
  "masterId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

### Шаг 2: Увольнение — отложенное прекращение обработки

```json
POST /persons/cessation/deferred
{
  "identifiers": [
    {
      "sourceSystemId": "HR",
      "externalMasterId": "ext-emp-100"
    }
  ],
  "scheduledDeletionDate": "2026-09-30T23:59:59Z"
}
```

**Ответ:**
```json
{
  "masterId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "scheduledDeletionDate": "2026-09-30T23:59:59Z"
}
```

### Шаг 3: Повторный приём (через месяц)

```json
POST /persons/resolve
{
  "lastName": "Казаков",
  "firstName": "Пётр",
  "evidence": {
    "dulType": "21",
    "dulSeries": "4510",
    "dulNumber": "123456"
  },
  "identifiers": [
    {
      "sourceSystemId": "HR",
      "externalMasterId": "ext-emp-100"
    }
  ]
}
```

**Ответ:**
```json
{
  "status": "Matched",
  "masterId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "scheduledDeletionDate": null
}
```

### Результат

- Возвращён тот же `masterId`
- `scheduledDeletionDate` = `null` — отложенное прекращение обработки автоматически отменено при повторном resolve

---

## Проверка scheduledDeletionDate

Сценарий проверяет, что `scheduledDeletionDate` возвращается в ответе resolve при наличии отложенного отзыва и исчезает после его отмены.

### Шаг 1: Создание персоны

```json
POST /persons/resolve
{
  "lastName": "Белов",
  "firstName": "Сергей",
  "evidence": {
    "dulType": "21",
    "dulSeries": "7701",
    "dulNumber": "123456"
  },
  "identifiers": [
    {
      "sourceSystemId": "HR",
      "externalMasterId": "ext-emp-200"
    }
  ]
}
```

**Ответ:**
```json
{
  "status": "Unmatched",
  "masterId": "b2c3d4e5-f6a7-8901-bcde-f12345678901"
}
```

### Шаг 2: Установка отложенного отзыва

```json
POST /persons/cessation/deferred
{
  "identifiers": [
    {
      "sourceSystemId": "HR",
      "externalMasterId": "ext-emp-200"
    }
  ],
  "scheduledDeletionDate": "2026-10-31T23:59:59Z"
}
```

**Ответ:**
```json
{
  "masterId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "scheduledDeletionDate": "2026-10-31T23:59:59Z"
}
```

### Шаг 3: Resolve с отложенным отзывом

```json
POST /persons/resolve
{
  "lastName": "Белов",
  "firstName": "Сергей",
  "identifiers": [
    {
      "sourceSystemId": "CRM",
      "externalMasterId": "ext-001-crm"
    }
  ]
}
```

**Ответ:**
```json
{
  "status": "Matched",
  "masterId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "scheduledDeletionDate": "2026-10-31T23:59:59Z"
}
```

`scheduledDeletionDate` **присутствует** — отзыв установлен.

### Шаг 4: Resolve через другую систему (отмена отзыва)

```json
POST /persons/resolve
{
  "lastName": "Белов",
  "firstName": "Сергей",
  "identifiers": [
    {
      "sourceSystemId": "ERP",
      "externalMasterId": "emp-001-erp"
    }
  ]
}
```

**Ответ:**
```json
{
  "status": "Matched",
  "masterId": "b2c3d4e5-f6a7-8901-bcde-f12345678901"
}
```

`scheduledDeletionDate` **отсутствует** — отложенный отзыв отменён.

### Результат

- При наличии отложенного отзыва `scheduledDeletionDate` возвращается в ответе
- При resolve от другой системы (которая не устанавливала отзыв) отзыв автоматически отменяется
- `scheduledDeletionDate` исчезает из ответа
