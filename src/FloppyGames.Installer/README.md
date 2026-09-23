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

`.\build.ps1 -Version 0.2.0` sobrepõe a versão de `Directory.Build.props` e de `FloppyGames.iss` —
fica essa no instalador e no rodapé das duas apps.

## Release automático (GitHub Actions)

[`.github/workflows/release.yml`](../../.github/workflows/release.yml) corre os testes, compila o
instalador (`build.ps1 -Version`) e publica-o numa GitHub Release, com notas geradas a partir dos
commits:

- **Cada push para o `main`** → release automática `vX.Y.N`: `X.Y` vem de `<Version>` em
  `Directory.Build.props` (hoje `0.1`) e `N` é o número do run, sempre crescente. Para mudar de
  série (ex. passar a `0.2.x`), basta alterar `<Version>` para `0.2.0`.
- **Tag `vX.Y.Z` enviada à mão** → release com exatamente essa versão:

  ```powershell
  git tag v0.2.0
  git push origin v0.2.0
  ```

- **Correr à mão** (separador Actions → *Release* → *Run workflow*) → só deixa o instalador como
  artefacto do run, sem criar release.

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
- **O diálogo de escolha de idioma aparece antes de `InitializeSetup` correr** — com mais que um
  idioma configurado, `/configure` mostraria sempre essa janela antes de conseguir agir. Resolvido
  com `ShowLanguageDialog=no`: corre sempre silencioso, no primeiro idioma da lista `[Languages]`
  (Francês). O instalador tem os 5 idiomas da app (Francês, Inglês, Português, Espanhol, Italiano);
  `/LANG=xx` continua disponível para escolher um dos outros explicitamente. As mensagens do modo
  `/configure` (os `MsgBox` no `[Code]`) vêm de `[CustomMessages]`, uma variante por idioma.
