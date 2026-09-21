using FloppyGames.Core.Configuration;

namespace FloppyGames.LabelStudio;

/// <summary>
/// Um item do combo de plataforma. Um <c>record</c> em vez de um tuplo — o <c>DisplayMemberPath</c>
/// do WPF resolve nomes de propriedade por reflexão em runtime, e nomes de elementos de um
/// <c>ValueTuple</c> só existem em tempo de compilação (ver o mesmo bug já corrigido no combo de
/// idioma das Definições do Agent).
/// </summary>
public sealed record PlatformOption(GamePlatform Platform, string DisplayName);
