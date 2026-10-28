using System;
using Godmist.Core.Enums;
using Godmist.Core.Utilities;
using Godmist.Core.Constants;
using Godmist.Data.Configuration;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public readonly struct CombatSkillHealingPreview
    {
        public CombatSkillHealingPreview(int minimum,int maximum,int criticalMinimum,int criticalMaximum,float criticalChance)
        {Minimum=minimum;Maximum=maximum;CriticalMinimum=criticalMinimum;CriticalMaximum=criticalMaximum;CriticalChance=criticalChance;}
        public int Minimum {get;}
        public int Maximum {get;}
        public int CriticalMinimum {get;}
        public int CriticalMaximum {get;}
        public float CriticalChance {get;}
    }

    public readonly struct CombatSkillDamageRange
    {
        public CombatSkillDamageRange(int minimum, int maximum, bool hasDamage)
        {
            Minimum = Mathf.Max(0, minimum);
            Maximum = Mathf.Max(Minimum, maximum);
            HasDamage = hasDamage;
        }

        public int Minimum { get; }
        public int Maximum { get; }
        public bool HasDamage { get; }
        public string ToDisplayString() => Minimum == Maximum ? Minimum.ToString() : $"{Minimum}–{Maximum}";
    }

    public readonly struct CombatSkillTargetPreview
    {
        public CombatSkillTargetPreview(
            CombatSkillDamageRange damage,
            float hitChance,
            bool hasHitChance,
            string statusId = null,
            float statusChance = 0f, int? healing = null)
        {
            Damage = damage;
            HitChance = Mathf.Clamp01(hitChance);
            HasHitChance = hasHitChance;
            StatusId = statusId;
            StatusChance = Mathf.Clamp01(statusChance);
            Healing = healing;
        }

        public CombatSkillDamageRange Damage { get; }
        public float HitChance { get; }
        public bool HasHitChance { get; }
        public string StatusId { get; }
        public float StatusChance { get; }
        public int? Healing { get; }
        public bool HasStatusChance => !string.IsNullOrWhiteSpace(StatusId);
        public bool IsValid => Damage.HasDamage || HasStatusChance || Healing.HasValue;
    }

    /// <summary>
    /// Shared deterministic preview math for combat UI. It deliberately reports a
    /// worst/best-case range for a regular hit: optional bonus multipliers are
    /// included only in the upper bound. Critical damage is intentionally not
    /// part of the displayed range.
    /// </summary>
    public static class CombatSkillPreviewCalculator
    {
        public static int? CalculateHealing(BattleUnit caster, BattleUnit target, SkillData skill, int targetModeIndex = -1)
            => CalculateHealingAt(caster,target,skill,targetModeIndex,0,false,PaladinCombatSystem.IsSingleHealingPreview(caster?.WarriorState.Session,caster,target,skill));

        public static CombatSkillHealingPreview CalculateHealingPreview(CombatSession session,BattleUnit caster,
            BattleUnit target,SkillData skill,int targetModeIndex=-1)
        {
            if (caster?.Character == null || target?.Character == null || skill == null) return default;
            var chance=0f;
            foreach(var effect in skill?.Effects ?? Array.Empty<SkillEffectData>())
                if((targetModeIndex<0||effect.TargetModeIndex==targetModeIndex)
                    && effect.EffectType is EffectType.PaladinHeal or EffectType.PaladinRegeneration)
                {
                    var single=effect.EffectType==EffectType.PaladinRegeneration||skill.SkillId=="skill.paladin.heal"
                        ||session!=null&&System.Linq.Enumerable.Count(session.Units,u=>u.Character?.LifeState==CharacterLifeState.Alive&&u.IsPlayerSide==caster.IsPlayerSide)==1;
                    chance=Mathf.Max(chance,PaladinCombatSystem.HealingCritChance(caster,single));
                }
            var singleHealing=PaladinCombatSystem.IsSingleHealingPreview(session,caster,target,skill);
            return new CombatSkillHealingPreview(
                CalculateHealingAt(caster,target,skill,targetModeIndex,-1,false,singleHealing)??0,
                CalculateHealingAt(caster,target,skill,targetModeIndex,1,false,singleHealing)??0,
                CalculateHealingAt(caster,target,skill,targetModeIndex,-1,true,singleHealing)??0,
                CalculateHealingAt(caster,target,skill,targetModeIndex,1,true,singleHealing)??0,chance);
        }

        private static int? CalculateHealingAt(BattleUnit caster,BattleUnit target,SkillData skill,
            int targetModeIndex,float variation,bool critical,bool? singleTarget=null)
        {
            if (caster?.Character == null || target?.Character == null || target.Character.LifeState == CharacterLifeState.Dead) return null;
            var maximum = Mathf.Max(0, Mathf.RoundToInt(target.Character.Stats.MaxHealth.GetValue()));
            var missing = Mathf.Max(0, maximum - target.Character.CurrentHealth);
            var total = 0;
            var hasHealing = false;
            foreach (var authored in skill?.Effects ?? Array.Empty<SkillEffectData>())
            {
                var effect = PassiveEffectCombatExecutionSystem.ResolveEffect(caster, skill, authored);
                if (effect == null || (targetModeIndex >= 0 && effect.TargetModeIndex != targetModeIndex)) continue;
                int amount;
                switch (effect.EffectType)
                {
                    case EffectType.Heal: amount = Mathf.RoundToInt(CombatRecoverySystem.CalculateSkillHealing(caster,effect)*(singleTarget==true?PaladinCombatSystem.SingleTargetHealingMultiplier(caster):1)); break;
                    case EffectType.HealPercent: amount = Mathf.RoundToInt(Mathf.CeilToInt(maximum*effect.PercentageValue)*(singleTarget==true?PaladinCombatSystem.SingleTargetHealingMultiplier(caster):1)); break;
                    case EffectType.HealMissingHealthPercent: amount = Mathf.RoundToInt(missing * effect.PercentageValue); break;
                    case EffectType.PaladinHeal:
                        amount = Mathf.RoundToInt(PaladinCombatSystem.CalculateHealing(caster, skill, effect,singleTarget)
                            *(1+variation*PaladinCombatSystem.HealingVariation)
                            *(critical?Mathf.Max(1,caster.Character.Stats.CritDamage.GetValue()):1)); break;
                    case EffectType.PaladinRegeneration: amount = PaladinCombatSystem.CalculateRegenerationUpfront(caster, target,critical); break;
                    default: continue;
                }
                hasHealing = true;
                var restored = Mathf.Clamp(amount, 0, missing);
                total += restored; missing -= restored;
            }
            return hasHealing ? total : null;
        }
        public static CombatSkillDamageRange CalculateDamageRange(
            BattleUnit caster,
            SkillData skill)
        {
            var minimum = 0;
            var maximum = 0;
            var hasDamage = false;

            foreach (var baseEffect in skill?.Effects ?? Array.Empty<SkillEffectData>())
            {
                var effect = PassiveEffectCombatExecutionSystem.ResolveEffect(caster, skill, baseEffect);
                if (effect?.EffectType != EffectType.DealDamage)
                {
                    continue;
                }

                var effectRange = CalculateDamageRange(caster, effect, skill);
                minimum += effectRange.Minimum;
                maximum += effectRange.Maximum;
                hasDamage |= effectRange.HasDamage;
            }

            return new CombatSkillDamageRange(minimum, maximum, hasDamage);
        }

        /// <summary>
        /// Calculates the complete damage contribution of one DealDamage effect,
        /// including its hit count and caster-side damage modifiers. This is kept
        /// separate from the skill overload so tooltip lines can describe each
        /// effect without rebuilding a temporary SkillData asset.
        /// </summary>
        public static CombatSkillDamageRange CalculateDamageRange(
            BattleUnit caster,
            SkillEffectData effect, SkillData skill = null)
        {
            if (effect?.EffectType != EffectType.DealDamage)
            {
                return new CombatSkillDamageRange(0, 0, false);
            }

            var range = CalculateEffectDamageRange(caster, effect);
            if (!range.HasDamage)
            {
                return range;
            }

            var mageMultiplier = MageCombatSystem.DamageMultiplier(new PassiveEffectCombatContext(source:caster, skill:skill, skillId:skill?.SkillId));
            if (skill?.SkillId == WarriorDischargeRules.SkillId) mageMultiplier *= WarriorDischargeRules.Multiplier(WarriorDischargeRules.Cost(caster));
            range = new CombatSkillDamageRange(Mathf.RoundToInt(range.Minimum * mageMultiplier), Mathf.RoundToInt(range.Maximum * mageMultiplier), true);
            var hitCount = Mathf.Max(1, effect.DamageHitCount);
            return new CombatSkillDamageRange(
                ApplyCurrentDamageModifiers(caster, null, range.Minimum, skill) * hitCount,
                ApplyCurrentDamageModifiers(caster, null, range.Maximum, skill) * effect.MaximumHitCount,
                true);
        }

        public static CombatSkillDamageRange CalculateDirectDamageRange(BattleUnit source,DamageType type,float coefficient,CombatValueExplanation explanation=null)
        {
            if(source?.Character?.Stats==null)return default;
            explanation?.Add("attack",source.Character.Stats.MinAttack.GetValue().ToString("0.##",UiTerms.PolishCulture)+"–"+source.Character.Stats.MaxAttack.GetValue().ToString("0.##",UiTerms.PolishCulture));
            explanation?.Add("coefficient",coefficient);
            var low=CombatDamageValueCalculator.CalculateDamageValue(source,DamageValueSource.MinAttack,coefficient);
            var high=CombatDamageValueCalculator.CalculateDamageValue(source,DamageValueSource.MaxAttack,coefficient);
            var context=new PassiveEffectCombatContext(source:source,damageType:type);
            var result=new CombatSkillDamageRange(CombatDamageSystem.CalculateIncomingBeforeRedirect(source.WarriorState.Session,source,null,low,type,context,explanation),
                CombatDamageSystem.CalculateIncomingBeforeRedirect(source.WarriorState.Session,source,null,high,type,context),true);
            explanation?.Add("result",result.ToDisplayString());return result;
        }

        // One ordinary hit, before a target's defenses. Random double-damage
        // branches are described separately; only an actual guarantee includes crit.
        public static CombatSkillDamageRange CalculateSingleHitRange(BattleUnit caster,SkillData skill,
            SkillEffectData effect, CombatValueExplanation explanation = null, bool bonusProc = false)
        {
            var range = CalculateEffectDamageRange(caster,effect,bonusProc:bonusProc);
            if (!range.HasDamage) return range;
            explanation?.Add("attack",$"{caster.Character.Stats.MinAttack.GetValue():0.##}–{caster.Character.Stats.MaxAttack.GetValue():0.##}");
            explanation?.Add("coefficient",effect.DamageMultiplier);
            if(bonusProc)explanation?.Add("bonus_damage",effect.BonusDamageMultiplier);
            explanation?.Add("base_rounding",range.ToDisplayString());
            var context = new PassiveEffectCombatContext(source:caster,skill:skill,skillId:skill?.SkillId,
                attackType:skill?.AttackType ?? AttackType.Unknown,damageType:effect.DamageType);
            var mage = MageCombatSystem.DamageMultiplier(context,explanation);
            var scout = ScoutCombatSystem.DamageMultiplier(context,explanation);

            var multiplier = mage * scout;
            if (skill?.SkillId == WarriorDischargeRules.SkillId)
            {
                var spent = Mathf.Max(WarriorDischargeRules.MinimumRage,WarriorDischargeRules.Cost(caster));
                multiplier *= WarriorDischargeRules.Multiplier(spent);
                explanation?.Add("rage_spent",spent);
                explanation?.Add("rage_multiplier",WarriorDischargeRules.Multiplier(spent));
            }
            var guaranteed = skill?.GuaranteedCritical == true || CombatDamageSystem.HasUniqueBuff(caster,UniqueBuffType.GuaranteedCriticalHit);
            if (guaranteed)
            {
                var critical = Mathf.Max(1,caster.Character.Stats.CritDamage.GetValue());
                explanation?.Add("guaranteed_critical",critical);
                // Execution rounds the hero multipliers before applying critical damage.
                range = new CombatSkillDamageRange(Mathf.CeilToInt(Mathf.RoundToInt(range.Minimum*multiplier)*critical),
                    Mathf.CeilToInt(Mathf.RoundToInt(range.Maximum*multiplier)*critical),true);
            }
            else range = new CombatSkillDamageRange(Mathf.RoundToInt(range.Minimum*multiplier),Mathf.RoundToInt(range.Maximum*multiplier),true);
            var theme=caster?.WarriorState.Session;
            range=new CombatSkillDamageRange(CombatDamageSystem.ApplyJewelryDamagePassive(theme,caster,range.Minimum),
                CombatDamageSystem.ApplyJewelryDamagePassive(theme,caster,range.Maximum),true);
            if(StatusEffectSystem.HasStatus(caster,StatusEffectIds.Weakening))
            {
                range=new CombatSkillDamageRange(Mathf.CeilToInt(range.Minimum*.8f),Mathf.CeilToInt(range.Maximum*.8f),true);
                explanation?.Add("weakening",.8f);
            }
            var min = ApplyCurrentDamageModifiers(caster,null,range.Minimum,skill,effect.DamageType,explanation:explanation);
            var max = ApplyCurrentDamageModifiers(caster,null,range.Maximum,skill,effect.DamageType);
            explanation?.Add("result",min==max ? min.ToString() : $"{min}–{max}");
            return new CombatSkillDamageRange(min,max,true);
        }

        public static CombatSkillTargetPreview CalculateTargetPreview(
            BattleUnit caster,
            BattleUnit target,
            SkillData skill,
            int targetModeIndex = -1)
        {
            var guaranteed=skill?.GuaranteedCritical==true||CombatDamageSystem.HasUniqueBuff(caster,UniqueBuffType.GuaranteedCriticalHit);
            var avoidance=Mathf.Clamp01(EquipmentEffectSystem.Sum(target?.Character,GalduryteEffectType.CriticalAvoidance));
            if(!guaranteed||avoidance<=0)return CalculateTargetPreviewInternal(caster,target,skill,targetModeIndex,null);
            var normal=CalculateTargetPreviewInternal(caster,target,skill,targetModeIndex,false);
            if(avoidance>=1)return normal;
            var critical=CalculateTargetPreviewInternal(caster,target,skill,targetModeIndex,true);
            return new CombatSkillTargetPreview(new CombatSkillDamageRange(normal.Damage.Minimum,critical.Damage.Maximum,normal.Damage.HasDamage),
                normal.HitChance,normal.HasHitChance,normal.StatusId,normal.StatusChance,normal.Healing);
        }

        // The UI still shows its regular-hit range. AI compares expected regular
        // and critical hits; both branches use the same defense/modifier math.
        public static float CalculateExpectedTargetDamage(CombatSession session, BattleUnit caster, BattleUnit target,
            SkillData skill, int targetModeIndex, SkillEffectData singleEffect = null)
        {
            if(skill?.Effects==null || caster?.Character==null || target?.Character==null)return 0f;
            var total=0f;
            foreach(var authored in skill.Effects)
            {
                if(authored?.EffectType!=EffectType.DealDamage || authored.TargetModeIndex!=targetModeIndex
                    || singleEffect!=null && authored!=singleEffect)continue;
                var effect=PassiveEffectCombatExecutionSystem.ResolveEffect(caster,skill,authored);
                var crit=CombatDamageSystem.PreviewCriticalChance(session,caster,target,skill,effect.DamageType);
                float Branch(bool critical,bool proc)
                {
                    var preview=CalculateTargetPreviewInternal(caster,target,skill,targetModeIndex,critical,authored,proc,true);
                    return (preview.Damage.Minimum+preview.Damage.Maximum)*.5f*preview.HitChance;
                }
                float WithProc(bool critical)
                {
                    var normal=effect.BonusDamageMultiplierChance<1f?Branch(critical,false):0;
                    return normal*(1f-effect.BonusDamageMultiplierChance)
                        +(effect.BonusDamageMultiplierChance>0f?Branch(critical,true)*effect.BonusDamageMultiplierChance:0);
                }
                var normal=crit<1f?WithProc(false):0;
                total+=(normal*(1f-crit)+(crit>0?WithProc(true)*crit:0))*effect.ExpectedHitCount/effect.DamageHitCount;
            }
            return total;
        }

        // Bounded convolution of independent per-hit crit/proc outcomes. Damage
        // bins round down; optional hits never become guaranteed hits. Three attack
        // samples approximate a variable attack range without consuming game RNG.
        public static float EstimateKillChance(CombatSession session, BattleUnit caster, BattleUnit target,
            SkillData skill, SkillEffectData authored)
        {
            if(skill?.Effects==null || authored==null || caster?.Character==null || target?.Character==null)return 0f;
            var required=target.Character.CurrentHealth+(target.Character.ShieldPool?.TotalShield ?? 0);
            if(required<=0)return 0f;
            var effect=PassiveEffectCombatExecutionSystem.ResolveEffect(caster,skill,authored);
            var bin=Mathf.Max(1,Mathf.CeilToInt(required/128f));var limit=Mathf.CeilToInt(required/(float)bin);
            var crit=CombatDamageSystem.PreviewCriticalChance(session,caster,target,skill,effect.DamageType);
            Span<(int damage,float chance)> outcomes=stackalloc (int,float)[12];
            Span<(int damage,float chance)> laterOutcomes=stackalloc (int,float)[12];
            var firstCount=0;var laterCount=0;
            var hitChance=1f;
            for(var opening=0;opening<2;opening++)
            {
            var result=opening==0?outcomes:laterOutcomes;var count=0;
            for(var critical=0;critical<2;critical++)
            for(var proc=0;proc<2;proc++)
            {
                var hitCrit=opening==0?crit:CombatDamageSystem.PreviewCriticalChance(session,caster,target,skill,effect.DamageType,ignoreConsumedGuaranteedCritical:true);
                var chance=(critical==1?hitCrit:1f-hitCrit)*(proc==1?effect.BonusDamageMultiplierChance:1f-effect.BonusDamageMultiplierChance);
                if(chance<=0)continue;
                var preview=CalculateTargetPreviewInternal(caster,target,skill,effect.TargetModeIndex,critical==1,authored,proc==1,true,true,opening==0);
                hitChance=preview.HitChance;var range=preview.Damage;
                if(range.Minimum==range.Maximum)result[count++]=(Mathf.FloorToInt(range.Minimum/(float)bin),chance);
                else
                {
                    result[count++]=(Mathf.FloorToInt(range.Minimum/(float)bin),chance*.25f);
                    result[count++]=(Mathf.FloorToInt((range.Minimum+range.Maximum)*.5f/bin),chance*.5f);
                    result[count++]=(Mathf.FloorToInt(range.Maximum/(float)bin),chance*.25f);
                }
            if(opening==0)firstCount=count;else laterCount=count;
            }
            }
            Span<float> state=stackalloc float[129];Span<float> next=stackalloc float[129];
            state.Clear();state[0]=1f;
            var mandatory=Mathf.Min(8,effect.DamageHitCount);
            var trials=Mathf.Min(Mathf.Max(0,8-mandatory),effect.AdditionalHitTrials);
            for(var hit=0;hit<mandatory+trials;hit++)
            {
                var occurs=hit<mandatory?1f:effect.AdditionalHitChance;
                var choices=hit==0?outcomes:laterOutcomes;var count=hit==0?firstCount:laterCount;
                next.Clear();
                for(var damage=0;damage<=limit;damage++)
                {
                    if(state[damage]<=0)continue;
                    next[damage]+=state[damage]*(1f-occurs);
                    for(var i=0;i<count;i++)
                    {
                        var outcome=choices[i];
                        next[Mathf.Min(limit,damage+outcome.damage)]+=state[damage]*occurs*outcome.chance;
                    }
                }
                var previous=state;state=next;next=previous;
            }
            return Mathf.Clamp01(state[limit]*hitChance);
        }

        private static CombatSkillTargetPreview CalculateTargetPreviewInternal(
            BattleUnit caster, BattleUnit target, SkillData skill, int targetModeIndex, bool? criticalPreview, SkillEffectData singleEffect = null, bool? bonusProc = null, bool guaranteedHitsOnly = false, bool singleHitOnly = false, bool openingDamage = true)
        {
            var minimum = 0;
            var maximum = 0;
            var hasDamage = false;
            var hasGuaranteedHit = true;
            var minimumProtectionUsed = target?.RibbedTunicProtectionUsed ?? false;
            var maximumProtectionUsed = minimumProtectionUsed;
            var minimumShield = target?.Character?.ShieldPool?.TotalShield ?? 0;
            var maximumShield = minimumShield;
            string statusId = null;
            var statusChance = 0f;

            foreach (var baseEffect in skill?.Effects ?? Array.Empty<SkillEffectData>())
            {
                if (singleEffect != null && baseEffect != singleEffect) continue;
                var effect = PassiveEffectCombatExecutionSystem.ResolveEffect(caster, skill, baseEffect);
                if (effect?.EffectType != EffectType.DealDamage)
                {
                    continue;
                }

                if (targetModeIndex >= 0
                    && effect.TargetModeIndex != targetModeIndex)
                {
                    continue;
                }

                var range = CalculateEffectDamageRange(caster, effect,
                    ScoutCombatSystem.Behind(caster, target) ? 1f + effect.RearAttackDamageBonus : 1f, bonusProc);
                var distance = Mathf.Abs(caster.Position.X-target.Position.X)+Mathf.Abs(caster.Position.Y-target.Position.Y);
                var attackType=skill.ResolveAttackType(target.Position.X - caster.Position.X, target.Position.Y - caster.Position.Y);
                var context=new PassiveEffectCombatContext(source:caster,target:target,skill:skill,skillId:skill.SkillId,attackType:attackType,damageType:effect.DamageType,distance:distance);
                var scoutMultiplier=CombatDamageSystem.HawkeyeDamageMultiplier(caster,target,effect.DamageType)*PaladinCombatSystem.DamageMultiplier(context)*MageCombatSystem.DamageMultiplier(context)*WarriorCombatSystem.DamageMultiplier(context,true)*ScoutCombatSystem.DamageMultiplier(context)*(attackType==AttackType.Melee?skill.PointBlankDamageMultiplier:1f);
                if(criticalPreview ?? (skill.GuaranteedCritical || CombatDamageSystem.HasUniqueBuff(caster,UniqueBuffType.GuaranteedCriticalHit)))scoutMultiplier*=Mathf.Max(1f,caster.Character.Stats.CritDamage.GetValue()+ScoutCombatSystem.WeakPointCriticalDamage(target));
                range=new CombatSkillDamageRange(Mathf.RoundToInt(range.Minimum*scoutMultiplier),Mathf.RoundToInt(range.Maximum*scoutMultiplier),true);
                var hitCount = Mathf.Max(1, effect.DamageHitCount);
                var strike=BanditLeaderBossSystem.StrikeMultiplier(caster?.BanditLeaderState?.Session ?? caster?.WarriorState.Session,target,skill);
                range=new CombatSkillDamageRange(Mathf.RoundToInt(range.Minimum*strike),Mathf.RoundToInt(range.Maximum*strike),true);
                for (var hitIndex = 0; hitIndex < (singleHitOnly ? 1 : guaranteedHitsOnly ? effect.DamageHitCount : effect.MaximumHitCount); hitIndex++)
                {
                    var opening=1f;
                    if(hitIndex==0 && openingDamage)
                    {
                        var freeze=target?.Character?.PassiveEffects?.Find(e=>e.EffectId==StatusEffectIds.Freeze
                            && e.Lifetime==Godmist.Core.Enums.PassiveEffectLifetime.Timed && e.RemainingDuration>0);
                        if(freeze!=null)opening*=1f+freeze.RemainingDuration*GameDefinesRuntime.GetFloat(GameDefineIds.FreezeDamagePerRemainingTurn,.5f);
                        var exposure=target?.Character?.PassiveEffects?.Find(e=>e.EffectId=="paladin.exposure");
                        opening*=1f+(float)(exposure?.GetStateValue("strength")?.NumberValue ?? 0);
                    }
                    var minimumRaw=Mathf.CeilToInt((range.Minimum+(caster?.SkeletonSlayerState?.NextAttackBonus ?? 0))*opening);
                    var maximumRaw=Mathf.CeilToInt((range.Maximum+(caster?.SkeletonSlayerState?.NextAttackBonus ?? 0))*opening);
                    if(StatusEffectSystem.HasStatus(caster,StatusEffectIds.Weakening))
                    {minimumRaw=Mathf.CeilToInt(minimumRaw*.8f);maximumRaw=Mathf.CeilToInt(maximumRaw*.8f);}
                    var minimumAfterDefense = CombatDamageSystem.ApplyDefense(
                        caster,
                        target,
                        minimumRaw,
                        effect.DamageType,
                        effect.BonusPenetration, WarriorDischargeRules.DefenseIgnore(caster, skill, type: effect.DamageType));
                    var maximumAfterDefense = CombatDamageSystem.ApplyDefense(
                        caster,
                        target,
                        maximumRaw,
                        effect.DamageType,
                        effect.BonusPenetration, WarriorDischargeRules.DefenseIgnore(caster, skill, type: effect.DamageType));
                    if (hitIndex < hitCount)
                    {
                        var amount = ApplyCurrentDamageModifiers(caster, target, minimumAfterDefense,
                            skill, effect.DamageType, attackType, minimumProtectionUsed);
                        minimum += amount;
                        if (effect.DamageType == DamageType.Physical && amount > minimumShield) minimumProtectionUsed = true;
                        minimumShield = Mathf.Max(0, minimumShield - amount);
                    }
                    var maximumAmount = ApplyCurrentDamageModifiers(caster, target, maximumAfterDefense,
                        skill, effect.DamageType, attackType, maximumProtectionUsed);
                    maximum += maximumAmount;
                    if (effect.DamageType == DamageType.Physical && maximumAmount > maximumShield) maximumProtectionUsed = true;
                    maximumShield = Mathf.Max(0, maximumShield - maximumAmount);
                }
                hasDamage = true;

                if (!effect.AlwaysHits
                    && !CombatDamageSystem.IsGuaranteedHitDamageType(effect.DamageType))
                {
                    hasGuaranteedHit = false;
                }
            }

            var hitChance = 1f;
            if (hasDamage && !hasGuaranteedHit && !CombatDamageSystem.HasUniqueBuff(caster, UniqueBuffType.Hawkeye))
            {
                hitChance = CombatDamageSystem.CalculateSkillHitChance(caster, target, skill);
            }

            // Show only a status whose trigger can be estimated from the current
            // target. Conditional effects with other dependencies are omitted.
            var effects = skill?.Effects;
            if (effects != null)
            {
                for (var index = 0; index < effects.Count; index++)
                {
                    var effect = PassiveEffectCombatExecutionSystem.ResolveEffect(caster, skill, effects[index]);
                    if (effect == null || caster == null || target == null
                        || caster.IsPlayerSide == target.IsPlayerSide
                        || effect.Polarity != EffectPolarity.Negative
                        || targetModeIndex >= 0 && effect.TargetModeIndex != targetModeIndex)
                        continue;

                    var id = effect.EffectType == EffectType.ApplyStatus
                        ? effect.StatusId
                        : effect.EffectType == EffectType.DealDamage ? effect.OnHitStatusId : null;
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    if (effect.EffectType == EffectType.DealDamage && effect.Conditions != null)
                    {
                        var conditional = false;
                        foreach (var condition in effect.Conditions)
                            conditional |= condition != null
                                && condition.ConditionType != EffectConditionType.Always;
                        if (conditional) continue;
                    }

                    var triggerChance = 1f;
                    if (effect.EffectType == EffectType.ApplyStatus)
                    {
                        if (effect.Conditions != null)
                        {
                            var unsupported = false;
                            foreach (var condition in effect.Conditions)
                            {
                                if (condition == null || condition.ConditionType == EffectConditionType.Always) continue;
                                if (condition.ConditionType != EffectConditionType.ReferencedEffectSucceeded
                                    || condition.ReferencedEffectIndex < 0
                                    || condition.ReferencedEffectIndex >= index)
                                {
                                    unsupported = true;
                                    break;
                                }

                                var prior = PassiveEffectCombatExecutionSystem.ResolveEffect(
                                    caster, skill, effects[condition.ReferencedEffectIndex]);
                                if (prior?.EffectType != EffectType.DealDamage)
                                {
                                    unsupported = true;
                                    break;
                                }
                                var priorHit = prior.AlwaysHits
                                    || CombatDamageSystem.IsGuaranteedHitDamageType(prior.DamageType)
                                    ? 1f : CalculateHitChance(caster, target, skill);
                                triggerChance *= 1f - Mathf.Pow(1f - priorHit, prior.DamageHitCount)
                                    * Mathf.Pow(1f - prior.AdditionalHitChance * priorHit,
                                        prior.AdditionalHitTrials);
                            }
                            if (unsupported) continue;
                        }
                        if (!effect.AlwaysHits && effect.Polarity == EffectPolarity.Negative)
                            triggerChance *= CalculateHitChance(caster, target, skill);
                    }
                    else
                    {
                        var perHit = effect.AlwaysHits
                            || CombatDamageSystem.IsGuaranteedHitDamageType(effect.DamageType)
                            ? 1f : CalculateHitChance(caster, target, skill);
                        var application = StatusEffectSystem.CalculateStatusApplicationChance(
                            caster, target, id, effect.OnHitStatusChance);
                        statusId = id;
                        statusChance = 1f - Mathf.Pow(1f - perHit * application,
                                effect.DamageHitCount)
                            * Mathf.Pow(1f - effect.AdditionalHitChance * perHit * application,
                                effect.AdditionalHitTrials);
                        break;
                    }

                    statusId = id;
                    statusChance = triggerChance * StatusChanceAfterResistanceModifier(
                        caster, target, skill, index, id, effect.BaseChance);
                    break;
                }
            }

            return new CombatSkillTargetPreview(
                new CombatSkillDamageRange(minimum, maximum, hasDamage),
                hitChance,
                hasDamage,
                statusId,
                statusChance, CalculateHealing(caster, target, skill, targetModeIndex));
        }

        private static float StatusChanceAfterResistanceModifier(BattleUnit caster, BattleUnit target,
            SkillData skill, int statusIndex, string statusId, float baseChance)
        {
            var unchanged = StatusEffectSystem.CalculateStatusApplicationChance(caster,target,statusId,baseChance);
            if (!StatusEffectIds.UsesSuppressionResistance(statusId)) return unchanged;
            var current = skill.Effects[statusIndex];
            // A preceding modifier gated by the same hit is rolled before the
            // status. Average its success/failure branches without mutating the target.
            for (var i = statusIndex - 1; i >= 0; i--)
            {
                var prior = PassiveEffectCombatExecutionSystem.ResolveEffect(caster,skill,skill.Effects[i]);
                if (prior?.EffectType != EffectType.ModifyResistance || prior.TargetModeIndex != current.TargetModeIndex
                    || prior.ResistanceType is not (ResistanceType.Suppression or ResistanceType.All)) continue;
                foreach (var condition in prior.Conditions ?? Array.Empty<SkillEffectConditionData>())
                {
                    if (condition == null || condition.ConditionType == EffectConditionType.Always) continue;
                    if (condition.ConditionType != EffectConditionType.ReferencedEffectSucceeded
                        || !System.Linq.Enumerable.Any(current.Conditions ?? Array.Empty<SkillEffectConditionData>(), c => c != null
                            && c.ConditionType == condition.ConditionType && c.ReferencedEffectIndex == condition.ReferencedEffectIndex)) return unchanged;
                }
                var delta = prior.ModifierValue;
                var key = ModifyResistanceSystem.BuildEffectId(skill.SkillId,i);
                foreach (var active in target.Character.PassiveEffects)
                    if (active.EffectId == key && PassiveEffectLifecycleSystem.IsActive(active))
                        delta -= active.GetPayload<Godmist.Runtime.Effects.ResistanceModifierPayload>()?.Value ?? 0f;
                var applied = StatusEffectSystem.CalculateStatusApplicationChance(caster,target,statusId,baseChance,delta);
                var chance = Mathf.Clamp01(prior.BaseChance);
                if (!prior.AlwaysHits && prior.Polarity == EffectPolarity.Negative) chance *= CalculateHitChance(caster,target,skill);
                return Mathf.Lerp(unchanged,applied,chance);
            }
            return unchanged;
        }

        private static float CalculateHitChance(BattleUnit caster, BattleUnit target, SkillData skill)
        {
            return CombatDamageSystem.CalculateSkillHitChance(caster, target, skill);
        }

        private static CombatSkillDamageRange CalculateEffectDamageRange(
            BattleUnit caster,
            SkillEffectData effect, float positionalMultiplier = 1f, bool? bonusProc = null)
        {
            if (caster?.Character?.Stats == null || effect == null)
            {
                return new CombatSkillDamageRange(0, 0, false);
            }

            if (effect.DamageValueSource == DamageValueSource.AlchemicalPower)
            {
                var strength = Mathf.Max(0f, effect.DamageMultiplier * positionalMultiplier) * (bonusProc==true?effect.BonusDamageMultiplier:1f);
                var maximumStrength = bonusProc==null && effect.BonusDamageMultiplierChance > 0f
                    ? strength * effect.BonusDamageMultiplier : strength;
                return new CombatSkillDamageRange(
                    CombatDamageValueCalculator.CalculateAlchemicalDamage(caster.Character.Level, strength),
                    CombatDamageValueCalculator.CalculateAlchemicalDamage(caster.Character.Level, maximumStrength), true);
            }

            var minimumAttack = Mathf.Max(0,
                Mathf.RoundToInt(caster.Character.Stats.MinAttack.GetValue()));
            var maximumAttack = Mathf.Max(minimumAttack,
                Mathf.RoundToInt(caster.Character.Stats.MaxAttack.GetValue()));

            if (CombatDamageSystem.HasUniqueBuff(caster, UniqueBuffType.MageMaximumDamage)) minimumAttack = maximumAttack;
            var baseMinimum = effect.DamageValueSource switch
            {
                DamageValueSource.MinAttack => minimumAttack,
                DamageValueSource.MaxAttack => maximumAttack,
                DamageValueSource.AverageAttack =>
                    Mathf.RoundToInt((minimumAttack + maximumAttack) * 0.5f),
                _ => minimumAttack
            };
            var baseMaximum = effect.DamageValueSource switch
            {
                DamageValueSource.MinAttack => minimumAttack,
                DamageValueSource.MaxAttack => maximumAttack,
                DamageValueSource.AverageAttack => baseMinimum,
                _ => maximumAttack
            };

            var multiplier = Mathf.Max(0f, effect.DamageMultiplier * positionalMultiplier * SummonSystem.DamageScale(caster, effect)) * (bonusProc==true?effect.BonusDamageMultiplier:1f);
            var minimum = CombatDamageValueCalculator.ScaleAttackDamage(baseMinimum,multiplier);
            var maximum = 0;
            var maximumMultiplier = multiplier;
            if (bonusProc==null && effect.BonusDamageMultiplierChance > 0f)
            {
                maximumMultiplier *= effect.BonusDamageMultiplier;
            }

            maximum = CombatDamageValueCalculator.ScaleAttackDamage(baseMaximum,maximumMultiplier);
            return new CombatSkillDamageRange(
                minimum,
                maximum,
                true);
        }

        private static int ApplyCurrentDamageModifiers(
            BattleUnit caster,
            BattleUnit target,
            int damage, SkillData skill = null, DamageType damageType = DamageType.Physical,
            AttackType? resolvedAttackType = null, bool? firstProtectionUsed = null, CombatValueExplanation explanation = null)
        {
            if (damage <= 0)
            {
                return 0;
            }

            var context=new PassiveEffectCombatContext(source:caster,target:target,skill:skill,skillId:skill?.SkillId,
                attackType:resolvedAttackType??skill?.AttackType??AttackType.Unknown,damageType:damageType);
            var amount=CombatDamageSystem.CalculateIncomingBeforeRedirect(caster?.WarriorState.Session,caster,target,
                damage,damageType,context,explanation,skill);
            return Mathf.Max(0,Mathf.RoundToInt(amount*UniqueEquipmentCombatRules.IncomingMultiplier(target,damageType,
                resolvedAttackType??skill?.AttackType??AttackType.Unknown,firstProtectionUsed)));
        }
    }
}
