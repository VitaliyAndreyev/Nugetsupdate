using System.Globalization;
using System.Numerics;

namespace ProjectManager.App.Services;

public sealed class PackageVersionComparer : IComparer<string>
{
  public static readonly PackageVersionComparer Instance = new();

  public int Compare(string? left, string? right)
  {
    if (ReferenceEquals(left, right))
    {
      return 0;
    }

    if (left is null)
    {
      return -1;
    }

    if (right is null)
    {
      return 1;
    }

    var leftParts = TrimTrailingZeros(SplitVersion(left));
    var rightParts = TrimTrailingZeros(SplitVersion(right));

    if (leftParts.SequenceEqual(rightParts))
    {
      return 0;
    }

    var stablePrefixLength = Math.Min(2, Math.Min(leftParts.Length, rightParts.Length));
    for (var index = 0; index < stablePrefixLength; index++)
    {
      var prefixComparison = leftParts[index].CompareTo(rightParts[index]);
      if (prefixComparison != 0)
      {
        return prefixComparison;
      }
    }

    if (leftParts.Length != rightParts.Length)
    {
      var compactComparison = ToCompactNumber(leftParts.Skip(stablePrefixLength))
          .CompareTo(ToCompactNumber(rightParts.Skip(stablePrefixLength)));
      if (compactComparison != 0)
      {
        return compactComparison;
      }
    }

    var partCount = Math.Max(leftParts.Length, rightParts.Length);
    for (var index = stablePrefixLength; index < partCount; index++)
    {
      var leftPart = index < leftParts.Length ? leftParts[index] : 0;
      var rightPart = index < rightParts.Length ? rightParts[index] : 0;
      var comparison = leftPart.CompareTo(rightPart);
      if (comparison != 0)
      {
        return comparison;
      }
    }

    return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
  }

  private static BigInteger ToCompactNumber(IEnumerable<long> parts)
  {
    var compactValue = string.Concat(parts.Select(part => part.ToString(CultureInfo.InvariantCulture)));
    return BigInteger.TryParse(compactValue, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
        ? number
        : BigInteger.Zero;
  }

  private static long[] SplitVersion(string version)
      => version
          .Split('-', 2)[0]
          .Split('.', StringSplitOptions.RemoveEmptyEntries)
          .Select(part => long.TryParse(part, out var number) ? number : 0)
          .ToArray();

  private static long[] TrimTrailingZeros(long[] parts)
  {
    var length = parts.Length;
    while (length > 1 && parts[length - 1] == 0)
    {
      length--;
    }

    return length == parts.Length ? parts : parts[..length];
  }
}
