#!/usr/bin/env python3
"""
Проверка компонентов проекта по БДУ ФСТЭК.
Скачивает XML-базу данных и проверяет наличие уязвимостей.
"""

import xml.etree.ElementTree as ET
import subprocess
import os
import sys
from typing import List, Dict
from packaging import version

# Компоненты проекта для проверки
COMPONENTS = [
    {"name": "PostgreSQL", "version": "16", "type": "СУБД"},
    {"name": "Entity Framework Core", "version": "10.0.11", "type": "ORM"},
    {"name": "Npgsql.EntityFrameworkCore.PostgreSQL", "version": "10.0.3", "type": "Провайдер"},
    {"name": "ASP.NET Core", "version": "10.0", "type": "Фреймворк"},
    {"name": ".NET", "version": "10.0", "type": "Runtime"},
]

def download_fstec_database(output_path: str) -> bool:
    """Скачивает XML-базу данных БДУ ФСТЭК."""
    url = "https://bdu.fstec.ru/files/documents/vulxml.zip"
    
    print(f"Скачивание базы данных БДУ ФСТЭК с {url}...")
    
    result = subprocess.run(
        ["curl", "-k", "-L", "-o", output_path, url, 
         "-H", "User-Agent: Mozilla/5.0",
         "-s", "-w", "%{http_code}"],
        capture_output=True,
        text=True
    )
    
    http_code = result.stdout.strip()
    if http_code != "200":
        print(f"Ошибка скачивания: HTTP {http_code}")
        return False
    
    print(f"База данных скачана: {os.path.getsize(output_path)} байт")
    return True

def extract_zip(zip_path: str, extract_dir: str) -> bool:
    """Извлекает ZIP-архив."""
    print(f"Извлечение архива {zip_path}...")
    
    result = subprocess.run(
        ["unzip", "-o", zip_path, "-d", extract_dir],
        capture_output=True,
        text=True
    )
    
    if result.returncode != 0:
        print(f"Ошибка извлечения: {result.stderr}")
        return False
    
    print("Архив извлечён успешно")
    return True

def parse_fstec_xml(xml_path: str) -> List[Dict]:
    """Парсит XML-файл БДУ ФСТЭК и возвращает список уязвимостей."""
    print(f"Парсинг XML-файла {xml_path}...")
    
    vulnerabilities = []
    
    try:
        # Используем iterparse для экономии памяти
        for event, elem in ET.iterparse(xml_path, events=['end']):
            if elem.tag == 'vul':
                vuln = parse_vulnerability(elem)
                if vuln:
                    vulnerabilities.append(vuln)
                elem.clear()  # Освобождаем память
        
        print(f"Найдено уязвимостей: {len(vulnerabilities)}")
        return vulnerabilities
        
    except ET.ParseError as e:
        print(f"Ошибка парсинга XML: {e}")
        return []

def parse_vulnerability(elem) -> Dict:
    """Парсит один элемент уязвимости."""
    try:
        identifier = elem.findtext('identifier', '')
        name = elem.findtext('name', '')
        description = elem.findtext('description', '')
        severity = elem.findtext('severity', '')
        
        # Извлекаем CVE
        cve = ''
        identifiers = elem.find('identifiers')
        if identifiers is not None:
            for ident in identifiers.findall('identifier'):
                if ident.get('type') == 'CVE':
                    cve = ident.text or ''
                    break
        
        # Извлекаем затронутое ПО
        affected_software = []
        vulnerable_software = elem.find('vulnerable_software')
        if vulnerable_software is not None:
            for soft in vulnerable_software.findall('soft'):
                soft_name = soft.findtext('name', '')
                vendor = soft.findtext('vendor', '')
                version_str = soft.findtext('version', '')
                if soft_name:
                    affected_software.append({
                        'name': soft_name,
                        'vendor': vendor,
                        'version': version_str
                    })
        
        return {
            'identifier': identifier,
            'name': name,
            'description': description[:200] + '...' if len(description) > 200 else description,
            'cve': cve,
            'severity': severity,
            'affected_software': affected_software
        }
    except Exception as e:
        return None

def parse_version_range(version_str: str):
    """Парсит строку с версией и возвращает (start_version, end_version)."""
    version_str = version_str.strip()
    
    if not version_str:
        return None, None
    
    # Обработка "от X до Y"
    if 'от' in version_str and 'до' in version_str:
        parts = version_str.replace('от', '').replace('до', '').split('включительно')
        if len(parts) >= 2:
            try:
                start = version.parse(parts[0].strip())
                end = version.parse(parts[1].strip())
                return start, end
            except:
                return None, None
    
    # Обработка "до X"
    if 'до' in version_str:
        ver_str = version_str.replace('до', '').replace('включительно', '').strip()
        try:
            end = version.parse(ver_str)
            return None, end
        except:
            return None, None
    
    # Обработка "от X"
    if 'от' in version_str:
        ver_str = version_str.replace('от', '').strip()
        try:
            start = version.parse(ver_str)
            return start, None
        except:
            return None, None
    
    return None, None

def is_version_affected(component_version: str, affected_version_str: str) -> bool:
    """Проверяет, затронута ли версия компонента уязвимостью."""
    try:
        comp_ver = version.parse(component_version)
        start_ver, end_ver = parse_version_range(affected_version_str)
        
        if start_ver is None and end_ver is None:
            return True  # Если не удалось распарсить, считаем что затронуто
        
        if start_ver is not None and comp_ver < start_ver:
            return False
        
        if end_ver is not None and comp_ver > end_ver:
            return False
        
        return True
    except:
        return True

def check_component(vulnerabilities: List[Dict], component: Dict) -> List[Dict]:
    """Проверяет наличие уязвимостей для конкретного компонента."""
    found = []
    
    for vuln in vulnerabilities:
        for software in vuln['affected_software']:
            # Проверяем совпадение названия ПО
            if component['name'].lower() in software['name'].lower():
                # Проверяем версию
                if is_version_affected(component['version'], software['version']):
                    found.append({
                        'vulnerability': vuln,
                        'software': software
                    })
                    break
    
    return found

def main():
    # Директория для временных файлов
    temp_dir = "/tmp/fstec_check"
    os.makedirs(temp_dir, exist_ok=True)
    
    zip_path = os.path.join(temp_dir, "vulxml.zip")
    xml_path = os.path.join(temp_dir, "export", "vulxml.xml")
    
    # Скачиваем базу данных
    if not os.path.exists(xml_path):
        if not download_fstec_database(zip_path):
            sys.exit(1)
        
        if not extract_zip(zip_path, temp_dir):
            sys.exit(1)
    
    # Парсим XML
    vulnerabilities = parse_fstec_xml(xml_path)
    
    if not vulnerabilities:
        print("Не удалось распарсить базу данных")
        sys.exit(1)
    
    # Проверяем компоненты
    print("\n" + "=" * 60)
    print("РЕЗУЛЬТАТЫ ПРОВЕРКИ ПО БДУ ФСТЭК")
    print("=" * 60)
    
    all_findings = []
    
    for component in COMPONENTS:
        print(f"\nПроверка: {component['name']} {component['version']}")
        print("-" * 40)
        
        findings = check_component(vulnerabilities, component)
        
        if findings:
            print(f"  Найдено уязвимостей: {len(findings)}")
            for f in findings[:5]:  # Показываем первые 5
                vuln = f['vulnerability']
                print(f"  - {vuln['identifier']}: {vuln['name'][:80]}...")
                if vuln['cve']:
                    print(f"    CVE: {vuln['cve']}")
            if len(findings) > 5:
                print(f"  ... и ещё {len(findings) - 5}")
            
            all_findings.extend(findings)
        else:
            print("  Уязвимостей не найдено")
    
    # Итоговый отчёт
    print("\n" + "=" * 60)
    print("ИТОГИ")
    print("=" * 60)
    print(f"Всего проверено компонентов: {len(COMPONENTS)}")
    print(f"Всего найдено уязвимостей: {len(all_findings)}")
    
    if all_findings:
        print("\nРекомендации:")
        print("1. Проверить наличие обновлений для затронутых компонентов")
        print("2. Обновить компоненты до последних версий")
        print("3. Провести повторную проверку после обновления")
    
    return 0 if not all_findings else 1

if __name__ == "__main__":
    sys.exit(main())
