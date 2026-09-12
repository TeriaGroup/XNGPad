# XNGPad

Windows-пульт для локального [SearXNG](https://docs.searxng.org/) в Docker и MCP в [OpenCode](https://opencode.ai). JSON руками не правится.

A Windows remote for local [SearXNG](https://docs.searxng.org/) in Docker and MCP servers in [OpenCode](https://opencode.ai). No hand-editing JSON.

---

## RU

### Что это

XNGPad сам не ищет в интернете. Он:

- проверяет Docker Desktop и поднимает SearXNG на `http://127.0.0.1:8888`
- ставит Node.js, Docker Desktop и OpenCode, если их нет
- пишет MCP в `%USERPROFILE%\.config\opencode\opencode.json` (локальный `npx` или remote)
- подтягивает каталог с [MCP Registry](https://registry.modelcontextprotocol.io/), плюс свой список
- ищет по каталогу, фильтрует (все / нужен ключ / без ключа / npx / удалённые), скрывает лишнее
- для токенов открывает короткое окно и сам прописывает ключ в конфиг
- UI на русском и английском; описания MCP с Registry в русском режиме переводятся

Docker внутрь себя не встраивает и молча не ставит. SearXNG слушает только localhost.

### Как пользоваться

1. Скачай релиз `XNGPad.exe` (self-contained, .NET ставить не нужно).
2. **Скачать Docker** → установи Docker Desktop → дождись зелёного кита. Если программа его не видит — перезапусти ПК.
3. **Включить SearXNG**. Первый раз качает образ.
4. **Открыть SearXNG** — в браузере должен открыться поиск.
5. **Установка** — Node.js, OpenCode и каталог MCP. Добавь нужные серверы (для части нужен API-ключ).
6. **Подключить к OpenCode**. Перезапусти OpenCode и в чате: `use searxng`.

### Как отключить

- **Выключить SearXNG** — останавливает контейнер `xngpad-searxng`.
- **Отключить от OpenCode** — удаляет только `mcp.searxng`, остальной конфиг не трогает.
- В каталоге MCP у каждого сервера есть **Убрать**.

Перед записью `opencode.json` делается бэкап `opencode.json.bak-xngpad-…`. Секреты в лог не пишутся.

Настройки: `%AppData%\XNGPad\settings.json`.

### Сборка

Нужен .NET 10 SDK.

```bat
dotnet test
dotnet publish XNGPad.App\XNGPad.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

Готовый файл: `XNGPad.App\bin\Release\net10.0-windows\win-x64\publish\XNGPad.exe`.

---

## EN

### What it is

XNGPad is not a search engine. It:

- checks Docker Desktop and starts SearXNG at `http://127.0.0.1:8888`
- can install Node.js, Docker Desktop, and OpenCode
- writes MCP entries into `%USERPROFILE%\.config\opencode\opencode.json` (local `npx` or remote)
- loads the [MCP Registry](https://registry.modelcontextprotocol.io/) plus a built-in list
- searches and filters the catalog (all / needs key / no key / npx / remote), hide/restore
- asks for API tokens in a small window and writes them into the OpenCode config
- UI in Russian and English; Registry descriptions are translated when the UI is Russian

It does not bundle Docker and will not install it silently. SearXNG binds to localhost only.

### How to use

1. Download `XNGPad.exe` from Releases (self-contained, no .NET install).
2. **Download Docker**, install Docker Desktop, wait for the green whale. Restart the PC if XNGPad still cannot see it.
3. **Enable SearXNG**. The first run may pull the image.
4. **Open SearXNG** — search should open in the browser.
5. **Install** — Node.js, OpenCode, and the MCP catalog. Add the servers you need (some require an API key).
6. **Connect to OpenCode**. Restart OpenCode and type `use searxng`.

### How to turn it off

- **Disable SearXNG** — stops the `xngpad-searxng` container.
- **Disconnect OpenCode** — removes only `mcp.searxng`, leaves the rest of the file.
- Each MCP card has **Remove**.

A backup `opencode.json.bak-xngpad-…` is created before writing. Secrets are not logged.

Settings: `%AppData%\XNGPad\settings.json`.

### Build

Requires the .NET 10 SDK.

```bat
dotnet test
dotnet publish XNGPad.App\XNGPad.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

Output: `XNGPad.App\bin\Release\net10.0-windows\win-x64\publish\XNGPad.exe`.
