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
        tblConfig = new TableLayoutPanel();
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
        lblPolling = new Label();
        pnlPolling = new Panel();
        numPolling = new NumericUpDown();
        lblPollingUnit = new Label();
        btnSave = new Button();
        btnProcessNow = new Button();
        lblLog = new Label();
        rtbLog = new RichTextBox();
        btnClearLog = new Button();
        tblConfig.SuspendLayout();
        pnlPolling.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numPolling).BeginInit();
        SuspendLayout();

        // Title
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        lblTitle.Location = new Point(12, 8);
        lblTitle.Text = "DocToPDF — Configuração";

        // Config table: label | textbox | browse
        tblConfig.ColumnCount = 3;
        tblConfig.RowCount = 6;
        tblConfig.Location = new Point(12, 34);
        tblConfig.Size = new Size(576, 168);
        tblConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155F));
        tblConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tblConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
        for (var i = 0; i < 6; i++)
            tblConfig.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));

        ConfigureDirectoryRow(lblInput, "Diretório de Entrada", txtInput, btnInput, 0);
        ConfigureDirectoryRow(lblOutput, "Diretório de Saída PDF", txtOutput, btnOutput, 1);
        ConfigureDirectoryRow(lblProcessed, "Diretório Processados", txtProcessed, btnProcessed, 2);
        ConfigureDirectoryRow(lblError, "Diretório de Erros", txtError, btnError, 3);
        ConfigureDirectoryRow(lblRobot, "Robô (opc.)", txtRobot, btnRobot, 4);

        lblPolling.Text = "Intervalo de Polling";
        lblPolling.Dock = DockStyle.Fill;
        lblPolling.TextAlign = ContentAlignment.MiddleRight;
        tblConfig.Controls.Add(lblPolling, 0, 5);

        pnlPolling.Dock = DockStyle.Fill;
        numPolling.Location = new Point(0, 3);
        numPolling.Minimum = 1;
        numPolling.Maximum = 86400;
        numPolling.Value = 30;
        numPolling.Width = 80;
        lblPollingUnit.AutoSize = true;
        lblPollingUnit.Location = new Point(88, 7);
        lblPollingUnit.Text = "segundos";
        pnlPolling.Controls.Add(numPolling);
        pnlPolling.Controls.Add(lblPollingUnit);
        tblConfig.Controls.Add(pnlPolling, 1, 5);
        tblConfig.SetColumnSpan(pnlPolling, 2);

        // Action buttons
        btnSave.Location = new Point(12, 212);
        btnSave.Size = new Size(150, 30);
        btnSave.Text = "Salvar Configurações";
        btnSave.Click += BtnSave_Click;

        btnProcessNow.Location = new Point(168, 212);
        btnProcessNow.Size = new Size(130, 30);
        btnProcessNow.Text = "Processa Agora";
        btnProcessNow.Click += BtnProcessNow_Click;

        // Log
        lblLog.AutoSize = true;
        lblLog.Location = new Point(12, 252);
        lblLog.Text = "Log de Eventos";

        btnClearLog.Location = new Point(488, 248);
        btnClearLog.Size = new Size(100, 25);
        btnClearLog.Text = "Limpar Log";
        btnClearLog.Click += BtnClearLog_Click;

        rtbLog.Location = new Point(12, 274);
        rtbLog.Size = new Size(576, 88);
        rtbLog.ReadOnly = true;
        rtbLog.Font = new Font("Consolas", 9F);
        rtbLog.BackColor = Color.White;
        rtbLog.ScrollBars = RichTextBoxScrollBars.Vertical;
        rtbLog.WordWrap = false;

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(600, 400);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Controls.Add(lblTitle);
        Controls.Add(tblConfig);
        Controls.Add(btnSave);
        Controls.Add(btnProcessNow);
        Controls.Add(lblLog);
        Controls.Add(btnClearLog);
        Controls.Add(rtbLog);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "DocToPDF";

        tblConfig.ResumeLayout(false);
        pnlPolling.ResumeLayout(false);
        pnlPolling.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numPolling).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    private void ConfigureDirectoryRow(Label label, string text, TextBox textBox, Button button, int row)
    {
        label.Text = text;
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleRight;

        textBox.Dock = DockStyle.Fill;

        button.Text = "...";
        button.Dock = DockStyle.Fill;
        button.Tag = textBox;
        button.Click += BtnBrowse_Click;

        tblConfig.Controls.Add(label, 0, row);
        tblConfig.Controls.Add(textBox, 1, row);
        tblConfig.Controls.Add(button, 2, row);
    }

    private Label lblTitle = null!;
    private TableLayoutPanel tblConfig = null!;
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
    private Label lblPolling = null!;
    private Panel pnlPolling = null!;
    private NumericUpDown numPolling = null!;
    private Label lblPollingUnit = null!;
    private Button btnSave = null!;
    private Button btnProcessNow = null!;
    private Label lblLog = null!;
    private RichTextBox rtbLog = null!;
    private Button btnClearLog = null!;
}
