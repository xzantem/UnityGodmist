using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.NPC
{
    public abstract class NpcRoleData : ScriptableObject
    {
    }

    /// <summary>
    /// Contract for NPC roles that expose material recipes. The role owns the
    /// discipline and recipe list so console and UI workflows can stay
    /// specialist-agnostic.
    /// </summary>
    public interface IMaterialCraftingSpecialist
    {
        MaterialDiscipline CraftingDiscipline { get; }
        IReadOnlyList<CraftableItemDefinition> MaterialRecipes { get; }
    }
}
