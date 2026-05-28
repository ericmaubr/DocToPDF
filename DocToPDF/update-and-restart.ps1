param(
    [string]$RepoRoot = "C:\DocToPDF\repo",
    [string]$ProjectPath = "DocToPDF\DocToPDF.csproj",
    [string]$InstallDir = "C:\DocToPDF",
    [string]$ServiceName = "DocToPDF",
    [ValidateSet("compressed", "full")]
    [string]$Variant = "compressed",
    [switch]$SkipGitPull,
    [switch]$StartTrayUi
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Require-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Execute este script como Administrador."
    }
}

function Stop-ServiceSafe([string]$Name) {
    $svc = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if ($null -eq $svc) {
        Write-Host "Serviço '$Name' não existe. Pulando parada."
        return
    }

    if ($svc.Status -ne "Stopped") {
        Write-Host "Parando serviço '$Name'..."
        Stop-Service -Name $Name -Force
        $svc.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(30))
    }
}

function Start-ServiceSafe([string]$Name) {
    $svc = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if ($null -eq $svc) {
        throw "Serviço '$Name' não encontrado. Crie com sc create antes de usar este script."
    }

    if ($svc.Status -ne "Running") {
        Write-Host "Iniciando serviço '$Name'..."
        Start-Service -Name $Name
        $svc.WaitForStatus("Running", [TimeSpan]::FromSeconds(30))
    }
}

try {
    Require-Admin

    if (-not (Test-Path -LiteralPath $RepoRoot)) {
        throw "RepoRoot não encontrado: $RepoRoot"
    }

    $projectFile = Join-Path $RepoRoot $ProjectPath
    if (-not (Test-Path -LiteralPath $projectFile)) {
        throw "Projeto não encontrado: $projectFile"
    }

    if (-not (Test-Path -LiteralPath $InstallDir)) {
        Write-Step "Criando diretório de instalação"
        New-Item -ItemType Directory -Path $InstallDir | Out-Null
    }

    Write-Step "Parando serviço e finalizando processos antigos"
    Stop-ServiceSafe -Name $ServiceName
    Get-Process -Name "DocToPDF" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    Push-Location $RepoRoot
    try {
        if (-not $SkipGitPull) {
            Write-Step "Atualizando repositório (git pull)"
            git pull
        }

        Write-Step "Publicando versão '$Variant'"
        $publishDir = Join-Path $RepoRoot "artifacts\publish-$Variant"
        if (Test-Path -LiteralPath $publishDir) {
            Remove-Item -Recurse -Force -LiteralPath $publishDir
        }

        if ($Variant -eq "compressed") {
            dotnet publish $projectFile /p:PublishProfile=win-x64-compressed -o $publishDir
        }
        else {
            dotnet publish $projectFile -r win-x64 -c Release --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -o $publishDir
        }

        Write-Step "Copiando binários para $InstallDir"
        Copy-Item -LiteralPath (Join-Path $publishDir "DocToPDF.exe") -Destination (Join-Path $InstallDir "DocToPDF.exe") -Force
        Copy-Item -LiteralPath (Join-Path $RepoRoot "DocToPDF\DocToPDF.conf") -Destination (Join-Path $InstallDir "DocToPDF.conf") -Force
    }
    finally {
        Pop-Location
    }

    Write-Step "Iniciando serviço"
    Start-ServiceSafe -Name $ServiceName

    if ($StartTrayUi) {
        Write-Step "Abrindo interface de bandeja"
        Start-Process -FilePath (Join-Path $InstallDir "DocToPDF.exe") -ArgumentList "--ui"
    }

    Write-Step "Concluído"
    Write-Host "Instalação atualizada com sucesso." -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "Falha: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
