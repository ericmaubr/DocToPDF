# Prompt: Build a Windows Service + Tray UI app (.NET 8)

> Reusable prompt for generating a Windows application that runs as a **headless
> Windows Service** and talks to a **system-tray UI** in the user's session over a
> **named pipe**. The hard part is *not* the domain work — it is the service /
> Session-0 / IPC / tray scaffolding. This prompt prescribes that scaffolding in
> detail, including the non-obvious snippets that are easy to get wrong.
>
> Fill in the **PROJECT PARAMETERS** block, then follow the architecture exactly.
> Replace the *domain worker* placeholder with your actual logic; leave the
> scaffolding as specified.

---

## PROJECT PARAMETERS (fill these in)

```
APP_NAME            = MyApp                 # assembly + exe name
SERVICE_NAME        = MyApp                 # Windows Service name (sc create)
ROOT_NAMESPACE      = MyApp
PIPE_NAME           = MyApp.IPC.v1          # must be stable across versions
INSTALL_DIR         = C:\MyApp              # where the published exe lives
CONFIG_FILE         = MyApp.conf            # sits next to the exe
WORKER_DESCRIPTION  = <one sentence: what the background worker actually does>
WORKER_SETTINGS     = <list the config keys the worker needs>
UI_LANGUAGE         = <e.g. English / Portuguese — affects user-facing strings only>
```

---

## 1. Goal

Produce a single .NET 8 Windows executable that runs in **three modes** from the
same binary:

| Mode | Started by | Responsibility |
|------|------------|----------------|
| **Service** | SCM (`services.msc`) | Domain worker + IPC server. **No UI.** Runs in Session 0. |
| **Tray (attached)** | Auto-launched by the service into the user session | Tray icon + control panel; talks to the service over the pipe. No local worker. |
| **Standalone (local)** | User runs the exe directly with no service active | Worker + tray in the same process. |

Hard rules:

- **The service must never create a window, dialog, or tray icon.** Session 0
  isolation makes any UI invisible and can hang or crash the service.
- **The UI always runs in the interactive user session** and communicates with the
  service only through the named pipe (local IPC).
- If the service is not running, the user-launched exe **degrades gracefully to
  local mode** automatically.

```
  [Service]  Session 0                 [exe]  User session
   Worker + IPC  ◄──── named pipe ────►  Tray + panel
                  "PIPE_NAME"
```

---

## 2. Entry point — mode selection

`Program.Main` must be `[STAThread]` (WinForms tray). Select mode in this order:

```csharp
[STAThread]
static void Main(string[] args)
{
    if (IsHelp(args)) { ShowUsage(); return; }

    if (IsServiceMode(args)) { RunAsWindowsService(); return; }

    var attach = args.Contains("--attach-service", StringComparer.OrdinalIgnoreCase);
    RunInteractiveTray(attach);
}

// KEY: "not interactive" means SCM started us → service mode, even without the flag.
private static bool IsServiceMode(string[] args) =>
    args.Contains("--service", StringComparer.OrdinalIgnoreCase)
    || !Environment.UserInteractive;
```

CLI surface: `--service`, `--attach-service`, `--help`/`-h`/`/?`. (Add `--verify`
or similar for headless CI checks if useful.)

---

## 3. The service host

Use the Generic Host with `UseWindowsService`. Critical: set
`BackgroundServiceExceptionBehavior.StopHost` so a crashing worker stops the
service (and is visible in logs / SCM) instead of being silently swallowed.

```csharp
private static void RunAsWindowsService()
{
    ServiceLog.Initialize();                 // file logging FIRST — no console exists

    if (LocalModeLock.IsHeld())              // refuse to double-process (see §7)
    {
        ServiceLog.Error("Standalone instance active; service will not start.");
        Environment.Exit(1);
    }

    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        { if (e.ExceptionObject is Exception ex) ServiceLog.Fatal(ex, "UnhandledException"); };
    TaskScheduler.UnobservedTaskException += (_, e) =>
        { ServiceLog.Fatal(e.Exception, "UnobservedTaskException"); e.SetObserved(); };

    Host.CreateDefaultBuilder()
        .UseWindowsService(o => o.ServiceName = "SERVICE_NAME")
        .ConfigureServices(services =>
        {
            services.Configure<HostOptions>(o =>
                o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost);

            services.AddSingleton<SettingsStore>();
            services.AddSingleton<IpcServer>();
            services.AddSingleton<Worker>();          // your domain worker
            services.AddHostedService<BackgroundService>();
        })
        .Build()
        .Run();
}
```

The hosted `BackgroundService.ExecuteAsync` should: start the IPC server, start the
worker, then **schedule the tray auto-launch on a short delay** and idle until
cancellation:

```csharp
protected override async Task ExecuteAsync(CancellationToken stop)
{
    _ipcServer.Start(_worker);
    await _worker.StartAsync(stop);

    _ = Task.Run(async () =>
    {
        try { await Task.Delay(1500, stop); UserSessionLauncher.TryLaunchUiInUserSession(); }
        catch (Exception ex) { ServiceLog.Error($"Auto-launch tray: {ex.Message}"); }
    }, stop);

    await Task.Delay(Timeout.Infinite, stop);
}
```

`StopAsync` must stop the worker and dispose the IPC server.

---

## 4. IPC protocol (named pipe) — the tricky part

A single duplex pipe carries **two interleaved streams**: synchronous
request/response *and* asynchronous log broadcasts. Get these four details right.

### 4.1 Encoding — UTF-8 **without** BOM (hard-won)

`Encoding.UTF8` emits a BOM on the first `AutoFlush` write. On a line-protocol
pipe with a 0-byte buffer this BOM is written before any read happens and **the
writer blocks forever**. Always use a BOM-less encoding on both ends:

```csharp
// Shared by server and client.
public static readonly Encoding Protocol =
    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

// Readers must also be told not to sniff a BOM:
new StreamReader(pipe, Protocol, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
new StreamWriter(pipe, Protocol, leaveOpen: true) { AutoFlush = true };
```

### 4.2 Server: accept loop + per-client handler

```csharp
public const string PipeName = "PIPE_NAME";

private async Task ListenAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        NamedPipeServerStream? server = null;
        try
        {
            server = NamedPipeHost.CreateServer(PipeName);  // see §4.6 for ACLs
            await server.WaitForConnectionAsync(ct);
            var connected = server; server = null;          // ownership moves to handler
            _ = Task.Run(() => HandleClientAsync(connected, ct), ct);
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex) { ServiceLog.Error($"IPC listen: {ex.Message}"); await Task.Delay(500, ct); }
        finally { server?.Dispose(); }
    }
}
```

Commands are line-based; reply with `OK`/`OK <payload>`/`ERR <msg>`:

```csharp
private string ExecuteCommand(string cmd, Guid clientId, StreamWriter writer) =>
    cmd.ToUpperInvariant() switch
    {
        "PING"           => "OK",
        "GET_STATUS"     => _worker.IsRunning ? "OK RUNNING" : "OK STOPPED",
        "START"          => Run(() => { _worker.Start();   return "OK"; }),
        "STOP"           => Run(() => { _worker.Stop();    return "OK"; }),
        "DO_WORK_NOW"    => Run(() => { _worker.RunOnce(); return "OK"; }),
        "RELOAD_SETTINGS"=> Run(() => { _worker.Reload();  return "OK"; }),
        "SUBSCRIBE_LOGS" => SubscribeLogs(clientId, writer),
        _                => "ERR Unknown command."
    };
```

### 4.3 Log broadcast with atomic history replay (hard-won)

New subscribers must receive the recent history **and** be registered without
losing or duplicating lines that arrive in between. Do both under the *same lock*
the broadcaster uses:

```csharp
private readonly object _broadcastLock = new();
private readonly LinkedList<string> _history = new();          // cap at e.g. 500
private readonly ConcurrentDictionary<Guid, StreamWriter> _subscribers = new();

private string SubscribeLogs(Guid clientId, StreamWriter writer)
{
    lock (_broadcastLock)                       // <-- same lock as BroadcastLog
    {
        foreach (var line in _history)
            try { writer.WriteLine(line); } catch { return "ERR history send failed."; }
        _subscribers[clientId] = writer;        // register only after replay
    }
    return "OK";
}

private void BroadcastLog(string message)
{
    var line = $"LOG {message}";
    lock (_broadcastLock)
    {
        _history.AddLast(line);
        while (_history.Count > 500) _history.RemoveFirst();
        foreach (var (id, w) in _subscribers)
            try { w.WriteLine(line); } catch { _subscribers.TryRemove(id, out _); }
    }
}
```

### 4.4 Client: multiplexing responses and async logs (hard-won)

The client has one read loop. Lines prefixed `LOG ` are async broadcasts;
everything else is the answer to the *oldest* pending command. Correlate with a
queue of `TaskCompletionSource`:

```csharp
private readonly ConcurrentQueue<TaskCompletionSource<string>> _pending = new();

private async Task ReadLoopAsync(CancellationToken ct)
{
    string? line;
    while (!ct.IsCancellationRequested && (line = await _reader!.ReadLineAsync(ct)) != null)
    {
        if (line.StartsWith("LOG ", StringComparison.Ordinal))
        {
            var payload = line[4..];
            lock (_logGate)
            {
                if (_logReceived != null) _logReceived.Invoke(this, payload);
                else _earlyLogs.Enqueue(payload);     // see §4.5
            }
            continue;
        }
        if (_pending.TryDequeue(out var tcs)) tcs.TrySetResult(line);
    }
}

private string SendCommand(string command, TimeSpan timeout)
{
    if (_writer == null) return "ERR Disconnected.";
    var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    _pending.Enqueue(tcs);
    try { lock (_writeLock) _writer.WriteLine(command); }
    catch { DrainPending(tcs); return "ERR Connection lost."; }
    if (!tcs.Task.Wait(timeout)) { DrainPending(tcs); return "ERR Timeout."; }
    return tcs.Task.GetAwaiter().GetResult();
}
```

### 4.5 Early-log queue (hard-won)

The history sent during `SUBSCRIBE_LOGS` can arrive *before* the UI attaches its
log handler. Queue those lines and flush them when the first subscriber attaches,
so nothing is lost:

```csharp
public event EventHandler<string> LogReceived
{
    add    { lock (_logGate) { _logReceived += value;
                               while (_earlyLogs.Count > 0) value(this, _earlyLogs.Dequeue()); } }
    remove { lock (_logGate) _logReceived -= value; }
}
```

### 4.6 Pipe ACLs (cross-session)

The service runs as LocalSystem; the UI runs as the logged-in user. Create the
server pipe with a security descriptor that allows the interactive user (or
Authenticated Users) to read/write, using `NamedPipeServerStreamAcl.Create`.
Keep a single `CreateServer` helper so both ends stay consistent.

---

## 5. Resilient connection from the UI (hard-won)

The UI must **open instantly** and connect in the background — never block the UI
thread on the pipe. Two failure modes bit us:

1. A long synchronous pre-detection froze the window and it closed before the
   service finished starting. → Keep any pre-detection to **≤ 400 ms total** (e.g.
   2 quick pings of 200 ms) or skip it entirely and rely on the background loop.
2. Reusing one client object across retries left corrupt state after a partial
   failure. → **Create a fresh client each attempt and dispose it on failure.**

```csharp
private async Task ConnectLoopAsync()
{
    const int maxAttempts = 25;
    string? lastError = null;
    for (var i = 0; i < maxAttempts; i++)
    {
        var client = new IpcClient();                       // NEW client each time
        if (client.TryConnect(TimeSpan.FromMilliseconds(400)))
        {
            AttachConnectedBackend(client);
            return;
        }
        lastError = client.LastError;
        client.Dispose();                                   // discard corrupt state
        if (i < maxAttempts - 1) await Task.Delay(200);
    }
    RaiseLog($"Could not connect to service. Last error: {lastError}. " +
             "Check services.msc, or run the exe with no service (local mode).");
}
```

And in `TryConnect`, dispose on the **non-exceptional** failure path too (a
`SUBSCRIBE_LOGS` timeout returns a string, it does not throw):

```csharp
var subscribe = SendCommand("SUBSCRIBE_LOGS", timeout);
if (subscribe.StartsWith("OK", StringComparison.Ordinal)) { LastError = null; return true; }
LastError = $"SUBSCRIBE_LOGS -> {subscribe}";
Dispose();          // <-- easy to forget; leaves the pipe half-open otherwise
return false;
```

Model the UI backend as an interface (`IBackend`) with two implementations —
`LocalBackend` (drives the in-process worker) and `RemoteBackend` (sends IPC
commands) — plus a `DeferredRemoteBackend` that shows the UI immediately and swaps
in the remote backend once connected. The tray reads the same interface either way.

---

## 6. Auto-launching the tray into the user session (hard-won)

A Session-0 service cannot show UI, but it can spawn a process **in the active
user session** with `CreateProcessAsUser`. The dance is: get the active console
session → query its user token → duplicate it to a primary token → build its
environment block → create the process hidden, passing `--attach-service`.

```csharp
private static bool TryLaunchInActiveSession(string exePath)
{
    var sessionId = WTSGetActiveConsoleSessionId();
    if (sessionId is 0xFFFFFFFF or 0) return false;                 // no interactive session
    if (!WTSQueryUserToken(sessionId, out var userToken) || userToken == IntPtr.Zero) return false;

    try
    {
        if (!DuplicateTokenEx(userToken,
                TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY |
                TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID,
                IntPtr.Zero, SecurityImpersonation, TokenPrimary, out var primary))
            return false;
        try
        {
            if (!CreateEnvironmentBlock(out var env, primary, false)) return false;
            try
            {
                var cmd = $"\"{exePath}\" --attach-service";
                var si = new STARTUPINFO
                {
                    cb = Marshal.SizeOf<STARTUPINFO>(),
                    lpDesktop = "winsta0\\default",
                    dwFlags = 0x00000001,   // STARTF_USESHOWWINDOW
                    wShowWindow = 0         // SW_HIDE — tray only, no console window
                };
                var ok = CreateProcessAsUser(primary, null, cmd, IntPtr.Zero, IntPtr.Zero,
                    false, CREATE_UNICODE_ENVIRONMENT, env, Path.GetDirectoryName(exePath),
                    ref si, out var pi);
                if (ok) { CloseHandle(pi.hProcess); CloseHandle(pi.hThread); }
                return ok;
            }
            finally { DestroyEnvironmentBlock(env); }
        }
        finally { CloseHandle(primary); }
    }
    finally { CloseHandle(userToken); }
}
```

P/Invoke set required: `WTSGetActiveConsoleSessionId` (kernel32),
`WTSQueryUserToken` (wtsapi32), `DuplicateTokenEx` (advapi32),
`CreateEnvironmentBlock`/`DestroyEnvironmentBlock` (userenv),
`CreateProcessAsUser` (advapi32, `CharSet.Unicode`), `CloseHandle` (kernel32),
plus the `STARTUPINFO` / `PROCESS_INFORMATION` structs and the token-access
constants. Before launching, check the single-instance mutex so you don't spawn a
second tray; if one exists, activate it instead.

---

## 7. Single-instance and duplicate-work guards

- **Single instance per session:** a named `Mutex` acquired at tray startup. If it
  is already held, signal the existing instance to show its window (a second tiny
  named pipe or a `WM_COPYDATA`/activation host works) and exit. Suppress any
  "already running" popup when started with `--attach-service`.
- **Local-mode lock:** standalone mode acquires a system-wide lock
  (`LocalModeLock`). The service checks it at startup (§3) and refuses to run so
  the same input is never processed twice. Release it on exit.

---

## 8. Tray UI behavior

- **Status indicator** with distinct meanings (choose colors per project): running
  locally / running attached to service / connecting / stopped.
- Detect **service death**: the tray pings the service on a timer; after N missed
  pings (e.g. 2 × 3 s) it stops, notifies the user, and closes itself — never leave
  an orphaned tray pretending to be connected.
- Menu: open panel, start/stop worker, run-now, and **"Exit" only in standalone**
  (attaching to the service, closing the tray must not stop the service).
- All cross-thread UI updates via `BeginInvoke`; do IPC calls on background threads.

---

## 9. Logging (no console in a service)

- **Service log file** next to the exe (`APP_NAME-service.log`): info/error/fatal,
  timestamped, lock-guarded appends. Write fatals to the Windows **Event Log** too.
- **Separate IPC diagnostic log** (`APP_NAME-ipc.log`) for connection attempts and
  pipe errors; fall back to `%TEMP%` if the exe directory is not writable.
- Initialize the log *first thing* in service mode so startup failures are captured.

---

## 10. Configuration

- Plain `key=value` file (`CONFIG_FILE`) next to the exe; ignore blank lines and
  `#`/`;` comments. Name it after the exe so renamed copies stay self-describing.
- Reloadable at runtime (the worker reloads before each cycle / on `RELOAD_SETTINGS`).
- Validate and create required directories/resources on save and on startup.

---

## 11. Project file and publish

`csproj` essentials:

```xml
<OutputType>WinExe</OutputType>
<TargetFramework>net8.0-windows</TargetFramework>
<UseWindowsForms>true</UseWindowsForms>
<UseWindowsService>true</UseWindowsService>  <!-- via the Hosting.WindowsServices package -->
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

Packages: `Microsoft.Extensions.Hosting`,
`Microsoft.Extensions.Hosting.WindowsServices`.

Publish variants (offer all three, document the trade-off):

| Variant | Size | Needs .NET on target? |
|---------|------|-----------------------|
| Self-contained + single-file + compression | largest exe, simplest deploy | No |
| Self-contained + single-file, no compression | larger | No |
| Framework-dependent | small folder | Yes (Desktop Runtime) |

```powershell
dotnet publish -r win-x64 -c Release --self-contained `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

Install / register:

```powershell
sc.exe create SERVICE_NAME binPath= "INSTALL_DIR\APP_NAME.exe --service" start= auto
sc.exe start SERVICE_NAME
# Optionally: a logon shortcut in the Startup folder pointing at the exe (no args),
# so the tray opens each login and attaches to the service when present.
```

---

## 12. Verification checklist

The generated app must pass all of these:

- [ ] `--service` (and SCM start) runs with **no window** and writes to the service log.
- [ ] Tray launched by the service connects in **< 1 s** and shows "attached" state.
- [ ] Running the exe with **no service** starts in local mode immediately (no freeze).
- [ ] Starting the service *after* the tray: the tray connects within a few seconds.
- [ ] **Stopping the service** makes the attached tray notify and close itself.
- [ ] Two exe launches in one session: the second activates the first, no double tray.
- [ ] Service refuses to start while a standalone instance holds the local-mode lock.
- [ ] Build is clean with `TreatWarningsAsErrors`.

---

## What this prompt deliberately leaves out

- **The domain logic.** Whatever the worker actually does is a one-line placeholder
  (`WORKER_DESCRIPTION`). Do not bake a specific problem (file conversion, polling,
  etc.) into the scaffold.
- **Library choices for the domain** (PDF, parsing, HTTP, …) — pick per project.
- **Localized user-facing strings and the exact status colors** — project decisions,
  not part of the reusable pattern.
- **Absolute paths and version numbers** — parameterized at the top.
- **A full copy-paste solution.** The snippets above are the *hard-won* parts;
  everything else the implementer writes normally. The goal is a guide that adapts,
  not a rigid template.
```
