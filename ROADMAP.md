# Roadmap — FloppyGames

Plano de desenvolvimento faseado. Cada fase produz algo executável e testável — nada de "big bang". As fases são sequenciais, mas dentro de cada uma as tarefas podem avançar em paralelo.

## Fase 0 — Fundação do Projeto ✅

- [x] Criar solução .NET (`FloppyGames.slnx`) com os projetos `Core`, `Agent`, `LabelStudio`, `Installer`.
- [x] Definir `.editorconfig`, `nullable enable`, análise estática (`Directory.Build.props`, analisadores).
- [x] Configurar `.gitignore` para artefactos .NET/Windows.
- [x] Definir o esquema formal do `GAME.INI` (documentado no README) e escrever o *parser* + testes unitários.
- [x] Definir estrutura de logging (Serilog, ficheiro rotativo em `%LOCALAPPDATA%\FloppyGames\logs`).

**Critério de saída:** `FloppyGames.Core` compila, com parser de `GAME.INI` testado (casos válidos, inválidos, campos opcionais em falta).

## Fase 1 — Agent: Deteção de Mídia ✅

**Disquetes reais são o suporte principal do projeto; pens USB são um extra.** Isto exigiu uma decisão de arquitetura: o Windows deteta por evento quando uma pen aparece/desaparece, mas **não** deteta de forma fiável a troca de disco dentro de uma drive de disquetes já ligada (a letra de unidade mantém-se atribuída, só o estado "pronta" muda). Ver [nota técnica no README](README.md#nota-técnica-deteção-de-disquetes).

- [x] Implementar `IRemovableMediaWatcher` para pens USB — decisão: WMI (`Win32_VolumeChangeEvent`) em vez de `RegisterDeviceNotification`/`WM_DEVICECHANGE`, por não exigir um `HWND`/message loop, o que mantém o `Core` livre de dependências de UI e testável.
- [x] Implementar `PollingFloppyDriveWatcher` para disquetes reais — sondagem leve (~1.5s) de `DriveInfo.IsReady` nas letras candidatas (`A:\`, `B:\` por omissão), única exceção deliberada ao princípio "sem polling", por ser a única forma fiável de detetar a troca de disco numa drive já montada.
- [x] `CompositeRemovableMediaWatcher` funde as duas fontes num único `IRemovableMediaWatcher`, para o resto do sistema não distinguir a origem.
- [x] Filtrar apenas unidades amovíveis (`DriveType.Removable`), ignorar discos fixos e óticos — em `FileSystemDriveInspector` + `GameMediaScanner`.
- [x] Validar presença de `GAME.INI` na raiz do volume recém-inserido, com retry de leitura (disquetes reais têm latência mecânica de arranque do motor).
- [x] Emitir eventos internos `MediaInserted` / `MediaRemoved` / `InvalidMediaDetected` via `RemovableGameMediaService`, com o `GameConfig` sempre disponível (também no evento de remoção, para o Agent saber que processo terminar sem estado próprio) e deduplicação/lock contra deteções concorrentes das duas fontes.
- [x] Testado manualmente com hardware real — pen USB e disquete/drive real, ambas reconhecidas corretamente nos logs.

**Como testar manualmente:** correr `dotnet run --project src/FloppyGames.Agent`, inserir um suporte com um `GAME.INI` válido na raiz — a janela e o ficheiro `%LOCALAPPDATA%\FloppyGames\logs\Agent-*.log` devem mostrar "Disquete reconhecida"; ao remover, deve aparecer "Disquete removida".

**Critério de saída:** inserir/remover uma disquete real (ou pen USB) com `GAME.INI` gera logs corretos no Agent, sem eventos duplicados. ✅ Verificado.

## Fase 2 — Agent: Lançamento e Vigilância ✅ (pendente confirmação end-to-end com hardware real)

- [x] Janela de splash (WPF, sem *chrome*, sempre no topo) com capa + barra de progresso; fecha-se sozinha ao chegar a um estado final (`SplashWindow`).
- [x] Implementar `LaunchDelaySeconds` antes de disparar `steam://run/<APPID>` (`GameSessionManager`).
- [x] Implementar espera pelo processo: `IProcessGateway`/`Win32ProcessGateway` sondam por `PROCESS` até `WatchTimeoutSeconds`; uma vez encontrado, a deteção de saída passa a ser orientada a eventos (`Process.Exited`), sem mais polling.
- [x] Fechar a splash quando o processo é confirmado; mostrar erro (com auto-close) se o timeout expirar.
- [x] Implementar encerramento no *eject*: `GracefulShutdown` (`CloseMainWindow` + espera) com fallback para `Kill(entireProcessTree: true)`.
- [x] Suportar múltiplos suportes em simultâneo — `GameSessionManager` rastreia sessões e lançamentos pendentes por `driveRoot`, sem interferência entre eles.
- [x] Extra: cancelamento de lançamentos em curso se o suporte for removido antes de o processo ser confirmado (evita "jogos fantasma" lançados depois de a disquete já ter saído).

**Verificação:** 43/43 testes automatizados (incluindo fluxos assíncronos completos: lançamento, timeout, cancelamento a meio do atraso, encerramento gracioso/forçado, saída espontânea do processo). Confirmado com WMI real que `A:\` é reportada como disquete genuína (`MediaType` floppy) e que a Steam está instalada na máquina de desenvolvimento — falta apenas o teste manual com uma disquete real a lançar um jogo de facto.

**Como testar manualmente:** correr `dotnet run --project src/FloppyGames.Agent`, inserir uma disquete/pen com o `GAME.INI` do CS2 ([samples/CS2/GAME.INI](samples/CS2/GAME.INI)) — deve aparecer uma splash com o título, a Steam deve abrir o CS2 ao fim de ~2s, a splash fecha quando o `cs2.exe` for confirmado. Remover a disquete deve fechar o jogo.

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

- [ ] Som de motor de disquete a tocar durante a animação de loading (efeito opcional).
- [ ] Animação CRT/scanlines configurável na splash.
- [ ] Catálogo partilhável de `GAME.INI` + capas (comunidade), para não obrigar cada utilizador a recriar o mapeamento AppID → capa.
- [ ] Suporte a outros lançadores além de Steam (Epic, GOG) via *deep links* próprios, mantendo `GAME.INI` genérico (`LAUNCHER=steam|epic|gog`).
- [ ] Telemetria local opcional: histórico de jogos "inseridos", tempo de jogo por disquete (nostálgico "tempo de cartucho").
- [ ] Suporte a etiquetas NFC/RFID coladas na disquete como gatilho alternativo à deteção de volume (mais fiável em pens genéricas).

---

## Princípios de Engenharia a Manter em Todas as Fases

- **Sem *polling* agressivo** — usar sempre notificações do sistema (`WMI`) em vez de *loops* a verificar unidades. Única exceção deliberada e documentada: `PollingFloppyDriveWatcher`, confinado a 1-2 letras de unidade candidatas a disquete, porque não há alternativa fiável no Windows para detetar troca de disco numa drive já montada.
- **Falhas silenciosas nunca** — qualquer erro no fluxo (AppID inválido, Steam não instalado, timeout) tem de ser visível na bandeja/logs, nunca engolido.
- **Zero-admin por defeito** — toggle de arranque automático e configuração vivem em `HKCU`, não exigem elevação.
- **Idempotência do instalador** — instalar/desinstalar/reinstalar repetidamente nunca deve deixar entradas órfãs no Registo.
- **Núcleo (`Core`) sem dependências de UI** — para ser testável e reutilizável entre Agent e Label Studio.
