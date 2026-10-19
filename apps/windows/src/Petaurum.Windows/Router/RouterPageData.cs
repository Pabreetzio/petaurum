using System.Text.Json.Serialization;

namespace Petaurum.Windows.Router;

/// <summary>What one router status page shows, as data. See specs/features/router.md, "Reading".</summary>
public sealed record RouterPageData(List<RouterSection> Sections);

/// <summary>Everything under one heading. The first section has no heading when the page starts without one.</summary>
public sealed record RouterSection(string? Heading, List<RouterBlock> Blocks);

public enum RouterBlockKind { Fields, Records, Table }

/// <summary>
/// One run of content in a section: label/value <see cref="Fields"/>, <see cref="Records"/> (groups of fields,
/// such as one per device), or a <see cref="Columns"/>/<see cref="Rows"/> table.
/// </summary>
public sealed record RouterBlock(
    RouterBlockKind Kind,
    List<RouterField>? Fields = null,
    List<RouterRecord>? Records = null,
    List<string>? Columns = null,
    List<List<string>>? Rows = null);

public sealed record RouterField(string Label, string Value);

public sealed record RouterRecord(List<RouterField> Fields)
{
    /// <summary>A record's name: its Name field (devices list "IPv4 Address / Name" as "ip / name"), else its first value.</summary>
    [JsonIgnore]
    public string Title
    {
        get
        {
            var named = Fields.FirstOrDefault(f => f.Label.Contains("Name", StringComparison.OrdinalIgnoreCase));
            if (named is not null)
            {
                var slash = named.Value.LastIndexOf(" / ", StringComparison.Ordinal);
                return slash >= 0 ? named.Value[(slash + 3)..] : named.Value;
            }
            return Fields.Count > 0 ? Fields[0].Value : "";
        }
    }

    /// <summary>The first field, when the title came from another one (on the device list, the MAC address).</summary>
    [JsonIgnore]
    public string? Subtitle => Fields.Count > 0 && !Fields[0].Value.EndsWith(Title, StringComparison.Ordinal)
        ? Fields[0].Value
        : null;
}

/// <summary>A page as stored on disk, with when it was read.</summary>
public sealed record StoredRouterPage(string Path, DateTimeOffset ReadAt, RouterPageData Data);
