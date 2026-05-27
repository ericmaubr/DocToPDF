#nullable enable
namespace DocToPDF.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        lblTitle = new Label();
        lblInput = new Label();
        txtInput = new TextBox();
        btnInput = new Button();
        lblOutput = new Label();
        txtOutput = new TextBox();
        btnOutput = new Button();
        lblProcessed = new Label();
        txtProcessed = new TextBox();
        btnProcessed = new Button();
        lblError = new Label();
        txtError = new TextBox();
        btnError = new Button();
        lblRobot = new Label();
        txtRobot = new TextBox();
        btnRobot = new Button();
        lblRobotHint = new Label();
        lblPolling = new Label();
        numPolling = new NumericUpDown();
        lblPollingUnit = new Label();
        btnSave = new Button();
        btnProcessNow = new Button();
        lblLog = new Label();
        rtbLog = new RichTextBox();
        btnClearLog = new Button();
        ((System.ComponentModel.ISupportInitialize)numPolling).BeginInit();
        SuspendLayout();

        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        lblTitle.Location = new Point(12, 9);
        lblTitle.Text = "DocToPDF — Configuração";

        lblInput.AutoSize = true;
        lblInput.Location = new Point(12, 48);
        lblInput.Text = "Diretório de Entrada";

        txtInput.Location = new Point(12, 66);
        txtInput.Size = new Size(480, 23);

        btnInput.Location = new Point(498, 65);
        btnInput.Size = new Size(100, 25);
        btnInput.Text = "...";
        btnInput.Click += BtnBrowse_Click;

        lblOutput.AutoSize = true;
        lblOutput.Location = new Point(12, 98);
        lblOutput.Text = "Diretório de Saída PDF";

        txtOutput.Location = new Point(12, 116);
        txtOutput.Size = new Size(480, 23);

        btnOutput.Location = new Point(498, 115);
        btnOutput.Size = new Size(100, 25);
        btnOutput.Text = "...";
        btnOutput.Tag = txtOutput;
        btnOutput.Click += BtnBrowse_Click;

        lblProcessed.AutoSize = true;
        lblProcessed.Location = new Point(12, 148);
        lblProcessed.Text = "Diretório Processados";

        txtProcessed.Location = new Point(12, 166);
        txtProcessed.Size = new Size(480, 23);

        btnProcessed.Location = new Point(498, 165);
        btnProcessed.Size = new Size(100, 25);
        btnProcessed.Text = "...";
        btnProcessed.Tag = txtProcessed;
        btnProcessed.Click += BtnBrowse_Click;

        lblError.AutoSize = true;
        lblError.Location = new Point(12, 198);
        lblError.Text = "Diretório de Erros";

        txtError.Location = new Point(12, 216);
        txtError.Size = new Size(480, 23);

        btnError.Location = new Point(498, 215);
        btnError.Size = new Size(100, 25);
        btnError.Text = "...";
        btnError.Tag = txtError;
        btnError.Click += BtnBrowse_Click;

        lblRobot.AutoSize = true;
        lblRobot.Location = new Point(12, 248);
        lblRobot.Text = "Diretório do Robô";

        txtRobot.Location = new Point(12, 266);
        txtRobot.Size = new Size(480, 23);

        btnRobot.Location = new Point(498, 265);
        btnRobot.Size = new Size(100, 25);
        btnRobot.Text = "...";
        btnRobot.Tag = txtRobot;
        btnRobot.Click += BtnBrowse_Click;

        lblRobotHint.AutoSize = true;
        lblRobotHint.ForeColor = SystemColors.GrayText;
        lblRobotHint.Location = new Point(12, 292);
        lblRobotHint.Text = "(opcional — deixe vazio para desativar)";

        lblPolling.AutoSize = true;
        lblPolling.Location = new Point(12, 320);
        lblPolling.Text = "Intervalo de Polling";

        numPolling.Location = new Point(160, 318);
        numPolling.Minimum = 1;
        numPolling.Maximum = 86400;
        numPolling.Value = 30;
        numPolling.Width = 80;

        lblPollingUnit.AutoSize = true;
        lblPollingUnit.Location = new Point(246, 320);
        lblPollingUnit.Text = "segundos";

        btnSave.Location = new Point(12, 356);
        btnSave.Size = new Size(150, 30);
        btnSave.Text = "Salvar Configurações";
        btnSave.Click += BtnSave_Click;

        btnProcessNow.Location = new Point(172, 356);
        btnProcessNow.Size = new Size(130, 30);
        btnProcessNow.Text = "Processa Agora";
        btnProcessNow.Click += BtnProcessNow_Click;

        lblLog.AutoSize = true;
        lblLog.Location = new Point(12, 398);
        lblLog.Text = "Log de Eventos";

        rtbLog.Location = new Point(12, 416);
        rtbLog.ReadOnly = true;
        rtbLog.Size = new Size(586, 120);
        rtbLog.Font = new Font("Consolas", 9F);
        rtbLog.BackColor = Color.White;
        rtbLog.ScrollBars = RichTextBoxScrollBars.Vertical;
        rtbLog.WordWrap = false;

        btnClearLog.Location = new Point(12, 542);
        btnClearLog.Size = new Size(100, 25);
        btnClearLog.Text = "Limpar Log";
        btnClearLog.Click += BtnClearLog_Click;

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(620, 580);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Controls.Add(lblTitle);
        Controls.Add(lblInput);
        Controls.Add(txtInput);
        Controls.Add(btnInput);
        Controls.Add(lblOutput);
        Controls.Add(txtOutput);
        Controls.Add(btnOutput);
        Controls.Add(lblProcessed);
        Controls.Add(txtProcessed);
        Controls.Add(btnProcessed);
        Controls.Add(lblError);
        Controls.Add(txtError);
        Controls.Add(btnError);
        Controls.Add(lblRobot);
        Controls.Add(txtRobot);
        Controls.Add(btnRobot);
        Controls.Add(lblRobotHint);
        Controls.Add(lblPolling);
        Controls.Add(numPolling);
        Controls.Add(lblPollingUnit);
        Controls.Add(btnSave);
        Controls.Add(btnProcessNow);
        Controls.Add(lblLog);
        Controls.Add(rtbLog);
        Controls.Add(btnClearLog);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "DocToPDF";

        btnInput.Tag = txtInput;
        ((System.ComponentModel.ISupportInitialize)numPolling).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    private Label lblTitle = null!;
    private Label lblInput = null!;
    private TextBox txtInput = null!;
    private Button btnInput = null!;
    private Label lblOutput = null!;
    private TextBox txtOutput = null!;
    private Button btnOutput = null!;
    private Label lblProcessed = null!;
    private TextBox txtProcessed = null!;
    private Button btnProcessed = null!;
    private Label lblError = null!;
    private TextBox txtError = null!;
    private Button btnError = null!;
    private Label lblRobot = null!;
    private TextBox txtRobot = null!;
    private Button btnRobot = null!;
    private Label lblRobotHint = null!;
    private Label lblPolling = null!;
    private NumericUpDown numPolling = null!;
    private Label lblPollingUnit = null!;
    private Button btnSave = null!;
    private Button btnProcessNow = null!;
    private Label lblLog = null!;
    private RichTextBox rtbLog = null!;
    private Button btnClearLog = null!;
}
