using DocToPDF.Core;
using DocToPDF.Models;

namespace DocToPDF.UI;

public partial class MainForm : Form
{
    private const int MaxLogLines = 500;
    private readonly SettingsStore _settingsStore;
    private readonly PollingService _pollingService;
    private readonly List<string> _logLines = new();
    private readonly System.Windows.Forms.Timer _processCooldownTimer;

    public MainForm(SettingsStore settingsStore, PollingService pollingService)
    {
        _settingsStore = settingsStore;
        _pollingService = pollingService;
        InitializeComponent();

        _processCooldownTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _processCooldownTimer.Tick += (_, _) =>
        {
            btnProcessNow.Enabled = true;
            _processCooldownTimer.Stop();
        };

        LoadSettingsToUi();
        _pollingService.LogEvent += OnLogEvent;
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
    }

    private AppSettings ReadSettingsFromUi() => new()
    {
        InputDirectory = txtInput.Text.Trim(),
        OutputDirectory = txtOutput.Text.Trim(),
        ProcessedDirectory = txtProcessed.Text.Trim(),
        ErrorDirectory = txtError.Text.Trim(),
        RobotDirectory = txtRobot.Text.Trim(),
        PollingIntervalSeconds = (int)numPolling.Value
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

        if (!ValidateAndCreateDirectories(settings, out var error))
        {
            AppendLog($"❌ {error}");
            return;
        }

        _settingsStore.Save(settings);
        _pollingService.RestartTimer();
        AppendLog("✅ Configurações salvas.");
    }

    private static bool ValidateAndCreateDirectories(AppSettings settings, out string error)
    {
        var required = new (string Path, string Name)[]
        {
            (settings.InputDirectory, "Diretório de Entrada"),
            (settings.OutputDirectory, "Diretório de Saída PDF"),
            (settings.ProcessedDirectory, "Diretório Processados"),
            (settings.ErrorDirectory, "Diretório de Erros")
        };

        foreach (var (path, name) in required)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                error = $"{name} é obrigatório.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                error = $"Não foi possível criar {name}: {ex.Message}";
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.RobotDirectory))
        {
            try
            {
                Directory.CreateDirectory(settings.RobotDirectory);
            }
            catch (Exception ex)
            {
                error = $"Não foi possível criar Diretório do Robô: {ex.Message}";
                return false;
            }
        }

        error = "";
        return true;
    }

    private void BtnProcessNow_Click(object? sender, EventArgs e)
    {
        btnProcessNow.Enabled = false;
        _processCooldownTimer.Stop();
        _processCooldownTimer.Start();
        Task.Run(() => _pollingService.ProcessNow());
    }

    private void BtnClearLog_Click(object? sender, EventArgs e)
    {
        _logLines.Clear();
        rtbLog.Clear();
    }

    private void OnLogEvent(object? sender, string message) =>
        BeginInvoke(() => AppendLog(message));

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
            _pollingService.LogEvent -= OnLogEvent;
        }

        base.OnFormClosing(e);
    }
}
