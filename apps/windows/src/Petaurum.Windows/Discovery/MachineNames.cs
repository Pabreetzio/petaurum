using System.Text.RegularExpressions;

namespace Petaurum.Windows.Discovery;

/// <summary>Spots names a machine made up rather than a person chose. See specs/domain/device.md, "Names".</summary>
public static partial class MachineNames
{
    /// <summary>A UUID, a long run of hex digits (such as unknown0c02bd322d7e), or no letters at all.</summary>
    public static bool LooksMachineGenerated(string name) =>
        LongHexRun().IsMatch(name.Replace("-", "")) || !name.Any(char.IsLetter);

    [GeneratedRegex("[0-9a-fA-F]{12,}")]
    private static partial Regex LongHexRun();
}
