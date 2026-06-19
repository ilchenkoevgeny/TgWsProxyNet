# TgWsProxyNet10

Порт идеи [`Flowseal/tg-ws-proxy`](https://github.com/Flowseal/tg-ws-proxy) на C# / .NET 10 LTS.

Цель проекта — запускать локальный Telegram MTProto WebSocket proxy как обычный Windows Service без Python, venv, PyInstaller, NSSM и иконки в трее.

> Важно: это первичная C#-реализация ядра прокси. Она сделана как прозрачный порт основных механизмов: локальный TCP listener, MTProto obfuscated handshake, AES-CTR re-encrypt, WebSocket `/apiws`, direct TCP fallback. Tray UI, Cloudflare fallback, fake TLS и pool warmup сюда сознательно не перенесены.

## Схема работы

```text
Telegram Desktop
    ↓
127.0.0.1:1443
    ↓
TgWsProxy.exe / Windows Service
    ↓
MTProto obfuscation decrypt/re-encrypt
    ↓
WebSocket /apiws к Telegram DC
    ↓
Telegram DC
```

## Что реализовано

- .NET 10 Worker Service.
- Запуск как Windows Service через стандартный `sc.exe`.
- Локальный TCP listener `127.0.0.1:1443`.
- Разбор 64-байтного MTProto obfuscation handshake.
- AES-CTR transform без внешних крипто-пакетов.
- Raw WebSocket client с ручной сборкой frame-ов.
- WebSocket bridge с re-encrypt в обе стороны.
- Direct TCP fallback на Telegram DC.
- Конфиг через `appsettings.json`.
- Простой файловый логгер без Serilog/NLog.
- Опция `SkipTlsCertificateValidation` вынесена в конфиг.

## Что не реализовано в этой версии

- Tray UI.
- Cloudflare proxy fallback.
- Cloudflare Worker fallback.
- Fake TLS / `ee` secret.
- PROXY protocol.
- WebSocket pool warmup.
- Auto-update.

## Подготовка

Нужен установленный .NET 10 SDK.

Проверка:

```powershell
dotnet --info
```

## Генерация secret

```powershell
.\tools\generate-secret.ps1
```

Скрипт выведет:

```text
Internal secret: 32_HEX_CHARS
Telegram secret: dd32_HEX_CHARS
```

В `appsettings.json` нужно вставлять именно **Internal secret**, без `dd`.

В Telegram Desktop нужно вставлять **Telegram secret**, то есть с префиксом `dd`.

## Настройка appsettings.json

Открой:

```text
src/TgWsProxy/appsettings.json
```

И замени:

```json
"Secret": "CHANGE_ME_32_HEX_CHARS"
```

на свой 32-символьный secret.

Для безопасного локального использования оставь:

```json
"Host": "127.0.0.1"
```

Не ставь `0.0.0.0`, если не понимаешь последствия.

## Запуск из консоли

```powershell
cd .\src\TgWsProxy
dotnet run
```

В Telegram Desktop:

```text
Тип: MTProto
Server: 127.0.0.1
Port: 1443
Secret: dd + твой internal secret
```

Например, если в `appsettings.json`:

```text
d032632e7eb3e05c579ea4fc59ae011d
```

то в Telegram:

```text
ddd032632e7eb3e05c579ea4fc59ae011d
```

Первые две `d` — это префикс `dd`, третья `d` — первая буква самого secret.

## Publish

```powershell
cd .\src\TgWsProxy

dotnet publish -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true
```

Готовый exe будет здесь:

```text
src\TgWsProxy\bin\Release\net10.0\win-x64\publish\TgWsProxy.exe
```

## Установка Windows Service

После publish можно использовать скрипт:

```powershell
.\tools\install-service.ps1 `
  -ExePath "D:\Services\TgWsProxy\TgWsProxy.exe" `
  -ServiceName "TgWsProxy"
```

Либо вручную:

```powershell
sc.exe create TgWsProxy binPath= "D:\Services\TgWsProxy\TgWsProxy.exe" start= auto
sc.exe start TgWsProxy
```

## Удаление Windows Service

```powershell
.\tools\uninstall-service.ps1 -ServiceName "TgWsProxy"
```

## Проверка порта

```powershell
netstat -ano | findstr :1443
```

Нормально:

```text
127.0.0.1:1443
```

Плохо для локального сценария:

```text
0.0.0.0:1443
```

## Логи

По умолчанию лог пишется в:

```text
logs/tg-ws-proxy.log
```

Если приложение запущено как single-file service, относительный путь считается от папки с exe.

## Безопасность

- Не публикуй `appsettings.json`, если там указан реальный secret.
- Не публикуй логи, если там есть ссылка `tg://proxy`.
- Для локального использования держи `Host = 127.0.0.1`.
- `SkipTlsCertificateValidation = true` оставлено для совместимости с поведением оригинального Python-проекта. Если у тебя работает с `false`, лучше поставить `false`.

## Лицензия

Исходный проект `Flowseal/tg-ws-proxy` распространяется под MIT License. Этот порт также поставляется под MIT License, с сохранением NOTICE.
