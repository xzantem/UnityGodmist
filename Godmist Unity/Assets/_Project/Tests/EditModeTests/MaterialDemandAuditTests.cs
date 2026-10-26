using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class MaterialDemandAuditTests
    {
        [Serializable] public sealed class Entry
        {
            public string path, guid, type, json;
            public bool active;
        }
        [Serializable] public sealed class Snapshot
        {
            public string utc;
            public List<Entry> assets = new();
        }
        [Test, Explicit("Read-only snapshot of production recipes, parts, raw materials, loot and specialist costs.")]
        public void ExportRecipesAndSupply()
        {
            const string root="Assets/_Project/ScriptableObjects";
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(root+"/World/GC_MainContentCatalog.asset");
            Assert.That(catalog,Is.Not.Null);
            var active=new HashSet<string>(catalog.ItemDefinitions.Where(x=>x!=null).Select(AssetDatabase.GetAssetPath)
                .Concat(catalog.EquipmentPartDefinitions.Where(x=>x!=null).Select(AssetDatabase.GetAssetPath))
                .Concat(catalog.CharacterClasses.Where(x=>x!=null).Select(AssetDatabase.GetAssetPath))
                .Concat(catalog.NpcDefinitions.Where(x=>x!=null).Select(AssetDatabase.GetAssetPath)));
            var snapshot=new Snapshot{utc=DateTime.UtcNow.ToString("O")};
            var types=new[]{typeof(ItemDefinition),typeof(EquipmentPartDefinition),typeof(JewelryGemDefinition),
                typeof(EnchanterRoleData),typeof(BlacksmithRoleData),typeof(DungeonLootTableCatalog),typeof(NpcDefinition),
                typeof(GameContentCatalog),typeof(CharacterClassData)};
            var seen=new HashSet<string>();
            foreach(var type in types) foreach(var guid in AssetDatabase.FindAssets("t:"+type.Name,new[]{root}).OrderBy(x=>x))
            {
                if(!seen.Add(guid))continue;
                var path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if(asset==null)continue;
                snapshot.assets.Add(new Entry{path=path,guid=guid,type=asset.GetType().Name,
                    active=active.Contains(path),json=EditorJsonUtility.ToJson(asset,true)});
            }
            var folder="../LocalHandoffs/material-demand-20261025";Directory.CreateDirectory(folder);
            File.WriteAllText(folder+"/snapshot.json",JsonUtility.ToJson(snapshot,true));
            Assert.That(snapshot.assets.Count,Is.GreaterThan(200));
        }
    }
}
