# DocToPDF

Aplicação Windows (.NET 8 / Windows Forms) que monitora uma pasta de entrada, converte arquivos `.xml` e `.json` em PDFs legíveis e oferece um ícone na bandeja do sistema com painel de configuração. Pode rodar como **serviço Windows**, como **bandeja anexada ao serviço** ou em **modo local (standalone)** — tudo a partir do mesmo `DocToPDF.exe`.

**Versão atual:** v0.4.6

---

## Sumário

- [O que o programa faz](#o-que-o-programa-faz)
- [Diretórios (o que cada um significa)](#diretórios-o-que-cada-um-significa)
- [Configuração (`DocToPDF.conf`)](#configuração-doctopdfconf)
- [Parâmetros de linha de comando](#parâmetros-de-linha-de-comando)
- [Arquitetura](#arquitetura)
- [O que acontece ao iniciar o serviço](#o-que-acontece-ao-iniciar-o-serviço)
- [Bandeja: cores, menu e mensagens](#bandeja-cores-menu-e-mensagens)
- [Geração do PDF](#geração-do-pdf)
- [Logs e diagnóstico](#logs-e-diagnóstico)
- [Pré-requisitos](#pré-requisitos-para-compilação)
- [Compilação](#compilação)
- [Gerar o executável (publish)](#gerar-o-executável-publish)
- [Scripts auxiliares](#scripts-auxiliares)
- [Instalação típica](#instalação-típica)
- [Amostras e verificação](#amostras-e-verificação)

---

## O que o programa faz

A cada ciclo de *polling* (ou sob demanda), o DocToPDF varre o **diretório de entrada**, processa cada arquivo `.xml`/`.json` (em ordem alfabética) e, para cada um:

1. Faz o **parse** do conteúdo numa árvore genérica (`DocumentNode`).
   - **XML:** atributos viram filhos prefixados com `@`; elementos repetidos viram itens de array (`[1]`, `[2]`, …). Detecta automaticamente codificação `iso-8859-1` declarada no cabeçalho; caso contrário lê como UTF-8.
   - **JSON:** objetos, arrays, números, strings, booleanos e `null` são convertidos recursivamente. Um objeto-raiz com uma única propriedade é "desembrulhado"; com várias propriedades, usa o nome do arquivo como título.
2. **Gera o PDF** (A4, margem 20 mm, fonte monoespaçada *Liberation Mono* com fallback *Courier New*) no **diretório de saída**, com o mesmo nome do arquivo e extensão `.pdf`. Chaves são formatadas (camelCase → "Camel Case"; títulos de seção em MAIÚSCULAS e negrito).
3. (Opcional) **Copia o PDF** para o **diretório do robô**, se configurado.
4. **Move o arquivo original** para o **diretório de processados** (sobrescreve se já existir lá).
5. Em caso de erro de parse/geração, **move o original** para o **diretório de erros** e registra a falha no log.

Cada arquivo processado emite uma linha de log: `✅ arquivo.xml → arquivo.pdf` (sucesso) ou `❌ arquivo.xml — <motivo>` (erro).

---

## Diretórios (o que cada um significa)

| Diretório | Chave `.conf` | Obrigatório | Função |
|-----------|---------------|:-----------:|--------|
| **Entrada** | `DiretorioEntrada` | Sim | Pasta monitorada. Arquivos `.xml`/`.json` colocados aqui são processados. |
| **Saída PDF** | `DiretorioSaidaPdf` | Sim | Onde os PDFs gerados são gravados. |
| **Processados** | `DiretorioProcessados` | Sim | Para onde o arquivo original é movido após gerar o PDF com sucesso. |
| **Erros** | `DiretorioErros` | Sim | Para onde o arquivo original é movido quando o parse ou a geração falha. |
| **Robô** | `DiretorioRobo` | Não | Se preenchido, recebe uma **cópia** do PDF gerado (deixe vazio para desativar). |

Os quatro diretórios obrigatórios são **criados automaticamente** se não existirem — ao iniciar o serviço/modo local e ao salvar configurações pelo painel. O diretório do robô só é criado quando há um PDF a copiar.

---

## Configuração (`DocToPDF.conf`)

Formato texto simples `chave=valor`, uma por linha. Linhas vazias e linhas iniciadas por `#` ou `;` são ignoradas. O arquivo fica **na mesma pasta do executável** e tem o nome do executável (`DocToPDF.exe` → `DocToPDF.conf`).

```ini
# DocToPDF — Configuração
DiretorioEntrada=C:\DocToPDF\entrada
DiretorioSaidaPdf=C:\DocToPDF\saida
DiretorioProcessados=C:\DocToPDF\processados
DiretorioErros=C:\DocToPDF\erros
DiretorioRobo=
IntervaloPollingSegundos=30
```

- **`IntervaloPollingSegundos`** — intervalo entre varreduras. Mínimo 1, máximo 86400 (24 h). Padrão 30.
- O painel grava as chaves em português; chaves antigas em inglês (`InputDirectory`, `PollingIntervalSeconds`, …) ainda são lidas para compatibilidade.
- Se existir um `appsettings.json` legado e nenhum `.conf`, ele é migrado automaticamente para o novo formato no primeiro carregamento.
- Salvar pelo painel revalida os diretórios obrigatórios, cria as pastas e reinicia o timer de polling.

---

## Parâmetros de linha de comando

| Comando | Efeito |
|---------|--------|
| `DocToPDF.exe` | Abre a bandeja. Conecta ao serviço se ele estiver ativo; caso contrário entra em **modo local**. |
| `DocToPDF.exe --attach-service` | Abre a bandeja **anexada ao serviço** (só IPC, sem polling local). Usado pelo lançamento automático do serviço. |
| `DocToPDF.exe --service` | Executa como **serviço Windows** (sem UI). Também é o modo assumido quando o processo não é interativo. |
| `DocToPDF.exe --verify [pasta]` | Roda a verificação *headless* de processamento e encerra com código 0 (ok) ou 1 (falha). Útil em CI. |
| `DocToPDF.exe --help` / `-h` / `/?` / `/help` | Mostra a ajuda em uma caixa de mensagem. |

Apenas **uma instância interativa por sessão** é permitida (mutex). Ao tentar abrir uma segunda, a janela existente é trazida para a frente (sem popup quando o lançamento veio do serviço via `--attach-service`).

---

## Arquitetura

O mesmo `DocToPDF.exe` tem **três modos**, alinhados à [orientação da Microsoft sobre serviços interativos](https://learn.microsoft.com/en-us/windows/win32/services/interactive-services) e à prática consolidada para apps .NET ([Stephen Cleary — Managed Services and UIs](https://blog.stephencleary.com/2011/05/managed-services-and-uis.html)):

| Modo | Quem inicia | O que faz |
|------|-------------|-----------|
| **Serviço Windows** | SCM (`services.msc`) | Só processamento + IPC (named pipe). **Sem UI** — Session 0. |
| **Bandeja + serviço** | Usuário no login / lançada pelo serviço | Conecta ao pipe e controla o worker remoto. |
| **Standalone (local)** | Usuário (`DocToPDF.exe` sem serviço ativo) | Polling e bandeja no mesmo processo. |

```
  [Serviço DocToPDF]  Session 0          [DocToPDF.exe]  Sessão do usuário
   Polling + PDF  ◄──── named pipe ────►  Bandeja + painel
                  "DocToPDF.IPC.v1"
```

Regras importantes:

- O **serviço nunca abre janela nem bandeja** (evita Session 0, `CreateProcessAsUser` frágil e crashes).
- A **UI sempre roda na sessão do usuário** e fala com o serviço por **named pipe** (`DocToPDF.IPC.v1`, protocolo de linha UTF-8 sem BOM).
- Se o serviço não estiver ativo, o exe na sessão do usuário entra em **modo local** automaticamente.
- Uma **trava de modo local** (`LocalModeLock`) impede processamento duplicado: se uma instância standalone está rodando, o serviço se recusa a iniciar (e registra o motivo no log).

### Comandos IPC

A bandeja remota troca comandos de linha com o serviço: `PING`, `GET_STATUS`, `START`, `STOP`, `RESTART_TIMER`, `PROCESS_NOW`, `RELOAD_SETTINGS`, `SUBSCRIBE_LOGS`. As respostas começam com `OK` ou `ERR`. Linhas de log são transmitidas com o prefixo `LOG ` e o servidor mantém um histórico das últimas 500 linhas, reproduzido a cada nova assinatura.

A conexão da bandeja ao serviço é **diferida**: a UI abre imediatamente e tenta conectar em segundo plano (até 25 tentativas de 400 ms, com 200 ms de intervalo). Enquanto conecta, o ícone fica amarelo.

---

## O que acontece ao iniciar o serviço

1. Inicializa o log em arquivo (`DocToPDF-service.log`).
2. Verifica a trava de modo local — se houver uma instância standalone ativa, **aborta** com erro no log (evita processar a mesma pasta duas vezes).
3. Registra handlers globais de exceção (`UnhandledException`, `UnobservedTaskException`).
4. Sobe o host de serviço (`UseWindowsService`, nome do serviço: **DocToPDF**) com o `DocToPDFBackgroundService`, que:
   - inicia o **servidor IPC** (named pipe);
   - carrega configurações, garante os diretórios e **inicia o polling**;
   - após ~1,5 s, tenta **lançar a bandeja na sessão ativa do usuário** via `CreateProcessAsUser` com `--attach-service` (janela oculta, só ícone).
5. Ao **parar o serviço Windows**, o polling é encerrado, o IPC é descartado e a bandeja conectada detecta a queda (2 pings perdidos, ~6 s), exibe um aviso e **se fecha sozinha** — não fica processando órfã.

Para **modo local contínuo**: pare o serviço e execute `DocToPDF.exe` (sem serviço ativo).

---

## Bandeja: cores, menu e mensagens

### Cores do ícone (bolinha)

| Cor | Significado |
|-----|-------------|
| 🔵 **Azul** (DodgerBlue) | Rodando em **modo local** (standalone). |
| 🟢 **Verde** (LimeGreen) | Rodando **conectado ao serviço Windows**. |
| 🟡 **Amarelo** (Goldenrod) | **Conectando** ao serviço (ainda sem conexão). |
| ⚪ **Cinza** (Gray) | **Parado** (processamento desligado, em qualquer modo). |

### Menu de contexto

- **Modo: …** (somente leitura) — mostra `Local`, `Serviço Windows` ou `Conectando ao serviço…`.
- **Abrir Painel** — abre a janela de configuração (duplo-clique no ícone faz o mesmo).
- **Iniciar processamento / Parar processamento** — alterna o timer de polling (texto muda conforme o estado).
- **Processa Agora** — força uma varredura imediata.
- **Sair** — *só aparece no modo standalone*. Anexado ao serviço, fechar a bandeja não para o serviço — apenas oculta o ícone.

O texto da dica do ícone (*tooltip*) mostra versão, estado e modo, por exemplo: `DocToPDF v0.4.6 — Rodando · Serviço Windows`.

### Mensagens (balões e log)

- **Inicialização:** balão com o modo atual e a legenda das cores (`Verde = serviço; azul = local; cinza = conectando/sem conexão`).
- **Conectando:** `Conectando ao serviço… (1ª tentativa: …)`; ao conseguir: `Conectado ao serviço DocToPDF.`
- **Falha de conexão:** `⚠ Não foi possível conectar ao serviço DocToPDF. Último erro: … Verifique services.msc ou execute DocToPDF.exe sem o serviço (modo local).`
- **Serviço parou:** balão `Serviço Windows parado. A bandeja será fechada.` seguido do encerramento da bandeja.
- **Processamento:** `✅ <arquivo> → <pdf>` ou `❌ <arquivo> — <motivo>`; mensagens de polling/lote também aparecem prefixadas por data/hora. O painel mantém as últimas 500 linhas, coloridas (verde = sucesso, vermelho = erro).

---

## Geração do PDF

- Biblioteca **QuestPDF** (licença Community).
- Página **A4**, margem **20 mm**, fonte padrão **Liberation Mono** (fallback **Courier New**), corpo em 9 pt.
- Estrutura hierárquica renderizada com indentação de 4 espaços por nível.
- Cabeçalhos de seção (nós sem valor) saem em **MAIÚSCULAS e negrito** (10 pt).
- Itens de array são prefixados por `[índice]`.
- Chaves `camelCase` viram "Camel Case"; o prefixo `@` (atributos XML) é removido na exibição.

---

## Logs e diagnóstico

- **`DocToPDF-service.log`** — ao lado do `.exe` (ex.: `C:\DocToPDF\DocToPDF-service.log`). Eventos do serviço; útil quando o serviço falha sem UI.
- **`DocToPDF-ipc.log`** — diagnóstico de conexão IPC da bandeja (tentativas, pings, erros). Cai em `%TEMP%` se a pasta do exe não for gravável.
- **Visualizador de Eventos** → *Aplicativo* — registra `FATAL`/`APPCRASH` do serviço.

---

## Pré-requisitos para compilação

- **Windows 10/11** ou **Windows Server** (o projeto usa Windows Forms; `EnableWindowsTargeting` permite restaurar/compilar em outras plataformas, mas o destino é Windows).
- **.NET 8 SDK** — <https://dotnet.microsoft.com/download/dotnet/8.0>.
- Para **executar** a variante *framework-dependent*: **.NET 8 Desktop Runtime** instalado na máquina alvo. As variantes self-contained não exigem runtime.

Dependências NuGet (restauradas automaticamente): `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices`, `QuestPDF`.

> O projeto compila com `TreatWarningsAsErrors` — qualquer warning quebra o build.

---

## Compilação

```powershell
# Restaurar e compilar (Debug)
dotnet build DocToPDF\DocToPDF.csproj

# Compilar em Release
dotnet build DocToPDF\DocToPDF.csproj -c Release
```

---

## Gerar o executável (publish)

Três variantes suportadas:

| Variante | Tamanho aprox. | Precisa de .NET na máquina? | Arquivo |
|----------|---------------:|:---------------------------:|---------|
| **Self-contained + compressão** (recomendada) | ~78 MB | Não | `.exe` único |
| **Self-contained sem compressão** | ~173 MB | Não | `.exe` único |
| **Framework-dependent** | ~21 MB (pasta) | Sim | pasta com vários arquivos |

### Comprimida (single-file, self-contained)

```powershell
dotnet publish DocToPDF\DocToPDF.csproj /p:PublishProfile=win-x64-compressed
# saída: DocToPDF\bin\publish\win-x64-compressed\DocToPDF.exe
```

### Full (single-file, sem compressão)

```powershell
dotnet publish DocToPDF\DocToPDF.csproj -r win-x64 -c Release `
  --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false
```

### Framework-dependent (pasta menor; exige runtime na máquina alvo)

```powershell
dotnet publish DocToPDF\DocToPDF.csproj /p:PublishProfile=win-x64-framework-dependent
# saída: DocToPDF\bin\publish\win-x64-fdd\
```

---

## Scripts auxiliares

Em `DocToPDF\`:

### `update-and-restart.ps1` — atualizar instalação no Windows

Para o serviço, finaliza processos antigos, faz `git pull`, publica, copia `DocToPDF.exe` + `DocToPDF.conf` para o diretório de instalação e reinicia o serviço. **Requer Administrador.**

```powershell
powershell -ExecutionPolicy Bypass -File C:\DocToPDF\repo\DocToPDF\update-and-restart.ps1 `
  -RepoRoot C:\DocToPDF\repo `
  -InstallDir C:\DocToPDF `
  -Variant compressed     # ou: full
```

Parâmetros úteis: `-SkipGitPull` (não atualiza o repo), `-StartTrayUi` (abre a bandeja ao final), `-ServiceName` (padrão `DocToPDF`).

### `install-tray-at-logon.ps1` — registrar a bandeja no logon

Cria um atalho em *Inicializar* do usuário apontando para o `.exe`. A bandeja abrirá automaticamente a cada logon e usará o serviço quando ele estiver ativo.

```powershell
powershell -ExecutionPolicy Bypass -File C:\DocToPDF\repo\DocToPDF\install-tray-at-logon.ps1 `
  -ExePath C:\DocToPDF\DocToPDF.exe
```

### `Resources\gen-appicon.ps1` — regenerar o ícone do app

Gera `Resources\appicon.ico` (multi-resolução) a partir de desenho vetorial.

---

## Instalação típica

1. Publicar e copiar `DocToPDF.exe` para `C:\DocToPDF\` (use `update-and-restart.ps1`).
2. Criar o serviço (uma vez), apontando para o exe com `--service`:

   ```powershell
   sc.exe create DocToPDF binPath= "C:\DocToPDF\DocToPDF.exe --service" start= auto
   sc.exe start DocToPDF
   ```

3. Registrar a bandeja no logon (uma vez):

   ```powershell
   powershell -ExecutionPolicy Bypass -File C:\DocToPDF\repo\DocToPDF\install-tray-at-logon.ps1 -ExePath C:\DocToPDF\DocToPDF.exe
   ```

4. A cada atualização, rode `update-and-restart.ps1` para republicar e reiniciar o serviço.

---

## Amostras e verificação

A pasta `Samples/` contém um XML, um JSON válido e um `bad.json` (para testar o fluxo de erro).

```powershell
DocToPDF.exe --verify C:\caminho\para\Samples
```

A verificação gera PDFs a partir das amostras em uma pasta temporária, confirma que o `bad.json` vai para o diretório de erros e valida a formatação de chaves. Retorna `0` em caso de sucesso (`Verification passed.`) ou `1` em falha — ideal para CI.

---

## Repositório

```bash
git clone https://github.com/ericmaubr/DocToPDF.git
cd DocToPDF
git checkout main
git pull
```

### Estrutura

```
DocToPDF/
├── Program.cs                      # Entry point: serviço | bandeja | standalone | verify
├── Core/                           # Polling, parsers, geração de PDF, backends, IPC
│   ├── Ipc/                        # Servidor e cliente named pipe
│   └── …
├── UI/                             # TrayApp, MainForm, ícones
├── Models/                         # AppSettings, DocumentNode
├── Verify/                         # Verificação headless (--verify)
├── Resources/                      # Ícone do app + gerador
├── Properties/PublishProfiles/     # win-x64-compressed | win-x64-framework-dependent
├── DocToPDF.conf                   # Configuração de exemplo
├── install-tray-at-logon.ps1
└── update-and-restart.ps1
```
