---
title: Установка и запуск
description: Скачайте готовую сборку или соберите Firebird Trace Analyzer из исходников.
---

## Скачать релиз

Готовые сборки для Windows, macOS и Linux публикуются на странице
[**Releases**](https://github.com/deliciousNesquik/firebird-trace-analyzer/releases).
Скачайте архив под свою платформу, распакуйте и запустите приложение — установка не требуется.

## Сборка из исходников

### Что нужно

- [.NET 10 SDK](https://dotnet.microsoft.com/download).

### Клонировать, запустить, собрать

```bash
git clone https://github.com/deliciousNesquik/firebird-trace-analyzer.git
cd firebird-trace-analyzer

# запустить приложение
dotnet run --project FirebirdTraceAnalyzer

# либо собрать Release-сборку
dotnet build FirebirdTrace.sln -c Release
```

### Запустить тесты

```bash
dotnet test FirebirdTrace.sln
```

## Первый запуск

При первом запуске приложение создаёт папки конфигурации и данных в вашем пользовательском профиле
(например, настройки, опциональное хранилище событий и скачанные с сервера файлы). За пределами
профиля ничего не пишется, пока вы сами явно не выберете путь для экспорта.

Переходите к разделу [**Загрузка логов**](/firebird-trace-analyzer/ru/guides/loading-logs/), чтобы
открыть первый трейс-файл.
