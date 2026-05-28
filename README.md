# DocToPDF

Aplicação Windows (.NET 8) que monitora uma pasta de entrada, converte arquivos `.xml` e `.json` em PDFs legíveis para robôs e oferece ícone na bandeja do sistema com painel de configuração.

## Arquitetura (recomendação Microsoft)

O mesmo `DocToPDF.exe` tem **três modos**, alinhados à [orientação oficial](https://learn.microsoft.com/en-us/windows/win32/services/interactive-services) e à prática consolidada para apps .NET ([Stephen Cleary — Managed Services and UIs](https://blog.stephencleary.com/2011/05/managed-services-and-uis.html)):

| Modo | Quem inicia | O que faz |
|------|-------------|-----------|
| **Serviço Windows** | SCM (`services.msc`) | Só processamento + IPC (named pipe). **Sem UI** — Session 0. |
| **Bandeja + serviço** | Usuário no login (atalho em Inicializar) | Conecta ao pipe; controla o worker remoto. |
| **Standalone** | Usuário (`DocToPDF.exe` sem serviço) | Polling e bandeja no mesmo processo. |

Regras importantes:

- O **serviço não abre janela nem bandeja** (evita Session 0, `CreateProcessAsUser` frágil e crashes).
- A **UI sempre roda na sessão do usuário** e fala com o serviço por **named pipe** (IPC local).
- Se o serviço não estiver ativo, o exe na sessão do usuário entra em **modo local** automaticamente.

```
  [Serviço DocToPDF]  Session 0          [DocToPDF.exe]  Session do usuário
   Polling + PDF  ◄──── named pipe ────►  Bandeja + painel
```

### Instalação típica

1. Instalar o serviço apontando para `C:\DocToPDF\DocToPDF.exe` (o host detecta modo serviço automaticamente).
2. Registrar a bandeja no logon (uma vez):

```powershell
powershell -ExecutionPolicy Bypass -File C:\DocToPDF\repo\DocToPDF\install-tray-at-logon.ps1 -ExePath C:\DocToPDF\DocToPDF.exe
```

3. Reiniciar o serviço após publicar atualizações (`update-and-restart.ps1`).

**Versão atual:** v0.3.2

### Serviço + bandeja

- A bandeja aberta pelo serviço usa `--attach-service` (só IPC, sem polling local).
- Ao **parar o serviço Windows**, a bandeja **fecha sozinha** em ~6 s (não fica processando órfão).
- Para **modo local** contínuo: pare o serviço e execute `DocToPDF.exe` (sem serviço ativo).

## Repositório (branch `main`)

```bash
git clone https://github.com/ericmaubr/DocToPDF.git
cd DocToPDF
git checkout main
git pull
```

## Estrutura

```
DocToPDF/
├── Program.cs              # Modos: serviço | bandeja | standalone
├── Core/                   # Polling, parsers, PDF, IPC
├── UI/                     # TrayApp, MainForm
├── install-tray-at-logon.ps1
└── DocToPDF.conf
```

## Requisitos

- Windows 10/11 ou Windows Server
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (apenas no modo framework-dependent)

## Build

```bash
dotnet build DocToPDF/DocToPDF.csproj
```

## Atualização rápida no Windows

```powershell
powershell -ExecutionPolicy Bypass -File C:\DocToPDF\repo\DocToPDF\update-and-restart.ps1 `
  -RepoRoot C:\DocToPDF\repo `
  -InstallDir C:\DocToPDF `
  -Variant compressed
```

Depois do publish, execute `DocToPDF.exe` uma vez (ou use o atalho em Inicializar) para a bandeja.

## Publicação

| Modo | Tamanho aprox. | Precisa instalar .NET no PC? |
|------|----------------|------------------------------|
| Self-contained + compressão (recomendado) | **~78 MB** | Não |
| Self-contained sem compressão | ~173 MB | Não |
| Framework-dependent | **~21 MB** (pasta) | Sim |

```bash
dotnet publish DocToPDF/DocToPDF.csproj /p:PublishProfile=win-x64-compressed
```

## Diagnóstico

- **`DocToPDF-service.log`** ao lado do `.exe` (ex.: `C:\DocToPDF\DocToPDF-service.log`)
- Event Viewer → Aplicativo (`APPCRASH` no serviço)

## Amostras

Pasta `Samples/` — XML, JSON válido e `bad.json` para teste de erro.

```bash
DocToPDF.exe --verify C:\caminho\para\Samples
```
