using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using WorkshopTracker.Models;

namespace WorkshopTracker.Services
{
    public class WorkFileService
    {
        private readonly ConfigServices _config;

        // accepted date formats when reading CSV
        private static readonly string[] DateFormats =
        {
            "dd/MM/yyyy", "d/M/yyyy", "d/MM/yyyy", "dd/M/yyyy"
        };

        public WorkFileService(ConfigServices config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public string GetOpenPath(string branch) => _config.GetOpenCsvPath(branch);
        public string GetClosedPath(string branch) => _config.GetClosedCsvPath(branch);

        public List<WorkRow> LoadOpen(string branch) => LoadFromCsv(GetOpenPath(branch));
        public List<WorkRow> LoadClosed(string branch) => LoadFromCsv(GetClosedPath(branch));

        public void SaveOpen(string branch, IEnumerable<WorkRow> rows) =>
            SaveToCsv(GetOpenPath(branch), rows);

        public void SaveClosed(string branch, IEnumerable<WorkRow> rows) =>
            SaveToCsv(GetClosedPath(branch), rows);

        private static List<WorkRow> LoadFromCsv(string path)
        {
            var result = new List<WorkRow>();

            if (!File.Exists(path))
                return result;

            var lines = File.ReadAllLines(path);
            if (lines.Length == 0)
                return result;

            // assume first line is header
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 15)
                    continue;

                var row = new WorkRow
                {
                    Retail = parts[0],
                    OE = parts[1],
                    Customer = parts[2],
                    Serial = parts[3],
                    DayDue = parts[4],
                    DateDue = ParseNullableDate(parts[5]),
                    Status = parts[6],
                    Qty = ParseInt(parts[7]),
                    WhatIsIt = parts[8],
                    PO = parts[9],
                    WhatAreWeDoing = parts[10],
                    Parts = parts[11],
                    Shaft = parts[12],
                    Priority = parts[13],
                    LastUser = parts[14]
                };

                result.Add(row);
            }

            return result;
        }

        private static void SaveToCsv(string path, IEnumerable<WorkRow> rows)
        {
            var list = rows.ToList();

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var writer = new StreamWriter(path, false);

            // header
            writer.WriteLine("RETAIL,OE,CUSTOMER,SERIAL,DAY DUE,DATE DUE,STATUS,QTY,WHAT IS IT,PO,WHAT ARE WE DOING,PARTS,SHAFT,PRIORITY,LAST USER");

            foreach (var r in list)
            {
                if (r.IsGroupRow)
                    continue; // group/header rows are not stored

                var fields = new[]
                {
                    EscapeCsv(r.Retail),
                    EscapeCsv(r.OE),
                    EscapeCsv(r.Customer),
                    EscapeCsv(r.Serial),
                    EscapeCsv(r.DayDue),
                    r.DateDue?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "",
                    EscapeCsv(r.Status),
                    r.Qty.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(r.WhatIsIt),
                    EscapeCsv(r.PO),
                    EscapeCsv(r.WhatAreWeDoing),
                    EscapeCsv(r.Parts),
                    EscapeCsv(r.Shaft),
                    EscapeCsv(r.Priority),
                    EscapeCsv(r.LastUser)
                };

                writer.WriteLine(string.Join(",", fields));
            }
        }

        private static int ParseInt(string s)
        {
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return value;
            return 0;
        }

        private static DateTime? ParseNullableDate(string s)
        {
            if (DateTime.TryParseExact(
                    s,
                    DateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                return dt.Date;
            }
            return null;
        }

        private static string EscapeCsv(string? s)
        {
            s ??= "";
            if (s.Contains(",") || s.Contains("\""))
            {
                s = s.Replace("\"", "\"\"");
                return $"\"{s}\"";
            }
            return s;
        }
    }
}
