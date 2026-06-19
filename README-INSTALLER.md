# TgWsProxy installer

Этот патч добавляет Inno Setup installer для TgWsProxy.

## Что делает инсталлер

- копирует опубликованный `TgWsProxy.exe` в `Program Files\TgWsProxy`;
- копирует `appsettings.json`, если его там еще нет;
- спрашивает internal secret на этапе установки;
- заменяет `CHANGE_ME_32_HEX_CHARS` в `appsettings.json` на введенный secret;
- создает или обновляет Windows Service `TgWsProxy`;
- выставляет автозапуск службы;
- при удалении останавливает и удаляет службу.

## Подготовка

Перед сборкой публичного инсталлера в `src\TgWsProxy\appsettings.json` лучше оставить:

```json
"Secret": "CHANGE_ME_32_HEX_CHARS"
```

Настоящий secret не коммить в GitHub и не клади в публичный релиз.

## Сборка

Установи Inno Setup 6, затем из корня проекта выполни:

```powershell
.\tools\build-installer.ps1 -Version "1.0.0"
```

Результат будет здесь:

```text
artifacts\installer\TgWsProxyNet-Setup-1.0.0-win-x64.exe
```

## Что вводить в Telegram Desktop

Если при установке ты ввел internal secret:

```text
d032632e7eb3e05c579ea4fc59ae011d
```

то в Telegram Desktop нужно вводить secret с префиксом `dd`:

```text
ddd032632e7eb3e05c579ea4fc59ae011d
```

Параметры:

```text
Type: MTProto
Server: 127.0.0.1
Port: 1443
Secret: dd + internal secret
```
