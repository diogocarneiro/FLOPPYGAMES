using System.Configuration;
using System.Data;
using System.Windows;
using FloppyGames.Core.Localization;
using FloppyGames.Core.Settings;

namespace FloppyGames.LabelStudio;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Tem de correr ANTES de base.OnStartup: é o StartupUri (App.xaml) que constrói a
        // MainWindow, dentro da implementação base — se a cultura só fosse aplicada depois,
        // a MainWindow já teria lido o texto por omitir (o de Francês, o idioma neutro).
        // O Label Studio não tem seletor de idioma próprio — só lê o valor guardado pelas
        // Definições do Agent, no settings.json partilhado.
        LocalizationManager.Apply(new AgentSettingsStore().Load().Language);

        base.OnStartup(e);
    }
}

