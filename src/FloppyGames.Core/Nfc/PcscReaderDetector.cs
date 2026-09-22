using PCSC;
using PCSC.Exceptions;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Lista os leitores PC/SC ligados via <c>winscard.dll</c> (pacote NuGet <c>PCSC</c>).
/// NÃO VERIFICADO CONTRA HARDWARE REAL nesta sessão — não havia nenhum leitor PC/SC ligado à
/// máquina de desenvolvimento. Devolve sempre uma lista vazia em vez de lançar quando o
/// subsistema PC/SC não está disponível (serviço "Cartão Inteligente" do Windows desligado, por
/// exemplo) — a ausência de leitor deve ser um estado normal da UI, nunca um crash.
/// </summary>
public sealed class PcscReaderDetector : INfcReaderDetector
{
    public IReadOnlyList<string> ListConnectedReaders()
    {
        try
        {
            using var context = ContextFactory.Instance.Establish(SCardScope.System);
            return context.GetReaders() ?? [];
        }
        catch (PCSCException)
        {
            // Sem serviço de Cartão Inteligente a correr, ou sem nenhum leitor instalado.
            return [];
        }
    }
}
