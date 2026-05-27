# DocToPDF

Aplicação Windows (.NET 8) que monitora uma pasta de entrada, converte arquivos `.xml` e `.json` em PDFs legíveis para robôs e oferece ícone na bandeja do sistema com painel de configuração.

## Estrutura

```
DocToPDF/
├── Program.cs
├── Core/          # Polling, parsers, PDF, processamento de arquivos
├── Models/
├── UI/            # TrayApp, MainForm
└── appsettings.json
```

## Requisitos

- Windows 10/11 ou Windows Server
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (apenas se não usar o executável self-contained)

## Build

```bash
dotnet build DocToPDF/DocToPDF.csproj
```

## Publicação (executável único)

```bash
dotnet publish DocToPDF/DocToPDF.csproj -r win-x64 --self-contained -p:PublishSingleFile=true -c Release
```

O `.exe` fica em `DocToPDF/bin/Release/net8.0-windows/win-x64/publish/DocToPDF.exe`.

## Uso

1. Execute `DocToPDF.exe` (modo interativo: ícone na bandeja).
2. Abra o painel (duplo clique no ícone ou **Abrir Painel**).
3. Configure os diretórios, salve e use **Iniciar Serviço** / **Processa Agora**.
4. Para instalar como serviço Windows, registre o mesmo executável com `sc create` apontando para o binário; em sessão não interativa o app usa `UseWindowsService()`.

## Amostras

Arquivos de exemplo em `Samples/`:

- `729494492026040001.xml` (encoding `iso-8859-1`)
- `72949449-MIT-202604.json`
- `bad.json` (inválido, para teste de erro)

## Verificação headless (Windows)

```bash
DocToPDF.exe --verify C:\caminho\para\Samples
```
