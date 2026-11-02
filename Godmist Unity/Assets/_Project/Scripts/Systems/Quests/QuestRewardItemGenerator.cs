using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Data.Items;
using Godmist.Data.Quests;
using Godmist.Runtime.Items;
using Godmist.Systems.Items;
namespace Godmist.Systems.Quests
{
    public static class QuestRewardItemGenerator
    {
        public static List<ItemInstance> Generate(QuestRewardDefinition reward, int questLevel,
            GameContentCatalog catalog, Random random, ItemInstanceFactory factory)
        {
            if(reward?.ItemDefinition==null || reward.Amount<1 || questLevel<1 || questLevel>50)
                throw new InvalidOperationException("Invalid item reward amount, level or definition.");
            var result=new List<ItemInstance>();
            var loot=new LootTableSystem(random,catalog?.EquipmentPartDefinitions,factory,questLevel);
            var equipmentGenerator=new EquipmentGenerationService(random,factory);
            IReadOnlyList<EquipmentPartDefinition> parts=null;
            if(reward.ItemDefinition is EquipmentItemDefinition equipment)
            {
                var kinds=EquipmentGenerationService.GetRequiredKinds(equipment);
                if(kinds==null || equipment.RequiredClass==null || catalog?.EquipmentPartDefinitions==null)
                    throw new InvalidOperationException("Item reward has no equipment generation configuration.");
                var pools=catalog.EquipmentPartDefinitions.Where(part=>part!=null && part.RequiredClass==equipment.RequiredClass
                        && part.IsStarterOnly==equipment.IsStarterEquipment && part.Stats!=null && !string.IsNullOrWhiteSpace(part.Id))
                    .GroupBy(part=>part.LevelBracket).Where(group=>kinds.All(kind=>group.Any(part=>part.Kind==kind)))
                    .OrderBy(group=>Math.Abs(Math.Max(1,(int)group.Key+EquipmentGenerationService.GetQualityLevelAdjustment(reward.EquipmentQuality))-questLevel))
                    .ThenBy(group=>(int)group.Key).ToList();
                if(pools.Count==0) throw new InvalidOperationException("No complete equipment part tier for quest reward.");
                parts=pools[0].ToList();
            }
            if(reward.ItemDefinition is LootBagItemDefinition bag
                && !bag.TryGetBracket(LootLevelBracketResolver.Resolve(questLevel),out _))
                throw new InvalidOperationException("No loot bag pool for quest level.");
            for(var i=0;i<reward.Amount;i++)
            {
                ItemInstance item;
                if(reward.ItemDefinition is EquipmentItemDefinition definition)
                {
                    var kinds=EquipmentGenerationService.GetRequiredKinds(definition);
                    var selected=kinds.Select(kind=>parts.Where(part=>part.Kind==kind).ToList())
                        .Select(pool=>pool[random.Next(pool.Count)]).ToList();
                    item=equipmentGenerator.GenerateFromSelectedParts(definition,selected,reward.EquipmentQuality,reward.EquipmentRarity);
                }
                else item=loot.GenerateSingleItem(reward.ItemDefinition);
                if(item==null) throw new InvalidOperationException("Cannot generate quest reward: "+reward.ItemDefinition.Id);
                result.Add(item);
            }
            return result;
        }
    }
}
