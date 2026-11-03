using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Serialization;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Data.World;
using Godmist.Data.Characters;

namespace Godmist.Systems.SaveLoad
{
    public static partial class CombatPersistence
    {
        static CombatUnitSaveData CaptureUnit(BattleUnit unit,GameSession game,CaptureCharacter character)
        {
            var s=CaptureScalars(unit);s.Id=Id(unit);s.DefinitionId=unit.Character.Definition.Id;s.Position=Point(unit.Position);
            s.CurrentHealth=unit.Character.CurrentHealth;s.Resource=GameSessionSaveSnapshotFactory.CopyResourcePool(unit.Character.ResourcePool);
            s.BaseResistances=GameSessionSaveSnapshotFactory.CopyResistances(unit.Character.BaseResistances);
            s.CurrentResistances=GameSessionSaveSnapshotFactory.CopyResistances(unit.Character.Resistances);
            if(!game.CharactersById.TryGetValue(s.Id,out var party)||!ReferenceEquals(party,unit.Character))
                { s.HasOwnCharacter=true; s.Character=character(unit.Character); }
            s.Stats=Enum.GetValues(typeof(CharacterStatType)).Cast<CharacterStatType>().Select(type=>new CombatStatSaveData
                {Type=type,Base=unit.Character.Stats.GetStat(type).BaseValue,Effective=unit.Character.Stats.GetStat(type).GetValue()}).ToList();
            s.UniqueBuffs=unit.UniqueBuffs.ToList();s.ActiveToggles=unit.ActiveToggleSkillIds.ToList();
            s.UsedToggles=unit.ToggleSkillIdsUsedThisTurn.ToList();s.UsedConsumables=unit.ConsumableUseTypesUsedThisTurn.ToList();
            s.Resistances=unit.ResistanceModifiers.Select(p=>new CombatFloatEntrySaveData {Id=p.Key.ToString(),Value=p.Value}).ToList();
            s.Tolerances=unit.StatusTolerances.Select(p=>new CombatIntEntrySaveData
                {Id=p.Key,Value=p.Value.Stacks,ExtraValue=p.Value.RemainingTurns}).ToList();
            s.Auras=unit.ActiveAuras.Select(a=>new CombatAuraSaveData {AuraId=a.AuraId,DisplayName=a.DisplayName,StatusId=a.StatusId,
                CreatorId=Id(a.Creator),RemainingTurns=a.RemainingTurns,Radius=a.Radius,DurationTurns=a.DurationTurns,
                StatusDamagePerTurn=a.StatusDamagePerTurn,IncludesCaster=a.IncludesCaster,TargetingType=a.TargetingType,
                Chance=a.Chance,StatusDamageMultiplier=a.StatusDamageMultiplier}).ToList();
            s.Redirects=unit.OutgoingDamageRedirects.Select(a=>new CombatRedirectSaveData {TargetId=Id(a.Target),
                SourceEffectId=a.SourceEffectId,TargetCharacterId=a.TargetCharacterId,TargetDisplayName=a.TargetDisplayName,Percent=a.RedirectPercent}).ToList();
            s.Scout=CaptureScalars(unit.ScoutState);s.Scout.FacingX=unit.ScoutState.Facing.x;s.Scout.FacingY=unit.ScoutState.Facing.y;
            s.Scout.Anger=unit.ScoutState.AngerBySource.Select(p=>new CombatIntEntrySaveData {Id=Id(p.Key),Value=p.Value}).ToList();
            s.Scout.Knives=unit.ScoutState.Knives.Select(p=>new CombatFloatEntrySaveData {Id=Id(p.Owner),Value=p.BleedMultiplier}).ToList();
            s.Warrior=CaptureScalars(unit.WarriorState);s.Warrior.OrderSourceId=Id(unit.WarriorState.OrderSource);
            s.Warrior.ProtectorSourceId=Id(unit.WarriorState.ProtectorSource);
            s.Mage=CaptureScalars(unit.MageState);
            s.Mage.Adaptation=unit.MageState.Adaptation.Select(p=>new CombatIntEntrySaveData
                {Id=p.Key,Value=p.Value.RemainingHits,ExtraValue=p.Value.Reduction}).ToList();
            s.Mage.FireErosion=unit.MageState.FireErosion.Select(p=>new CombatIntEntrySaveData {Id=p.Key,Value=p.Value}).ToList();
            s.Mage.HasTiringAttempts=unit.MageState.TiringAttempts!=null;
            s.Mage.TiringAttempts=unit.MageState.TiringAttempts?.Select(p=>new CombatStringSetSaveData {Id=p.Key,Values=p.Value.ToList()}).ToList();
            s.Paladin=CaptureScalars(unit.PaladinState);
            if(unit.ForestEntState is {} ent)
            {
                s.HasEnt=true;s.Ent=CaptureScalars(ent);s.Ent.OriginalDefinitionId=ent.OriginalDefinition.Id;
                s.Ent.Fields=ent.Fields.Select(Point).ToList();s.Ent.Markers=ent.Markers.Select(m=>m.ObjectId).ToList();
                s.Ent.PeriodicHits=ent.PeriodicHits.Select(Id).ToList();s.Ent.FireHits=ent.FireHits.Select(Id).ToList();
                s.Ent.Plague=ent.Plague.Select(p=>new CombatIntEntrySaveData {Id=Id(p.Key),Value=p.Value}).ToList();
                s.Ent.ActionHits=ent.ActionHits.GroupBy(p=>p.Item1).Select(p=>new CombatStringSetSaveData {Id=p.Key,Values=p.Select(v=>Id(v.Item2)).ToList()}).ToList();
            }
            if(unit.DeadKingState is {} king)
            {
                s.HasKing=true;s.King=CaptureScalars(king);s.King.OriginalDefinitionId=king.OriginalDefinition.Id;
                s.King.SafeFields=king.SafeFields.Select(Point).ToList();s.King.DarkFields=king.DarkFields.Select(Point).ToList();
                s.King.Markers=king.Markers.Select(m=>m.ObjectId).ToList();
            }
            if(unit.SkeletonSlayerState is {} slayer)
            {
                s.HasSlayer=true;s.Slayer=CaptureScalars(slayer);s.Slayer.OriginalDefinitionId=slayer.OriginalDefinition.Id;
                s.Slayer.HasSelectedHammerCenter=slayer.SelectedHammerCenter.HasValue;s.Slayer.HammerCenter=Point(slayer.HammerCenter);s.Slayer.SelectedHammerCenter=slayer.SelectedHammerCenter.HasValue?Point(slayer.SelectedHammerCenter.Value):null;
                s.Slayer.HammerFields=slayer.HammerFields.Select(Point).ToList();s.Slayer.Markers=slayer.Markers.Select(m=>m.ObjectId).ToList();
            }
            if(unit.BanditLeaderState is {} bandit)
            {
                s.HasBandit=true;s.Bandit=CaptureScalars(bandit);s.Bandit.OriginalDefinitionId=bandit.OriginalDefinition.Id;
                s.Bandit.Guards=bandit.Guards.Select(Id).ToList();
            }
            return s;
        }

        static void RestoreUnit(CombatUnitSaveData s,BattleUnit unit,CombatSession combat,Func<string,BattleUnit> actor,
            Func<string,SummonedCombatMapObject> obj,GameContentCatalog catalog)
        {
            CharacterDefinition Definition(string id)
            {Require(catalog.TryGetCharacter(id,out var definition),"Boss/OriginalDefinitionId");return definition;}
            unit.UniqueBuffs=s.UniqueBuffs.ToList();unit.ActiveToggleSkillIds.UnionWith(s.ActiveToggles);
            unit.ToggleSkillIdsUsedThisTurn.UnionWith(s.UsedToggles);unit.ConsumableUseTypesUsedThisTurn.UnionWith(s.UsedConsumables);
            foreach(var pair in s.Resistances)
            {Require(Enum.TryParse(pair.Id,out ResistanceType type)&&Enum.IsDefined(typeof(ResistanceType),type)&&float.IsFinite(pair.Value),"ResistanceModifiers");unit.ResistanceModifiers.Add(type,pair.Value);}
            foreach(var pair in s.Tolerances) unit.StatusTolerances.Add(pair.Id,new StatusToleranceRuntime
                {Stacks=pair.Value,RemainingTurns=pair.ExtraValue});
            unit.ActiveAuras=s.Auras.Select(a=>new CombatAuraRuntime {AuraId=a.AuraId,DisplayName=a.DisplayName,StatusId=a.StatusId,
                Creator=actor(a.CreatorId),RemainingTurns=a.RemainingTurns,Radius=a.Radius,DurationTurns=a.DurationTurns,
                StatusDamagePerTurn=a.StatusDamagePerTurn,IncludesCaster=a.IncludesCaster,TargetingType=a.TargetingType,
                Chance=a.Chance,StatusDamageMultiplier=a.StatusDamageMultiplier}).ToList();
            unit.OutgoingDamageRedirects=s.Redirects.Select(a=>new CombatDamageRedirectRuntime {Target=actor(a.TargetId),
                SourceEffectId=a.SourceEffectId,TargetCharacterId=a.TargetCharacterId,TargetDisplayName=a.TargetDisplayName,RedirectPercent=a.Percent}).ToList();
            RestoreScalars(s.Scout,unit.ScoutState);unit.ScoutState.Facing=new(s.Scout.FacingX,s.Scout.FacingY);
            foreach(var p in s.Scout.Anger)unit.ScoutState.AngerBySource.Add(actor(p.Id),p.Value);
            foreach(var p in s.Scout.Knives)unit.ScoutState.Knives.Add(new EmbeddedScoutKnife {Owner=actor(p.Id),BleedMultiplier=p.Value});
            RestoreScalars(s.Warrior,unit.WarriorState);unit.WarriorState.OrderSource=actor(s.Warrior.OrderSourceId);
            unit.WarriorState.ProtectorSource=actor(s.Warrior.ProtectorSourceId);unit.WarriorState.Session=combat;
            RestoreScalars(s.Mage,unit.MageState);
            foreach(var p in s.Mage.Adaptation)unit.MageState.Adaptation.Add(p.Id,new MageAdaptation
                {RemainingHits=p.Value,Reduction=p.ExtraValue});
            foreach(var p in s.Mage.FireErosion)unit.MageState.FireErosion.Add(p.Id,p.Value);
            unit.MageState.TiringAttempts=!s.Mage.HasTiringAttempts?null:s.Mage.TiringAttempts.ToDictionary(p=>p.Id,p=>new HashSet<string>(p.Values));
            RestoreScalars(s.Paladin,unit.PaladinState);
            if(s.HasEnt && s.Ent is {} ent)
            {
                var state=unit.ForestEntState=new ForestEntCombatState {OriginalDefinition=Definition(ent.OriginalDefinitionId)};
                RestoreScalars(ent,state);state.Fields.UnionWith(ent.Fields.Select(Point));state.Markers.AddRange(ent.Markers.Select(obj));
                state.PeriodicHits.UnionWith(ent.PeriodicHits.Select(actor));state.FireHits.UnionWith(ent.FireHits.Select(actor));
                foreach(var p in ent.Plague)state.Plague.Add(actor(p.Id),p.Value);
                foreach(var p in ent.ActionHits)foreach(var id in p.Values)state.ActionHits.Add((p.Id,actor(id)));
            }
            if(s.HasKing && s.King is {} king)
            {
                var state=unit.DeadKingState=new DeadKingCombatState {OriginalDefinition=Definition(king.OriginalDefinitionId)};
                RestoreScalars(king,state);state.SafeFields.UnionWith(king.SafeFields.Select(Point));state.DarkFields.UnionWith(king.DarkFields.Select(Point));
                state.Markers.AddRange(king.Markers.Select(obj));
            }
            if(s.HasSlayer && s.Slayer is {} slayer)
            {
                var state=unit.SkeletonSlayerState=new SkeletonSlayerCombatState {OriginalDefinition=Definition(slayer.OriginalDefinitionId)};
                RestoreScalars(slayer,state);state.HammerCenter=Point(slayer.HammerCenter);
                state.SelectedHammerCenter=!slayer.HasSelectedHammerCenter?null:Point(slayer.SelectedHammerCenter);
                state.HammerFields.UnionWith(slayer.HammerFields.Select(Point));state.Markers.AddRange(slayer.Markers.Select(obj));
            }
            if(s.HasBandit && s.Bandit is {} bandit)
            {
                var state=unit.BanditLeaderState=new BanditLeaderCombatState {OriginalDefinition=Definition(bandit.OriginalDefinitionId),Session=combat};
                RestoreScalars(bandit,state);state.Guards.UnionWith(bandit.Guards.Select(actor));
            }
        }
    }
}
