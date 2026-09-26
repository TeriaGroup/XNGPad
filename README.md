# XNGPad

Windows-пульт для локального [SearXNG](https://docs.searxng.org/) в Docker и MCP в [OpenCode](https://opencode.ai). JSON руками не правится.

A Windows remote for local [SearXNG](https://docs.searxng.org/) in Docker and MCP servers in [OpenCode](https://opencode.ai). No hand-editing JSON.

Релиз: [v1.2.1](https://github.com/TeriaGroup/XNGPad/releases/tag/v1.2.1)

---

## RU

### Что это

XNGPad сам не ищет в интернете. Он:

- проверяет Docker Desktop и поднимает SearXNG на `http://127.0.0.1:8888`
- ставит Node.js, Docker Desktop и OpenCode, если их нет
- пишет MCP в `%USERPROFILE%\.config\opencode\opencode.json`
- подтягивает каталог с [MCP Registry](https://registry.modelcontextprotocol.io/) и свой список
- в окне **Установка** один список и фильтр: все, добавленные, доступные, неинтересные, использованные, нужен ключ, без ключа, npx, удалённые
- удалённые MCP пускает через локальный прокси: OpenCode иначе отвергает их схемы
- для КОМПАС-3D при добавлении спрашивает инвайт и папку с `kStudy.exe`, затем сам запускает КОМПАС
- для токенов открывает короткое окно и сам прописывает ключ
- UI на русском и английском; описания с Registry в русском режиме переводятся

Docker внутрь себя не встраивает и молча не ставит. SearXNG слушает только localhost.

### Как пользоваться

1. Скачай `XNGPad.exe` из [релизов](https://github.com/TeriaGroup/XNGPad/releases) (self-contained, .NET ставить не нужно).
2. **Скачать Docker** → установи Docker Desktop → дождись зелёного кита. Если программа его не видит — перезапусти ПК.
3. **Включить SearXNG**. Первый раз качает образ.
4. **Открыть SearXNG** — в браузере должен открыться поиск.
5. **Установка** — Node.js, OpenCode и каталог MCP. Поиск и фильтр справа. Добавь нужные серверы.
6. **Подключить к OpenCode**. **Запуск** открывает OpenCode, **Рестарт** перезапускает его. В чате: `use searxng`.

Удалённые без поиска — только уже подключённые и свой список. Остальные из Registry видны, если ввести запрос.

КОМПАС: выбери папку установки (или `Bin`, где лежит `kStudy.exe`). MCP сам откроет КОМПАС, если он закрыт.

### Как отключить

- **Выключить SearXNG** — останавливает контейнер `xngpad-searxng`.
- **Стоп** — закрывает OpenCode.
- В **Настройках**: отключить SearXNG от OpenCode (только `mcp.searxng`), открыть папку конфига, показать лог контейнера.
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
- writes MCP entries into `%USERPROFILE%\.config\opencode\opencode.json`
- loads the [MCP Registry](https://registry.modelcontextprotocol.io/) plus a built-in list
- in **Install**, one list and a filter: all, added, available, hidden, used, needs key, no key, npx, remote
- sends remote MCPs through a local proxy so OpenCode accepts their schemas
- for KOMPAS-3D, asks for the invite and the folder that contains `kStudy.exe`, then starts KOMPAS itself
- asks for API tokens in a small window and writes them into the OpenCode config
- UI in Russian and English; Registry descriptions are translated when the UI is Russian

It does not bundle Docker and will not install it silently. SearXNG binds to localhost only.

### How to use

1. Download `XNGPad.exe` from [Releases](https://github.com/TeriaGroup/XNGPad/releases) (self-contained, no .NET install).
2. **Download Docker**, install Docker Desktop, wait for the green whale. Restart the PC if XNGPad still cannot see it.
3. **Enable SearXNG**. The first run may pull the image.
4. **Open SearXNG** — search should open in the browser.
5. **Install** — Node.js, OpenCode, and the MCP catalog. Search box, filter on the right. Add the servers you need.
6. **Connect to OpenCode**. **Start** launches OpenCode, **Restart** relaunches it. In chat: `use searxng`.

Remote without a search shows only connected servers and the built-in list. Other Registry remotes appear when you type a query.

KOMPAS: pick the install folder (or `Bin`, where `kStudy.exe` lives). The MCP starts KOMPAS if it is not already open.

### How to turn it off

- **Disable SearXNG** — stops the `xngpad-searxng` container.
- **Stop** — closes OpenCode.
- In **Settings**: disconnect SearXNG from OpenCode (only `mcp.searxng`), open the config folder, show the container log.
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
