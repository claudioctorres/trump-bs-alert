# trump-bs-alert

A lightweight Windows desktop app that monitors currency exchange rates and fires an intrusive,
sound-looping alert when the rate crosses a user-defined threshold.

Built for Brazilians who need to know the moment USD/BRL spikes — often triggered by erratic
political statements from abroad — before their bank account feels it.

## Features

- **Real-time monitoring** of any currency pair (default: USD/BRL)
- **30-day historical chart** so you can visually anchor your threshold
- **Intrusive alert window** with looping alarm sound — must be manually acknowledged
- **System tray** — lives quietly in the background, no visible window during normal operation
- **Configurable polling interval** (30s / 1m / 5m)
- **Auto-start with Windows** (optional, via registry)
- **Session history** showing the last 20 readings

## Screenshot

![Main window](docs/screenshot.png)

## Requirements

- Windows 10 21H1 or later
- .NET 10 Runtime (included if you download the self-contained release)

## Installation

### Option A: Download the release

1. Go to [Releases](../../releases) and download the latest `.zip`
2. Extract anywhere
3. Run `TrumpBsAlert.exe`

### Option B: Build from source

```bash
# Prerequisites: .NET 10 SDK with MAUI workload
dotnet workload install maui-windows
dotnet build -f net10.0-windows10.0.19041.0
dotnet run --project src/TrumpBsAlert -f net10.0-windows10.0.19041.0
```

## Usage

1. **Launch the app** — it opens the main window and adds an icon to the system tray
2. **Check the 30-day chart** to understand recent rate behavior
3. **Set a threshold** in the "Limite de alerta" field (e.g., `5.50`) and click **Salvar**
4. **Minimize or close** the window — the app keeps running in the system tray
5. **When the rate crosses your threshold**, an alert window pops up with a looping alarm sound
6. **Press "Entendido"** to acknowledge and dismiss the alert
7. The alert re-arms automatically — it will fire again on the next tick if the rate is still above,
   or when it crosses the threshold again after dropping below

### Tray icon

- **Left click**: Open the main window
- **Right click**: Context menu with "Sair" (quit)

### Changing the currency pair

Enter the base and quote currency codes (e.g., `EUR` / `USD`) and click **Alterar**.
The chart and live rate update to the new pair.

## Configuration

All settings are persisted automatically between sessions:

| Setting | Default | Description |
|---|---|---|
| Currency pair | USD / BRL | Any pair supported by the exchange rate APIs |
| Threshold | _(none)_ | No monitoring until you set a value |
| Polling interval | 1 minute | How often the rate is fetched |
| Minimize to tray | On | Close button minimizes instead of quitting |

## Data sources

- **Live rate**: [AwesomeAPI](https://economia.awesomeapi.com.br/) (free, no key required)
- **30-day history**: [Frankfurter API](https://api.frankfurter.app/) (free, no key required)

## Running tests

```bash
dotnet test
```

## Publishing

```bash
dotnet publish -f net10.0-windows10.0.19041.0 \
  -p:WindowsPackageType=None \
  -p:PublishSingleFile=true \
  -r win-x64 \
  --self-contained true
```

The output is a single `TrumpBsAlert.exe` in `src/TrumpBsAlert/bin/Release/.../publish/`.

## License

See [LICENSE](LICENSE).
