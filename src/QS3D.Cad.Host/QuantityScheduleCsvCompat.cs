global using QuantityScheduleCsv = QS3D.Cad.Host.StandaloneQuantityScheduleCsv;

using System.Globalization;
using System.Text;
using QS3D.Platform.Quantity;

namespace QS3D.Cad.Host;

internal static class StandaloneQuantityScheduleCsv
{
    public static string Write(QuantitySchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var output = new StringBuilder();
        output.Append("ElementId,ElementName,Code,Dimension,Value,CanonicalUnit\n");
        foreach (var row in schedule.Rows.OrderBy(static row => row.ElementId.Value))
        {
            foreach (var summary in row.Quantities.OrderBy(static summary => summary.Code, StringComparer.Ordinal))
            {
                Append(output, row.ElementId.Value.ToString("D", CultureInfo.InvariantCulture));
                Append(output, row.ElementName);
                Append(output, summary.Code);
                Append(output, summary.Quantity.Dimension.ToString());
                Append(output, summary.Quantity.Value.ToString("R", CultureInfo.InvariantCulture));
                Append(output, CanonicalSymbol(summary.Quantity.Dimension), true);
            }
        }
        return output.ToString();
    }

    private static void Append(StringBuilder output, string value, bool last = false)
    {
        var mustQuote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
        if (mustQuote)
        {
            output.Append('"');
            output.Append(value.Replace("\"", "\"\""));
            output.Append('"');
        }
        else output.Append(value);
        output.Append(last ? '\n' : ',');
    }

    private static string CanonicalSymbol(QuantityDimension dimension)
        => dimension switch
        {
            QuantityDimension.Count => "ea",
            QuantityDimension.Length => "m",
            QuantityDimension.Area => "m2",
            QuantityDimension.Volume => "m3",
            QuantityDimension.Mass => "kg",
            _ => throw new ArgumentOutOfRangeException(nameof(dimension))
        };
}
