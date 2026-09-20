namespace FloppyGames.Core.Steam;

/// <summary>
/// Nó de uma árvore VDF (formato Valve KeyValues, usado em <c>libraryfolders.vdf</c> e
/// <c>appmanifest_*.acf</c>). Um nó é ou uma folha (valor string) ou uma secção com filhos nomeados.
/// </summary>
public sealed class VdfNode
{
    private readonly Dictionary<string, VdfNode> _children;

    private VdfNode(string? value, Dictionary<string, VdfNode>? children)
    {
        Value = value;
        _children = children ?? new Dictionary<string, VdfNode>(StringComparer.OrdinalIgnoreCase);
    }

    public string? Value { get; }

    public bool IsLeaf => Value is not null;

    public IReadOnlyDictionary<string, VdfNode> Children => _children;

    internal static VdfNode Leaf(string value) => new(value, null);

    internal static VdfNode Object() => new(null, new Dictionary<string, VdfNode>(StringComparer.OrdinalIgnoreCase));

    internal void Set(string key, VdfNode child) => _children[key] = child;

    public VdfNode? this[string key] => _children.GetValueOrDefault(key);

    public string? GetString(string key) => this[key]?.Value;
}
