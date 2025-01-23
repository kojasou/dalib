using System.Collections.Generic;

namespace DALib.Comparers;

/// <summary>
///     A natural string comparer intended to work similarly to how windows orders files. Strings with numbers will be
///     compared numerically, rather than lexicographically.
/// </summary>
public sealed class NaturalStringComparer : IComparer<string>
{
    public static IComparer<string> Instance { get; } = new NaturalStringComparer();

    /// <inheritdoc />
    /// <inheritdoc />
    public int Compare(string? x, string? y)
    {
        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if ((x == null) && (y == null))
            return 0;

        if (x == null)
            return -1;

        if (y == null)
            return 1;

        var ix = 0;
        var iy = 0;

        while ((ix < x.Length) && (iy < y.Length))
            if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
            {
                var lx = 0;
                var ly = 0;

                while ((ix < x.Length) && char.IsDigit(x[ix]))
                {
                    lx = lx * 10 + (x[ix] - '0');
                    ix++;
                }

                while ((iy < y.Length) && char.IsDigit(y[iy]))
                {
                    ly = ly * 10 + (y[iy] - '0');
                    iy++;
                }

                if (lx != ly)
                    return lx.CompareTo(ly);
            }
            else
            {
                // Convert both characters to lower case for case-insensitive comparison
                var cx = char.ToLowerInvariant(x[ix]);
                var cy = char.ToLowerInvariant(y[iy]);

                if (cx != cy)
                    return cx.CompareTo(cy);

                ix++;
                iy++;
            }

        return x.Length.CompareTo(y.Length);
    }

}