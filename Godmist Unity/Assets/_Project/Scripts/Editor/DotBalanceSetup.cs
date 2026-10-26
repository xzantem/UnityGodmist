using System;
using System.IO;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using UnityEditor;

namespace Godmist.Editor
{
    public static class DotBalanceSetup
    {
        public static readonly (string Id, string Path, string Status, float PerTurn, bool OnHit, string Description)[] Specs = {
            ("skill.mage.fire_arrow", "Assets/_Project/ScriptableObjects/Skills/SD_MageFireArrow.asset", "burn", 0.30f, false, "Losowe obrażenia magiczne, 30% penetracji; 70% szansy na podpalenie 30% średniego ataku na turę przez 2 tury i -15% obrażeń na 4 tury."),
            ("skill.mage.fireball", "Assets/_Project/ScriptableObjects/Skills/SD_MageFireball.asset", "burn", 0.25f, false, "Losowe obrażenia magiczne; 75% szansy na podpalenie 25% średniego ataku na turę przez 3 tury."),
            ("skill.scout.poisoned_dagger", "Assets/_Project/ScriptableObjects/Skills/SD_ScoutPoisonedDagger.asset", "poison", 0.40f, false, "Minimalne obrażenia fizyczne. Po trafieniu: 65% szansy na zatrucie 40% średniego ataku na turę przez 3 tury."),
            ("skill.warrior.bleeding_destruction", "Assets/_Project/ScriptableObjects/Skills/SD_WarriorBleedingDestruction.asset", "bleed", 0.40f, false, "Najmniejsze obrażenia fizyczne; 75% szansy na krwawienie 40% średniego ataku na turę przez 4 tury."),
            ("skill.enemy.forest.shooter_piercing_bolt", "Assets/_Project/ScriptableObjects/Dungeons/Forest/SD_Forest_Shooter_piercing_bolt.asset", "bleed", 0.40f, false, "70% obrażeń fizycznych; po trafieniu 80% szansy na krwawienie 40% średniego ataku na turę przez 2 tury."),
            ("skill.enemy.catacombs.burning_arrow", "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_burning_arrow.asset", "burn", 0.25f, false, "Losowe obrażenia fizyczne; po trafieniu 90% szansy na podpalenie 25% średniego ataku na turę przez 3 tury."),
            ("skill.enemy.catacombs.cross_cut", "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_cross_cut.asset", "bleed", 0.30f, true, "Dwa ciosy po 60% obrażeń. Każde trafienie osobno ma 60% szansy na krwawienie 30% średniego ataku na turę przez 3 tury."),
            ("skill.enemy.catacombs.vomit", "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_vomit.asset", "poison", 0.50f, false, "90% szansy na zatrucie 50% średniego ataku na turę przez 3 tury i obniżenie szybkości celu o 8 na 3 tury."),
        };
        [MenuItem("Godmist/Content/Apply agreed DoT balance")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (var spec in Specs)
            {
                Backup(spec.Path); var skill = AssetDatabase.LoadAssetAtPath<SkillData>(spec.Path);
                if (skill == null || skill.SkillId != spec.Id) throw new InvalidOperationException("Missing approved skill: " + spec.Id);
                var data = new SerializedObject(skill); var effects = data.FindProperty("effects"); var count = 0;
                for (var n = 0; n < effects.arraySize; n++)
                {
                    var effect = effects.GetArrayElementAtIndex(n); var type = (EffectType)effect.FindPropertyRelative("effectType").intValue;
                    if (spec.OnHit ? type != EffectType.DealDamage || effect.FindPropertyRelative("onHitStatusId").stringValue != spec.Status
                        : type != EffectType.ApplyStatus || effect.FindPropertyRelative("statusId").stringValue != spec.Status) continue;
                    effect.FindPropertyRelative("statusDamageMultiplier").floatValue = spec.PerTurn; count++;
                }
                if (count != 1) throw new InvalidOperationException("Expected exactly one approved DoT effect: " + spec.Id);
                if (spec.Id == "skill.scout.poisoned_dagger") data.FindProperty("<ResourceCost>k__BackingField").intValue = 40;
                data.FindProperty("<TooltipDescription>k__BackingField").stringValue = spec.Description;
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(skill);
            }
            UpdateNode("ST_Mage_Fire", "node.mage.fire.fire_arrow", "podpalenie 90%", "podpalenie 30%");
            UpdateNode("ST_Scout_BladeDancer", "node.scout.blade_dancer.poisoned_dagger", "zatrucie 90%", "zatrucie 40%");
        }
        static void UpdateNode(string file, string id, string before, string after)
        {
            var path = "Assets/_Project/ScriptableObjects/Skills/SkillTrees/" + file + ".asset"; Backup(path);
            var tree = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(path); var data = new SerializedObject(tree);
            var nodes = data.FindProperty("<Nodes>k__BackingField"); var found = false;
            for (var n = 0; n < nodes.arraySize; n++)
            {
                var node = nodes.GetArrayElementAtIndex(n); if (node.FindPropertyRelative("<Id>k__BackingField").stringValue != id) continue;
                var description = node.FindPropertyRelative("<Description>k__BackingField"); description.stringValue = description.stringValue.Replace(before, after); found = true;
            }
            if (!found) throw new InvalidOperationException("Missing node " + id);
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(tree);
        }
        static void Backup(string path)
        {
            var backup = Path.GetFullPath("../LocalHandoffs/dot-balance-20261024/before/" + path);
            if (!File.Exists(backup)) { Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(path, backup); }
        }
    }
}
