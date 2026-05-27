using DocToPDF.Core;

namespace DocToPDF.UI;

public sealed class TrayApp : ApplicationContext, IDisposable
{
    private readonly IDocToPDFBackend _backend;
    private readonly MainForm _mainForm;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleServiceItem;
    private readonly ToolStripMenuItem _processNowItem;

    public TrayApp(SettingsStore settingsStore, IDocToPDFBackend backend, MainForm mainForm)
    {
        _ = settingsStore;
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
            Text = "DocToPDF — Parado",
            Icon = CreateCircleIcon(Color.Gray),
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMainForm();
        _backend.LogEvent += OnBackendLog;

        UpdateTrayState();
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
        if (string.IsNullOrEmpty(message))
            UpdateTrayState();
    }

    private void UpdateTrayState()
    {
        if (_backend.IsRemote && _backend is RemoteDocToPDFBackend remote)
            remote.RefreshStatus();

        if (_backend.IsRunning)
        {
            _notifyIcon.Icon = CreateCircleIcon(Color.LimeGreen);
            _notifyIcon.Text = _backend.IsRemote
                ? "DocToPDF — Rodando (serviço)"
                : "DocToPDF — Rodando";
            _toggleServiceItem.Text = "Parar Serviço";
        }
        else
        {
            _notifyIcon.Icon = CreateCircleIcon(Color.Gray);
            _notifyIcon.Text = _backend.IsRemote
                ? "DocToPDF — Parado (serviço)"
                : "DocToPDF — Parado";
            _toggleServiceItem.Text = "Iniciar Serviço";
        }
    }

    private static Icon CreateCircleIcon(Color color)
    {
        const int size = 16;
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.FillEllipse(brush, 1, 1, size - 2, size - 2);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public new void Dispose()
    {
        _backend.LogEvent -= OnBackendLog;
        _notifyIcon.Dispose();
        _backend.Dispose();
        base.Dispose();
    }
}
