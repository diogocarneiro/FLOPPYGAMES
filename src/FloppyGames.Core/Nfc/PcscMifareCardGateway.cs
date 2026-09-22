using PCSC;
using PCSC.Exceptions;

namespace FloppyGames.Core.Nfc;

/// <summary>
/// Autentica e lê/escreve blocos de um cartão Mifare Classic através dos comandos pseudo-APDU
/// PC/SC popularizados pelos leitores ACR (largamente copiados por outros fabricantes, mas não
/// universais — ver risco documentado no ROADMAP): <c>FF 82</c> carrega a chave, <c>FF 86</c>
/// autentica um setor, <c>FF B0</c> lê um bloco, <c>FF D6</c> escreve um bloco.
///
/// NÃO VERIFICADO CONTRA HARDWARE REAL nesta sessão.
///
/// Mantém uma única ligação PC/SC aberta ao leitor entre chamadas: <see cref="Authenticate"/>
/// carrega a chave e autentica o setor NA MESMA sessão do cartão que os <see cref="ReadBlock"/>/
/// <see cref="WriteBlock"/> seguintes vão usar — reconectar a meio perderia o estado de
/// autenticação. A ligação só é trocada quando o nome do leitor muda.
/// </summary>
public sealed class PcscMifareCardGateway : IMifareCardGateway, IDisposable
{
    private readonly Lock _lock = new();
    private ISCardContext? _context;
    private ICardReader? _reader;
    private string? _connectedReaderName;

    public bool Authenticate(string readerName, int sector, byte[] keyA)
    {
        if (keyA.Length != 6)
        {
            throw new ArgumentException("Uma chave Mifare tem sempre 6 bytes.", nameof(keyA));
        }

        lock (_lock)
        {
            try
            {
                var reader = Connect(readerName);

                var loadKey = Transmit(reader, [0xFF, 0x82, 0x00, 0x00, 0x06, .. keyA]);
                if (!IsSuccess(loadKey))
                {
                    return false;
                }

                // Qualquer bloco do setor serve de alvo — o leitor resolve para o setor inteiro;
                // usa-se o bloco trailer por convenção.
                var trailerBlock = SectorTrailerAbsoluteBlock(sector);

                var authenticate = Transmit(reader, [0xFF, 0x86, 0x00, 0x00, 0x05, 0x01, 0x00, (byte)trailerBlock, 0x60, 0x00]);
                return IsSuccess(authenticate);
            }
            catch (PCSCException)
            {
                return false;
            }
        }
    }

    public byte[] ReadBlock(string readerName, int absoluteBlock)
    {
        lock (_lock)
        {
            var reader = Connect(readerName);

            var response = Transmit(reader, [0xFF, 0xB0, 0x00, (byte)absoluteBlock, 0x10]);
            if (!IsSuccess(response) || response.Length != 18)
            {
                throw new InvalidOperationException($"Falha ao ler o bloco {absoluteBlock} do leitor {readerName}.");
            }

            return response[..16];
        }
    }

    public void WriteBlock(string readerName, int absoluteBlock, byte[] data)
    {
        if (data.Length != 16)
        {
            throw new ArgumentException("Um bloco Mifare Classic tem sempre 16 bytes.", nameof(data));
        }

        lock (_lock)
        {
            var reader = Connect(readerName);

            var response = Transmit(reader, [0xFF, 0xD6, 0x00, (byte)absoluteBlock, 0x10, .. data]);
            if (!IsSuccess(response))
            {
                throw new InvalidOperationException($"Falha ao escrever o bloco {absoluteBlock} do leitor {readerName}.");
            }
        }
    }

    private ICardReader Connect(string readerName)
    {
        if (_reader is not null && string.Equals(_connectedReaderName, readerName, StringComparison.OrdinalIgnoreCase))
        {
            return _reader;
        }

        DisconnectCore();

        _context = ContextFactory.Instance.Establish(SCardScope.System);
        _reader = _context.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any);
        _connectedReaderName = readerName;
        return _reader;
    }

    private void DisconnectCore()
    {
        _reader?.Dispose();
        _reader = null;
        _context?.Dispose();
        _context = null;
        _connectedReaderName = null;
    }

    /// <summary>Índice absoluto do bloco trailer (último bloco) do setor indicado.</summary>
    private static int SectorTrailerAbsoluteBlock(int sector)
    {
        if (sector < 32)
        {
            return sector * 4 + 3;
        }

        return 128 + (sector - 32) * 16 + 15;
    }

    /// <summary>Transmite um pseudo-APDU em bruto e devolve a resposta completa (dados + SW1 SW2).</summary>
    private static byte[] Transmit(ICardReader reader, byte[] apdu)
    {
        var receiveBuffer = new byte[258];
        var received = reader.Transmit(apdu, receiveBuffer);
        return receiveBuffer[..received];
    }

    private static bool IsSuccess(byte[] response) =>
        response.Length >= 2 && response[^2] == 0x90 && response[^1] == 0x00;

    public void Dispose()
    {
        lock (_lock)
        {
            DisconnectCore();
        }
    }
}
