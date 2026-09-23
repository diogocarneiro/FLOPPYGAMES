; Instalador do FloppyGames (Agent + Label Studio).
; Compilar com build.ps1 (publica as apps primeiro) ou diretamente com ISCC, desde que
; publish\Agent e publish\LabelStudio já existam nesta pasta.

#define MyAppName "FloppyGames"
; Manter igual a <Version> em Directory.Build.props (a versão mostrada no rodapé das apps). O
; build.ps1 -Version (usado pelo pipeline de release) passa-a com /DMyAppVersion=..., e aí tem
; prioridade sobre este valor.
#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#define MyAppPublisher "Diogo Carneiro"
#define MyAppURL "https://www.diogocarneiro.fr"
#define MyAppCopyright "© 2026 Diogo Carneiro. Tous droits réservés."
#define AgentExeName "FloppyGames.Agent.exe"
#define LabelStudioExeName "FloppyGames.LabelStudio.exe"

[Setup]
AppId={{BC2C9071-1DB5-4978-A7DB-4B3CA3E79F66}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
; Sem isto, o nome em "Programas e Funcionalidades" seria "FloppyGames versão 0.1.0" (AppVerName
; por omissão) — a versão já aparece na sua própria coluna.
UninstallDisplayName={#MyAppName}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright={#MyAppCopyright}
AppComments=Relie une disquette physique 3.5" à ta bibliothèque Steam/Epic/GOG : insère le support, le jeu démarre ; retire-le, le jeu se ferme.
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
SetupIconFile=..\FloppyGames.Agent\Assets\AppIcon.ico
; Instala em {localappdata}, não em Program Files — não exige privilégios de administrador,
; consistente com o princípio de zero-admin do resto do projeto (ver ROADMAP.md).
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=FloppyGamesSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Agent\{#AgentExeName}
; Sem diálogo de escolha de idioma: além de ser um passo a menos para o utilizador, o diálogo
; aparece ANTES de InitializeSetup correr, o que faria o modo /configure mostrar sempre essa
; janela antes de conseguir agir. Como fica sempre silencioso, o idioma por omissão é
; simplesmente o primeiro da lista [Languages] abaixo (Francês); -LANG=xx continua disponível
; a quem quiser um dos outros 4 explicitamente.
ShowLanguageDialog=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[CustomMessages]
french.AutostartTaskDescription=Démarrer FloppyGames Agent avec Windows
english.AutostartTaskDescription=Start FloppyGames Agent with Windows
portuguese.AutostartTaskDescription=Iniciar o FloppyGames Agent com o Windows
spanish.AutostartTaskDescription=Iniciar FloppyGames Agent con Windows
italian.AutostartTaskDescription=Avvia FloppyGames Agent con Windows

french.AdditionalOptionsGroup=Options supplémentaires :
english.AdditionalOptionsGroup=Additional options:
portuguese.AdditionalOptionsGroup=Opções adicionais:
spanish.AdditionalOptionsGroup=Opciones adicionales:
italian.AdditionalOptionsGroup=Opzioni aggiuntive:

french.RunAfterInstallDescription=Démarrer FloppyGames Agent maintenant
english.RunAfterInstallDescription=Start FloppyGames Agent now
portuguese.RunAfterInstallDescription=Iniciar o FloppyGames Agent agora
spanish.RunAfterInstallDescription=Iniciar FloppyGames Agent ahora
italian.RunAfterInstallDescription=Avvia FloppyGames Agent ora

french.ConfigureNotInstalled=FloppyGames n'est pas encore installé. Lance d'abord l'installateur normalement.
english.ConfigureNotInstalled=FloppyGames isn't installed yet. Run the installer normally first.
portuguese.ConfigureNotInstalled=O FloppyGames ainda não está instalado. Corre o instalador normalmente primeiro.
spanish.ConfigureNotInstalled=FloppyGames aún no está instalado. Ejecuta primero el instalador normalmente.
italian.ConfigureNotInstalled=FloppyGames non è ancora installato. Esegui prima il programma di installazione normalmente.

french.ConfigureExeMissingFormat=FloppyGames semble installé, mais %1 est introuvable.
english.ConfigureExeMissingFormat=FloppyGames appears to be installed, but %1 wasn't found.
portuguese.ConfigureExeMissingFormat=O FloppyGames parece estar instalado, mas não encontrei %1.
spanish.ConfigureExeMissingFormat=FloppyGames parece estar instalado, pero no se encontró %1.
italian.ConfigureExeMissingFormat=FloppyGames sembra installato, ma non è stato trovato %1.

french.ConfigureAutostartActiveAsk=Le démarrage automatique de FloppyGames Agent est ACTIVÉ.%nVeux-tu le désactiver ?
english.ConfigureAutostartActiveAsk=FloppyGames Agent autostart is ON.%nDo you want to turn it off?
portuguese.ConfigureAutostartActiveAsk=O arranque automático do FloppyGames Agent está ATIVO.%nQueres desativá-lo?
spanish.ConfigureAutostartActiveAsk=El inicio automático de FloppyGames Agent está ACTIVADO.%n¿Quieres desactivarlo?
italian.ConfigureAutostartActiveAsk=L'avvio automatico di FloppyGames Agent è ATTIVO.%nVuoi disattivarlo?

french.ConfigureAutostartDisabledMsg=Démarrage automatique désactivé.
english.ConfigureAutostartDisabledMsg=Autostart turned off.
portuguese.ConfigureAutostartDisabledMsg=Arranque automático desativado.
spanish.ConfigureAutostartDisabledMsg=Inicio automático desactivado.
italian.ConfigureAutostartDisabledMsg=Avvio automatico disattivato.

french.ConfigureAutostartInactiveAsk=Le démarrage automatique de FloppyGames Agent est DÉSACTIVÉ.%nVeux-tu l'activer ?
english.ConfigureAutostartInactiveAsk=FloppyGames Agent autostart is OFF.%nDo you want to turn it on?
portuguese.ConfigureAutostartInactiveAsk=O arranque automático do FloppyGames Agent está INATIVO.%nQueres ativá-lo?
spanish.ConfigureAutostartInactiveAsk=El inicio automático de FloppyGames Agent está DESACTIVADO.%n¿Quieres activarlo?
italian.ConfigureAutostartInactiveAsk=L'avvio automatico di FloppyGames Agent è DISATTIVATO.%nVuoi attivarlo?

french.ConfigureAutostartEnabledMsg=Démarrage automatique activé.
english.ConfigureAutostartEnabledMsg=Autostart turned on.
portuguese.ConfigureAutostartEnabledMsg=Arranque automático ativado.
spanish.ConfigureAutostartEnabledMsg=Inicio automático activado.
italian.ConfigureAutostartEnabledMsg=Avvio automatico attivato.

[Tasks]
Name: "autostart"; Description: "{cm:AutostartTaskDescription}"; GroupDescription: "{cm:AdditionalOptionsGroup}"

[Files]
Source: "publish\Agent\*"; DestDir: "{app}\Agent"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "publish\LabelStudio\*"; DestDir: "{app}\LabelStudio"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\FloppyGames Agent"; Filename: "{app}\Agent\{#AgentExeName}"
Name: "{group}\FloppyGames Label Studio"; Filename: "{app}\LabelStudio\{#LabelStudioExeName}"
Name: "{group}\Desinstalar FloppyGames"; Filename: "{uninstallexe}"

[Registry]
; O nome do valor tem de corresponder exatamente ao usado por AutostartManager (Core/Startup),
; para que o toggle "Iniciar com o Windows" nas Definições do Agent e este instalador
; escrevam/leiam a mesma entrada.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "FloppyGamesAgent"; ValueData: """{app}\Agent\{#AgentExeName}"""; Tasks: autostart

[Run]
Filename: "{app}\Agent\{#AgentExeName}"; Description: "{cm:RunAfterInstallDescription}"; \
    Flags: postinstall nowait skipifsilent

[Code]
const
  RunKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValueName = 'FloppyGamesAgent';
  // Mesma chave que o Inno Setup usa para registar a desinstalação (AppId + "_is1").
  // Lida diretamente em vez de usar a constante {app}, que não está disponível
  // dentro de InitializeSetup (só fica pronta depois do assistente escolher a pasta).
  UninstallKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{BC2C9071-1DB5-4978-A7DB-4B3CA3E79F66}_is1';

function IsConfigureMode(): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
  begin
    if CompareText(ParamStr(I), '/configure') = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function GetInstalledAppPath(): String;
begin
  if not RegQueryStringValue(HKCU, UninstallKeyPath, 'InstallLocation', Result) then
    Result := '';
end;

// Modo "/configure": em vez do assistente de instalação completo, alterna diretamente
// o arranque automático de uma instalação já existente e sai. Evita obrigar o utilizador
// a reinstalar só para ligar/desligar esta opção.
function InitializeSetup(): Boolean;
var
  InstallPath: String;
  ExePath: String;
  IsCurrentlyEnabled: Boolean;
  DummyValue: String;
  MsgResult: Integer;
begin
  if not IsConfigureMode() then
  begin
    Result := True;
    Exit;
  end;

  Result := False; // não seguir para o assistente normal, seja qual for o desfecho abaixo

  InstallPath := GetInstalledAppPath();
  if InstallPath = '' then
  begin
    MsgBox(CustomMessage('ConfigureNotInstalled'), mbError, MB_OK);
    Exit;
  end;

  ExePath := AddBackslash(InstallPath) + 'Agent\{#AgentExeName}';
  if not FileExists(ExePath) then
  begin
    MsgBox(Format(CustomMessage('ConfigureExeMissingFormat'), [ExePath]), mbError, MB_OK);
    Exit;
  end;

  IsCurrentlyEnabled := RegQueryStringValue(HKCU, RunKeyPath, RunValueName, DummyValue);

  if IsCurrentlyEnabled then
  begin
    MsgResult := MsgBox(CustomMessage('ConfigureAutostartActiveAsk'), mbConfirmation, MB_YESNO);
    if MsgResult = IDYES then
    begin
      RegDeleteValue(HKCU, RunKeyPath, RunValueName);
      MsgBox(CustomMessage('ConfigureAutostartDisabledMsg'), mbInformation, MB_OK);
    end;
  end
  else
  begin
    MsgResult := MsgBox(CustomMessage('ConfigureAutostartInactiveAsk'), mbConfirmation, MB_YESNO);
    if MsgResult = IDYES then
    begin
      RegWriteStringValue(HKCU, RunKeyPath, RunValueName, '"' + ExePath + '"');
      MsgBox(CustomMessage('ConfigureAutostartEnabledMsg'), mbInformation, MB_OK);
    end;
  end;
end;

// Limpeza garantida no desinstalar, independentemente de a entrada ter sido criada pela
// task de instalação, pelo /configure, ou pelas Definições do próprio Agent em execução —
// nunca deve sobrar uma entrada órfã no Registo.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKCU, RunKeyPath, RunValueName);
  end;
end;
