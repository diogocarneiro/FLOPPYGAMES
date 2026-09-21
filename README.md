# FloppyGames

> Reviver o ritual de inserir uma disquete — e ver um jogo Steam a arrancar.

FloppyGames é um sistema para Windows que liga uma **disquete física 3.5" real** à tua biblioteca Steam. Insere a disquete numa drive USB de disquetes, vê a animação de carregamento, e o jogo arranca. Remove a disquete, e o jogo fecha-se sozinho — como se estivesses a tirar a cassete.

O suporte a **pen USB dedicada** existe como alternativa extra — útil para quem não tem (ou não quer arriscar) uma drive de disquetes física, mas o suporte principal e obrigatório do projeto são disquetes reais.

## Índice

- [Visão Geral](#visão-geral)
- [Fluxo de Funcionamento](#fluxo-de-funcionamento)
- [Componentes do Projeto](#componentes-do-projeto)
- [O ficheiro GAME.INI](#o-ficheiro-gameini)
- [Requisitos](#requisitos)
- [Instalação](#instalação)
- [Arranque Automático com o Windows](#arranque-automático-com-o-windows)
- [Idiomas](#idiomas)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Stack Tecnológica](#stack-tecnológica)
- [Roadmap](#roadmap)
- [Contribuir](#contribuir)
- [Licença](#licença)

## Visão Geral

O objetivo do FloppyGames é simples: transformar o gesto físico de inserir um suporte amovível num "cartão de acesso" a um jogo Steam. Cada disquete/pen corresponde a **um jogo**, identificado por um ficheiro `GAME.INI` na raiz do suporte, com a capa do jogo e o `AppID` da Steam.

Um agente residente no Windows (`FloppyGames Agent`) vigia as unidades amovíveis do sistema. Assim que deteta um suporte válido, mostra uma pequena animação de "a carregar" (estética retro/CRT), lança o jogo através do protocolo `steam://run/<APPID>`, confirma que o processo do jogo arrancou, e mantém-se em segundo plano a vigiar. Quando o suporte é removido, o agente termina automaticamente o processo do jogo configurado.

Um segundo componente, o `FloppyGames Label Studio`, permite criar as disquetes: gera o `GAME.INI`, guarda a capa do jogo e ajuda a desenhar/imprimir o autocolante/label físico do suporte.

## Fluxo de Funcionamento

```
┌─────────────────────┐
│ Inserção do disquete │
└──────────┬───────────┘
           ▼
┌─────────────────────┐
│ Deteção da mídia     │  (WM_DEVICECHANGE / WMI)
└──────────┬───────────┘
           ▼
┌─────────────────────┐
│ Leitura do GAME.INI  │
└──────────┬───────────┘
           ▼
┌─────────────────────┐
│ Carregamento da capa │
└──────────┬───────────┘
           ▼
┌─────────────────────┐
│ Animação de loading  │  (janela splash retro)
└──────────┬───────────┘
           ▼
┌──────────────────────────────┐
│ Execução de steam://run/APPID │
└──────────┬────────────────────┘
           ▼
┌───────────────────────────────┐
│ Confirmação do processo (PROCESS) │
└──────────┬─────────────────────┘
           ▼
┌─────────────────────┐
│ Jogo em execução     │  (agente fica em vigilância)
└──────────┬───────────┘
           ▼
┌─────────────────────┐
│ Remoção do disquete  │
└──────────┬───────────┘
           ▼
┌─────────────────────────────────┐
│ Encerramento automático do PROCESS │
└─────────────────────────────────┘
```

### Detalhe de cada etapa

1. **Inserção do disquete** — o utilizador insere uma disquete 3.5" real numa drive USB de disquetes (ou, em alternativa, uma pen USB dedicada).
2. **Deteção da mídia** — o agente combina duas fontes: eventos de sistema (WMI) para quando um volume aparece/desaparece (pens USB), e uma sondagem leve e dedicada às letras de unidade candidatas a drive de disquetes, porque o Windows **não notifica de forma fiável** a troca de disco dentro de uma drive já ligada — ao contrário de uma pen, a letra da drive de disquetes mantém-se atribuída, só o estado "pronta" muda consoante haja ou não disco lá dentro. Ver [nota técnica](#nota-técnica-deteção-de-disquetes) abaixo.
3. **Leitura do `GAME.INI`** — valida se a raiz do suporte contém um `GAME.INI` bem formado; ignora o suporte caso contrário.
4. **Carregamento da capa** — lê a imagem referenciada em `COVER=` a partir do próprio suporte.
5. **Animação** — uma splash widescreen (1280×720) no estilo de um ecrã de arranque retro: a capa do jogo como wallpaper desfocado, título, tipo de suporte (disquete/USB) logo por baixo do nome, `DESCRIPTION`, um varrimento CRT a deslizar continuamente (devagar, nunca "flicker" rápido — por acessibilidade), e um painel de estatísticas apurado localmente — tamanho do suporte e estado "instalado" (e tamanho em disco) em todas as plataformas; build ID, data da última atualização e tempo de jogo total só na Steam (`appmanifest_*.acf` e `localconfig.vdf` — sem equivalente local fiável na Epic/GOG). Uma barra de progresso real de 0% a 100% avança em função do atraso/timeout configurados, e só atinge 100% quando o processo do jogo é mesmo confirmado. Se for mesmo uma disquete física (não uma pen), toca também um som de motor/cabeça de leitura sintetizado localmente. O som e o efeito CRT são ambos desligáveis em Definições.
   > **Conquistas (opcional, só Steam):** a Steam só as guarda localmente num cache binário não documentado (`appcache\stats\UserGameStats_*.bin`, conquistas embrulhadas em bits dentro de stats inteiros, sem esquema estável entre jogos) — fiável de mais para depender disso. Em vez disso, se configurares uma chave da Steam Web API nas Definições do Agent (grátis, em steamcommunity.com/dev/apikey), a splash faz um pedido a `ISteamUserStats/GetPlayerAchievements` e mostra "X/Y conquistas". Sem chave, essa linha simplesmente não aparece — o resto do FloppyGames continua 100% offline.
6. **Lançamento** — despacha para a loja certa consoante `PLATFORM`: `steam://run/<APPID>` na Steam, o URI documentado da Epic Games Launcher na Epic, ou o `.exe` instalado diretamente na GOG (ver [nota técnica](#nota-técnica-suporte-multi-plataforma) abaixo).
7. **Confirmação do processo** — sondagem da lista de processos até detetar `PROCESS` (com timeout configurável); fecha a splash quando confirmado.
8. **Vigilância** — o agente mantém-se a monitorizar o par (suporte inserido ↔ processo vivo).
9. **Remoção do disquete** — deteção de remoção do volume.
10. **Encerramento automático** — termina o processo configurado em `PROCESS` (kill "gentil" com `WM_CLOSE`, escalando para `TerminateProcess` se necessário), devolvendo o sistema ao estado de repouso.

### Nota técnica: deteção de disquetes

Uma pen USB e uma disquete comportam-se de forma muito diferente aos olhos do Windows:

- **Pen USB** — inserir/remover a pen faz a letra de unidade inteira aparecer/desaparecer. O Windows notifica isto por evento (`WMI Win32_VolumeChangeEvent`), sem necessidade de sondagem.
- **Disquete** — a drive USB de disquetes, uma vez ligada, mantém sempre a mesma letra atribuída (tipicamente `A:\`). Trocar o disco lá dentro **não** gera o mesmo evento de sistema de forma fiável; o que muda é apenas se o Windows reporta a unidade como "pronta" (`DriveInfo.IsReady`). Isto é uma limitação conhecida — o próprio Explorador de Ficheiros do Windows por vezes falha a detetar uma troca de disquete sem um refresh manual.

Por isso, o Agent combina as duas fontes num único watcher composto:
- `WmiRemovableMediaWatcher` — eventos de sistema, cobre pens USB.
- `PollingFloppyDriveWatcher` — sondagem leve (≈1.5s) das letras candidatas a drive de disquetes (`A:\`, `B:\` por omissão), cobre a troca de disco físico.

É a única exceção deliberada ao princípio "sem *polling* agressivo": está confinada a 1-2 letras de unidade específicas, a um intervalo modesto, e existe porque não há alternativa fiável no Windows para este caso.

## Componentes do Projeto

### 1. FloppyGames Agent
Serviço/aplicação de bandeja (*system tray*) que corre em segundo plano. É o motor de todo o fluxo acima: deteção de mídia, parsing do `GAME.INI`, splash de arranque, lançamento via protocolo Steam, vigilância de processo e encerramento no eject. Também expõe um menu de bandeja com estado atual, logs e acesso rápido às definições.

### 2. FloppyGames Label Studio
Aplicação de ambiente de trabalho para **criar** as disquetes/pens, organizada em 4 passos visíveis
na própria janela (escolher jogo → detalhes → capa → gravar):
- Seletor de plataforma (Steam / Epic Games / GOG) — pesquisa a biblioteca local de cada uma sem precisar de *API key* nem autenticação (ver [nota técnica](#nota-técnica-suporte-multi-plataforma)). Só mostra as plataformas ligadas nas Definições do Agent (Steam ligada por omissão; Epic e GOG desligadas) — não afeta o Agent, que continua a lançar qualquer `GAME.INI` já criado independentemente disto.
- Sugestão automática do executável a vigiar, a partir da pasta de instalação (o utilizador confirma/corrige).
- Pré-visualização da capa: descarregada do CDN público da Steam quando a plataforma é Steam; na Epic/GOG (sem CDN público equivalente) a caixa explica isso diretamente em vez de ficar vazia sem explicação, com escolha manual de imagem local sempre disponível.
- **Catálogo partilhável** (`catalog/catalog.json`, versionado no repositório — sem rede): quando o jogo escolhido já está catalogado, preenche automaticamente o processo (verificado) e a descrição (já traduzida nos 5 idiomas), e usa a capa do catálogo para Epic/GOG quando existir uma. Ver [catalog/README.md](catalog/README.md) para adicionar entradas.
- Geração automática do `GAME.INI`, incluindo as opções avançadas (timeout, atraso, encerramento suave).
- Escrita direta do `GAME.INI` + capa para o suporte amovível selecionado, com validação de espaço e aviso antes de sobrescrever.
- Desenho e impressão do label físico (impressão direta ou exportação para PNG).

### 3. Instalador (FloppyGames Setup)
Instalador único para Windows que:
- Instala o Agent e o Label Studio.
- Pergunta se o Agent deve arrancar automaticamente com o Windows.
- Regista/desregista o arranque automático de forma reversível a qualquer momento (não só na instalação).
- Faz desinstalação limpa (remove entradas de arranque, atalhos e ficheiros).

## O ficheiro GAME.INI

Cada suporte contém, na raiz, um `GAME.INI` com a seguinte estrutura mínima:

```ini
[Game]
TITLE=Half-Life 2
PLATFORM=STEAM
APPID=220
PROCESS=hl2.exe
COVER=cover.jpg
DESCRIPTION=Regressa a City 17 numa revolta contra o Combine.

[Options]
; tempo máximo (segundos) à espera que o processo do jogo apareça
WatchTimeoutSeconds=30
; atraso (segundos) antes de disparar o lançamento
LaunchDelaySeconds=2
; encerrar o processo de forma suave (WM_CLOSE) antes de forçar (TerminateProcess)
GracefulShutdown=true
```

| Campo | Obrigatório | Descrição |
|---|---|---|
| `TITLE` | Sim | Nome apresentado na animação de loading. |
| `PLATFORM` | Não | `STEAM` (default), `EPIC` ou `GOG` — decide que campo de identidade abaixo é exigido. |
| `APPID` | Só se `PLATFORM=STEAM` | AppID da Steam, usado em `steam://run/APPID`. |
| `EPIC_NAMESPACE` / `EPIC_ITEM` / `EPIC_APP` | Só se `PLATFORM=EPIC` | Identidade do jogo no catálogo da Epic (ver [nota técnica](#nota-técnica-suporte-multi-plataforma) abaixo). |
| `GOG_ID` | Só se `PLATFORM=GOG` | ID do jogo (`Products.id`) na base de dados local do GOG Galaxy. |
| `PROCESS` | Sim | Nome do executável a vigiar e a terminar na remoção do disquete. |
| `COVER` | Não | Caminho relativo à capa (na raiz do suporte). |
| `DESCRIPTION` | Não | Frase curta (uma linha) mostrada no ecrã de arranque, por baixo do título. |
| `WatchTimeoutSeconds` | Não | Timeout da confirmação de arranque (default 30s). |
| `LaunchDelaySeconds` | Não | Atraso antes do lançamento, para efeito de animação (default 2s). |
| `GracefulShutdown` | Não | Se `true`, tenta fechar o processo de forma suave antes de forçar. |

> **Nota de capacidade:** uma disquete 3.5" tem tipicamente 1.44 MB. O `GAME.INI` ocupa bytes irrelevantes, mas a `COVER` deve ser uma imagem pequena (JPEG comprimido, poucas dezenas de KB) para deixar espaço de sobra. O Label Studio valida o espaço disponível antes de escrever para o suporte.

### Nota técnica: suporte multi-plataforma

O Label Studio e o Agent suportam três lojas, todas com o mecanismo confirmado contra dados reais:

- **Steam** — protocolo `steam://run/<appid>`. Biblioteca lida de `appmanifest_*.acf`.
- **Epic Games Launcher** — protocolo documentado
  `com.epicgames.launcher://apps/{namespace}%3A{item}%3A{appname}?action=launch&silent=true`.
  Biblioteca lida de `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item` (JSON simples).
  **Verificado** contra um manifesto real — os nomes de campo (`DisplayName`, `InstallLocation`,
  `InstallSize`, `CatalogNamespace`, `CatalogItemId`, `AppName`) vêm de lá, não de suposição.
- **GOG Galaxy** — sem protocolo oficial fiável para lançar um jogo por URI, por isso o Agent
  lança o `.exe` instalado diretamente. Tanto a biblioteca como o executável a lançar são lidos da
  própria base de dados do GOG Galaxy 2.0 (`%ProgramData%\GOG.com\Galaxy\storage\galaxy-2.0.db`,
  SQLite, via `Microsoft.Data.Sqlite`, aberta só-leitura) — tabelas `Products`,
  `InstalledBaseProducts`, `DiskSizes` e `PlayTasks`/`PlayTaskLaunchParameters` (a tarefa de
  arranque principal, `isPrimary = 1`, dá o executável correto). O caminho do jogo não é gravado no
  `GAME.INI` (só o `GOG_ID`), para o floppy continuar portátil entre reinstalações.
  **Esquema verificado** com `sqlite3` contra a base de dados de uma instalação real e em execução
  do GOG Galaxy 2.0 — a primeira tentativa desta implementação assumia entradas no Registo (como as
  instalações standalone antigas da GOG faziam, e como ferramentas como o Playnite ainda descrevem
  nalguma documentação), mas o Galaxy 2.0 não usa o Registo para isto de todo; essa base ficou como
  lição de que "documentado pela comunidade" não substitui inspecionar os dados reais. A biblioteca
  de teste estava vazia (sem jogos instalados), por isso as consultas foram validadas contra o
  esquema real e cobertas por testes automatizados com dados sintéticos — mas o percurso completo
  só fica 100% confirmado com um jogo GOG realmente instalado.

## Requisitos

- Windows 10/11 (x64).
- Cliente Steam instalado e autenticado.
- **Drive de disquetes 3.5" USB** (suporte principal) — ou, em alternativa, uma pen USB dedicada por jogo.
- .NET 10 Desktop Runtime (incluído no instalador).

## Instalação

Ainda sem *releases* publicados (falta a Fase 5's pipeline de CI) — por agora, compila o instalador a partir do código-fonte:

1. Instalar o [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).
2. Correr `src\FloppyGames.Installer\build.ps1` — publica o Agent e o Label Studio e gera `FloppyGamesSetup.exe` em `src\FloppyGames.Installer\Output\`. Ver [detalhes](src/FloppyGames.Installer/README.md).
3. Executar o instalador — escolher se o Agent deve arrancar com o Windows.
4. Abrir o **FloppyGames Label Studio** para preparar a primeira disquete.
5. Inserir o suporte preparado e confirmar que o jogo arranca.

## Arranque Automático com o Windows

O arranque automático do Agent pode ser ativado/desativado em qualquer altura, sem reinstalar:

- Durante a instalação, através de uma opção explícita no assistente.
- Depois de instalado, através do menu da bandeja do sistema (*FloppyGames → Definições → Iniciar com o Windows*) ou executando `FloppyGamesSetup.exe /configure`.

Tecnicamente, o toggle escreve/remove uma entrada em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, evitando a necessidade de privilégios de administrador.

## Idiomas

O Agent, o Label Studio e o instalador suportam 5 idiomas — **Francês** (por omissão), Inglês,
Português, Espanhol e Italiano:

- Muda-se num único sítio: menu da bandeja → *Definições* → combo "Idioma" (mostra cada idioma no
  seu próprio nome nativo). O Label Studio não tem seletor próprio — lê o mesmo valor de
  `settings.json`, para nunca ficar dessincronizado entre as duas apps. Uma alteração aplica-se no
  próximo arranque de cada app (o texto é atribuído em código, não via *binding* dinâmico, por
  isso não muda a quente numa janela já aberta).
- O instalador também tem os 5 idiomas — Francês primeiro, por isso é o idioma por omissão sem
  mostrar nenhum diálogo (`ShowLanguageDialog=no`, para o modo `FloppyGamesSetup.exe /configure`
  continuar a não mostrar diálogos); `/LANG=english`, `/LANG=portuguese`, `/LANG=spanish` ou
  `/LANG=italian` escolhem um dos outros explicitamente.
- Implementação: um único conjunto de recursos `.resx` partilhado em `FloppyGames.Core`
  (`Core/Localization/`), com Francês como recurso neutro e Inglês/Português/Espanhol/Italiano
  como satélites — a mesma mecânica standard do .NET, sem nada por medida. Datas e números
  (tamanhos, por exemplo) seguem também o estilo de cada idioma.

## Estrutura do Projeto

```
FLOPPYGAMES/
├── src/
│   ├── FloppyGames.Agent/          # Serviço de bandeja — deteção, lançamento, vigilância
│   ├── FloppyGames.LabelStudio/    # Editor de disquetes/labels
│   ├── FloppyGames.Core/           # Lógica partilhada (GAME.INI, logging, Steam, processos)
│   └── FloppyGames.Installer/      # Script Inno Setup (Fase 5)
├── tests/
│   └── FloppyGames.Core.Tests/     # Testes unitários do Core (xUnit)
├── docs/
│   └── assets/                     # Diagramas, mockups
├── Directory.Build.props           # Definições MSBuild partilhadas (nullable, analisadores, etc.)
├── FloppyGames.slnx                # Solução .NET
├── LICENSE.md
├── README.md
└── ROADMAP.md
```

## Stack Tecnológica

| Camada | Escolha | Justificação |
|---|---|---|
| Agent + Label Studio | C# / .NET 10 (WPF) | Interop Win32 maduro (`RegisterDeviceNotification`, `Process`), UI nativa rápida a desenvolver, *single-file publish*, versão LTS mais recente disponível. |
| Deteção de mídia | `WMI (Win32_VolumeChangeEvent)` + sondagem dedicada para disquetes | Eventos para pens USB (sem *polling*); sondagem leve e confinada a `A:\`/`B:\` para troca de disco em drives de disquete, onde o Windows não notifica por evento. |
| Lançamento por plataforma | `steam://run/`, URI da Epic, `.exe` direto na GOG | Cada loja delega a validação/atualização do jogo em si mesma, quando tem protocolo para isso; a GOG não tem, por isso é a exceção. |
| Biblioteca GOG | `Microsoft.Data.Sqlite` sobre `galaxy-2.0.db` (só-leitura) | O GOG Galaxy 2.0 guarda tudo numa base de dados SQLite própria, não no Registo — única dependência externa nova, justificada por não haver alternativa razoável a implementar à mão (ver [nota técnica](#nota-técnica-suporte-multi-plataforma)). |
| Instalador | Inno Setup | Leve, scriptável, suporta tarefas opcionais (arranque automático). |
| Persistência de config | `settings.json` (Agent) + Registo do Windows (para o toggle de arranque) | Simples, sem dependência de base de dados própria do FloppyGames. |

## Roadmap

Ver [ROADMAP.md](ROADMAP.md) para o plano de desenvolvimento faseado.

## Contribuir

Este é um projeto pessoal orientado a hobby/nostalgia. Sugestões e *pull requests* são bem-vindos — abre uma *issue* antes de propor mudanças estruturais grandes.

## Licença

Distribuído sob licença MIT — ver [LICENSE.md](LICENSE.md).
