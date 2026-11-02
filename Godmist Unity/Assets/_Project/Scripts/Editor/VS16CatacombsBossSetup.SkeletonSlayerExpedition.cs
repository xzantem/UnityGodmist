using System;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static partial class VS16CatacombsBossSetup
    {
        public const string SlayerBagPath=Loot+"ID_Catacombs_SkeletonSlayerBag.asset";
        public const string SlayerBonePath=Loot+"ID_SkeletonSlayerBone.asset";
        public const string SlayerDungeonPath=Root+"Dungeons/DD_Catacombs_SkeletonSlayerExpedition.asset";
        public const string SlayerLootCatalogPath=Root+"Dungeons/DLC_Catacombs_SkeletonSlayer_Loot.asset";
        public const string SlayerDungeonId="dungeon.catacombs.skeleton_slayer_expedition";
        public const string SlayerBagId="item.lootbag.catacombs.skeleton_slayer";
        public const string SlayerBoneId="item.material.skeleton_slayer_bone";
        static T SlayerCopy<T>(T source,string path) where T:ScriptableObject
        {
            var result=GetOrCreate<T>(path);EditorUtility.CopySerialized(source,result);
            result.name=System.IO.Path.GetFileNameWithoutExtension(path);return result;
        }
        [MenuItem("Godmist/Content/Bosses 2.0/Configure Skeleton Slayer Expedition And Loot")]
        public static void ConfigureSkeletonSlayerExpedition()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var kingBag=Require<LootBagItemDefinition>(Loot+"ID_Catacombs_DeadKingBag.asset");
            var ectoplasm=Require<ItemDefinition>(Loot+"ID_DeadKingEctoplasm.asset");
            // Same economic/material profile as the replaced boss material. Reuse shipped bone art.
            var bone=SlayerCopy(ectoplasm,SlayerBonePath);var data=new SerializedObject(bone);
            Field(data,"Id",SlayerBoneId);Field(data,"DisplayName","Kość Zabójcy");
            Field(data,"Description","Kość pozostała po pokonaniu Szkieleta Zabójcy.");
            Field(data,"Icon",Require<ItemDefinition>(Root+"Items/Materials/Alchemy/ID_SkeletonBone.asset").Icon);Save(data);
            var bag=SlayerCopy(kingBag,SlayerBagPath);data=new SerializedObject(bag);
            Field(data,"Id",SlayerBagId);Field(data,"DisplayName","Torba Szkieleta Zabójcy");
            Field(data,"Description","Łupy wyniesione z komnaty Szkieleta Zabójcy.");
            var replacements=0;var brackets=data.FindProperty(F("Brackets"));
            for(var i=0;i<brackets.arraySize;i++)
            {
                var pools=brackets.GetArrayElementAtIndex(i).FindPropertyRelative(F("Pools"));
                for(var j=0;j<pools.arraySize;j++)
                {
                    var entries=pools.GetArrayElementAtIndex(j).FindPropertyRelative(F("Entries"));
                    for(var k=0;k<entries.arraySize;k++)
                    {
                        var item=entries.GetArrayElementAtIndex(k).FindPropertyRelative(F("ItemDefinition"));
                        if(item.objectReferenceValue!=ectoplasm)continue;
                        item.objectReferenceValue=bone;replacements++;
                    }
                }
            }
            if(replacements==0)throw new InvalidOperationException("King bag no longer contains ectoplasm; review source before configuring Slayer loot.");
            // Ordinary pools retain King's economics; approved Slayer uniques are a separate pool.
            var uniqueDefinitions = data.FindProperty(F("BossUniqueDrop")).FindPropertyRelative(F("EquipmentDefinitions"));
            uniqueDefinitions.ClearArray();
            foreach (var unique in ConfigureSlayerUniques()) Add(uniqueDefinitions).objectReferenceValue = unique;
            BossGemLootSetup.ConfigureBossBag(data, false);
            Save(data);
            var kingDungeon=Require<DungeonDefinition>(Root+"Dungeons/DD_Catacombs_BossExpedition.asset");
            var sourceLoot=kingDungeon.LootTableCatalog;
            var lootCatalog=SlayerCopy(sourceLoot,SlayerLootCatalogPath);data=new SerializedObject(lootCatalog);
            var lootEntries=data.FindProperty(F("Entries"));
            for(var i=0;i<sourceLoot.Entries.Count;i++)
            {
                var source=sourceLoot.Entries[i];if(source.BossLootTable==null)continue;
                var table=SlayerCopy(source.BossLootTable,Loot+"LT_Catacombs_SkeletonSlayer_"+source.Bracket+"_Production.asset");
                var tableData=new SerializedObject(table);var entries=tableData.FindProperty(F("Entries"));var bagCount=0;
                for(var j=0;j<entries.arraySize;j++)
                {
                    var item=entries.GetArrayElementAtIndex(j).FindPropertyRelative(F("ItemDefinition"));
                    if(item.objectReferenceValue!=kingBag)continue;item.objectReferenceValue=bag;bagCount++;
                }
                if(bagCount==0)throw new InvalidOperationException("King boss table lacks its bag: "+source.Bracket);
                Save(tableData);Sub(lootEntries.GetArrayElementAtIndex(i),"BossLootTable",table);
            }
            Save(data);
            foreach(var path in new[]{SlayerPath,SlayerPhasePath})
            {
                // Boss loot is owned by the dungeon bracket. A species table here
                // would be rolled AGAIN by reward preparation and duplicate the bag.
                var bossData=new SerializedObject(Require<CharacterDefinition>(path));Field(bossData,"LootTable",null);Save(bossData);
            }
            // Own room/template catalog: retain the original geometry, replace only the final-room identity.
            var roomCatalog=SlayerCopy(kingDungeon.RoomTemplateCatalog,Root+"Dungeons/RoomTemplates/DRTC_Catacombs_SkeletonSlayer.asset");
            data=new SerializedObject(roomCatalog);Field(data,"Id","rooms.catacombs.skeleton_slayer");
            var templates=data.FindProperty(F("Templates"));
            for(var i=0;i<kingDungeon.RoomTemplateCatalog.Templates.Count;i++)
            {
                var original=kingDungeon.RoomTemplateCatalog.Templates[i];if(original==null || original.RoomType!=DungeonRoomType.Final)continue;
                var room=SlayerCopy(original,Root+"Dungeons/RoomTemplates/DRT_Catacombs_SkeletonSlayer_Final_"+i+".asset");
                var roomData=new SerializedObject(room);Field(roomData,"Id","room.catacombs.skeleton_slayer.final."+i);
                Field(roomData,"DisplayName","Komnata Szkieleta Zabójcy");Save(roomData);templates.GetArrayElementAtIndex(i).objectReferenceValue=room;
            }
            Save(data);
            var dungeon=SlayerCopy(kingDungeon,SlayerDungeonPath);data=new SerializedObject(dungeon);
            Field(data,"Id",SlayerDungeonId);Field(data,"DisplayName","Katakumby — Szkielet Zabójca");
            Field(data,"BossEncounterDefinition",Require<EncounterDefinition>(SlayerEncounterPath));
            Field(data,"LootTableCatalog",lootCatalog);Field(data,"RoomTemplateCatalog",roomCatalog);Field(data,"IsAvailableAsOffer",true);Save(data);
            var catalog=new SerializedObject(Require<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset"));
            AddDistinct(catalog.FindProperty(F("ItemDefinitions")),bone);AddDistinct(catalog.FindProperty(F("ItemDefinitions")),bag);
            AddDistinct(catalog.FindProperty(F("DungeonDefinitions")),dungeon);Save(catalog);
            foreach (var unique in bag.BossUniqueDrop.EquipmentDefinitions) AddDistinct(catalog.FindProperty(F("ItemDefinitions")), unique);
            Save(catalog);
            AssetDatabase.SaveAssets();Debug.Log("SKELETON_SLAYER_EXPEDITION_CONFIGURED: King ordinary pools, own Slayer uniques, "+replacements+" material replacements.");
        }
    }
}
