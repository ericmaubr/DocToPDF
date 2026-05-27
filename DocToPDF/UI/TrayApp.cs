using DocToPDF.Core;

namespace DocToPDF.UI;

public sealed class TrayApp : ApplicationContext, IDisposable
{
    private readonly PollingService _pollingService;
    private readonly MainForm _mainForm;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleServiceItem;
    private readonly ToolStripMenuItem _processNowItem;

    public TrayApp(SettingsStore settingsStore, PollingService pollingService, MainForm mainForm)
    {
        _pollingService = pollingService;
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
        _pollingService.LogEvent += (_, _) => UpdateTrayState();

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
        if (_pollingService.IsRunning)
            _pollingService.StopTimer();
        else
            _pollingService.StartTimer();

        UpdateTrayState();
    }

    private void OnProcessNow(object? sender, EventArgs e) =>
        Task.Run(() => _pollingService.ProcessNow());

    private void OnExit(object? sender, EventArgs e)
    {
        _pollingService.StopTimer();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }

    private void UpdateTrayState()
    {
        if (_pollingService.IsRunning)
        {
            _notifyIcon.Icon = CreateCircleIcon(Color.LimeGreen);
            _notifyIcon.Text = "DocToPDF — Rodando";
            _toggleServiceItem.Text = "Parar Serviço";
        }
        else
        {
            _notifyIcon.Icon = CreateCircleIcon(Color.Gray);
            _notifyIcon.Text = "DocToPDF — Parado";
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
        _notifyIcon.Dispose();
        base.Dispose();
    }
}
