using System.Text;

namespace FloppyGames.Core.Steam;

/// <summary>
/// Parser mínimo do formato VDF (Valve KeyValues): uma chave raiz seguida de um bloco
/// <c>{ }</c> com pares chave/valor ou secções aninhadas, strings entre aspas e comentários <c>//</c>.
/// Suficiente para <c>libraryfolders.vdf</c> e <c>appmanifest_*.acf</c> — não pretende ser um parser VDF completo.
/// </summary>
public static class VdfParser
{
    public static VdfNode Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var pos = 0;
        SkipWhitespaceAndComments(text, ref pos);
        ReadQuotedString(text, ref pos); // nome da chave raiz — descartado, só nos interessa o conteúdo
        SkipWhitespaceAndComments(text, ref pos);
        return ParseObject(text, ref pos);
    }

    private static VdfNode ParseObject(string text, ref int pos)
    {
        Expect(text, ref pos, '{');
        var node = VdfNode.Object();

        while (true)
        {
            SkipWhitespaceAndComments(text, ref pos);
            if (pos >= text.Length)
            {
                throw new FormatException("VDF malformado: chaveta de fecho '}' em falta.");
            }

            if (text[pos] == '}')
            {
                pos++;
                return node;
            }

            var key = ReadQuotedString(text, ref pos);
            SkipWhitespaceAndComments(text, ref pos);

            if (pos < text.Length && text[pos] == '{')
            {
                node.Set(key, ParseObject(text, ref pos));
            }
            else
            {
                var value = ReadQuotedString(text, ref pos);
                node.Set(key, VdfNode.Leaf(value));
            }
        }
    }

    private static string ReadQuotedString(string text, ref int pos)
    {
        SkipWhitespaceAndComments(text, ref pos);

        if (pos >= text.Length || text[pos] != '"')
        {
            throw new FormatException($"VDF malformado: esperava-se '\"' na posição {pos}.");
        }

        pos++; // abre aspas
        var sb = new StringBuilder();

        while (pos < text.Length && text[pos] != '"')
        {
            if (text[pos] == '\\' && pos + 1 < text.Length)
            {
                sb.Append(text[pos + 1]);
                pos += 2;
                continue;
            }

            sb.Append(text[pos]);
            pos++;
        }

        if (pos >= text.Length)
        {
            throw new FormatException("VDF malformado: string por fechar.");
        }

        pos++; // fecha aspas
        return sb.ToString();
    }

    private static void SkipWhitespaceAndComments(string text, ref int pos)
    {
        while (pos < text.Length)
        {
            if (char.IsWhiteSpace(text[pos]))
            {
                pos++;
                continue;
            }

            if (text[pos] == '/' && pos + 1 < text.Length && text[pos + 1] == '/')
            {
                while (pos < text.Length && text[pos] != '\n')
                {
                    pos++;
                }

                continue;
            }

            break;
        }
    }

    private static void Expect(string text, ref int pos, char expected)
    {
        SkipWhitespaceAndComments(text, ref pos);
        if (pos >= text.Length || text[pos] != expected)
        {
            throw new FormatException($"VDF malformado: esperava-se '{expected}' na posição {pos}.");
        }

        pos++;
    }
}
