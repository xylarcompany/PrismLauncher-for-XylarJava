using System;
using System.IO;
using System.Text.Json;

namespace XylarJavaLauncher;

/// <summary>Reads modrinth.index.json from an extracted .mrpack folder.</summary>
internal static class ModrinthPackIndexReader
{
    public static ModrinthPackInfo? TryRead(string packRoot)
    {
        var path = Path.Combine(packRoot, "modrinth.index.json");
        if (!File.Exists(path))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            var name = root.TryGetProperty("name", out var ne) ? ne.GetString() ?? "Modpack" : "Modpack";
            if (!root.TryGetProperty("dependencies", out var deps) || deps.ValueKind != JsonValueKind.Object)
                return new ModrinthPackInfo(name, "1.20.1", "Vanilla", null, packRoot);

            string? mc = GetDepString(deps, "minecraft");
            if (string.IsNullOrWhiteSpace(mc))
                mc = "1.20.1";

            if (HasDep(deps, "fabric-loader", out var fv))
                return new ModrinthPackInfo(name, mc, "Fabric", fv, packRoot);
            if (HasDep(deps, "quilt-loader", out var qv))
                return new ModrinthPackInfo(name, mc, "Quilt", qv, packRoot);
            if (HasDep(deps, "forge", out var forgeV))
                return new ModrinthPackInfo(name, mc, "Forge", forgeV, packRoot);
            if (HasDep(deps, "neoforge", out var nv))
                return new ModrinthPackInfo(name, mc, "NeoForge", nv, packRoot);
            if (HasDep(deps, "neo-forge", out var nv2))
                return new ModrinthPackInfo(name, mc, "NeoForge", nv2, packRoot);

            return new ModrinthPackInfo(name, mc, "Vanilla", null, packRoot);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetDepString(JsonElement deps, string key)
    {
        if (!deps.TryGetProperty(key, out var el))
            return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            _                  => el.GetString()
        };
    }

    private static bool HasDep(JsonElement deps, string key, out string? version)
    {
        version = GetDepString(deps, key);
        return !string.IsNullOrWhiteSpace(version);
    }
}

internal sealed record ModrinthPackInfo(
    string Name,
    string MinecraftVersion,
    string Loader,
    string? LoaderVersion,
    string PackRoot);
