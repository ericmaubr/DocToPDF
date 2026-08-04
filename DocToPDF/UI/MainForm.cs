using DocToPDF.Core;
using DocToPDF.Models;

namespace DocToPDF.UI;

public partial class MainForm : Form
{
    private const int MaxLogLines = 500;
    private readonly SettingsStore _settingsStore;
    private readonly IDocToPDFBackend _backend;
    private readonly List<string> _logLines = new();
    private readonly System.Windows.Forms.Timer _processCooldownTimer;
    private readonly System.Windows.Forms.Timer _countdownTimer;
    private DateTime? _nextPollAt;

    public MainForm(SettingsStore settingsStore, IDocToPDFBackend backend)
    {
        _settingsStore = settingsStore;
        _backend = backend;
        InitializeComponent();
        Icon = AppIconFactory.Create();
        UpdateRunModeDisplay();

        var toolTip = new ToolTip();
        toolTip.SetToolTip(lblRobot, "Opcional — deixe vazio para desativar");
        toolTip.SetToolTip(txtRobot, "Opcional — deixe vazio para desativar");

        _processCooldownTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _processCooldownTimer.Tick += (_, _) =>
        {
            btnProcessNow.Enabled = true;
            _processCooldownTimer.Stop();
        };

        _countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _countdownTimer.Tick += OnCountdownTick;
        _countdownTimer.Start();

        LoadSettingsToUi();
        _backend.LogEvent += OnLogEvent;
    }

    public void UpdateRunModeDisplay()
    {
        var mode = AppRunMode.Describe(_backend);
        lblVersion.Text = $"{AppVersion.Display} — {mode}";
        Text = $"DocToPDF {AppVersion.Display} — Configuração — {mode}";
    }

    private void LoadSettingsToUi()
    {
        var s = _settingsStore.Settings;
        txtInput.Text = s.InputDirectory;
        txtOutput.Text = s.OutputDirectory;
        txtProcessed.Text = s.ProcessedDirectory;
        txtError.Text = s.ErrorDirectory;
        txtRobot.Text = s.RobotDirectory;
        numPolling.Value = Math.Clamp(s.PollingIntervalSeconds, (int)numPolling.Minimum, (int)numPolling.Maximum);
        chkPadronizarPdfAtivo.Checked = s.PadronizarPdfAtivo;
        txtPadronizarPdfUrl.Text = s.PadronizarPdfUrl;
    }

    private AppSettings ReadSettingsFromUi() => new()
    {
        InputDirectory = txtInput.Text.Trim(),
        OutputDirectory = txtOutput.Text.Trim(),
        ProcessedDirectory = txtProcessed.Text.Trim(),
        ErrorDirectory = txtError.Text.Trim(),
        RobotDirectory = txtRobot.Text.Trim(),
        PollingIntervalSeconds = (int)numPolling.Value,
        PadronizarPdfAtivo = chkPadronizarPdfAtivo.Checked,
        PadronizarPdfUrl = txtPadronizarPdfUrl.Text.Trim()
    };

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.Tag is not TextBox target)
            return;

        using var dialog = new FolderBrowserDialog();
        if (!string.IsNullOrWhiteSpace(target.Text) && Directory.Exists(target.Text))
            dialog.SelectedPath = target.Text;

        if (dialog.ShowDialog(this) == DialogResult.OK)
            target.Text = dialog.SelectedPath;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var settings = ReadSettingsFromUi();

        if (!ConfiguredDirectories.ValidateRequired(settings, out var error))
        {
            AppendLog($"❌ {error}");
            return;
        }

        foreach (var message in ConfiguredDirectories.EnsureExist(settings))
            AppendLog(message);

        _settingsStore.Save(settings);
        _backend.RestartTimer();
        AppendLog("✅ Configurações salvas.");
    }

    private void BtnProcessNow_Click(object? sender, EventArgs e)
    {
        btnProcessNow.Enabled = false;
        _processCooldownTimer.Stop();
        _processCooldownTimer.Start();
        Task.Run(() => _backend.ProcessNow());
    }

    private void BtnClearLog_Click(object? sender, EventArgs e)
    {
        _logLines.Clear();
        rtbLog.Clear();
    }

    private void OnLogEvent(object? sender, string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (message.StartsWith(PollingService.StatusPrefix, StringComparison.Ordinal))
        {
            var phase = message[PollingService.StatusPrefix.Length..];
            BeginInvoke(() => ApplyStatus(phase));
            return;
        }

        BeginInvoke(() => AppendLog(message));
    }

    private void ApplyStatus(string phase)
    {
        if (phase.StartsWith("Aguardando", StringComparison.Ordinal))
        {
            _nextPollAt = DateTime.Now.AddSeconds((double)numPolling.Value);
            UpdateCountdownLabel();
        }
        else
        {
            _nextPollAt = null;
            lblStatusBar.Text = phase;
        }
    }

    private void OnCountdownTick(object? sender, EventArgs e) => UpdateCountdownLabel();

    private void UpdateCountdownLabel()
    {
        if (_nextPollAt is not { } next)
            return;

        var remaining = Math.Max(0, (int)Math.Ceiling((next - DateTime.Now).TotalSeconds));
        lblStatusBar.Text = $"Aguardando próxima verificação em {remaining}s";
    }

    private void AppendLog(string message)
    {
        var line = message.StartsWith('✅') || message.StartsWith('❌')
            ? message
            : $"ℹ️ {message}";

        _logLines.Add(line);
        while (_logLines.Count > MaxLogLines)
            _logLines.RemoveAt(0);

        rtbLog.Clear();
        foreach (var entry in _logLines)
        {
            var color = entry.Contains('❌')
                ? Color.DarkRed
                : entry.Contains('✅')
                    ? Color.DarkGreen
                    : Color.Black;

            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionColor = color;
            rtbLog.AppendText(entry + Environment.NewLine);
        }

        rtbLog.SelectionStart = rtbLog.TextLength;
        rtbLog.ScrollToCaret();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _backend.LogEvent -= OnLogEvent;
            _countdownTimer.Stop();
        }

        base.OnFormClosing(e);
    }
}
