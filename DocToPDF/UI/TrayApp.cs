using DocToPDF.Core;
using DocToPDF.Core.Ipc;

namespace DocToPDF.UI;

public sealed class TrayApp : ApplicationContext, IDisposable
{
    private readonly InteractiveSession _session;
    private readonly IDocToPDFBackend _backend;
    private readonly MainForm _mainForm;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _modeMenuItem;
    private readonly ToolStripMenuItem _toggleServiceItem;
    private readonly ToolStripMenuItem _processNowItem;
    private readonly System.Windows.Forms.Timer _statusTimer;
    private int _missedServicePings;
    private int _statusRefreshInFlight;

    public TrayApp(InteractiveSession session, IDocToPDFBackend backend, MainForm mainForm)
    {
        _session = session;
        _backend = backend;
        _mainForm = mainForm;

        if (_backend is DeferredRemoteBackend deferred)
            deferred.ConnectionStateChanged += OnConnectionStateChanged;

        _modeMenuItem = new ToolStripMenuItem { Enabled = false };
        _toggleServiceItem = new ToolStripMenuItem("Iniciar Serviço", null, OnToggleService);
        _processNowItem = new ToolStripMenuItem("Processa Agora", null, OnProcessNow);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_modeMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Abrir Painel", null, (_, _) => ShowMainForm());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_toggleServiceItem);
        menu.Items.Add(_processNowItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, OnExit);

        _notifyIcon = new NotifyIcon
        {
            Text = $"DocToPDF {AppVersion.Display}",
            Icon = TrayIconFactory.Create(Color.Gray),
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMainForm();
        _backend.LogEvent += OnBackendLog;

        _statusTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _statusTimer.Tick += (_, _) => BeginRefreshTrayState();
        _statusTimer.Start();

        ApplyTrayVisuals();
        ShowStartupNotification();
    }

    public void ActivateFromRunningInstance()
    {
        if (_mainForm.InvokeRequired)
            _mainForm.BeginInvoke(ShowMainForm);
        else
            ShowMainForm();
    }

    private void ShowStartupNotification()
    {
        try
        {
            var mode = AppRunMode.Describe(_backend);
            _notifyIcon.BalloonTipTitle = $"DocToPDF — {mode}";
            _notifyIcon.BalloonTipText =
                $"{AppVersion.Display}. Verde = serviço; azul = local; amarelo = conectando.";
            _notifyIcon.ShowBalloonTip(4000);
        }
        catch
        {
            // Ignore.
        }
    }

    private void ShowMainForm()
    {
        _mainForm.Show();
        _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.BringToFront();
        _mainForm.Activate();
        BeginRefreshTrayState();
    }

    private void OnToggleService(object? sender, EventArgs e)
    {
        if (_backend.IsRunning)
            _backend.StopTimer();
        else
            _backend.StartTimer();

        BeginRefreshTrayState();
    }

    private void OnProcessNow(object? sender, EventArgs e) =>
        Task.Run(() => _backend.ProcessNow());

    private void OnExit(object? sender, EventArgs e)
    {
        if (!_backend.IsRemote)
            _backend.StopTimer();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e) => BeginRefreshTrayState();

    private void OnBackendLog(object? sender, string message) => BeginRefreshTrayState();

    private void BeginRefreshTrayState()
    {
        if (_session.UsesServiceBackend && CheckServiceStopped())
            return;

        if (_backend is not (DeferredRemoteBackend or RemoteDocToPDFBackend))
        {
            if (_mainForm.InvokeRequired)
                _mainForm.BeginInvoke(ApplyTrayVisuals);
            else
                ApplyTrayVisuals();
            return;
        }

        if (Interlocked.CompareExchange(ref _statusRefreshInFlight, 1, 0) != 0)
            return;

        Task.Run(() =>
        {
            try
            {
                if (_backend is DeferredRemoteBackend deferred)
                    deferred.RefreshStatus();
                else if (_backend is RemoteDocToPDFBackend remote)
                    remote.TryRefreshStatus();
            }
            finally
            {
                Interlocked.Exchange(ref _statusRefreshInFlight, 0);
                if (_mainForm.IsHandleCreated)
                    _mainForm.BeginInvoke(ApplyTrayVisuals);
                else
                    ApplyTrayVisuals();
            }
        });
    }

    private void ApplyTrayVisuals()
    {
        var isRunning = _backend.IsRunning;
        var modeLabel = AppRunMode.Describe(_backend);

        _modeMenuItem.Text = $"Modo: {modeLabel}";
        _mainForm.UpdateRunModeDisplay();

        _notifyIcon.Icon = TrayIconFactory.Create(AppRunMode.TrayIndicatorColor(_backend, isRunning));
        _notifyIcon.Text = $"DocToPDF {AppVersion.Display} — {AppRunMode.TrayStatusSuffix(_backend, isRunning)}";
        _toggleServiceItem.Text = isRunning ? "Parar Serviço" : "Iniciar Serviço";
    }

    private bool CheckServiceStopped()
    {
        if (!DocToPDFIpcClient.TryQuickPing())
            _missedServicePings++;
        else
            _missedServicePings = 0;

        if (_missedServicePings < 2)
            return false;

        _session.StopLocalProcessing();
        try
        {
            _backend.StopTimer();
        }
        catch
        {
            // Ignore.
        }

        try
        {
            _notifyIcon.BalloonTipTitle = "DocToPDF";
            _notifyIcon.BalloonTipText =
                "Serviço Windows parado. A bandeja será fechada.";
            _notifyIcon.ShowBalloonTip(4000);
        }
        catch
        {
            // Ignore.
        }

        Application.Exit();
        return true;
    }

    public new void Dispose()
    {
        if (_backend is DeferredRemoteBackend deferred)
            deferred.ConnectionStateChanged -= OnConnectionStateChanged;

        _statusTimer.Stop();
        _statusTimer.Dispose();
        _backend.LogEvent -= OnBackendLog;
        _notifyIcon.Dispose();
        _backend.Dispose();
        base.Dispose();
    }
}
