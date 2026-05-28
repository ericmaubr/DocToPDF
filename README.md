# DocToPDF

Aplicação Windows (.NET 8) que monitora uma pasta de entrada, converte arquivos `.xml` e `.json` em PDFs legíveis para robôs e oferece ícone na bandeja do sistema com painel de configuração.

## Repositório (branch `main`)

Todo o desenvolvimento ativo é feito na branch **`main`**. Clone, trabalhe e faça push direto nela — não há fluxo com branches de feature.

```bash
git clone https://github.com/ericmaubr/DocToPDF.git
cd DocToPDF
git checkout main
git pull
```

**Versão atual no código:** v0.2.12 (bandeja instantânea; IPC com o serviço em segundo plano).

## Estrutura

```
DocToPDF/
├── Program.cs
├── Core/          # Polling, parsers, PDF, processamento de arquivos
├── Models/
├── UI/            # TrayApp, MainForm
└── DocToPDF.conf
```

## Requisitos

- Windows 10/11 ou Windows Server
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (apenas no modo framework-dependent)

## Build

```bash
dotnet build DocToPDF/DocToPDF.csproj
```

## CI

Push ou pull request em `main` dispara [.github/workflows/build.yml](.github/workflows/build.yml) (compilação Release no GitHub Actions).

## Atualização rápida no Windows (script)

Arquivo: `DocToPDF/update-and-restart.ps1`

Executar no **PowerShell como Administrador**:

```powershell
powershell -ExecutionPolicy Bypass -File C:\DocToPDF\repo\DocToPDF\update-and-restart.ps1 `
  -RepoRoot C:\DocToPDF\repo `
  -InstallDir C:\DocToPDF `
  -Variant compressed `
  -StartTrayUi
```

O script:

1. para o serviço `DocToPDF`
2. encerra processos `DocToPDF.exe` antigos
3. faz `git pull` na branch `main` do repositório
4. executa `dotnet publish` (comprimido ou full)
5. copia `DocToPDF.exe` e `DocToPDF.conf` para `C:\DocToPDF`
6. inicia o serviço novamente
7. opcionalmente abre a UI com `--ui`

## Publicação

O executável parece “grande” porque o modo **self-contained** embute o runtime .NET 8 + WinForms + bibliotecas nativas do QuestPDF (Skia). O código do app em si é pequeno.

| Modo | Tamanho aprox. | Precisa instalar .NET no PC? |
|------|----------------|------------------------------|
| Self-contained + compressão (recomendado) | **~78 MB** (um `.exe`) | Não |
| Self-contained sem compressão | ~173 MB | Não |
| Framework-dependent (pasta com DLLs) | **~21 MB** no total | Sim — [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |

### Recomendado (menor `.exe` sem depender do runtime)

```bash
dotnet publish DocToPDF/DocToPDF.csproj /p:PublishProfile=win-x64-compressed
```

Saída: `DocToPDF/bin/publish/win-x64-compressed/DocToPDF.exe`

### Mínimo absoluto (exige runtime instalado)

```bash
dotnet publish DocToPDF/DocToPDF.csproj /p:PublishProfile=win-x64-framework-dependent
```

Copie a pasta inteira `win-x64-fdd` para o PC de destino (não só o `.exe` de ~150 KB).

## Uso

1. Execute `DocToPDF.exe` (modo interativo: ícone na bandeja).
2. Abra o painel (duplo clique no ícone ou **Abrir Painel**).
3. Configure os diretórios, salve e use **Iniciar Serviço** / **Processa Agora**.
4. **Serviço Windows:** ao iniciar o serviço, o processamento fica em segundo plano e a **bandeja** abre na sessão do usuário logado. Use o painel para **Processa Agora** e salvar o `DocToPDF.conf`. **Sair** na bandeja fecha só a interface; o serviço continua.
5. **Sem serviço:** execute `DocToPDF.exe` — bandeja e processamento no mesmo processo; **Sair** encerra o timer.

## Amostras

Arquivos de exemplo em `Samples/`:

- `729494492026040001.xml` (encoding `iso-8859-1`)
- `72949449-MIT-202604.json`
- `bad.json` (inválido, para teste de erro)


## Diagnóstico do serviço

Se a UI mostrar *Serviço DocToPDF indisponível* ou o serviço parar sozinho:

1. Abra o **Visualizador de Eventos** → **Logs do Windows** → **Aplicativo** (erros `DocToPDF.exe` / `APPCRASH`).
2. Leia o arquivo **`DocToPDF-service.log`** ao lado do executável (ex.: `C:\DocToPDF\DocToPDF-service.log`).
3. Reinicie o serviço após atualizar para a versão mais recente do `main`.

## Verificação headless (Windows)

```bash
DocToPDF.exe --verify C:\caminho\para\Samples
```
