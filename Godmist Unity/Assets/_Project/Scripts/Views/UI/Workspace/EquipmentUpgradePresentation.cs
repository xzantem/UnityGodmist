using System;
namespace Godmist.Views.UI.Workspace
{
    /// <summary>Only show a price or shortfall when the service has calculated an actual cost.</summary>
    public static class EquipmentUpgradePresentation
    {
        public static string Cost(int goldCost) => goldCost > 0 ? goldCost.ToString("N0") : "—";
        public static string Shortfall(int goldCost, long gold)
        {
            var missing = Math.Max(0L, goldCost - gold);
            return goldCost > 0 && missing > 0 ? $"Brakuje: {missing:N0} złota" : string.Empty;
        }
    }
}
