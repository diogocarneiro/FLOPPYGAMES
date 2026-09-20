using Serilog;

namespace FloppyGames.Core.Logging;

/// <summary>
/// Configura o logging partilhado por todos os componentes do FloppyGames.
/// Cada componente (Agent, Label Studio) escreve para o seu próprio ficheiro
/// rotativo diário em %LOCALAPPDATA%\FloppyGames\logs, para nunca misturar
/// nem perder eventos de falhas silenciosas.
/// </summary>
public static class LoggingBootstrapper
{
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FloppyGames",
        "logs");

    public static ILogger CreateLogger(string componentName)
    {
        Directory.CreateDirectory(LogDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("Component", componentName)
            .WriteTo.File(
                Path.Combine(LogDirectory, $"{componentName}-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Component}) {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
