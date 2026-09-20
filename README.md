# FloppyGames

> Reviver o ritual de inserir uma disquete — e ver um jogo Steam a arrancar.

FloppyGames é um sistema para Windows que liga um suporte físico nostálgico (disquete 3.5", ou uma pen USB formatada para simular uma) à tua biblioteca Steam. Insere o suporte, vê a animação de carregamento, e o jogo arranca. Remove o suporte, e o jogo fecha-se sozinho — como se estivesses a tirar a cassete.

## Índice

- [Visão Geral](#visão-geral)
- [Fluxo de Funcionamento](#fluxo-de-funcionamento)
- [Componentes do Projeto](#componentes-do-projeto)
- [O ficheiro GAME.INI](#o-ficheiro-gameini)
- [Requisitos](#requisitos)
- [Instalação](#instalação)
- [Arranque Automático com o Windows](#arranque-automático-com-o-windows)
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

1. **Inserção do disquete** — o utilizador insere o suporte físico (disquete real via leitor USB, ou pen USB dedicada).
2. **Deteção da mídia** — o agente recebe uma notificação de novo volume amovível (sem *polling* agressivo).
3. **Leitura do `GAME.INI`** — valida se a raiz do suporte contém um `GAME.INI` bem formado; ignora o suporte caso contrário.
4. **Carregamento da capa** — lê a imagem referenciada em `COVER=` a partir do próprio suporte.
5. **Animação** — mostra uma janela de splash a fingir o "acesso ao disco" (barra de progresso, som opcional de motor de disquete).
6. **Execução do comando Steam** — invoca `steam://run/<APPID>` via `ShellExecute`, deixando o cliente Steam tratar do lançamento/atualização do jogo.
7. **Confirmação do processo** — sondagem da lista de processos até detetar `PROCESS` (com timeout configurável); fecha a splash quando confirmado.
8. **Vigilância** — o agente mantém-se a monitorizar o par (suporte inserido ↔ processo vivo).
9. **Remoção do disquete** — deteção de remoção do volume.
10. **Encerramento automático** — termina o processo configurado em `PROCESS` (kill "gentil" com `WM_CLOSE`, escalando para `TerminateProcess` se necessário), devolvendo o sistema ao estado de repouso.

## Componentes do Projeto

### 1. FloppyGames Agent
Serviço/aplicação de bandeja (*system tray*) que corre em segundo plano. É o motor de todo o fluxo acima: deteção de mídia, parsing do `GAME.INI`, splash de arranque, lançamento via protocolo Steam, vigilância de processo e encerramento no eject. Também expõe um menu de bandeja com estado atual, logs e acesso rápido às definições.

### 2. FloppyGames Label Studio
Aplicação de ambiente de trabalho para **criar** as disquetes/pens:
- Pesquisa de jogos da biblioteca Steam local (lê `AppID`, nome e capa via Steam Web API / cache local do cliente Steam).
- Geração automática do `GAME.INI`.
- Download/recorte da capa (formato "front cover" de disquete).
- Escrita direta do `GAME.INI` + capa para o suporte amovível selecionado.
- Desenho e impressão do label/autocolante físico da disquete (moldes 3.5").

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
APPID=220
PROCESS=hl2.exe
COVER=cover.jpg

[Options]
; tempo máximo (segundos) à espera que o processo do jogo apareça
WatchTimeoutSeconds=30
; atraso (segundos) antes de disparar o comando steam://run
LaunchDelaySeconds=2
; encerrar o processo de forma suave (WM_CLOSE) antes de forçar (TerminateProcess)
GracefulShutdown=true
```

| Campo | Obrigatório | Descrição |
|---|---|---|
| `TITLE` | Sim | Nome apresentado na animação de loading. |
| `APPID` | Sim | AppID da Steam usado em `steam://run/APPID`. |
| `PROCESS` | Sim | Nome do executável a vigiar e a terminar na remoção do disquete. |
| `COVER` | Não | Caminho relativo à capa (na raiz do suporte). |
| `WatchTimeoutSeconds` | Não | Timeout da confirmação de arranque (default 30s). |
| `LaunchDelaySeconds` | Não | Atraso antes do lançamento, para efeito de animação (default 2s). |
| `GracefulShutdown` | Não | Se `true`, tenta fechar o processo de forma suave antes de forçar. |

## Requisitos

- Windows 10/11 (x64).
- Cliente Steam instalado e autenticado.
- Leitor de disquetes USB **ou** pen USB dedicada por jogo.
- .NET 10 Desktop Runtime (incluído no instalador).

## Instalação

1. Descarregar o instalador mais recente (`FloppyGamesSetup.exe`) a partir da página de releases.
2. Executar o instalador — escolher se o Agent deve arrancar com o Windows.
3. Abrir o **FloppyGames Label Studio** para preparar a primeira disquete.
4. Inserir o suporte preparado e confirmar que o jogo arranca.

## Arranque Automático com o Windows

O arranque automático do Agent pode ser ativado/desativado em qualquer altura, sem reinstalar:

- Durante a instalação, através de uma opção explícita no assistente.
- Depois de instalado, através do menu da bandeja do sistema (*FloppyGames → Definições → Iniciar com o Windows*) ou executando `FloppyGamesSetup.exe /configure`.

Tecnicamente, o toggle escreve/remove uma entrada em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, evitando a necessidade de privilégios de administrador.

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
| Deteção de mídia | `WM_DEVICECHANGE` + `WMI (Win32_VolumeChangeEvent)` | Sem *polling*, reação imediata à inserção/remoção. |
| Lançamento Steam | `Process.Start("steam://run/<APPID>")` | Delega em Steam a validação/atualização do jogo. |
| Instalador | Inno Setup | Leve, scriptável, suporta tarefas opcionais (arranque automático). |
| Persistência de config | `appsettings.json` + Registo do Windows (para o toggle de arranque) | Simples, sem dependência de base de dados. |

## Roadmap

Ver [ROADMAP.md](ROADMAP.md) para o plano de desenvolvimento faseado.

## Contribuir

Este é um projeto pessoal orientado a hobby/nostalgia. Sugestões e *pull requests* são bem-vindos — abre uma *issue* antes de propor mudanças estruturais grandes.

## Licença

Distribuído sob licença MIT — ver [LICENSE.md](LICENSE.md).
