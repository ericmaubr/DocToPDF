using DocToPDF.Core;
using DocToPDF.Core.Ipc;

namespace DocToPDF.UI;

public sealed class TrayApp : ApplicationContext, IDisposable
{
    private readonly InteractiveSession _session;
    private readonly IDocToPDFBackend _backend;
    private readonly MainForm _mainForm;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleServiceItem;
    private readonly ToolStripMenuItem _processNowItem;
    private readonly System.Windows.Forms.Timer _statusTimer;
    private int _missedServicePings;

    public TrayApp(InteractiveSession session, IDocToPDFBackend backend, MainForm mainForm)
    {
        _session = session;
        _backend = backend;
        _mainForm = mainForm;

        _toggleServiceItem = new ToolStripMenuItem("Iniciar Serviço", null, OnToggleService);
        _processNowItem = new ToolStripMenuItem("Processa Agora", null, OnProcessNow);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir Painel", null, (_, _) => ShowMainForm());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_toggleServiceItem);
        menu.Items.Add(_processNowItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, OnExit);

        _notifyIcon = new NotifyIcon
        {
            Text = $"DocToPDF {AppVersion.Display} — Parado",
            Icon = TrayIconFactory.Create(Color.Gray),
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMainForm();
        _backend.LogEvent += OnBackendLog;

        _statusTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _statusTimer.Tick += (_, _) => UpdateTrayState();
        _statusTimer.Start();

        UpdateTrayState();
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
            _notifyIcon.BalloonTipTitle = "DocToPDF";
            _notifyIcon.BalloonTipText = _session.UsesServiceBackend
                ? $"{AppVersion.Display} — conectado ao serviço. Ícone na bandeja (^ se oculto)."
                : $"{AppVersion.Display} — modo local. Ícone na bandeja (^ se oculto).";
            _notifyIcon.ShowBalloonTip(3000);
        }
        catch
        {
            // Balloon tips may be disabled by policy.
        }
    }

    private void ShowMainForm()
    {
        _mainForm.Show();
        _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.BringToFront();
        _mainForm.Activate();
    }

    private void OnToggleService(object? sender, EventArgs e)
    {
        if (_backend.IsRunning)
            _backend.StopTimer();
        else
            _backend.StartTimer();

        UpdateTrayState();
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

    private void OnBackendLog(object? sender, string message)
    {
        if (_mainForm.IsHandleCreated)
            _mainForm.BeginInvoke(UpdateTrayState);
        else
            UpdateTrayState();
    }

    private void UpdateTrayState()
    {
        if (_session.UsesServiceBackend && CheckServiceStopped())
            return;

        if (_backend is DeferredRemoteBackend deferred)
            deferred.RefreshStatus();
        else if (_backend is RemoteDocToPDFBackend remote)
            remote.RefreshStatus();

        if (_backend.IsRunning)
        {
            _notifyIcon.Icon = TrayIconFactory.Create(Color.LimeGreen);
            _notifyIcon.Text = _backend.IsRemote
                ? $"DocToPDF {AppVersion.Display} — Rodando (serviço)"
                : $"DocToPDF {AppVersion.Display} — Rodando";
            _toggleServiceItem.Text = "Parar Serviço";
        }
        else
        {
            _notifyIcon.Icon = TrayIconFactory.Create(Color.Gray);
            _notifyIcon.Text = _backend.IsRemote
                ? $"DocToPDF {AppVersion.Display} — Parado (serviço)"
                : $"DocToPDF {AppVersion.Display} — Parado";
            _toggleServiceItem.Text = "Iniciar Serviço";
        }
    }

  /// <summary>
    /// Bandeja aberta pelo serviço encerra quando o serviço Windows para (evita processamento órfão).
    /// </summary>
    private bool CheckServiceStopped()
    {
        if (!DocToPDFIpcClient.TryQuickPing())
            _missedServicePings++;
        else
            _missedServicePings = 0;

        if (_missedServicePings < 2)
            return false;

        _session.StopLocalProcessing();
        _backend.StopTimer();

        try
        {
            _notifyIcon.BalloonTipTitle = "DocToPDF";
            _notifyIcon.BalloonTipText =
                "Serviço Windows parado. A bandeja será fechada. " +
                "Para modo local, execute DocToPDF.exe com o serviço desligado.";
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
        _statusTimer.Stop();
        _statusTimer.Dispose();
        _backend.LogEvent -= OnBackendLog;
        _notifyIcon.Dispose();
        _backend.Dispose();
        base.Dispose();
    }
}
