namespace XylarJavaLauncher;

/// <summary>Launch profile: default .minecraft or a Modrinth instance folder (--gameDir).</summary>
public sealed class PlayProfile
{
    public const string StandardId = "standard";

    public string Id { get; init; } = StandardId;
    public string Title { get; init; } = "Standard (.minecraft)";
    public string? GameDirectory { get; init; }
    public string? MinecraftVersion { get; init; }
    public string Loader { get; init; } = "Vanilla";
    public string? LoaderVersion { get; init; }

    public bool IsStandard => string.IsNullOrEmpty(GameDirectory);

    public override string ToString() => Title;

    public static PlayProfile Standard { get; } = new()
    {
        Id    = StandardId,
        Title = "Standard (.minecraft)"
    };
}
