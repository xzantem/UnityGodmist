using System;
using Godmist.Data.Items;
namespace Godmist.Views.UI.Workspace
{
    /// <summary>Presentation families use stable catalog IDs, independent of translated names.</summary>
    public static class MaterialRecipePresentation
    {
        public static int GroupIndex(CraftableItemDefinition recipe)
        {
            if (recipe == null) return 7;
            if (recipe.CraftingDiscipline == Godmist.Core.Enums.MaterialDiscipline.Alchemy) return 5;
            if (recipe.CraftingDiscipline == Godmist.Core.Enums.MaterialDiscipline.Runic) return 6;
            var id = recipe.Id ?? string.Empty;
            var key = id.Substring(id.LastIndexOf('.') + 1);
            if (Ends(key,"_ingot","_plate","_chain","_blacha","_lancuch") || key.StartsWith("sztabka_", StringComparison.Ordinal)
                || key.StartsWith("wielka_sztabka_", StringComparison.Ordinal) || key.StartsWith("sproszkowan", StringComparison.Ordinal)) return 0;
            if (Ends(key,"_hide","_hide_strips") || key.StartsWith("wyprawiona_skora_", StringComparison.Ordinal)
                || key.StartsWith("paski_ze_skory_", StringComparison.Ordinal)) return 1;
            if (Ends(key,"_plank","_stick","_deska","_patyk")) return 2;
            if (Ends(key,"_thread","_fabric","_nitka","_tkanina") || key.StartsWith("nitka_z_", StringComparison.Ordinal)
                || key.StartsWith("tkanina_z_", StringComparison.Ordinal)) return 3;
            if (Ends(key,"_needle") || key == "igla_do_nici") return 4;
            return 7;
        }
        private static bool Ends(string key, params string[] suffixes)
        {
            foreach (var suffix in suffixes) if (key.EndsWith(suffix, StringComparison.Ordinal)) return true;
            return false;
        }
        public static string GroupLabel(int index) => index switch
        {
            0 => "Metale", 1 => "Skóry", 2 => "Drewno", 3 => "Tkaniny", 4 => "Narzędzia", 5 => "Alchemia", 6 => "Materiały runiczne", _ => "Pozostałe"
        };
    }
}
