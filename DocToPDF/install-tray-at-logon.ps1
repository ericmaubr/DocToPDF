# Registra DocToPDF.exe na pasta Inicializar do usuário (bandeja na sessão interativa).
# O serviço Windows continua só com processamento + IPC — não abre UI.
param(
    [string]$ExePath = "C:\DocToPDF\DocToPDF.exe"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $ExePath)) {
    throw "Executável não encontrado: $ExePath"
}

$startup = [Environment]::GetFolderPath("Startup")
$shortcutPath = Join-Path $startup "DocToPDF.lnk"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $ExePath
$shortcut.WorkingDirectory = Split-Path $ExePath -Parent
$shortcut.Description = "DocToPDF — bandeja (conecta ao serviço se estiver ativo)"
$shortcut.Save()

Write-Host "Atalho criado: $shortcutPath" -ForegroundColor Green
Write-Host "Na próxima sessão, a bandeja abrirá automaticamente e usará o serviço quando ele estiver em execução."
