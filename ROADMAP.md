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

## Fase 3 — Agent: Bandeja do Sistema e Configuração ✅

- [x] Ícone de bandeja com estado (*idle* cinza, *a carregar* âmbar, *jogo em execução* verde) — `TrayIconController` + `TrayIconFactory`, orientado a eventos do `GameSessionManager` (sem polling), suporta múltiplos jogos em simultâneo (o ícone mostra "a carregar" se houver pelo menos um lançamento em curso, "em execução" se houver pelo menos uma sessão ativa).
- [x] Menu de contexto: abrir FloppyGames, abrir pasta de logs, abrir Label Studio, definições, sair.
- [x] Fechar a janela principal já não termina o Agent — esconde para a bandeja (`ShutdownMode=OnExplicitShutdown`); só "Sair" no menu da bandeja encerra de facto (liberta o watcher de mídia e a sessão de jogo).
- [x] Toggle "Iniciar com o Windows" — `AutostartManager` + `WindowsAutostartRegistry`, escreve/remove `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, sem exigir admin, refletido de imediato no ecrã de Definições.
- [x] Ecrã de Definições: checkbox de arranque automático + caminho/atalho para a pasta de logs.

**Decisão consciente:** os itens "tempo de *timeout*" e "som de motor de disquete" do plano original ficaram de fora do ecrã de Definições — o timeout já é configurável por jogo via `WatchTimeoutSeconds` no `GAME.INI` (um valor global duplicaria essa configuração sem necessidade), e o som do motor ainda não existe como funcionalidade (só chega na Fase 6). Um controlo na UI sem comportamento real por trás seria um ecrã meio-feito; fica para quando a funcionalidade existir.

**Verificação:** 48/48 testes automatizados (5 novos cobrem `AutostartManager`: ativar, desativar, estado sem valor, desativar sem nunca ter ativado, não interferir com outras entradas do Run). Confirmado por linha de comandos que o Agent arranca e corre sem exceções com o novo código da bandeja; o fluxo de lançamento por trás (que o ícone reflete) já foi validado com hardware real na Fase 2. **Falta confirmação visual** — abrir o Agent numa sessão normal e ver o ícone a aparecer/mudar de cor, o menu de contexto, a janela a esconder-se ao fechar, e "Sair" a encerrar tudo — algo que não consigo verificar sem ecrã.

**Critério de saída:** Agent utilizável sem consola/terminal aberta; toggle de arranque automático funcional sem reinstalar.

## Fase 4 — Label Studio (Criador de Disquetes) ✅

- [x] Ecrã de pesquisa de jogos da biblioteca Steam local — `SteamLibraryScanner` lê `libraryfolders.vdf` (todas as bibliotecas, não só a principal) e cada `appmanifest_*.acf` via um parser VDF (Valve KeyValues) escrito de raiz (`VdfParser`); caixa de pesquisa filtra por nome. API Web da Steam ficou de fora — a leitura local já dá tudo o que é preciso (AppID, título, pasta de instalação) sem exigir *API key* do utilizador.
- [x] Sugestão do executável a vigiar — `GameExecutableFinder` + `ExecutableSuggester` (heurística pura, testável sem tocar em disco): ignora instaladores/redistribuíveis conhecidos, prefere o nome que corresponde à pasta de instalação. Utilizador confirma/corrige antes de escrever.
- [x] Pré-visualização da capa — `SteamCdnCoverArtProvider` descarrega a arte vertical (`library_600x900`) do CDN público da Steam, sem autenticação; *fallback* de escolha manual de imagem local.
- [x] Geração do `GAME.INI` a partir da seleção — `GameIniWriter` (serialização inversa do `GameIniParser`, com teste de *round-trip*), incluindo secção de opções avançadas (timeout, atraso, encerramento suave) editável na UI.
- [x] Escrita direta para o suporte amovível selecionado — `FloppyMediaWriter`: valida que é amovível, calcula espaço necessário vs. disponível, avisa antes de sobrescrever um GAME.INI já existente.
- [x] Módulo de desenho/impressão do label físico — `LabelPrintWindow`: pré-visualização quadrada (capa + título), impressão direta via `PrintDialog`/`PrintVisual`, exportação para PNG via `RenderTargetBitmap`. Exportação para PDF ficou de fora — exigiria uma biblioteca de terceiros (WPF não gera PDF nativamente) só para esse formato; impressão direta e PNG já cobrem o caso de uso real (imprimir a etiqueta).

**Verificação:** 73/73 testes automatizados (25 novos cobrem `VdfParser`, `SteamLibraryScanner`, `ExecutableSuggester`, `GameExecutableFinder`, `GameIniWriter` e `FloppyMediaWriter`, todos com dublês de teste — nada toca em disco real ou Steam real durante os testes). Confirmado com um script de verificação contra a instalação Steam real desta máquina: encontrou corretamente 3 jogos instalados, incluindo o Counter-Strike 2 (AppID 730) com `cs2.exe` sugerido como processo — o mesmo valor já usado manualmente ao longo deste projeto.

**Confirmação visual da UI:** feita via screenshot + Windows UI Automation (sem tocar no botão "Escrever para o suporte", para não sobrescrever o suporte real). Selecionar "Counter-Strike 2" na lista preencheu Título/AppID/Processo corretamente e descarregou a capa real da Steam; "Opções avançadas" mostrou os defaults certos (30s/2s/suave); a janela "Desenhar / imprimir label..." mostrou a capa com o título sobreposto tal como desenhado. Esta verificação também apanhou um bug real de acessibilidade — o nome exposto ao UI Automation (e a leitores de ecrã como o Narrator) era o `ToString()` completo do registo `InstalledSteamGame` em vez de só o nome do jogo, porque `DisplayMemberPath` só controla o texto visível, não `AutomationProperties.Name`. Corrigido com um `ItemContainerStyle` a vincular `AutomationProperties.Name` ao nome do jogo, e reverificado.

**Critério de saída:** criar uma disquete/pen do zero, sem editar `GAME.INI` à mão, e imprimir o respetivo label. ✅ Confirmado visualmente (exceto o clique final em "Escrever para o suporte", propositadamente evitado para não sobrescrever o suporte real do utilizador).

## Fase 5 — Instalador e Distribuição ✅ (exceto assinatura de código e CI)

- [x] Script Inno Setup (`src/FloppyGames.Installer/FloppyGames.iss`) + `build.ps1`: publica Agent e Label Studio *self-contained* (`win-x64`, sem exigir .NET à parte) e compila o instalador. Instala em `%LOCALAPPDATA%\Programs\FloppyGames` — sem privilégios de administrador — com atalhos no Menu Iniciar.
- [x] Passo opcional no assistente: "Iniciar o FloppyGames Agent com o Windows".
- [x] Suporte a `FloppyGamesSetup.exe /configure` para alternar o arranque automático pós-instalação, sem passar pelo assistente completo.
- [x] Desinstalação limpa: remove ficheiros, atalhos e a entrada de arranque automático (`CurUninstallStepChanged`, cobre qualquer origem da entrada — tarefa, `/configure`, ou Definições do Agent). Nunca toca em suportes amovíveis, e preserva deliberadamente os logs do utilizador em `%LOCALAPPDATA%\FloppyGames\logs`.
- [ ] Assinatura do executável (*code signing*) — exige um certificado adquirido; fora do alcance deste ambiente. Sem isto, o Windows SmartScreen vai avisar na primeira execução — aceitável para um projeto pessoal, mas a documentar para quem for distribuir mais largamente.
- [ ] Pipeline de release (GitHub Actions) — build + empacotamento automático a cada tag; ainda não configurado.

**Duas armadilhas reais do Inno Setup encontradas e corrigidas ao testar** (detalhadas em [src/FloppyGames.Installer/README.md](src/FloppyGames.Installer/README.md)):
- A constante `{app}` não está disponível dentro de `InitializeSetup` (só depois da página de escolha de pasta) — `/configure` precisa do caminho de instalação antes disso, por isso passou a lê-lo da própria chave de desinstalação que o Inno já escreve, em vez de `{app}`.
- O diálogo de escolha de idioma aparecia *antes* de `InitializeSetup` correr, o que fazia `/configure` mostrar sempre essa janela primeiro. Resolvido com `ShowLanguageDialog=no`.

**Verificação:** ciclo completo testado à mão nesta máquina — instalação silenciosa com a tarefa de arranque automático ligada (ficheiros, atalhos e chave de registo corretos), `/configure` nos dois sentidos (desativar quando ativo, ativar quando inativo, texto e registo corretos em ambos), e desinstalação silenciosa (ficheiros, atalhos e *ambas* as chaves de registo removidos; logs preservados). Instalador final: ~94 MB.

**Critério de saída:** instalar, usar, alternar arranque automático e desinstalar sem deixar resíduos, tudo via UI. ✅ Confirmado.

## Fase 6 — Polimento e Extras (Stretch Goals)

- [x] Som de motor de disquete a tocar durante a animação de loading (efeito opcional). Sintetizado
  localmente por script (hum de motor + cliques de posicionamento da cabeça, ~2.2s), sem depender
  de um asset licenciado de terceiros — `src/FloppyGames.Agent/Assets/floppy-motor.wav`. Só toca
  para disquetes físicas reais (não pens USB), com toggle em Definições.
- [ ] Animação CRT/scanlines configurável na splash.
- [ ] Catálogo partilhável de `GAME.INI` + capas (comunidade), para não obrigar cada utilizador a recriar o mapeamento AppID → capa.
- [x] Suporte a outros lançadores além de Steam (Epic, GOG), via `PLATFORM=` no `GAME.INI`. Epic
  (biblioteca + lançamento) **verificado** contra dados reais de uma instalação existente
  (`%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item`, URI de lançamento documentado
  pela Epic). GOG (lançamento direto do `.exe`, biblioteca via Registo
  `HKLM\...\GOG.com\Games`) implementado a partir do que é documentado pela comunidade, mas
  **não verificado em hardware real** — nenhuma máquina disponível tinha GOG Galaxy instalado.
  Detalhe em [README.md](README.md#nota-técnica-suporte-multi-plataforma).
- [ ] Telemetria local opcional: histórico de jogos "inseridos", tempo de jogo por disquete (nostálgico "tempo de cartucho").
- [ ] Suporte a etiquetas NFC/RFID coladas na disquete como gatilho alternativo à deteção de volume (mais fiável em pens genéricas).

---

## Princípios de Engenharia a Manter em Todas as Fases

- **Sem *polling* agressivo** — usar sempre notificações do sistema (`WMI`) em vez de *loops* a verificar unidades. Única exceção deliberada e documentada: `PollingFloppyDriveWatcher`, confinado a 1-2 letras de unidade candidatas a disquete, porque não há alternativa fiável no Windows para detetar troca de disco numa drive já montada.
- **Falhas silenciosas nunca** — qualquer erro no fluxo (AppID inválido, Steam não instalado, timeout) tem de ser visível na bandeja/logs, nunca engolido.
- **Zero-admin por defeito** — toggle de arranque automático e configuração vivem em `HKCU`, não exigem elevação.
- **Idempotência do instalador** — instalar/desinstalar/reinstalar repetidamente nunca deve deixar entradas órfãs no Registo.
- **Núcleo (`Core`) sem dependências de UI** — para ser testável e reutilizável entre Agent e Label Studio.
