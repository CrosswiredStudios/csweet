using System.Numerics;

namespace CSweet.Infrastructure.Setup;

internal static class SemanticVersionComparer
{
    public static int Compare(string left, string right)
    {
        var leftVersion = Parse(left);
        var rightVersion = Parse(right);
        for (var index = 0; index < leftVersion.Core.Length; index++)
        {
            var core = leftVersion.Core[index].CompareTo(rightVersion.Core[index]);
            if (core != 0) return core;
        }

        if (leftVersion.PreRelease is null)
        {
            return rightVersion.PreRelease is null ? 0 : 1;
        }

        if (rightVersion.PreRelease is null)
        {
            return -1;
        }

        var count = Math.Max(leftVersion.PreRelease.Length, rightVersion.PreRelease.Length);
        for (var index = 0; index < count; index++)
        {
            if (index == leftVersion.PreRelease.Length) return -1;
            if (index == rightVersion.PreRelease.Length) return 1;

            var leftPart = leftVersion.PreRelease[index];
            var rightPart = rightVersion.PreRelease[index];
            var leftNumeric = BigInteger.TryParse(leftPart, out var leftNumber);
            var rightNumeric = BigInteger.TryParse(rightPart, out var rightNumber);
            var comparison = leftNumeric && rightNumeric
                ? leftNumber.CompareTo(rightNumber)
                : leftNumeric
                    ? -1
                    : rightNumeric
                        ? 1
                        : string.CompareOrdinal(leftPart, rightPart);
            if (comparison != 0) return comparison;
        }

        return 0;
    }

    private static ParsedVersion Parse(string value)
    {
        var withoutBuild = value.Split('+', 2)[0];
        var parts = withoutBuild.Split('-', 2);
        var core = parts[0].Split('.').Select(BigInteger.Parse).ToArray();
        return new ParsedVersion(core, parts.Length == 1 ? null : parts[1].Split('.'));
    }

    private sealed record ParsedVersion(BigInteger[] Core, string[]? PreRelease);
}
