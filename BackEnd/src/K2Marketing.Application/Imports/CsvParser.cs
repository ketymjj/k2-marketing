using System.Globalization;
using System.Text;

namespace K2Marketing.Application.Imports;

/// <summary>
/// Leitor CSV RFC 4180 simples: detecta separador (vírgula ou ponto e vírgula), aceita campos entre aspas
/// (com aspas duplicadas e quebras de linha), ignora BOM e linhas vazias. Cabeçalhos são normalizados.
/// </summary>
public static class CsvParser
{
    public const int MaxRows = 20_000;

    public record CsvTable(IReadOnlyList<string> Headers, IReadOnlyList<CsvRow> Rows);
    public record CsvRow(int LineNumber, IReadOnlyDictionary<string, string> Values)
    {
        public string? Get(string column) => Values.TryGetValue(column, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
    }

    public static CsvTable Parse(string content)
    {
        content = content.TrimStart('﻿');
        var firstLine = content.Split('\n', 2)[0];
        var delimiter = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';
        var records = ReadRecords(content, delimiter).ToList();
        if (records.Count == 0) return new CsvTable([], []);

        var headers = records[0].Fields.Select(Normalize).ToList();
        var rows = new List<CsvRow>();
        foreach (var (line, fields) in records.Skip(1))
        {
            if (fields.All(string.IsNullOrWhiteSpace)) continue;
            if (rows.Count >= MaxRows) throw new FormatException($"O arquivo excede o limite de {MaxRows} linhas.");
            var values = new Dictionary<string, string>();
            for (var i = 0; i < headers.Count; i++) values[headers[i]] = i < fields.Count ? fields[i] : string.Empty;
            rows.Add(new CsvRow(line, values));
        }
        return new CsvTable(headers, rows);
    }

    /// <summary>"Attributed Revenue" → "attributed_revenue".</summary>
    public static string Normalize(string header) =>
        string.Join('_', header.Trim().ToLowerInvariant().Split([' ', '-', '.'], StringSplitOptions.RemoveEmptyEntries));

    private static IEnumerable<(int Line, List<string> Fields)> ReadRecords(string content, char delimiter)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;
        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else
                {
                    if (c == '\n') line++;
                    sb.Append(c);
                }
                continue;
            }
            switch (c)
            {
                case '"': inQuotes = true; break;
                case '\r': break;
                case '\n':
                    fields.Add(sb.ToString()); sb.Clear();
                    yield return (recordLine, fields);
                    fields = [];
                    line++;
                    recordLine = line;
                    break;
                default:
                    if (c == delimiter) { fields.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(c);
                    break;
            }
        }
        if (sb.Length > 0 || fields.Count > 0)
        {
            fields.Add(sb.ToString());
            yield return (recordLine, fields);
        }
    }

    /// <summary>Decimal com ponto (1234.56) ou vírgula decimal (1234,56 / 1.234,56). Símbolos de moeda são ignorados.</summary>
    public static bool TryDecimal(string? s, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim().Replace("R$", "").Replace("$", "").Replace(" ", "");
        if (t.Contains(',') && t.Contains('.'))
            t = t.LastIndexOf(',') > t.LastIndexOf('.') ? t.Replace(".", "").Replace(',', '.') : t.Replace(",", "");
        else if (t.Contains(',')) t = t.Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryLong(string? s, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim().Replace(".", "").Replace(",", "").Replace(" ", "");
        return long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Datas ISO (yyyy-MM-dd) ou brasileiras (dd/MM/yyyy).</summary>
    public static bool TryDate(string? s, out DateOnly value) =>
        DateOnly.TryParseExact(s?.Trim(), ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    public static bool TryDateTime(string? s, out DateTime value)
    {
        var ok = DateTime.TryParseExact(s?.Trim(), ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ssZ",
                "dd/MM/yyyy", "dd/MM/yyyy HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out value);
        value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return ok;
    }

    public static string Escape(string? value)
    {
        var v = value ?? string.Empty;
        // Neutraliza injeção de fórmulas ao abrir no Excel/Sheets.
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0]) && !decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) v = "'" + v;
        return v.IndexOfAny([',', '"', '\n', '\r', ';']) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}
