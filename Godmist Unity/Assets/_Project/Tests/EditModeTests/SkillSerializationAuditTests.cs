using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Data.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class SkillSerializationAuditTests
    {
        const string Output = "../LocalHandoffs/serialization-audit-20261026";
        [Serializable] public sealed class Value { public string path, kind, value; }
        [Serializable] public sealed class Asset
        { public string name, id, assetPath; public int maxNestedDepth, effectCount; public List<Value> fields = new(); }
        [Serializable] public sealed class Snapshot
        { public List<Asset> skills = new(); public List<string> catalogSkillIds = new(); }

        [Test]
        public void ExportReadOnlyEditorSkillFieldsAndActualCounterattackDepth()
        {
            var snapshot = new Snapshot();
            foreach (var guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (skill == null) continue;
                var before = File.ReadAllBytes(path);
                var asset = new Asset { name = skill.name, id = skill.SkillId, assetPath = path };
                Flatten(skill, "", asset.fields, true);
                void Effects(IReadOnlyList<SkillEffectData> effects, int depth)
                {
                    if (effects == null) return;
                    foreach (var effect in effects)
                    {
                        if (effect == null) continue;
                        asset.effectCount++; asset.maxNestedDepth = Math.Max(asset.maxNestedDepth, depth);
                        Assert.That(depth, Is.LessThanOrEqualTo(1), path + ": nested counterattack reactions are not supported in v1");
                        Effects(effect.CounterattackEffects, depth + 1);
                    }
                }
                Effects(skill.Effects, 0);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before), "The audit must not change serialized assets.");
                snapshot.skills.Add(asset);
            }
            Assert.That(snapshot.skills.Count, Is.GreaterThan(100));
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            snapshot.catalogSkillIds = catalog.SkillDefinitions.Where(s => s != null).Select(s => s.SkillId).OrderBy(id => id).ToList();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "editor-skills.json"), JsonUtility.ToJson(snapshot, true));
            TestContext.WriteLine("Editor assets=" + snapshot.skills.Count + "; nested max=" + snapshot.skills.Max(s => s.maxNestedDepth));
        }

        private static void Flatten(object value, string path, List<Value> rows, bool root = false)
        {
            void Add(string kind, string text) => rows.Add(new Value { path = path, kind = kind, value = text });
            if (value == null) { Add("null", ""); return; }
            if (!root && value is UnityEngine.Object reference)
            {
                Add("ref", reference != null ? reference.GetType().Name + ":" + reference.name : "null"); return;
            }
            var type = value.GetType();
            if (type == typeof(string)) { Add("string", (string)value ?? ""); return; }
            if (type == typeof(bool)) { Add("bool", (bool)value ? "true" : "false"); return; }
            if (type == typeof(float)) { Add("float", BitConverter.ToUInt32(BitConverter.GetBytes((float)value), 0).ToString("X8")); return; }
            if (type.IsEnum || type.IsPrimitive) { Add("int", Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture)); return; }
            if (value is IList list)
            {
                Add("array", list.Count.ToString(CultureInfo.InvariantCulture));
                for (var i = 0; i < list.Count; i++) Flatten(list[i], path + "[" + i + "]", rows);
                return;
            }
            Add("object", type.Name);
            for (var declaring = type; declaring != null && declaring != typeof(ScriptableObject); declaring = declaring.BaseType)
                foreach (var field in declaring.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized
                        || !field.IsPublic && !field.IsDefined(typeof(SerializeField), false) && !field.IsDefined(typeof(SerializeReference), false)) continue;
                    Flatten(field.GetValue(value), string.IsNullOrEmpty(path) ? field.Name : path + "." + field.Name, rows);
                }
        }
    }
}
