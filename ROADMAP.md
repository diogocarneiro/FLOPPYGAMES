# Roadmap — FloppyGames

Plano de desenvolvimento faseado. Cada fase produz algo executável e testável — nada de "big bang". As fases são sequenciais, mas dentro de cada uma as tarefas podem avançar em paralelo.

## Fase 0 — Fundação do Projeto ✅

- [x] Criar solução .NET (`FloppyGames.slnx`) com os projetos `Core`, `Agent`, `LabelStudio`, `Installer`.
- [x] Definir `.editorconfig`, `nullable enable`, análise estática (`Directory.Build.props`, analisadores).
- [x] Configurar `.gitignore` para artefactos .NET/Windows.
- [x] Definir o esquema formal do `GAME.INI` (documentado no README) e escrever o *parser* + testes unitários.
- [x] Definir estrutura de logging (Serilog, ficheiro rotativo em `%LOCALAPPDATA%\FloppyGames\logs`).

**Critério de saída:** `FloppyGames.Core` compila, com parser de `GAME.INI` testado (casos válidos, inválidos, campos opcionais em falta).

## Fase 1 — Agent: Deteção de Mídia

- [ ] Implementar `IRemovableMediaWatcher` com `RegisterDeviceNotification` / `WM_DEVICECHANGE` para arrival/removal de volumes.
- [ ] Filtrar apenas unidades amovíveis (`DRIVE_REMOVABLE`), ignorar discos fixos e óticos.
- [ ] Validar presença de `GAME.INI` na raiz do volume recém-inserido.
- [ ] Emitir eventos internos `MediaInserted(GameConfig)` / `MediaRemoved(driveLetter)`.
- [ ] Testes de integração manuais com pen USB (real floppy fica para Fase 6).

**Critério de saída:** inserir/remover uma pen USB com `GAME.INI` gera logs corretos no Agent, sem *polling*.

## Fase 2 — Agent: Lançamento e Vigilância

- [ ] Janela de splash (WPF, sem *chrome*, sempre no topo) com capa + barra de progresso animada estilo retro.
- [ ] Implementar `LaunchDelaySeconds` antes de disparar `steam://run/<APPID>`.
- [ ] Implementar `ProcessWatcher`: sondagem do `PROCESS` configurado, respeitando `WatchTimeoutSeconds`.
- [ ] Fechar a splash quando o processo é confirmado; mostrar erro se o timeout expirar (jogo não instalado, Steam não autenticado, etc.).
- [ ] Implementar encerramento no *eject*: `GracefulShutdown` (`WM_CLOSE`) com fallback para `TerminateProcess` após timeout curto.
- [ ] Suportar múltiplos suportes em simultâneo (mapa `driveLetter → processo lançado`), sem interferência entre eles.

**Critério de saída:** fluxo ponta-a-ponta funcional — inserir disquete/pen → jogo abre → remover → jogo fecha.

## Fase 3 — Agent: Bandeja do Sistema e Configuração

- [ ] Ícone de bandeja com estado (*idle*, *a carregar*, *jogo em execução*).
- [ ] Menu de contexto: abrir logs, abrir Label Studio, sair, definições.
- [ ] Ecrã de definições: caminho da pasta de logs, tempo de *timeout*, som de motor de disquete on/off.
- [ ] Toggle "Iniciar com o Windows" (escreve/remove chave em `HKCU\...\Run`), refletido de imediato na bandeja.

**Critério de saída:** Agent utilizável sem consola/terminal aberta; toggle de arranque automático funcional sem reinstalar.

## Fase 4 — Label Studio (Criador de Disquetes)

- [ ] Ecrã de pesquisa de jogos da biblioteca Steam local (ler `libraryfolders.vdf` / Steam Web API opcional com *API key* do utilizador).
- [ ] Pré-visualização da capa (capa Steam *library/header* + *fallback* manual de imagem local).
- [ ] Geração do `GAME.INI` a partir da seleção (AppID, título, processo sugerido a partir do executável instalado).
- [ ] Escrita direta para o suporte amovível selecionado (validar espaço, avisar antes de sobrescrever).
- [ ] Módulo de desenho/impressão do label físico (molde 3.5", exportação PDF/PNG para impressão em autocolante).

**Critério de saída:** criar uma disquete/pen do zero, sem editar `GAME.INI` à mão, e imprimir o respetivo label.

## Fase 5 — Instalador e Distribuição

- [ ] Script Inno Setup: instala Agent + Label Studio + Core, cria atalhos no Menu Iniciar.
- [ ] Passo opcional no assistente: "Iniciar o FloppyGames Agent com o Windows".
- [ ] Suporte a `FloppyGamesSetup.exe /configure` para alternar o arranque automático pós-instalação.
- [ ] Desinstalação limpa: remove chave de arranque, atalhos, ficheiros; preserva `GAME.INI`/capas do utilizador em disquetes (óbvio, mas confirmar que não toca em suportes externos).
- [ ] Assinatura do executável (*code signing*), se aplicável, para evitar avisos do SmartScreen.
- [ ] Pipeline de release (build + empacotamento) — GitHub Actions.

**Critério de saída:** instalar, usar, alternar arranque automático e desinstalar sem deixar resíduos, tudo via UI.

## Fase 6 — Polimento e Extras (Stretch Goals)

- [ ] Suporte a leitores de disquete físicos reais (3.5", via drive USB legado) como alternativa às pens.
- [ ] Som de motor de disquete a tocar durante a animação de loading (efeito opcional).
- [ ] Animação CRT/scanlines configurável na splash.
- [ ] Catálogo partilhável de `GAME.INI` + capas (comunidade), para não obrigar cada utilizador a recriar o mapeamento AppID → capa.
- [ ] Suporte a outros lançadores além de Steam (Epic, GOG) via *deep links* próprios, mantendo `GAME.INI` genérico (`LAUNCHER=steam|epic|gog`).
- [ ] Telemetria local opcional: histórico de jogos "inseridos", tempo de jogo por disquete (nostálgico "tempo de cartucho").
- [ ] Suporte a etiquetas NFC/RFID coladas na disquete como gatilho alternativo à deteção de volume (mais fiável em pens genéricas).

---

## Princípios de Engenharia a Manter em Todas as Fases

- **Sem *polling* agressivo** — usar sempre notificações do sistema (`WM_DEVICECHANGE`, `WMI`) em vez de *loops* a verificar unidades.
- **Falhas silenciosas nunca** — qualquer erro no fluxo (AppID inválido, Steam não instalado, timeout) tem de ser visível na bandeja/logs, nunca engolido.
- **Zero-admin por defeito** — toggle de arranque automático e configuração vivem em `HKCU`, não exigem elevação.
- **Idempotência do instalador** — instalar/desinstalar/reinstalar repetidamente nunca deve deixar entradas órfãs no Registo.
- **Núcleo (`Core`) sem dependências de UI** — para ser testável e reutilizável entre Agent e Label Studio.
