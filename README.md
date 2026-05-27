# DocToPDF

Aplicação Windows (.NET 8) que monitora uma pasta de entrada, converte arquivos `.xml` e `.json` em PDFs legíveis para robôs e oferece ícone na bandeja do sistema com painel de configuração.

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

## Verificação headless (Windows)

```bash
DocToPDF.exe --verify C:\caminho\para\Samples
```
