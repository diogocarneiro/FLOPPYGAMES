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
- [x] Pipeline de release (GitHub Actions, `.github/workflows/release.yml`): cada push para o
  `main` corre os testes, compila o instalador e publica-o numa GitHub Release com a versão que
  estiver em `<Version>` de `Directory.Build.props` nesse commit; uma tag `vX.Y.Z` enviada à mão
  publica exatamente essa versão em vez disso. A versão chega ao instalador e ao rodapé das apps
  (`build.ps1 -Version`) sem editar ficheiros à mão. Também corre à mão, deixando só o instalador
  como artefacto. Validado localmente com os mesmos comandos do workflow (testes em Release,
  `build.ps1 -Version 9.9.9` → instalador e apps a 9.9.9) — o primeiro run automático no GitHub só
  acontece com o próximo push para o `main`. Ao preparar o pipeline descobriu-se que o
  `proxmark3.exe` incluído dependia de 9 DLLs do MSYS2 que só existiam no PC de desenvolvimento
  (fora dele terminava com `0xC0000135`, "DLL not found" — o suporte Proxmark3 falhava em qualquer
  outra instalação); passaram a ser incluídos ao lado dele e no instalador, com as licenças em
  `tools/proxmark3/NOTICE.md`.
- [x] Versão automática por commit (`.githooks/pre-commit`): sobe o *patch* de `<Version>` em
  `Directory.Build.props` a cada `git commit` local e inclui essa alteração no próprio commit — a
  release do GitHub Actions acima publica sempre essa versão, sem passo manual nenhum entre um
  commit normal e uma release. Ativa-se uma vez por clone com `git config core.hooksPath
  .githooks` (documentado no [README.md](README.md#desenvolvimento-versão-automática)); sem esse
  passo, os commits continuam a funcionar normalmente, só sem subir a versão sozinhos. **Testado**
  num repositório Git isolado (não no próprio FloppyGames, para não sujar o histórico real): o
  primeiro commit já bumpou `0.1.0` → `0.1.1`, um segundo `0.1.1` → `0.1.2`, e `git show --stat`
  confirmou que `Directory.Build.props` fica sempre incluído no mesmo commit que o disparou.

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
- [x] Animação CRT/scanlines configurável na splash. O varrimento (já existente na splash
  widescreen) passou a deslizar continuamente (`TranslateTransform` animado sobre o `DrawingBrush`
  do padrão, loop sem costura) em vez de estático — deliberadamente lento e sem "flicker" rápido,
  por acessibilidade (fotossensibilidade). Toggle em Definições (`AgentSettings.CrtEffectEnabled`,
  default ligado).
- [x] Catálogo partilhável de `GAME.INI` + capas, para não obrigar cada utilizador a recriar o
  mapeamento AppID → capa. Ficheiro local versionado no repositório (`catalog/catalog.json`, como
  `samples/` — sem rede), com processo verificado + descrição traduzida nos 5 idiomas por jogo
  catalogado; o Label Studio pré-preenche automaticamente ao encontrar uma correspondência (por
  `SteamAppId`/`EpicItemId`/`GogGameId`). Suporta capas também (`catalog/covers/`), mas a pasta
  começa vazia: guardar arte comercial de jogos de terceiros no histórico do Git não é uma decisão
  para tomar sem o próprio utilizador escolher e rever as imagens — o mecanismo já lê e usa uma
  capa automaticamente assim que lá for colocada, mais útil ainda para Epic/GOG (sem CDN grátis).
  Seed com 2 entradas reais (CS2/Steam, INSIDE/Epic) usando dados já verificados nesta sessão.
- [x] Suporte a 5 idiomas (Francês por omissão, Inglês, Português, Espanhol, Italiano) em toda a
  app — Agent, Label Studio e instalador. Um único conjunto de recursos `.resx` partilhado em
  `Core/Localization/` (Francês neutro + 4 satélites, mecanismo standard do .NET), com
  `AgentSettings.Language` partilhado pelas duas apps (o Label Studio não tem seletor próprio, só
  lê o valor escolhido nas Definições do Agent). `GameLaunchFailedEventArgs.Reason` deixou de ser
  uma string em português e passou a `GameLaunchFailureReason` (enum) — o `Core` mantém-se
  agnóstico de idioma, quem traduz é a UI. Instalador: os 5 idiomas nativos do Inno Setup, Francês
  primeiro na lista (continua sem diálogo de escolha, para o `/configure` não mostrar janelas), com
  as mensagens do modo `/configure` movidas para `[CustomMessages]`.
- [x] Todas as janelas (Agent, splash, Definições, Label Studio, impressão de label) arrancam
  centradas no ecrã (`WindowStartupLocation="CenterScreen"`), incluindo as duas que antes usavam
  `CenterOwner` — mais previsível do que depender de onde a janela "dona" estava.
- [x] Ativar/desativar plataformas no Label Studio: novo `AgentSettings.SteamEnabled` (`true` por
  omissão) / `EpicEnabled` / `GogEnabled` (`false` por omissão), com 3 checkboxes nas Definições do
  Agent. O seletor de plataforma do Label Studio só mostra as ligadas (nunca fica vazio — se por
  acaso todas ficarem desligadas, mostra as 3 na mesma). Não afeta o Agent: continua a lançar
  qualquer `GAME.INI` já criado, mesmo de uma plataforma entretanto desligada nas Definições.
- [x] Suporte a outros lançadores além de Steam (Epic, GOG), via `PLATFORM=` no `GAME.INI`. Epic
  (biblioteca + lançamento) **verificado** contra dados reais de uma instalação existente
  (`%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item`, URI de lançamento documentado
  pela Epic). GOG **reescrito e verificado**: a primeira versão assumia entradas no Registo
  (`HKLM\...\GOG.com\Games`, documentado pela comunidade) — mas ao tentar validar em hardware real
  descobriu-se que o GOG Galaxy 2.0 não usa o Registo de todo, guardando tudo numa base de dados
  SQLite própria (`galaxy-2.0.db`). Reescrito para ler dessa base de dados (`Microsoft.Data.Sqlite`,
  só-leitura), com o esquema confirmado via `sqlite3` contra uma instalação real e em execução do
  GOG Galaxy, e testes automatizados cobrindo as consultas contra esse esquema real. **Confirmado
  depois com dois jogos GOG realmente instalados** (duas demos) — e foi isso que apanhou um bug: os
  dois tinham `Products.name` a NULL, e o scanner descartava-os, por isso a lista saía vazia. O
  título passou a vir de `GamePieces` (tipo `title`, o nome que o próprio Galaxy mostra), com
  `Products.name` e `LimitedDetails.title` como alternativas.
- [x] Capas automáticas também para Epic e GOG (antes só a Steam tinha), sem autenticação nem API
  key: Epic pela cache de catálogo local da Epic Games Launcher (`catcache.bin`, JSON em Base64,
  imagem `DieselGameBoxTall`), pedida já redimensionada ao CDN da Epic (a original chegava a 2.4 MB
  em PNG, mais do que cabe numa disquete — assim fica um JPEG de ~87 KB a 600×800); GOG pela base
  de dados do Galaxy (`GamePieces` tipo `originalImages`, `verticalCover`), pedida em `.jpg` em vez
  do `.webp` original, que o WPF só descodifica com a extensão WebP do Windows instalada. Usado pelo
  Label Studio e pela splash do Agent em lançamentos por cartão NFC. **Verificado** com todos os
  jogos instalados nesta máquina (4 Steam, 3 Epic, 2 GOG — todos com capa).
  Detalhe em [README.md](README.md#nota-técnica-suporte-multi-plataforma).
- [ ] Telemetria local opcional: histórico de jogos "inseridos", tempo de jogo por disquete (nostálgico "tempo de cartucho").
- [x] Verificação de atualizações a partir das releases do GitHub (`GitHubReleaseUpdateChecker`, em
  `Core/Updates/`): lê `GET /repos/diogocarneiro/FLOPPYGAMES/releases/latest` (API pública, sem
  autenticação — só funciona com o repositório público, ver nota abaixo), compara com a versão
  instalada (`AppInfo.Version`) e, se houver uma mais recente, descarrega o
  `FloppyGamesSetup.exe` anexado com progresso. O Agent **arranca minimizado à bandeja** (sem
  mostrar a janela principal — `ShutdownMode="OnExplicitShutdown"` permite ficar vivo com zero
  janelas) e verifica sozinho ao arrancar (opt-out em Definições, ligado por omissão, com 5s de
  atraso para não competir com o resto do arranque) — se encontrar uma versão nova, **instala-a
  sozinho**: descarrega o instalador e lança-o em modo silencioso (`/VERYSILENT
  /SUPPRESSMSGBOXES /NORESTART`, sem assistente visível), fecha o Agent de forma limpa para o
  instalador poder substituir o próprio executável em execução, e o instalador reabre o Agent
  sozinho no fim (minimizado, sem perturbar o utilizador). Falha só se não houver instalador
  anexado à release ou a transferência falhar — nesse caso fica só o aviso no menu da bandeja,
  que abre as Definições. O botão manual "Transferir e instalar" nas Definições usa exatamente o
  mesmo caminho silencioso, para o comportamento ser sempre o mesmo, quer a atualização seja
  automática ou pedida à mão. **Verificado ao vivo** contra a API real do GitHub (comparação de
  versões e download completo de ~103 MB com progresso corretos) e a UI de Definições testada com
  o código de produção real (instanciado fora do Agent, sem tocar no leitor NFC) nos dois
  estados — "já está atualizado" e "versão nova disponível". O clique final em "Instalar" (que
  dispararia mesmo o instalador silencioso e fecharia o Agent) não foi acionado de propósito.
  - **Pré-requisito descoberto ao construir isto**: a API `releases/latest` devolve 404 para um
    repositório privado, mesmo pedindo a própria release — não há forma de o Agent verificar
    atualizações sem autenticação (que não se pode embutir com segurança numa app distribuída) a
    não ser tornando o repositório público. Feito.
- [x] Suporte a cartões NFC/RFID (Mifare Classic 1K/4K) como gatilho alternativo à disquete/pen,
  com **dois backends** compostos automaticamente (`CompositeNfcBackend`, em `Core/Nfc/`): um
  leitor PC/SC genérico (ex. ACR122U) e um **Proxmark3**. O Label Studio deteta o(s) leitor(es)
  ligado(s) e grava o GAME.INI no cartão; o Agent mostra o UID na splash como "CARD ID" ao lançar
  a partir de um cartão.
  - **Backend PC/SC**: construído a partir de documentação pública do protocolo (pacote NuGet
    `PCSC`, comandos pseudo-APDU `FF 82/86/B0/D6` populares nos leitores ACR) — compila contra a
    API real do pacote instalado, mas **continua por verificar contra hardware real**: não houve
    nenhum leitor PC/SC genuíno disponível nesta sessão.
  - **Backend Proxmark3**: **verificado de ponta a ponta contra hardware real** — um Proxmark3
    RDV4 com firmware Iceman, ligado por USB, e um cartão Mifare Classic 1K Gen1a real. Deteção do
    leitor via WMI (VID USB `9AC4`, registado ao projeto Proxmark3 em pid.codes), leitura/escrita
    de blocos através do cliente oficial `proxmark3.exe` (compilado a partir do código-fonte do
    fork Iceman via MSYS2 UCRT64, GPL-2.0, corrido sempre como processo externo — nunca ligado ao
    código do FloppyGames, ver `tools/proxmark3/NOTICE.md`). O comando `-c "hf 14a info"` deu UID/
    SAK/tipo de cartão corretos; `hf mf rdbl`/`hf mf wrbl` leram e escreveram blocos com sucesso;
    e o fluxo completo `NfcCardConfigWriter.Write` → `NfcCardConfigReader.Read` reconstruiu um
    `GameConfig` real (título, AppID, processo, descrição) byte a byte a partir do cartão físico.
    Foi preciso escolher a tag `v4.21611` do repositório (não a `master`), porque o cliente recusa
    falar com firmware cujo `CAPABILITIES_VERSION` não corresponda ao seu — a versão certa
    encontrou-se comparando com a versão reportada pelo próprio dispositivo.
  - Arquitetura: pipeline paralela em `Core/Nfc/` + `Core/Launch/NfcCardSessionManager`, composta
    lado a lado com a pipeline de disquete/USB existente (não reaproveita `driveRoot` como chave —
    um cartão não tem sistema de ficheiros nem espaço para capa). Formato de gravação: reaproveita
    o texto GAME.INI já existente (`GameIniWriter`/`GameIniParser`), fatiado em blocos de 16 bytes
    com um prefixo de comprimento — sem inventar um formato binário novo. Capacidade útil: 750
    bytes num Mifare 1K (752 brutos − prefixo), ~3438 bytes num 4K, excluindo sempre o bloco de
    fabrico (UID) e os blocos trailer (chaves/bits de acesso) de cada setor. Autenticação com a
    chave de fábrica (`FFFFFFFFFFFF`) e, se configurada, a chave derivada da password de proteção
    (ver abaixo) — nunca re-chaveia um setor fora do fluxo explícito de proteção. Capacidade
    opcional e desligada por omissão (`AgentSettings.NfcEnabled`), para máquinas sem leitor nunca
    tocarem em PC/SC/Proxmark3.
  - **Gravação apaga sempre o cartão inteiro**: `NfcCardConfigWriter.Write`/`WriteMagic` percorrem
    sempre todos os blocos utilizáveis do cartão (não só os que o `GAME.INI` atual precisa) — os
    primeiros levam o conteúdo real, o resto é limpo a zeros. Evita que um jogo gravado com uma
    descrição longa deixe bytes residuais visíveis ao inspecionar o cartão diretamente, depois de
    uma gravação seguinte com um `GAME.INI` mais curto por cima. **Verificado ao vivo**: gravar uma
    config longa (39 blocos), depois uma curta (10 blocos) por cima, e confirmar byte a byte que os
    29 blocos residuais ficaram a zero. Contrapartida aceite: uma gravação num Mifare 1K passa a
    demorar sempre o tempo de escrever os 47 blocos (~3 min com o Proxmark3, incluindo re-tentativas
    de RF) em vez de só os poucos blocos que o conteúdo real ocupa.
  - **Proteção por password (opt-in por cartão)**: depois de uma gravação normal ter sucesso, uma
    checkbox no Passo 4 do Label Studio ("Proteger este cartão com a palavra-passe configurada")
    permite trocar a chave de fábrica pela derivada de uma password guardada nas Definições
    (`AgentSettings.NfcCardPassword`, partilhada entre Label Studio e Agent — só assim o Agent
    consegue voltar a ler um cartão protegido mais tarde). `NfcCardConfigWriter.ProtectWithPassword`
    reescreve o trailer de **todos** os setores utilizáveis (não só os do jogo atual, para
    acompanhar o âmbito de "apaga sempre o cartão inteiro" acima), mantendo os bits de acesso e o
    byte de utilizador de fábrica (`FF 07 80 69`) — só a chave muda, para o cartão continuar
    reescrevível por quem souber a password. Chave derivada por SHA-256 truncado a 6 bytes
    (`NfcCardPasswordKey`). Ação explícita, nunca automática, com confirmação antes de reescrever
    qualquer trailer (bits de acesso errados podem bloquear um setor permanentemente num cartão
    genuíno). **Verificado ao vivo** contra o cartão Gen1a real: gravar → proteger → confirmar que a
    chave de fábrica deixa de autenticar → confirmar que a chave derivada da password continua a
    ler o conteúdo corretamente.
  - **Dois bugs reais encontrados e corrigidos ao verificar a proteção por password ao vivo**, sem
    relação com a funcionalidade em si — ambos existiam desde a funcionalidade anterior de mostrar
    progresso da gravação bloco a bloco:
    - O Label Studio fechava-se sozinho (`0xe0434352`, sem entrada nenhuma no log) sempre que uma
      gravação NFC começava. Causa: `CreateNfcWriteProgress()` era chamado dentro do delegado
      passado a `Task.Run(...)`, por isso o `Progress<T>` construído aí nunca capturava o
      `SynchronizationContext` da thread de UI — `Task.Run` corre sempre numa thread do ThreadPool
      sem esse contexto ambiente. Cada `Report()` acabava por ser despachado para uma thread aleatória
      do ThreadPool, e a primeira tentativa de tocar num controlo WPF a partir daí lançava uma
      exceção verdadeiramente não tratada (sem try/catch por perto), a derrubar o processo de
      imediato. Corrigido construindo o `Progress<T>` na thread de UI, antes de entrar em `Task.Run`.
    - `PollingNfcCardWatcher` usava um `lock` simples à volta de cada ciclo de sondagem — mas o
      `Timer` interno dispara um novo ciclo a cada segundo de qualquer forma, mesmo que o anterior
      ainda esteja bloqueado à espera do lock exclusivo por porta COM partilhado com uma gravação
      deliberada. Cada ciclo bloqueado ficava a consumir uma nova thread do ThreadPool — observado a
      chegar a 135 threads em poucos minutos, um caminho direto para esgotar o ThreadPool e derrubar
      o processo. Corrigido com `Lock.TryEnter()`: um ciclo sobreposto agora salta de imediato em vez
      de bloquear, e tenta outra vez um segundo depois.
    Ambos verificados ao vivo: antes da correção, a gravação falhava consistentemente ao primeiro
    bloco; depois, várias gravações seguidas (incluindo gravações completas de 47 blocos) correram
    sem falhas, com a contagem de threads estável.
  - **Deteção NFC lenta a abrir a splash, corrigida com invocações em lote**: um utilizador reportou
    que aproximar um cartão do Agent demorava "imenso tempo" a abrir a splash. Medido ao vivo: ~2.3s
    para UM único comando `hf mf rdbl` (o cliente Proxmark3 é invocado como processo externo por
    comando — o custo dominante é arrancar o processo e ligar por USB-CDC, não a transação RF em
    si), e o fluxo de leitura fazia um comando por bloco. Confirmado que o cliente aceita vários
    comandos separados por `;` numa só invocação (`hf mf rdbl ...; hf mf rdbl ...; ...`) — 3 blocos
    combinados: ~1.2s, MENOS do que um único bloco isolado. `IMifareCardGateway` ganhou
    `ReadBlocks`/`WriteBlocks`/`TryMagicWriteBlocks`/`AuthenticateSectors` como métodos de interface
    com implementação por omissão (loop sobre os métodos individuais — PC/SC e o `Fake` de testes
    não precisaram de nenhuma alteração), e `Pm3MifareCardGateway` passou a ter overrides reais que
    agrupam até 10 blocos por invocação (repetindo o lote inteiro, não bloco a bloco, se algum falhar
    — RF flutuante já não era distinguível de chave errada na versão anterior, por isso não é um
    novo risco). `NfcCardConfigReader`/`NfcCardConfigWriter` foram reescritos para autenticar todos
    os setores em lote (por chave candidata) e ler/escrever em grupos de 10 blocos, continuando a
    reportar progresso após cada grupo (não só no fim) para a UI não parecer parada. **Verificado ao
    vivo**: uma gravação completa de 47 blocos passou de ~180s para ~12.6s (~14×); a leitura que o
    Agent faz ao reconhecer um cartão passou de 16-33s para ~6.8s (~3-4×), confirmado com o Agent
    real a abrir a splash muito mais depressa.
  - **Cartões protegidos por password continuavam lentos (~25s no Agent)**: cada chave errada era
    repetida 3× por setor (o código não distinguia "chave errada" de falha de RF), e a chave de
    fábrica era sempre tentada primeiro em todos os setores. Verificado ao vivo que o cliente
    Proxmark3 responde `[#] Auth error` de imediato a uma chave errada, e que num lote encadeado
    esse erro não interrompe os comandos seguintes. Agora uma rejeição explícita não é repetida (só
    a ausência de resposta é), os restantes setores começam pela chave que abriu o setor 0 e são
    autenticados num só lote, e o leitor lembra-se da chave que funcionou em cada cartão (por UID)
    para a leitura seguinte. **Verificado ao vivo** no cartão protegido: leitura de ~25s para 7.1s
    (primeira vez) e 6.2s (vezes seguintes).
  - **Checklist de validação manual ainda por fazer** (para o backend PC/SC, e para o Proxmark3 em
    cenários fora do já testado):
    - [ ] Leitor PC/SC genuíno (ex. ACR122U) ligado → confirmar deteção e leitura/escrita.
    - [ ] Cartão Mifare Classic **4K** (só 1K foi testado).
    - [ ] Cartão previamente usado noutro sistema (chaves não-standard) → confirmar erro de autenticação claro, sem exceção nem escrita parcial.
    - [ ] Etiqueta não-Mifare-Classic (ex. NTAG) → confirmar "tipo de cartão não suportado", sem crash nem leitura incorreta.
    - [ ] Desligar o leitor a meio de uma sessão do Agent (com `NfcEnabled` ativo) → confirmar aviso no log, disquete/USB continuam a funcionar.
    - [x] Fluxo completo pela UI do Label Studio: confirmado via Windows UI Automation contra o
      cartão real — selecionar jogo, mudar para alvo "Cartão NFC", clicar "Escrever para o
      suporte", ver o progresso bloco a bloco e a gravação a terminar com sucesso, várias vezes
      seguidas sem falhas.
    - [x] Fluxo completo pelo Agent: confirmado via Windows UI Automation + captura de ecrã com o
      Agent real e o cartão Gen1a no Proxmark3 — cartão gravado com Portal (AppID 400), splash
      aberta com título "Portal", ícone NFC + "CARTÃO NFC DETETADO", "CARD ID DA466B03", a descrição
      gravada no cartão e a capa descarregada do CDN da Steam. O lançamento em si expirou após o
      timeout de 30s (o Portal não arrancou pela Steam nesse momento — problema do lado da Steam,
      não do NFC), e o Agent reportou esse timeout corretamente.

---

## Princípios de Engenharia a Manter em Todas as Fases

- **Sem *polling* agressivo** — usar sempre notificações do sistema (`WMI`) em vez de *loops* a verificar unidades. Única exceção deliberada e documentada: `PollingFloppyDriveWatcher`, confinado a 1-2 letras de unidade candidatas a disquete, porque não há alternativa fiável no Windows para detetar troca de disco numa drive já montada.
- **Falhas silenciosas nunca** — qualquer erro no fluxo (AppID inválido, Steam não instalado, timeout) tem de ser visível na bandeja/logs, nunca engolido.
- **Zero-admin por defeito** — toggle de arranque automático e configuração vivem em `HKCU`, não exigem elevação.
- **Idempotência do instalador** — instalar/desinstalar/reinstalar repetidamente nunca deve deixar entradas órfãs no Registo.
- **Núcleo (`Core`) sem dependências de UI** — para ser testável e reutilizável entre Agent e Label Studio.
