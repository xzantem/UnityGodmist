using System;
using Godmist.Core.Enums;

namespace Godmist.Runtime.Items
{
    /// <summary>
    /// Formats the visible name of ordinary crafted equipment from its base definition,
    /// quality and the material used by its first structural part.
    /// </summary>
    internal static class CraftedEquipmentNameFormatter
    {
        public static string Format(string baseName, EquipmentQuality quality, string materialId,
            string legacyPrimaryPartName)
        {
            if (string.IsNullOrWhiteSpace(baseName)) return string.Empty;

            var feminine = IsFeminine(baseName);
            var qualityPrefix = GetQualityPrefix(quality, feminine);
            var materialPrefix = GetMaterialPrefix(materialId, legacyPrimaryPartName, feminine);
            return string.Join(" ", new[] { qualityPrefix, materialPrefix, baseName }
                .WhereNotEmpty());
        }

        private static bool IsFeminine(string baseName)
        {
            var firstWord = baseName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            return firstWord.EndsWith("a", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetQualityPrefix(EquipmentQuality quality, bool feminine) => quality switch
        {
            EquipmentQuality.Poor => feminine ? "Słaba" : "Słaby",
            EquipmentQuality.Excellent => feminine ? "Doskonała" : "Doskonały",
            EquipmentQuality.Masterpiece => feminine ? "Mistrzowska" : "Mistrzowski",
            _ => string.Empty
        };

        private static string GetMaterialPrefix(string materialId, string legacyPartName, bool feminine)
        {
            var source = $"{materialId} {legacyPartName}".ToLowerInvariant();
            if (source.Contains("iron") || source.Contains("żelaz"))
                return feminine ? "Żelazna" : "Żelazny";
            if (source.Contains("goat") || source.Contains("koziej skóry") || source.Contains("kozy"))
                return feminine ? "Skórzana" : "Skórzany";
            if (source.Contains("larch") || source.Contains("modrzew"))
                return feminine ? "Modrzewiowa" : "Modrzewiowy";
            if (source.Contains("cotton") || source.Contains("bawełn"))
                return feminine ? "Bawełniana" : "Bawełniany";
            return string.Empty;
        }

        private static System.Collections.Generic.IEnumerable<string> WhereNotEmpty(this string[] parts)
        {
            foreach (var part in parts)
                if (!string.IsNullOrWhiteSpace(part)) yield return part;
        }
    }
}
