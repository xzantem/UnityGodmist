using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEngine;

namespace Godmist.Editor
{
    public static class ContentImportSourceReader
    {
        public static bool TryRead(string path, out ContentImportFile file, out string error)
        {
            file = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "Nie znaleziono wybranego pliku.";
                return false;
            }

            try
            {
                var extension = Path.GetExtension(path);
                var sheets = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase)
                    ? ReadDelimited(path)
                    : extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                        ? ReadWorkbook(path)
                        : throw new InvalidDataException("Obsługiwane są wyłącznie pliki .xlsx, .csv i .tsv.");
                file = new ContentImportFile(path, sheets);
                return sheets.Count > 0;
            }
            catch (Exception exception)
            {
                error = $"Nie udało się odczytać pliku: {exception.Message}";
                Debug.LogException(exception);
                return false;
            }
        }

        public static IReadOnlyList<IReadOnlyList<string>> ParseDelimited(
            string content,
            char delimiter)
        {
            var rows = new List<IReadOnlyList<string>>();
            using var reader = new StringReader(content ?? string.Empty);
            var row = new List<string>();
            var value = new StringBuilder();
            var quoted = false;
            var character = -1;

            while ((character = reader.Read()) >= 0)
            {
                var current = (char)character;
                if (quoted)
                {
                    if (current == '"')
                    {
                        if (reader.Peek() == '"')
                        {
                            reader.Read();
                            value.Append('"');
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        value.Append(current);
                    }
                    continue;
                }

                if (current == '"' && value.Length == 0)
                {
                    quoted = true;
                }
                else if (current == delimiter)
                {
                    row.Add(value.ToString());
                    value.Clear();
                }
                else if (current == '\r' || current == '\n')
                {
                    if (current == '\r' && reader.Peek() == '\n') reader.Read();
                    row.Add(value.ToString());
                    value.Clear();
                    if (row.Any(cell => !string.IsNullOrWhiteSpace(cell))) rows.Add(row.ToArray());
                    row.Clear();
                }
                else
                {
                    value.Append(current);
                }
            }

            if (quoted) throw new InvalidDataException("CSV zawiera niedomknięty cudzysłów.");
            if (value.Length > 0 || row.Count > 0)
            {
                row.Add(value.ToString());
                if (row.Any(cell => !string.IsNullOrWhiteSpace(cell))) rows.Add(row.ToArray());
            }
            return rows;
        }

        private static IReadOnlyList<ContentImportSheet> ReadDelimited(string path)
        {
            var content = ReadDelimitedText(path);
            var delimiter = Path.GetExtension(path).Equals(".tsv", StringComparison.OrdinalIgnoreCase)
                ? '\t'
                : DetectDelimiter(content);
            var rows = ParseDelimited(content, delimiter);
            return new[]
            {
                new ContentImportSheet(
                    Path.GetFileNameWithoutExtension(path),
                    rows,
                    FindHeaderRow(rows))
            };
        }

        private static IReadOnlyList<ContentImportSheet> ReadWorkbook(string path)
        {
            using var archive = ZipFile.OpenRead(path);
            var sharedStrings = ReadSharedStrings(archive);
            var workbook = ReadXml(archive, "xl/workbook.xml");
            var relationships = ReadRelationships(archive, "xl/_rels/workbook.xml.rels");
            var sheets = new List<ContentImportSheet>();

            foreach (XmlNode sheet in workbook.GetElementsByTagName("sheet"))
            {
                var name = sheet.Attributes?["name"]?.Value ?? "Sheet";
                var relationshipId = sheet.Attributes?["r:id"]?.Value;
                if (string.IsNullOrWhiteSpace(relationshipId)
                    || !relationships.TryGetValue(relationshipId, out var target))
                {
                    continue;
                }

                var worksheetPath = NormalizeWorkbookPath(target);
                var worksheet = ReadXml(archive, worksheetPath);
                var rows = ReadWorksheetRows(worksheet, sharedStrings);
                sheets.Add(new ContentImportSheet(name, rows, FindHeaderRow(rows)));
            }

            return sheets;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return new List<string>();
            var document = ReadXml(entry);
            var values = new List<string>();
            foreach (XmlNode item in document.GetElementsByTagName("si"))
            {
                values.Add(string.Concat(item.SelectNodes(".//*[local-name()='t']")
                    .Cast<XmlNode>().Select(node => node.InnerText)));
            }
            return values;
        }

        private static Dictionary<string, string> ReadRelationships(
            ZipArchive archive,
            string path)
        {
            var document = ReadXml(archive, path);
            return document.GetElementsByTagName("Relationship")
                .Cast<XmlNode>()
                .Where(node => node.Attributes?["Id"] != null && node.Attributes?["Target"] != null)
                .ToDictionary(
                    node => node.Attributes["Id"].Value,
                    node => node.Attributes["Target"].Value,
                    StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<IReadOnlyList<string>> ReadWorksheetRows(
            XmlDocument document,
            IReadOnlyList<string> sharedStrings)
        {
            var rawRows = new List<Dictionary<int, string>>();
            var maxColumn = 0;
            foreach (XmlNode row in document.GetElementsByTagName("row"))
            {
                var values = new Dictionary<int, string>();
                foreach (XmlNode cell in row.ChildNodes)
                {
                    if (!string.Equals(cell.LocalName, "c", StringComparison.Ordinal)) continue;
                    var reference = cell.Attributes?["r"]?.Value;
                    var column = GetColumnIndex(reference);
                    if (column < 0) continue;
                    values[column] = ReadCellValue(cell, sharedStrings);
                    maxColumn = Math.Max(maxColumn, column);
                }
                rawRows.Add(values);
            }

            return rawRows.Select(row => (IReadOnlyList<string>)Enumerable.Range(0, maxColumn + 1)
                .Select(column => row.TryGetValue(column, out var value) ? value : string.Empty)
                .ToArray()).ToList();
        }

        private static string ReadCellValue(XmlNode cell, IReadOnlyList<string> sharedStrings)
        {
            var type = cell.Attributes?["t"]?.Value;
            if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
                return string.Concat(cell.SelectNodes(".//*[local-name()='t']").Cast<XmlNode>().Select(node => node.InnerText));

            var value = cell.SelectSingleNode("*[local-name()='v']")?.InnerText ?? string.Empty;
            if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(value, out var index)
                && index >= 0 && index < sharedStrings.Count)
            {
                return sharedStrings[index];
            }
            if (string.Equals(type, "b", StringComparison.OrdinalIgnoreCase)) return value == "1" ? "true" : "false";
            return value;
        }

        private static XmlDocument ReadXml(ZipArchive archive, string path)
        {
            var entry = archive.GetEntry(path.TrimStart('/'))
                ?? throw new InvalidDataException($"Brak wpisu '{path}' w pliku XLSX.");
            return ReadXml(entry);
        }

        private static XmlDocument ReadXml(ZipArchiveEntry entry)
        {
            var document = new XmlDocument();
            using var stream = entry.Open();
            document.Load(stream);
            return document;
        }

        private static string NormalizeWorkbookPath(string target)
        {
            var normalized = target.Replace('\\', '/').TrimStart('/');
            return normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : $"xl/{normalized}";
        }

        private static int GetColumnIndex(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference)) return -1;
            var result = 0;
            foreach (var character in cellReference.TakeWhile(char.IsLetter))
                result = result * 26 + char.ToUpperInvariant(character) - 'A' + 1;
            return result - 1;
        }

        private static int FindHeaderRow(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index].Count(cell => !string.IsNullOrWhiteSpace(cell)) >= 2) return index;
            }
            return 0;
        }

        private static char DetectDelimiter(string content)
        {
            var firstLine = (content ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? string.Empty;
            var candidates = new[] { ';', ',', '\t' };
            return candidates.OrderByDescending(candidate => firstLine.Count(character => character == candidate)).First();
        }

        private static string ReadDelimitedText(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var hasUtf8Bom = bytes.Length >= 3
                             && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var payload = hasUtf8Bom ? bytes.Skip(3).ToArray() : bytes;
            try
            {
                return new UTF8Encoding(false, true).GetString(payload);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(payload);
            }
        }
    }
}
