using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;
namespace Godmist.Editor
{
    public static partial class VS16CatacombsBossSetup
    {
        static UniqueEquipmentDefinition[] BanditUniques()
        {
            const string folder=Root+"Equipment/BossUnique/EID_BanditLeader_";
            var armor=ConfigureUnique(folder+"WarriorArmor.asset","unique.bandit_leader.armor.warrior","Kolczuga Dowódcy","EID_WarriorHauberk",
                PassiveEffectOperationType.OrderActionCostReduction,"",new[]{.35f,.40f},v=>$"Koszt PA rozkazów: −{100*v:0}%.");
            var dagger=ConfigureUnique(folder+"ScoutDagger.asset","unique.bandit_leader.dagger.scout","Sztylet z Kłami","EID_ScoutSwordAndDagger",
                PassiveEffectOperationType.ApplyStatus,Godmist.Core.Constants.StatusEffectIds.Bleed,new[]{.30f,.33f},v=>$"Trafienie krytyczne: {v*100:0}% szansy na krwawienie przez 3 tury.");
            var shield=ConfigureUnique(folder+"PaladinArmor.asset","unique.bandit_leader.armor.paladin","Gruboskórność Kłusownika","EID_PaladinPlateArmor",
                PassiveEffectOperationType.LowHealthShield,"",new[]{.20f,.23f},v=>$"Przy niskim HP: tarcza {v*100:0}% maks. HP na 5 tur. Odnowienie: 5 własnych tur.");
            for(var i=0;i<2;i++)
            {
                var tier=i==0?"Effects":"EffectsLevel20";
                var d=new SerializedObject(dagger);var e=d.FindProperty(F(tier)).GetArrayElementAtIndex(0);
                Sub(e.FindPropertyRelative(F("Triggers")).GetArrayElementAtIndex(0),"Type",PassiveEffectTriggerType.OnCriticalHit);
                var op=e.FindPropertyRelative(F("Operations")).GetArrayElementAtIndex(0);Sub(op,"DurationTurns",3);
                var parameter=Add(op.FindPropertyRelative(F("Parameters")));Sub(parameter,"Key","status_damage_multiplier");Sub(parameter,"NumberValue",i==0?.50f:.55f);Save(d);
                d=new SerializedObject(shield);e=d.FindProperty(F(tier)).GetArrayElementAtIndex(0);op=e.FindPropertyRelative(F("Operations")).GetArrayElementAtIndex(0);Sub(op,"DurationTurns",5);
                parameter=Add(op.FindPropertyRelative(F("Parameters")));Sub(parameter,"Key","health_threshold");Sub(parameter,"NumberValue",i==0?.30f:.29f);
                var text=$"Poniżej {(i==0?30:29)}% HP: tarcza {(i==0?20:23)}% maks. HP na 5 tur. Odnowienie: 5 własnych tur, dowolnie wiele aktywacji.";
                Sub(e,"Description",text);Sub(e.FindPropertyRelative(F("Tooltip")),"DescriptionOverride",text);Save(d);
            }
            var cloak=ConfigureUnique(SmugglerCloakPath,"unique.bandit_leader.armor.mage","Płaszcz Przemytnika","EID_MageRobe",
                PassiveEffectOperationType.ReduceIncomingDamage,Godmist.Systems.Combat.UniqueEquipmentCombatRules.PhysicalRanged,
                new[]{.30f,.35f,.40f,.45f,.55f},v=>$"Obrażenia fizyczne od ataków dystansowych: −{v*100:0}%.");
            return new[]{armor,dagger,cloak,shield};
        }
        public static void ConfigureBanditLeaderLoot()
        {
            var uniques=BanditUniques();
            var source=Require<LootBagItemDefinition>(BanditRoot+"Loot/ID_Forest_EntBag.asset");
            var spirit=source.Brackets.SelectMany(b=>b.Pools).SelectMany(p=>p.Entries).Select(e=>e.ItemDefinition).First(i=>i.Id=="item.alchemy.forest_spirit");
            var blood=SlayerCopy(Require<ItemDefinition>(SlayerBonePath),BanditRoot+"Loot/ID_BanditLeaderBlood.asset");
            var d=new SerializedObject(blood);Field(d,"Id","item.material.bandit_leader_blood");Field(d,"DisplayName","Krew przywódcy");Field(d,"Description","Krew pokonanego Przywódcy Bandytów.");
            Field(d,"BaseRarity",RarityTier.Ancient);Field(d,"BaseValue",6258);
            Field(d,"Icon",Require<Sprite>("Assets/_Project/Art/Icons/Statuses/VS22/bleed-32.png"));Save(d);
            var bag=SlayerCopy(source,BanditBagPath);d=new SerializedObject(bag);Field(d,"Id","item.lootbag.forest.bandit_leader");Field(d,"DisplayName","Torba Przywódcy Bandytów");Field(d,"Description","Łupy wyniesione z obozowiska bandytów.");
            var brackets=d.FindProperty(F("Brackets"));
            for(var i=0;i<brackets.arraySize;i++)
            {
                var pools=brackets.GetArrayElementAtIndex(i).FindPropertyRelative(F("Pools"));
                for(var j=0;j<pools.arraySize;j++)
                {
                    var pool=pools.GetArrayElementAtIndex(j);var entries=pool.FindPropertyRelative(F("Entries"));
                    for(var k=0;k<entries.arraySize;k++)
                    {
                        var entry=entries.GetArrayElementAtIndex(k);var item=entry.FindPropertyRelative(F("ItemDefinition"));
                        if(item.objectReferenceValue!=spirit)continue;
                        item.objectReferenceValue=blood;Sub(pool,"DropChance",.75f);var amount=entry.FindPropertyRelative(F("Amount"));Raw(amount,"Min",1);Raw(amount,"Max",1);
                    }
                }
            }
            BossGemLootSetup.ConfigureBossBag(d, true);
            var unique=d.FindProperty(F("BossUniqueDrop"));Sub(unique,"DropChance",.30f);var definitions=unique.FindPropertyRelative(F("EquipmentDefinitions"));definitions.ClearArray();
            foreach(var item in uniques)Add(definitions).objectReferenceValue=item;Save(d);
            var ent=Require<DungeonDefinition>(ForestExpeditionsSetup.BossPath);
            var loot=SlayerCopy(ent.LootTableCatalog,BanditRoot+"Loot/DLC_BanditLeader_Loot.asset");d=new SerializedObject(loot);var rows=d.FindProperty(F("Entries"));
            for(var i=0;i<ent.LootTableCatalog.Entries.Count;i++)
            {
                var original=ent.LootTableCatalog.Entries[i];if(original.BossLootTable==null)continue;
                var table=SlayerCopy(original.BossLootTable,BanditRoot+"Loot/LT_BanditLeader_"+original.Bracket+".asset");var td=new SerializedObject(table);var entries=td.FindProperty(F("Entries"));
                for(var j=0;j<entries.arraySize;j++)
                {var item=entries.GetArrayElementAtIndex(j).FindPropertyRelative(F("ItemDefinition"));if(item.objectReferenceValue==source)item.objectReferenceValue=bag;}
                Save(td);Sub(rows.GetArrayElementAtIndex(i),"BossLootTable",table);
            }
            Save(d);
            var rooms=SlayerCopy(ent.RoomTemplateCatalog,BanditRoot+"DRTC_BanditLeader.asset");d=new SerializedObject(rooms);Field(d,"Id","rooms.forest.bandit_leader");var templates=d.FindProperty(F("Templates"));
            for(var i=0;i<rooms.Templates.Count;i++)
            {
                var original=rooms.Templates[i];if(original==null || original.RoomType!=DungeonRoomType.Final)continue;
                var room=SlayerCopy(original,BanditRoot+"DRT_BanditLeader_Final_"+i+".asset");var rd=new SerializedObject(room);Field(rd,"Id","room.forest.bandit_leader.final."+i);Field(rd,"DisplayName","Obóz Przywódcy Bandytów");Save(rd);templates.GetArrayElementAtIndex(i).objectReferenceValue=room;
            }
            Save(d);
            var dungeon=SlayerCopy(ent,BanditDungeonPath);d=new SerializedObject(dungeon);Field(d,"Id","dungeon.forest.bandit_leader_expedition");Field(d,"DisplayName","Skażony Las — Przywódca Bandytów");
            Field(d,"BossEncounterDefinition",Require<Godmist.Data.Combat.EncounterDefinition>(BanditEncounterPath));Field(d,"LootTableCatalog",loot);Field(d,"RoomTemplateCatalog",rooms);Save(d);
            var catalog=new SerializedObject(Require<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset"));
            const string obsoletePath=BanditRoot+"Loot/ID_BanditLeaderSignet.asset";
            var obsolete=AssetDatabase.LoadAssetAtPath<ItemDefinition>(obsoletePath);
            var items=catalog.FindProperty(F("ItemDefinitions"));
            if(obsolete!=null && obsolete.Id=="item.material.bandit_leader_signet")
                for(var i=items.arraySize-1;i>=0;i--)
                    if(items.GetArrayElementAtIndex(i).objectReferenceValue==obsolete)
                    {items.GetArrayElementAtIndex(i).objectReferenceValue=null;items.DeleteArrayElementAtIndex(i);}
            foreach(var item in uniques.Cast<ItemDefinition>().Concat(new[]{blood,bag}))AddDistinct(items,item);
            AddDistinct(catalog.FindProperty(F("DungeonDefinitions")),dungeon);Save(catalog);
            if(obsolete!=null && obsolete.Id=="item.material.bandit_leader_signet")AssetDatabase.DeleteAsset(obsoletePath);
            AssetDatabase.SaveAssets();
        }
    }
}
