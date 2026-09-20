# FloppyGames.Installer

Script Inno Setup para empacotar o `FloppyGames.Agent` e o `FloppyGames.LabelStudio` num único instalador Windows.

Ainda não implementado — ver **Fase 5** em [../../ROADMAP.md](../../ROADMAP.md).

Conteúdo previsto:

- `FloppyGames.iss` — script principal do Inno Setup.
- Tarefa opcional no assistente: "Iniciar o FloppyGames Agent com o Windows".
- Suporte a `FloppyGamesSetup.exe /configure` para alternar o arranque automático sem reinstalar.
- Desinstalação limpa (remove chave de arranque, atalhos, ficheiros).
