; Instalador do FloppyGames (Agent + Label Studio).
; Compilar com build.ps1 (publica as apps primeiro) ou diretamente com ISCC, desde que
; publish\Agent e publish\LabelStudio já existam nesta pasta.

#define MyAppName "FloppyGames"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Diogo Carneiro"
#define AgentExeName "FloppyGames.Agent.exe"
#define LabelStudioExeName "FloppyGames.LabelStudio.exe"

[Setup]
AppId={{BC2C9071-1DB5-4978-A7DB-4B3CA3E79F66}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppSupportURL=https://github.com/
VersionInfoVersion={#MyAppVersion}
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
; Sem diálogo de escolha de idioma: além de ser um passo a menos para o utilizador,
; o diálogo aparece ANTES de InitializeSetup correr, o que faria o modo /configure
; mostrar sempre essa janela antes de conseguir agir. Português como único idioma
; instalado corre logo sem perguntar; -LANG=english continua disponível a quem precisar.
ShowLanguageDialog=no

[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Iniciar o FloppyGames Agent com o Windows"; GroupDescription: "Opções adicionais:"

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
Filename: "{app}\Agent\{#AgentExeName}"; Description: "Iniciar o FloppyGames Agent agora"; \
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
    MsgBox('O FloppyGames ainda não está instalado. Corre o instalador normalmente primeiro.',
      mbError, MB_OK);
    Exit;
  end;

  ExePath := AddBackslash(InstallPath) + 'Agent\{#AgentExeName}';
  if not FileExists(ExePath) then
  begin
    MsgBox('O FloppyGames parece estar instalado, mas não encontrei ' + ExePath + '.',
      mbError, MB_OK);
    Exit;
  end;

  IsCurrentlyEnabled := RegQueryStringValue(HKCU, RunKeyPath, RunValueName, DummyValue);

  if IsCurrentlyEnabled then
  begin
    MsgResult := MsgBox(
      'O arranque automático do FloppyGames Agent está ATIVO.' + #13#10 + 'Queres desativá-lo?',
      mbConfirmation, MB_YESNO);
    if MsgResult = IDYES then
    begin
      RegDeleteValue(HKCU, RunKeyPath, RunValueName);
      MsgBox('Arranque automático desativado.', mbInformation, MB_OK);
    end;
  end
  else
  begin
    MsgResult := MsgBox(
      'O arranque automático do FloppyGames Agent está INATIVO.' + #13#10 + 'Queres ativá-lo?',
      mbConfirmation, MB_YESNO);
    if MsgResult = IDYES then
    begin
      RegWriteStringValue(HKCU, RunKeyPath, RunValueName, '"' + ExePath + '"');
      MsgBox('Arranque automático ativado.', mbInformation, MB_OK);
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
