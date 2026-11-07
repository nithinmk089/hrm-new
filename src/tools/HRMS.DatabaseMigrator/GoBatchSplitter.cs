using System.Text.RegularExpressions;

namespace HRMS.DatabaseMigrator;

internal static partial class GoBatchSplitter
{
    // SQL Server tools treat a standalone GO line as a client-side batch separator.
    // These repository migrations use that canonical form (without GO repeat counts).
    [GeneratedRegex(@"^\s*GO\s*(?:--.*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GoSeparatorRegex();

    public static IReadOnlyList<string> Split(string script)
    {
        ArgumentNullException.ThrowIfNull(script);

        var batches = new List<string>();
        using var reader = new StringReader(script);
        var current = new List<string>();
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            if (GoSeparatorRegex().IsMatch(line))
            {
                AddBatchIfNotEmpty(batches, current);
                current.Clear();
                continue;
            }

            // GO with a repeat count is deliberately rejected rather than silently
            // executing a different number of batches than the author intended.
            if (Regex.IsMatch(line, @"^\s*GO\s+\d+\s*(?:--.*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                throw new InvalidOperationException("GO repeat counts are not supported in HRMS migration scripts.");
            }

            current.Add(line);
        }

        AddBatchIfNotEmpty(batches, current);
        return batches;
    }

    private static void AddBatchIfNotEmpty(ICollection<string> batches, IEnumerable<string> lines)
    {
        var batch = string.Join(Environment.NewLine, lines).Trim();
        if (batch.Length > 0)
        {
            batches.Add(batch);
        }
    }
}
