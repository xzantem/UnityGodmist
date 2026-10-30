using System;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Utilities;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Views.UI.Workspace
{
    internal static class SkillTreeTooltipPresentation
    {
        internal static string Describe(SkillTreeNodeDefinition node,int rank,CharacterInstance character,UIWorkspaceTooltipView tooltip)
        {
            var text=SkillTreeTooltipCopy.ForRank(node.Id,rank);
            if(text==null)return null;
            var preview=SkillTreeRankPreview.Create(character,node,rank);
            var u=preview==null?null:new BattleUnit {Character=preview};
            string N(float v)=>v.ToString("0.##",UiTerms.PolishCulture);
            int Dot(string status,float coefficient,int turns)
            {
                var damage=StatusEffectSystem.CalculateStatusDamagePerTurn(u,status,coefficient);
                return status==StatusEffectIds.Burn ? Mathf.CeilToInt(MageCombatSystem.CalculateInitialBurnTotal(u,damage*turns)/(float)turns) : damage;
            }
            SkillData Skill(string id)=>preview?.ClassData?.SkillTrees?.Where(t=>t!=null).SelectMany(t=>t.Nodes)
                .Select(n=>n.GrantedSkill).Concat(preview?.Skills ?? new()).FirstOrDefault(s=>s?.SkillId==id);
            SkillEffectData Effect(string id,EffectType type)=>Skill(id)?.Effects?.Where(e=>e.EffectType==type)
                .Select(e=>PassiveEffectCombatExecutionSystem.ResolveEffect(u,Skill(id),e)).FirstOrDefault();
            int Required(string suffix,int actual)=>Math.Max(actual,node.Requirements.Where(r=>r.NodeId.EndsWith("."+suffix)).Select(r=>r.MinimumRanks).DefaultIfEmpty(0).Max());
            if(u?.Character?.Stats!=null) switch(node.Id)
            {
                case "node.warrior.brawler.force_moment":
                    var whirl=Effect("skill.warrior.whirlwind",EffectType.DealDamage);
                    if(whirl!=null)text=$"Młyniec wykonuje {whirl.DamageHitCount} "+(whirl.DamageHitCount<=4?"trafienia.":"trafień.");break;
                case "node.warrior.commander.broken_bones":
                    var root=Effect("skill.warrior.immobilize",EffectType.ApplyStatus);
                    if(root!=null)text=$"Wydłuża Unieruchomienie do {root.DurationTurns} tur.";break;
                case "node.scout.assassin.farsight":
                case "node.scout.assassin.knife_bounce":
                    var ricochet=Skill("skill.scout.ricochet");
                    var mode=ricochet==null?null:PassiveEffectCombatExecutionSystem.ResolveMode(u,ricochet,ricochet.TargetModes[0]);
                    if(mode!=null)text=node.Id.EndsWith("farsight")?$"Zwiększa zasięg Rykoszetu do {mode.AbsoluteRange.MaxRange} pól, a odległość między jego celami do {mode.RelativeRange.MaxRange} pól.":$"Rykoszet może trafić do {mode.ChainTargets} celów.";break;
                case "node.scout.blade_dancer.fine_thrusts":
                    var quick=Effect("skill.scout.quick_strikes",EffectType.DealDamage);
                    if(quick!=null)text=$"Zwiększa szansę na podwójne obrażenia Szybkich Ciosów do {N(quick.BonusDamageMultiplierChance*100)}%.";break;
                case "node.warrior.commander.riposte":
                    if(tooltip!=null)
                    {
                        var table="Przylegli sojusznicy<pos=68%>Priorytet\n";
                        for(var i=0;i<=4;i++)table+=i+"<pos=68%>×"+N(WarriorCombatSystem.ProtectionPriority(i))+"\n";
                        text=text.Replace("priorytet celu dla przeciwników",tooltip.LinkCalculation("priorytet celu dla przeciwników","Riposta — priorytet",table+"\nPriorytet wpływa na wybór celu przez przeciwnika."));
                    }break;
                case "node.warrior.brawler.rage":
                    text=text.Replace("{furia}",N(RageResourceRule.PreviewOffensiveGain(preview)+HeroTalentEffectValues.CutRageBonus(rank))); break;
                case "node.warrior.brawler.conflagration": text=text.Replace("{X}",N(Dot(StatusEffectIds.Burn,HeroTalentEffectValues.ConflagrationMultiplier(rank),2))); break;
                case "node.warrior.commander.deep_wounds":text=text.Replace("{X}",N(Dot(StatusEffectIds.Bleed,HeroTalentEffectValues.DeepWoundsMultiplier(rank),3)));break;
                case "node.scout.assassin.running_attack":
                    var running=SkillEffectData.ForDamage(DamageType.Physical,DamageValueSource.AttackRange,HeroTalentEffectValues.RunningAttackMultiplier(rank));
                    var range=CombatSkillPreviewCalculator.CalculateSingleHitRange(u,null,running);
                    text="<size=85%>Celność: 77</size>\nRaz na turę po ruchu atakuje przyległych przeciwników, zadając "+range.ToDisplayString()+" obrażeń fizycznych.";break;
                case "node.scout.assassin.keen_eye":text=$"Zwiększa szansę na Głębokie Wbicie do {N(ScoutCombatSystem.KnifeEmbeddingChance(u)*100)}%.";break;
                case "node.scout.assassin.sharp_knife":text=text.Replace("15% szansy na wbicie",N(ScoutCombatSystem.KnifeEmbeddingChance(u)*100)+"% szansy na wbicie").Replace("28 obrażeń",N(Dot(StatusEffectIds.Bleed,HeroTalentEffectValues.KnifeBleedMultiplier(rank),3))+" obrażeń");break;
                case "node.scout.assassin.pommel_strike":
                    if(ScoutCombatSystem.AssassinRank(u,"precise_strike")>0)text=text.Replace("1 turę","2 tury");break;
                case "node.scout.blade_dancer.combo_master":text=text.Replace("46 obrażeń",N(Dot(StatusEffectIds.Bleed,HeroTalentEffectValues.ComboBleedMultiplier(rank),3))+" obrażeń");break;
                case "node.scout.blade_dancer.venom":
                    var poison=Effect("skill.scout.poisoned_dagger",EffectType.ApplyStatus);
                    if(poison!=null)text=text.Replace("{X}",N(StatusEffectSystem.CalculateStatusDamagePerTurn(u,poison)));break;
                case "node.scout.blade_dancer.series":
                    var trials=rank+ScoutCombatSystem.BladeRank(u,"dance_of_doom");
                    text=trials==1?"Szybkie Ciosy mają 50% szansy na wykonanie dodatkowego ciosu."
                        :$"Szybkie Ciosy mogą wykonać {trials} dodatkowe ciosy, każdy z niezależną szansą 50%.";break;
                case "node.scout.blade_dancer.dance_of_doom":text=text.Replace("{X}",N(rank+Required("series",ScoutCombatSystem.BladeRank(u,"series"))));break;
                case "node.scout.blade_dancer.anger":
                case "node.scout.blade_dancer.revenge":
                    var control=ScoutCombatSystem.BladeRank(u,"mental_control");
                    text=text.Replace("10 nałożeń",(10+(control==1?1:control==2?3:control==3?5:0))+" nałożeń")
                        .Replace("0,15%",N(.15f+.05f*ScoutCombatSystem.BladeRank(u,"strong_insult"))+"%");break;
                case "node.scout.blade_dancer.efficient_recovery":
                    var hook=Skill("skill.scout.hook");
                    if(hook!=null)text=$"Odzyskanie własnego sztyletu Linką z Hakiem kosztuje {ScoutCombatSystem.KnifeRecoveryCost(u,hook,hook.ActionCost,true)}% PA i {PassiveEffectCombatExecutionSystem.ResolveResourceCost(u,hook,true)} impetu.";break;
                case "node.mage.warlock.power_saving":
                    var split = Effect("skill.mage.energy_split", EffectType.DealDamage);
                    if (split != null) text = $"Zwiększa obrażenia Roztrojenia Energii do {N(split.DamageMultiplier * 100)}% ataku.";
                    break;
                case "node.mage.fire.pyromania":
                    var aura=Effect("skill.mage.fire_aura",EffectType.MageFireAura);
                    var fireball=Effect("skill.mage.fireball",EffectType.ApplyStatus);
                    if(aura!=null&&fireball!=null)text=$"Zwiększa szansę na podpalenie Ognistej Aury do {N(StatusEffectSystem.CalculateSourceStatusChance(u,aura.StatusId,aura.BaseChance)*100)}%, a Kuli Ognia do {N(StatusEffectSystem.CalculateSourceStatusChance(u,fireball.StatusId,fireball.BaseChance)*100)}%.";break;
                case "node.mage.warlock.superhuman_offering":
                    var bonus=MageCombatSystem.OfferingBonus(u)*100;
                    var penalty=bonus*(1-.1f*MageCombatSystem.W(u,"calm"));
                    text=Mathf.Approximately(bonus,penalty)?$"Ofiara zwiększa zadawane i otrzymywane obrażenia o {N(bonus)}%."
                        :$"Ofiara zwiększa zadawane obrażenia o {N(bonus)}% i otrzymywane obrażenia o {N(penalty)}%.";break;
                case "node.mage.warlock.calm":text=$"Ofiara zwiększa otrzymywane obrażenia o {N(MageCombatSystem.OfferingBonus(u)*100*(1-.1f*rank))}%.";break;
                case "node.paladin.priest.healing_aura":text=text.Replace("{X}",N(HeroTalentEffectValues.HealingAura(u,rank)));break;
                case "node.paladin.crusader.judgement_release":
                    var burst=SkillEffectData.ForDamage(DamageType.Magical,DamageValueSource.AttackRange,HeroTalentEffectValues.JudgementReleaseMultiplier(rank));
                    // Field burst applies defense before the shared damage application;
                    // it does not run skill-hit/critical hero procs.
                    var fieldBurst=CombatSkillPreviewCalculator.CalculateDirectDamageRange(u,DamageType.Magical,burst.DamageMultiplier);
                    text=text.Replace("{X–Y}",fieldBurst.ToDisplayString());break;
                case "node.paladin.crusader.severe_judge":text=text.Replace("{X}",N(20*Required("harsh_verdict",PaladinCombatSystem.C(u,"harsh_verdict"))+40));break;
                case "node.paladin.crusader.torment":text=text.Replace("4 nałożenia",(4+2*PaladinCombatSystem.C(u,"torment_max"))+" nałożeń");break;
            }
            // Missing character context stays explicit instead of displaying a value
            // from the old review screenshots as if it were the player's build.
            text=text.Replace("{X–Y}","zależne od ataku").Replace("{X}","zależne od postaci").Replace("{furia}","dodatkową");
            text=UiTerms.Present(text,preview?.ResourcePool?.ResourceType ?? ResourceType.None);
            var details=node.Id switch
            {
                "node.warrior.brawler.rage"=>"Podgląd przed limitem maksymalnej furii. Bazowy przyrost pochodzi z użycia ofensywnej umiejętności; dodatkowa furia z talentu wymaga zadania obrażeń. Premie generowania zasobu modyfikują przyrost bazowy.",
                "node.paladin.priest.healer_focus" or "node.paladin.priest.encouragement"=>"Bezpośrednie leczenie z umiejętności obejmujące jednego odbiorcę. Ticki leczenia okresowego, wtórny zwrot HP i Aura Leczenia nie wyzwalają tej premii. Brak faktycznie odzyskanego HP nie uruchamia Pokrzepienia.",
                "node.mage.fire.weakness"=>"Bazowa szansa krytyczna tej postaci przeciw podpalonemu przeciwnikowi. Pozostałe premie do szansy krytycznej nadal działają; gwarantowany cios krytyczny pozostaje gwarantowany. Nie przyznaje premii sojusznikom.",
                "node.mage.warlock.adaptation"=>"Jedno trafienie musi odebrać co najmniej 20% maksymalnego HP. Redukcja dotyczy trzech kolejnych trafień od tego samego przeciwnika; nowe uruchomienie zastępuje poprzednią redukcję.",
                "node.mage.fire.aggressive_fire"=>"Niezależne losowanie dla każdego przeciwnika przy pierwszym ticku podpalenia. Kopia zachowuje połowę pozostałych obrażeń; do trzech kolejnych przeniesień.",
                "node.paladin.crusader.advantage"=>"Przewaga jest liczona względem maksymalnego zdrowia celu, w sposób ciągły. Przykład: 150 maksymalnego HP przeciw 100 to premia 25%.",
                "node.paladin.crusader.torment_damage"=>"Wymaga co najmniej czterech nałożeń przed trafieniem. Podstawa: różnica obrażeń po obronie z Udręczeniem i bez niego, z uwzględnieniem penetracji. Przykład: wzrost ze 100 do 120 daje przy 30% dodatkowe 6 obrażeń nieuchronnych.",
                "node.scout.assassin.running_attack"=>"Pierwszy ruch z przyległymi przeciwnikami zużywa wyzwolenie w tej turze. Celność 77 jest parametrem ataku; szansa trafienia zależy także od broni i uniku przeciwnika.",
                "node.scout.assassin.throat_cut"=>"Próg zabicia uwzględnia aktualne HP i tarczę przeciwnika. Premia przeciw bossowi zastępuje natychmiastowe zabicie.",
                _=>string.Empty
            };
            if(tooltip!=null)
            {
                text=tooltip.AnnotateTalentValues(text,node,rank,preview,r=>Describe(node,r,character,null));
                if(!string.IsNullOrWhiteSpace(details))text+="\n<size=85%>"+tooltip.LinkRule("Zasady działania",UiTerms.Present(node.DisplayName),details)+"</size>";
            }
            return text;
        }
    }
}
