# CLAUDE.md — trump-bs-alert

This file defines constraints, conventions, and architecture rules for the trump-bs-alert project.
It is the source of truth for AI assistants (Claude Code, Copilot, etc.) and human contributors alike.
Read it fully before making any change.

---

## What this project is

A lightweight .NET MAUI Windows desktop application that monitors currency exchange rates and fires
an intrusive, sound-looping alert window when the rate crosses a user-defined threshold. The user must
explicitly acknowledge the alert to dismiss it. The app lives in the system tray and has no visible
window during normal operation.

The name reflects its origin: in Brazil, USD/BRL spikes are frequently triggered by erratic political
statements from abroad. The app was built to catch those moments before your bank account does.

The default pair is USD/BRL, but any pair supported by the exchange rate API can be configured.

**Target platform: Windows only.**
MAUI is used because it is the team's existing .NET stack, not for cross-platform ambition. Do not add
Android/iOS/macOS targets without opening a discussion first.

---

## Hard constraints — never violate these

- **One responsibility per class.** `ExchangeRateService` fetches. `AlertCoordinator` orchestrates.
  `SoundLoopService` plays audio. `TrayService` manages the icon. No class does two of these things.
- **No business logic in code-behind.** `AlertPage.xaml.cs` and `MainPage.xaml.cs` call commands
  or services only. No `if (rate >= threshold)` inside a page.
- **No static state.** Do not use `static` fields to share state between services. Use DI and
  constructor injection everywhere. `Application.Current` is the only acceptable static access point,
  and only from `MainThread` callbacks.
- **No blocking calls on the main thread.** All network I/O goes through `async/await`. If you need
  to touch the UI from a background thread, use `MainThread.InvokeOnMainThreadAsync`.
- **No raw `HttpClient` instantiation.** Always use `IHttpClientFactory`. The named client
  `"exchange"` is pre-configured in `MauiProgram.cs` with a base URL and a 10-second timeout.
- **Windows-only platform code lives in `Platforms/Windows/` only.** Use `#if WINDOWS` guards when
  a reference must exist in a shared file. Prefer moving the entire implementation to the platform
  folder and accessing it via an interface registered conditionally in DI.
- **`alert.wav` must stay under 500 KB.** It loops continuously. Large files waste memory.
- **The alert window must always be acknowledged manually.** Never auto-dismiss it on a timer,
  never close it when the rate drops back below the threshold, and never suppress it silently.
  The user must press the button.

---

## Architecture overview

```
MauiProgram.cs
  └── registers all services as singletons via IServiceCollection

App.xaml.cs
  └── starts BackgroundService host, shows tray icon, hides main window on startup

Services/
  ├── ExchangeRateService.cs     ← IHostedService, PeriodicTimer polling loop
  ├── HistoricalRateService.cs   ← fetches 30-day data from Frankfurter API
  ├── AlertCoordinator.cs        ← opens AlertPage window, owns alert state
  ├── SoundLoopService.cs        ← Windows MediaPlayer, loops until Stop()
  └── TrayService.cs             ← H.NotifyIcon wrapper, exposes menu commands

Pages/
  ├── MainPage.xaml              ← 30-day chart, threshold, pair selector, history
  └── AlertPage.xaml             ← intrusive alert with single ACK button

ViewModels/
  ├── MainViewModel.cs
  └── AlertViewModel.cs

Platforms/Windows/
  ├── NativeHelper.cs            ← P/Invoke: SetWindowPos, ShowWindow, registry
  └── App.xaml.cs                ← Windows-specific App partial

Resources/Raw/
  └── alert.wav                  ← looping alert sound, max 500 KB
```

---

## Service lifecycle rules

| Service | Lifetime | Notes |
|---|---|---|
| `ExchangeRateService` | Singleton / IHostedService | One instance for the app lifetime |
| `HistoricalRateService` | Singleton | Fetches once per launch; re-fetches on pair change |
| `AlertCoordinator` | Singleton | Holds `_isAlerting` flag, never concurrent |
| `SoundLoopService` | Singleton | Owns and disposes `MediaPlayer` |
| `TrayService` | Singleton | Created before the main window is hidden |
| ViewModels | Singleton | Registered once, reused across window reopens |

Do not register anything as Transient unless it has no state and no disposable resources.

---

## The polling loop — rules

`ExchangeRateService` uses `PeriodicTimer` (not `Task.Delay`, not `Timer`).

```csharp
// Correct pattern — do not change this structure
using var timer = new PeriodicTimer(Interval);
while (await timer.WaitForNextTickAsync(stoppingToken))
{
    var rate = await FetchRateAsync(stoppingToken);
    RateChanged?.Invoke(this, rate);

    if (Threshold is null) continue; // no alert until user sets a value

    if (rate >= Threshold && !_isAlerting)
        await _coordinator.TriggerAlertAsync(rate);

    if (rate < Threshold && _isAlerting)
        _isAlerting = false; // re-arm for the next crossing
}
```

Rules:
- `Threshold` is `decimal?`. When null, the loop skips the alert check entirely.
- `_isAlerting` is owned by `ExchangeRateService`, not `AlertCoordinator`.
- The alert only fires on a **rising edge** (was below, now at or above). It does not re-fire
  every tick while the rate stays high.
- The alert re-arms when: (a) the rate drops below the threshold, (b) the user acknowledges
  the alert, or (c) the `Threshold` value is changed.
- `Interval` and `Threshold` are mutable properties. Changing `Interval` requires restarting the
  service (call `StopAsync` then `StartAsync`). Changing `Threshold` resets the `_isAlerting` flag
  and takes effect on the next tick.

---

## Alert window rules

- Opens as a second `Window` instance via `Application.Current.OpenWindow(...)`.
- Pinned always-on-top using `SetWindowPos(hwnd, HWND_TOPMOST, ...)` immediately after opening.
- Fixed size: 420 × 290. Do not make it resizable.
- Contains: current rate (large), threshold value, timestamp, and one "Entendido / Acknowledged" button.
- No close button (override `Window.Destroying` to cancel if `_isAlerting` is still true).
- Calls `AlertCoordinator.Acknowledge()` on button press, which stops sound and closes the window.

---

## Sound rules

- Uses `Windows.Media.Playback.MediaPlayer` with `IsLoopingEnabled = true`.
- Source must be loaded from `AppContext.BaseDirectory` via file URI (unpackaged app; `ms-appx:///` does not work).
- `SoundLoopService.Start()` is idempotent: calling it twice does nothing if already playing.
- `SoundLoopService.Stop()` pauses and disposes the player, sets internal reference to null.
- Volume is fixed at 100% of system volume. Do not add a volume control — the point is urgency.

---

## Exchange rate API

Default: `https://open.er-api.com/v6/latest/USD` (free, no key required).

```csharp
record ExchangeApiResponse(string Result, Dictionary<string, decimal> Rates);
```

The base currency in the URL (`USD`) is derived from the configured `BaseCurrency` setting.
The target currency (`BRL` by default) is the `QuoteCurrency` setting.

Rules:
- Parse only `Rates[QuoteCurrency]`. Ignore every other field.
- If the response is non-2xx, null, or throws, log the error and skip the tick — do not alert on
  a failed fetch.
- Do not cache responses. Each tick is a fresh fetch.
- Do not add other exchange rate providers without updating this section and adding a provider
  abstraction (`IExchangeRateProvider`) so the service stays testable.

---

## Settings persistence

Settings are stored via `Microsoft.Maui.Storage.Preferences`. Keys:

| Key | Type | Default |
|---|---|---|
| `base_currency` | `string` | `"USD"` |
| `quote_currency` | `string` | `"BRL"` |
| `threshold` | `decimal` (stored as `double`) | `null` — no default, user must set |
| `interval_seconds` | `int` | `60` |
| `sound_enabled` | `bool` | `true` |

**Threshold has no hardcoded default.** On first launch, the threshold input is empty and the
polling service does not alert. The user must set a value manually — ideally informed by the
30-day chart on the main page. Monitoring only activates after a threshold is saved.

Do not use a JSON file, SQLite, or any other persistence mechanism for these values.
If new settings are needed, add them here first.

---

## What the main page shows

- **30-day rate chart** (rendered with `LiveChartsCore.SkiaSharpView.Maui`) showing the full
  month of the configured pair so the user can visually anchor their threshold choice.
  The threshold value is overlaid as a draggable horizontal line on the chart — dragging it
  updates the threshold input in real time and persists on release.
- Current rate (refreshed on each tick via event from `ExchangeRateService`)
- Currency pair selector: base and quote currency inputs (e.g. USD / BRL)
- Threshold input — starts empty on first launch, no monitoring until a value is saved.
  Inline validation: must be a positive decimal number.
- Interval selector (30s / 1m / 5m — restart service on change)
- Last-updated timestamp
- Session history: last 20 readings, newest first, showing rate and time
- Status badge: "Monitorando", "Sem limite definido", "Alerta ativo", "Erro na ultima busca"

No historical persistence across sessions. No export.

---

## 30-day chart

The chart is the primary tool for deciding where to set the threshold. It renders before the user
has configured anything and is the first thing visible on the main page.

**Data source:** `https://api.frankfurter.app/{from}..{to}?from=USD&to=BRL`
Frankfurter is free, requires no API key, and returns daily OHLC-style data back to 1999.
Use it only for historical chart data — the live rate still comes from `open.er-api.com`.

```csharp
// Services/HistoricalRateService.cs
public record DailyRate(DateOnly Date, decimal Close);

public async Task<IReadOnlyList<DailyRate>> GetLast30DaysAsync(
    string baseCurrency, string quoteCurrency, CancellationToken ct)
{
    var to   = DateOnly.FromDateTime(DateTime.UtcNow);
    var from = to.AddDays(-30);
    var url  = $"https://api.frankfurter.app/{from:yyyy-MM-dd}..{to:yyyy-MM-dd}"
             + $"?from={baseCurrency}&to={quoteCurrency}";
    // parse response["rates"] → List<DailyRate>
}
```

**Rendering:** `LiveChartsCore.SkiaSharpView.Maui` (add to approved packages list).
Use a `CartesianChart` with a single `LineSeries<DailyRate>`. No candlesticks, no volume bars.

**Threshold line:** rendered as a `ScatterSeries` with a single point at `(today, threshold)`,
connected to a full-width `RectangularSection` shading the danger zone above it.
When the threshold input changes, the section updates reactively via `ObservableValue`.

**Drag to set threshold:** the threshold line is draggable on the Y axis. On `PointerReleased`,
persist the new value via `Preferences`. This is the recommended way to set the threshold —
the numeric input is secondary.

**Chart loading states:**
- While fetching: show a skeleton shimmer (use `CommunityToolkit.Maui` skeleton view).
- On error: show a plain text message "Nao foi possivel carregar o historico" with a retry button.
  Do not block the rest of the page.

**Refresh:** chart data refreshes once per app launch, or when the currency pair changes.
It does not auto-refresh on every polling tick.

---



| Event | Action |
|---|---|
| Left click | Open/focus MainPage window |
| Right click | Context menu |
| Context menu → "Sair" | `Application.Current.Quit()` |

When an alert is active, the tray icon tooltip changes to `"🚨 USD/BRL = X.XXXX"`.
When idle, it shows `"trump-bs-alert — USD/BRL = X.XXXX"` (pair reflects current config).

---

## Auto-start with Windows

Implemented in `TrayService` using the registry key:
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run\TrumpBsAlert`

- Default: disabled.
- Exposed as a toggle in the main page settings.
- Writing the key uses `NativeHelper.SetAutoStart(bool enabled)`.
- Do not use the Task Scheduler or a Windows Service for this.

---

## Naming conventions

- **Services**: `{Noun}Service.cs` — `ExchangeRateService`, `SoundLoopService`
- **Coordinators**: `{Noun}Coordinator.cs` — `AlertCoordinator`
- **ViewModels**: `{Page}ViewModel.cs` — `MainViewModel`, `AlertViewModel`
- **Pages**: `{Name}Page.xaml` — `MainPage`, `AlertPage`
- **Interfaces**: `I{Name}` — `IExchangeRateProvider`, `ISoundLoopService`
- **Events**: past tense — `RateChanged`, `AlertTriggered`, `AlertAcknowledged`
- **Boolean properties**: `Is` prefix — `IsAlerting`, `IsSoundEnabled`
- Localization strings live in `Resources/Strings/AppResources.resx`. Do not hardcode UI strings
  in C# or XAML — even if the app is currently Portuguese-only.

---

## What not to add without discussion

- Any additional NuGet package beyond the approved list below.
- A database or file-based log.
- Push notifications or cloud sync.
- Cross-platform targets (Android, iOS, macOS).
- A web dashboard or REST API.
- Authentication of any kind.

If you think one of these is needed, open a GitHub issue with justification before implementing.

---

## Approved NuGet packages

| Package | Purpose | Version policy |
|---|---|---|
| `H.NotifyIcon.Maui` | System tray | Pin major version |
| `CommunityToolkit.Maui` | MAUI helpers + skeleton view | Pin major version |
| `CommunityToolkit.Mvvm` | Source-gen MVVM | Pin major version |
| `LiveChartsCore.SkiaSharpView.Maui` | 30-day chart rendering | Pin major version |
| `Microsoft.Extensions.Hosting` | BackgroundService | Match .NET 10 version |

Do not add transitive dependencies directly. If a package pulls in something useful, reference it
through the parent package only.

---

## Testing

Unit tests live in `TrumpBsAlert.Tests/`. Use xUnit 3 + NSubstitute (.NET 10).

What must be tested:
- `ExchangeRateService`: rising-edge detection, re-arm after drop, no alert on failed fetch,
  no alert when `Threshold` is null.
- `HistoricalRateService`: correct date range construction, graceful handling of API errors,
  correct mapping of response JSON to `DailyRate` list.
- `AlertCoordinator`: idempotent `TriggerAlertAsync`, state reset after `Acknowledge`.
- `MainViewModel`: threshold/interval persistence round-trips, null threshold → "Sem limite definido" status.

What is explicitly not tested:
- XAML rendering (no UI tests).
- `SoundLoopService` (depends on Windows `MediaPlayer`).
- `NativeHelper` P/Invokes.

To run: `dotnet test` from the repo root.

---

## Running locally

```bash
# Prerequisites: Visual Studio 2022 17.12+, .NET 10 MAUI workload, Windows 10 21H1+
dotnet workload install maui-windows
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0
```

Publishing as unpackaged (no MSIX, simple .exe):
```bash
dotnet publish -f net10.0-windows10.0.19041.0 \
  -p:WindowsPackageType=None \
  -p:PublishSingleFile=true \
  -r win-x64 \
  --self-contained true
```

---

## Forking checklist

If you fork this project, update these before shipping:

- [ ] Replace `alert.wav` with your own sound (license-clear)
- [ ] Update the tray icon (`Resources/Images/tray_icon.ico`)
- [ ] Change the registry key name in `NativeHelper.SetAutoStart` to avoid collisions
      with the original app if both are installed
- [ ] Review the default currency pair for your use case
      (defaults are USD/BRL — threshold has no default and must be set by the user)
- [ ] If changing the exchange rate API, implement `IExchangeRateProvider` and register
      the new implementation in `MauiProgram.cs`
- [ ] Update `README.md` — do not ship with the original project description

---

## PR checklist

Before opening a pull request:

- [ ] `dotnet build` passes with zero warnings
- [ ] `dotnet test` passes
- [ ] No new `static` state introduced
- [ ] No business logic added to code-behind
- [ ] No new NuGet packages added without updating the approved list above
- [ ] `CLAUDE.md` updated if any architecture decision changed
