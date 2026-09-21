using System.IO;
using System.Media;

namespace FloppyGames.Agent;

/// <summary>
/// Toca o som de motor/cabeça de leitura de uma disquete real, quando uma disquete física (não
/// pen USB) é detetada. O ficheiro é sintetizado localmente (ver script de geração no histórico
/// do repositório) em vez de embutir um asset licenciado de terceiros.
/// </summary>
internal static class FloppyMotorSoundPlayer
{
    private static readonly string SoundPath = Path.Combine(AppContext.BaseDirectory, "Assets", "floppy-motor.wav");
    private static SoundPlayer? _player;

    public static void PlayIfAvailable()
    {
        if (!File.Exists(SoundPath))
        {
            return;
        }

        try
        {
            _player ??= new SoundPlayer(SoundPath);
            _player.Play();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Áudio indisponível (ex.: sem dispositivo de som) — nunca deve impedir o lançamento do jogo.
        }
    }
}
