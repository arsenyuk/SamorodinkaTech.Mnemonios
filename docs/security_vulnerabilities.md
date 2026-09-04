# Отчёт по уязвимостям зависимостей

**Дата проверки:** 30.08.2026  
**Проект:** SamorodinkaTech.Mnemonios (ЕДИН)

---

## Обнаруженные уязвимости

### System.Security.Cryptography.Xml 9.0.0

| Параметр | Значение |
|----------|----------|
| Пакет | System.Security.Cryptography.Xml |
| Текущая версия | 9.0.0 |
| Безопасная версия | 10.0.11 |
| Уровень | **High** |
| Затронутые проекты | Infrastructure |
| Тип | Transitive (зависимость Microsoft.EntityFrameworkCore.Design) |

**Ссылки на advisory:**
- https://github.com/advisories/GHSA-37gx-xxp4-5rgx
- https://github.com/advisories/GHSA-w3x6-4m5h-cxqf
- https://github.com/advisories/GHSA-cvvh-rhrc-wg4q
- https://github.com/advisories/GHSA-g8r8-53c2-pm3f
- https://github.com/advisories/GHSA-23rf-6693-g89p
- https://github.com/advisories/GHSA-8q5v-6pqq-x66h
- https://github.com/advisories/GHSA-mmjf-rqrv-855v
- https://github.com/advisories/GHSA-6588-8gv4-xfgh

**Решение:** Обновить `Microsoft.EntityFrameworkCore.Design` с 10.0.0 до 10.0.11.

---

### Microsoft.OpenApi 2.7.5

| Параметр | Значение |
|----------|----------|
| Пакет | Microsoft.OpenApi |
| Текущая версия | 2.7.5 |
| Безопасная версия | 3.10.2 |
| Уровень | **High** |
| Затронутые проекты | Api |
| Тип | Transitive (зависимость Swashbuckle.AspNetCore) |

**Решение:** Обновить `Swashbuckle.AspNetCore` до последней версии.

---

## Решение

### 1. Обновление пакетов

| Пакет | Проект | Было | Стало |
|-------|--------|------|-------|
| Microsoft.EntityFrameworkCore.Design | Infrastructure | 10.0.0 | 10.0.11 |
| Microsoft.EntityFrameworkCore.Design | Api | 10.0.0 | 10.0.11 |
| Npgsql.EntityFrameworkCore.PostgreSQL | Infrastructure | 10.0.0 | 10.0.3 |
| Swashbuckle.AspNetCore | Api | 10.2.3 | актуальная |

### 2. Команды для обновления

```bash
# Infrastructure
dotnet add src/Infrastructure package Microsoft.EntityFrameworkCore.Design --version 10.0.11
dotnet add src/Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.3

# Api
dotnet add src/Applications/Api package Microsoft.EntityFrameworkCore.Design --version 10.0.11
dotnet add src/Applications/Api package Swashbuckle.AspNetCore
```

### 3. Проверка после обновления

```bash
# Сборка
dotnet build

# Unit-тесты
dotnet test tests/Unit

# E2E тесты
dotnet test tests/Integration

# Проверка уязвимостей
dotnet list package --vulnerable --include-transitive
```

---

## Результат

| Пакет | Версия | Безопасная | Уровень | Статус |
|-------|--------|------------|---------|--------|
| System.Security.Cryptography.Xml | 10.0.11 | 10.0.11 | High | **Устранено** |
| Microsoft.OpenApi | актуальная | актуальная | High | **Устранено** |

**Статус:** Все уязвимости устранены. Проверка `dotnet list package --vulnerable` не возвращает ошибок.

---

## Методика проверки

### 1. Проверка NuGet-пакетов на уязвимости

```bash
# Проверка всех пакетов решения (включая транзитивные)
dotnet list SamorodinkaTech.Mnemonios.slnx package --vulnerable --include-transitive

# Проверка конкретного проекта
dotnet list src/Infrastructure package --vulnerable --include-transitive
dotnet list src/Applications/Api package --vulnerable --include-transitive
```

Команда проверяет все установленные пакеты (прямые и транзитивные) по базе данных NuGet Security Advisories.

### 2. Проверка устаревших пакетов

```bash
# Проверка на наличие более новых версий
dotnet list src/Infrastructure package --outdated --include-transitive
dotnet list src/Applications/Api package --outdated --include-transitive
```

Помогает выявить пакеты, для которых доступны обновления безопасности.

### 3. Проверка предупреждений при сборке

```bash
# Сборка с выводом предупреждений
dotnet build 2>&1 | grep "NU1903"
```

Предупреждение `NU1903` указывает на уязвимости в зависимостях.

### 4. Источники данных

| Источник | Описание | URL |
|----------|----------|-----|
| NuGet Security Advisories | База уязвимостей пакетов NuGet | https://www.nuget.org/advisories |
| GitHub Security Advisories | GHSA-идентификаторы уязвимостей | https://github.com/advisories |
| Microsoft Security Response Center | Уязвимости в компонентах Microsoft | https://msrc.microsoft.com/ |
| **БДУ ФСТЭК** | **Банк данных угроз безопасности информации** | **https://bfriters.fstec.ru/bdu/** |

#### Российский реестр уязвимостей (БДУ ФСТЭК)

Банк данных угроз безопасности информации (БДУ ФСТЭК) — официальный государственный ресурс, ведущийся Федеральной службой по техническому и экспортному контролю. Содержит сведения об уязвимостях, актуальных для информационных систем в Российской Федерации.

**Проверка наличия уязвимости:**
1. Перейти на https://bfriters.fstec.ru/bdu/
2. Ввести CVE-идентификатор или описание уязвимости
3. Проверить наличие записи в банке данных

**Обязательность проверки:**
- Для критических информационных систем (КИС) проверка БДУ ФСТЭК обязательна в соответствии с Приказом ФСТЭК России № 21
- Для остальных систем — рекомендуется

### 5. Автоматическая проверка в CI/CD

Для постоянного мониторинга рекомендуется добавить проверку в пайплайн:

```yaml
# Пример для GitHub Actions
- name: Check for vulnerable packages
  run: |
    dotnet list package --vulnerable --include-transitive | grep -q "has the following known vulnerable packages" && exit 1 || exit 0
```
