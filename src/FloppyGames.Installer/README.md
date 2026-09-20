# FloppyGames.Installer

Instalador Windows único (Inno Setup) que empacota o `FloppyGames.Agent` e o `FloppyGames.LabelStudio`.

## Compilar

Requer o [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
.\build.ps1
```

Isto publica as duas apps como *self-contained* (`win-x64`, sem exigir .NET instalado à parte) para
`publish\Agent` e `publish\LabelStudio`, e depois compila `FloppyGames.iss`. O instalador final fica em
`Output\FloppyGamesSetup.exe`. Nenhuma destas pastas é versionada (ver `.gitignore`).

## O que o instalador faz

- Instala em `%LOCALAPPDATA%\Programs\FloppyGames` — sem privilégios de administrador (`PrivilegesRequired=lowest`),
  consistente com o princípio de zero-admin do resto do projeto.
- Cria atalhos no Menu Iniciar para o Agent, o Label Studio, e o desinstalador.
- Pergunta, no assistente, se o Agent deve arrancar com o Windows (tarefa opcional).
- `FloppyGamesSetup.exe /configure` — alterna o arranque automático de uma instalação já existente,
  sem passar pelo assistente completo.
- Desinstalação limpa: remove ficheiros, atalhos e a entrada de arranque automático, seja ela qual
  tiver sido a origem (tarefa de instalação, `/configure`, ou o toggle nas Definições do próprio Agent).
  Os logs do utilizador em `%LOCALAPPDATA%\FloppyGames\logs` são propositadamente preservados.

O nome da chave de registo do arranque automático (`FloppyGamesAgent`, em `HKCU\...\Run`) tem de
corresponder exatamente ao usado por `AutostartManager` (`src/FloppyGames.Core/Startup`), para que o
instalador e o toggle nas Definições do Agent leiam/escrevam a mesma entrada.

## Notas de implementação (armadilhas reais encontradas ao testar)

- **`{app}` não está disponível dentro de `InitializeSetup`** — só fica definida depois de o assistente
  passar pela página de escolha de pasta. O modo `/configure` corre `InitializeSetup` e precisa do
  caminho de instalação *antes* disso, por isso lê-o diretamente da própria chave de desinstalação que
  o Inno Setup já escreve (`HKCU\...\Uninstall\{AppId}_is1\InstallLocation`), em vez de usar `{app}`.
- **O diálogo de escolha de idioma aparece antes de `InitializeSetup` correr** — com dois idiomas
  configurados, `/configure` mostraria sempre essa janela antes de conseguir agir. Resolvido com
  `ShowLanguageDialog=no` (fica só em português, sem perguntar).
