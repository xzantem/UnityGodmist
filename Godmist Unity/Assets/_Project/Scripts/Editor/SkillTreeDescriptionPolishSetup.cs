using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.Skills;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Updates stale node names in descriptions without rebuilding skill trees.</summary>
    public static class SkillTreeDescriptionPolishSetup
    {
        private const string TreeRoot = "Assets/_Project/ScriptableObjects/Skills/SkillTrees/";
        private static readonly (string Tree, string Node, string Before, string After)[] Changes =
        {
            ("ST_Scout_Assassin", "node.scout.assassin.keen_eye", "Ostry Nóż", "Głębokie Wbicie"),
            ("ST_Scout_Assassin", "node.scout.assassin.light_step", "Wojownika Cieni", "Kroku w Cień"),
            ("ST_Scout_BladeDancer", "node.scout.blade_dancer.combo_master", "Kombo", "Podwójny Rzut"),
            ("ST_Mage_Fire", "node.mage.fire.expansion", "Agresywnego Ognia", "Rozprzestrzeniania Płomieni"),
            ("ST_Mage_Fire", "node.mage.fire.sanguine", "Choleryzm", "Obezwładniający Żar"),
            ("ST_Mage_Warlock", "node.mage.warlock.mass_sacrifice", "Poświęcenia", "Przejęcia Mocy"),
            ("ST_Paladin_Priest", "node.paladin.priest.salvation", "przez Łaskę", "przez Gromadzenie Łaski")
        };

        [Serializable]
        private sealed class Snapshot
        {
            public string path;
            public string before;
            public string after;
        }

        [Serializable]
        private sealed class Report
        {
            public int changedDescriptions;
            public Snapshot[] trees;
        }

        [MenuItem("Godmist/Content/Fix stale skill tree description names")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before updating descriptions.");

            var trees = Changes.Select(change => change.Tree).Distinct().ToDictionary(
                name => name,
                name => AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(TreeRoot + name + ".asset")
                    ?? throw new InvalidOperationException("Missing skill tree: " + name));

            // Validate every target before writing any asset. Already corrected assets are accepted.
            foreach (var change in Changes)
            {
                var node = trees[change.Tree].Nodes.Single(node => node.Id == change.Node);
                if (!node.Description.Contains(change.Before) && !node.Description.Contains(change.After))
                    throw new InvalidOperationException("Unexpected description for " + change.Node);
            }

            var snapshots = new List<Snapshot>();
            var changed = 0;
            foreach (var pair in trees)
            {
                var path = TreeRoot + pair.Key + ".asset";
                var snapshot = new Snapshot { path = path, before = EditorJsonUtility.ToJson(pair.Value) };
                var data = new SerializedObject(pair.Value);
                var nodes = data.FindProperty("<Nodes>k__BackingField");
                foreach (var change in Changes.Where(change => change.Tree == pair.Key))
                {
                    for (var index = 0; index < nodes.arraySize; index++)
                    {
                        var node = nodes.GetArrayElementAtIndex(index);
                        if (node.FindPropertyRelative("<Id>k__BackingField").stringValue != change.Node)
                            continue;
                        var description = node.FindPropertyRelative("<Description>k__BackingField");
                        var next = description.stringValue.Replace(change.Before, change.After);
                        if (next != description.stringValue)
                        {
                            description.stringValue = next;
                            changed++;
                        }
                    }
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(pair.Value);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                snapshot.after = EditorJsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(path));
                snapshots.Add(snapshot);
            }

            Verify();
            var reportPath = Path.GetFullPath("../LocalHandoffs/skill-tree-description-names-20261027/report-" + changed + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(new Report
            {
                changedDescriptions = changed,
                trees = snapshots.ToArray()
            }, true));
            Debug.Log("Skill tree description names: " + changed + " updated; 7/7 verified. " + reportPath);
        }

        [MenuItem("Godmist/Content/Verify skill tree description names")]
        public static void Verify()
        {
            foreach (var change in Changes)
            {
                var tree = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(TreeRoot + change.Tree + ".asset");
                if (tree == null)
                    throw new InvalidOperationException("Missing skill tree: " + change.Tree);
                var node = tree.Nodes.Single(node => node.Id == change.Node);
                if (node.Description.Contains(change.Before) || !node.Description.Contains(change.After))
                    throw new InvalidOperationException("Stale description for " + change.Node);
            }
        }

        public static void ApplyAndVerifyIdempotence()
        {
            Apply();
            var before = Changes.Select(change => change.Tree).Distinct().ToDictionary(
                name => name, name => File.ReadAllBytes(TreeRoot + name + ".asset"));
            Apply();
            foreach (var pair in before)
                if (!pair.Value.SequenceEqual(File.ReadAllBytes(TreeRoot + pair.Key + ".asset")))
                    throw new InvalidOperationException("Repeated application changed " + pair.Key);
            Debug.Log("Skill tree description names: idempotence PASS.");
        }
    }
}
