using System;
using System.IO;
using System.Linq;
using Godmist.Data.Characters;
using Godmist.Systems.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class EnemyHealthAttackRebalanceTests
    {
        static string Folder => Path.GetFullPath("../LocalHandoffs/enemy-health-attack-rebalance-20261103");
        [Serializable] public sealed class Row
        {
            public string id, path, json;
            public int[] hp;
            public float[] min, max;
        }
        [Serializable] public sealed class Snapshot { public Row[] rows; }
        static CharacterDefinition[] Definitions() => AssetDatabase.FindAssets("t:CharacterDefinition", new[]{"Assets/_Project/ScriptableObjects"})
            .Select(g=>AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .OrderBy(d=>d.Id,StringComparer.Ordinal).ToArray();
        static bool Enemy(CharacterDefinition d) => d.ClassData != null && d.Id != null && (d.Id.StartsWith("enemy.catacombs.",StringComparison.Ordinal)
            || d.Id.StartsWith("enemy.forest.",StringComparison.Ordinal) || d.Id.StartsWith("summon.forest.",StringComparison.Ordinal));
        static Row Capture(CharacterDefinition d)
        {
            var r=new Row{id=d.Id,path=AssetDatabase.GetAssetPath(d),json=EditorJsonUtility.ToJson(d),hp=new int[50],min=new float[50],max=new float[50]};
            if(!Enemy(d))return r;
            var factory=new CharacterInstanceFactory();
            for(var l=1;l<=50;l++)
            {
                var c=factory.Create(d,d.ClassData,l);
                r.hp[l-1]=c.MaximumHealth;r.min[l-1]=c.Stats.MinAttack.BaseValue;r.max[l-1]=c.Stats.MaxAttack.BaseValue;
            }
            return r;
        }
        [Test,Explicit("Capture current authored content before the approved data-only rebalance; never overwrite the baseline.")]
        public void CaptureBaseline()
        {
            Directory.CreateDirectory(Folder);
            var file=Path.Combine(Folder,"before.json");
            Assert.That(File.Exists(file),Is.False,"The baseline is immutable.");
            File.WriteAllText(file,JsonUtility.ToJson(new Snapshot{rows=Definitions().Select(Capture).ToArray()},true));
        }
        static readonly string[] AttackFields={"MinAttack","MaxAttack","EarlyMinAttack","EarlyMaxAttack","LateMinAttack","LateMaxAttack","LateMinAttackOffset","LateMaxAttackOffset"};
        static SerializedProperty Field(SerializedProperty p,string name)=>p.FindPropertyRelative("<"+name+">k__BackingField");
        [Test,Explicit("One-shot approved -20% HP / +10% ATK authoring through Unity SerializedObject. Requires immutable before.json.")]
        public void AuthorApprovedBalance()
        {
            var before=JsonUtility.FromJson<Snapshot>(File.ReadAllText(Path.Combine(Folder,"before.json")));
            foreach(var row in before.rows)
            {
                var d=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(row.path);
                Assert.That(EditorJsonUtility.ToJson(d),Is.EqualTo(row.json),row.id+" changed after the baseline; refuse to compound.");
            }
            foreach(var row in before.rows)
            {
                var d=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(row.path);
                if(!Enemy(d))continue;
                var data=new SerializedObject(d);var basis=data.FindProperty("<BaseStats>k__BackingField");var growth=data.FindProperty("<GrowthData>k__BackingField");
                // The knight's three HP are a three-hit counter, not a conventional health pool.
                if(d.Id!="enemy.catacombs.spectral_knight")
                {
                    var hp=Field(basis,"MaxHealth");hp.intValue=Mathf.Max(1,Mathf.RoundToInt(hp.intValue*.8f));
                    foreach(var name in new[]{"MaxHealth","EarlyMaxHealth","LateMaxHealth","LateMaxHealthOffset"})Field(growth,name).floatValue*=.8f;
                    var curve=Field(growth,"HealthCurve");
                    for(var i=0;i<curve.arraySize;i++)
                    { var value=Field(curve.GetArrayElementAtIndex(i),"MaxHealth");value.intValue=Mathf.Max(1,Mathf.RoundToInt(value.intValue*.8f)); }
                }
                Field(basis,"MinAttack").floatValue*=1.1f;Field(basis,"MaxAttack").floatValue*=1.1f;
                foreach(var name in AttackFields)Field(growth,name).floatValue*=1.1f;
                Assert.That(data.ApplyModifiedPropertiesWithoutUndo(),Is.True,row.id);
                EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);
            }
            File.WriteAllText(Path.Combine(Folder,"after.json"),JsonUtility.ToJson(new Snapshot{rows=Definitions().Select(Capture).ToArray()},true));
        }

        static string Fixture => "Assets/_Project/Tests/EditModeTests/Fixtures/EnemyStatsBeforeHealthAttackRebalance.json";
        static System.Collections.IEnumerable EnemyIds() => Definitions().Where(Enemy).Select(d=>d.Id);
        [TestCaseSource(nameof(EnemyIds))]
        public void ApprovedBalanceIsAuthoredAtEveryLevelAndRefreshDoesNotCompound(string id)
        {
            var expected=JsonUtility.FromJson<Snapshot>(File.ReadAllText(Fixture)).rows.Single(r=>r.id==id);
            var definition=Definitions().Single(d=>d.Id==id);var factory=new CharacterInstanceFactory();
            var growth=new CharacterLevelGrowthSystem();var previous=0;
            for(var level=1;level<=50;level++)
            {
                var c=factory.Create(definition,definition.ClassData,level);var index=level-1;
                var hp=id=="enemy.catacombs.spectral_knight"?3:Mathf.Max(1,Mathf.RoundToInt(expected.hp[index]*.8f));
                // Integer HP anchors and the integer base are rounded when authored; interpolation may differ by one HP.
                Assert.That(c.MaximumHealth,Is.EqualTo(hp).Within(1),id+" HP L"+level);
                if(id=="enemy.catacombs.spectral_knight")Assert.That(c.MaximumHealth,Is.EqualTo(3));
                Assert.That(c.MaximumHealth,Is.GreaterThanOrEqualTo(previous));previous=c.MaximumHealth;
                Assert.That(c.CurrentHealth,Is.EqualTo(c.MaximumHealth));
                Assert.That(c.Stats.MinAttack.BaseValue,Is.EqualTo(expected.min[index]*1.1f).Within(.003f),id+" ATK min L"+level);
                Assert.That(c.Stats.MaxAttack.BaseValue,Is.EqualTo(expected.max[index]*1.1f).Within(.003f),id+" ATK max L"+level);
                Assert.That(c.Stats.MinAttack.GetValue(),Is.EqualTo(c.Stats.MinAttack.BaseValue));
                Assert.That(c.Stats.MaxAttack.GetValue(),Is.EqualTo(c.Stats.MaxAttack.BaseValue));
                var attack=c.Stats.MinAttack.BaseValue;var maximum=c.MaximumHealth;c.CurrentHealth=1;
                growth.Apply(c,false);growth.Apply(c,false);
                Assert.That(c.CurrentHealth,Is.EqualTo(1),"Rebuild must not heal a wounded or restored NPC.");
                Assert.That(c.MaximumHealth,Is.EqualTo(maximum));
                Assert.That(c.Stats.MinAttack.BaseValue,Is.EqualTo(attack));
            }
        }
        [TestCaseSource(nameof(EnemyIds))]
        public void ReauthoringPreservesDeliberateEditsToHealthAndAttack(string id)
        {
            var clone=UnityEngine.Object.Instantiate(Definitions().Single(d=>d.Id==id));
            try
            {
                var data=new SerializedObject(clone);var basis=data.FindProperty("<BaseStats>k__BackingField");var growth=data.FindProperty("<GrowthData>k__BackingField");
                Field(basis,"MaxHealth").intValue=777;Field(basis,"MinAttack").floatValue=123;Field(basis,"MaxAttack").floatValue=234;
                Field(growth,"MaxHealth").floatValue=17;Field(growth,"MinAttack").floatValue=23;
                var curve=Field(growth,"HealthCurve");
                for(var i=0;i<curve.arraySize;i++)Field(curve.GetArrayElementAtIndex(i),"MaxHealth").intValue=888+i;
                data.ApplyModifiedPropertiesWithoutUndo();var before=EditorJsonUtility.ToJson(clone);
                for(var pass=0;pass<2;pass++)
                {
                    Godmist.Editor.EnemyScalingBalanceSetup.ApplyToDefinition(clone);
                    Godmist.Editor.EnemyRoleDurabilitySetup.ApplyToDefinition(clone);
                    Godmist.Editor.EnemyRoleStatsSetup.ApplyToDefinition(clone);
                    Assert.That(EditorJsonUtility.ToJson(clone),Is.EqualTo(before),id+" old authoring must not override canonical data");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }
    }
}
