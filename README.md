# DocToPDF

Aplicativo Windows (.NET 8) que monitora pastas e converte arquivos **XML** e **JSON** em PDF (QuestPDF). Inclui serviço Windows, bandeja do sistema e painel de configuração.

## Desenvolvimento

Todo o código ativo fica na branch **`main`**. Não usamos branches de feature no dia a dia — clone, altere e faça push direto no `main`.

```bash
git clone https://github.com/ericmaubr/DocToPDF.git
cd DocToPDF
git checkout main
git pull
```

### Build local

```bash
dotnet build DocToPDF/DocToPDF.csproj -c Release
```

### Publicar (Windows, single-file)

```bash
dotnet publish DocToPDF/DocToPDF.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

### Testar conversão (amostras)

```bash
dotnet run --project DocToPDF/DocToPDF.csproj -c Release -- --verify Samples
```

### Atualizar instalação no Windows

Com o repositório em `C:\DocToPDF\repo` e o programa em `C:\DocToPDF`:

```powershell
git -C C:\DocToPDF\repo checkout main
git -C C:\DocToPDF\repo pull
.\DocToPDF\update-and-restart.ps1 -RepoRoot C:\DocToPDF\repo -InstallDir C:\DocToPDF
```

## Configuração

Arquivo `DocToPDF.conf` (formato chave=valor) ao lado do executável. Ver `DocToPDF/DocToPDF.conf` de exemplo no projeto.

## Versão atual

**v0.2.10** — bandeja abre imediatamente; conexão com o serviço em segundo plano.

## CI

Push em `main` dispara o workflow [.github/workflows/build.yml](.github/workflows/build.yml) (compilação Release).
