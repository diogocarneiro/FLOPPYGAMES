using System.Management;
using System.Text.RegularExpressions;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Lista as portas COM onde há um Proxmark3 ligado, identificado pelo VID USB <c>9AC4</c>
/// (registado para o projeto comunitário Proxmark3 em pid.codes) — não basta procurar "portas
/// série quaisquer", isso apanharia qualquer outro dispositivo série ligado à máquina.
/// NÃO VERIFICADO CONTRA HARDWARE REAL PARA OUTROS MODELOS — só testado com um Proxmark3 RDV4
/// com firmware Iceman nesta sessão.
/// </summary>
public sealed partial class Pm3ReaderDetector : INfcReaderDetector
{
    private const string ProxmarkVendorId = "VID_9AC4";

    public IReadOnlyList<string> ListConnectedReaders()
    {
        var ports = new List<string>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT Name, DeviceID FROM Win32_PnPEntity WHERE DeviceID LIKE 'USB\\\\{ProxmarkVendorId}%'");

            foreach (ManagementBaseObject device in searcher.Get())
            {
                var name = device["Name"] as string;
                var match = ComPortPattern().Match(name ?? string.Empty);
                if (match.Success)
                {
                    ports.Add($"COM{match.Groups[1].Value}");
                }
            }
        }
        catch (ManagementException)
        {
            // WMI indisponível ou consulta falhou — sem Proxmark3 detetável nesta sessão.
        }

        return ports;
    }

    [GeneratedRegex(@"\(COM(\d+)\)")]
    private static partial Regex ComPortPattern();
}
