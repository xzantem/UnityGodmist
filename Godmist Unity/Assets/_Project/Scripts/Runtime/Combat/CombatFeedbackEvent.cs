using Godmist.Core.Enums;

namespace Godmist.Runtime.Combat
{
    public enum CombatFeedbackType
    {
        Damage,
        Heal,
        ShieldGain,
        ShieldLoss,
        ResourceGain,
        ResourceSpend,
        ActionPointSpend,
        Critical,
        Miss,
        StatusApplied,
        BuffApplied,
        DebuffApplied,
        ProcTriggered,
        AuraApplied,
        DamageRedirected,
        ResourceRegenerationBlocked,
        TurnSkipped,
        SkillUsed,
        MapObjectTriggered,
        VisualCue,
        TargetEffect,
        Downed
    }

    public readonly struct CombatFeedbackEvent
    {
        public CombatFeedbackEvent(
            BattleUnit target,
            CombatFeedbackType type,
            int amount = 0,
            DamageType? damageType = null,
            ResourceType? resourceType = null,
            GridPosition? position = null,
            string effectName = null,
            CombatSkillPresentation? skillPresentation = null,
            BattleUnit source = null,
            SummonedCombatMapObject mapObject = null,
            CombatTargetVfxKind? targetVfx = null,
            float cueDelaySeconds = 0,
            string actionId = null,
            bool isCritical = false)
        {
            Target = target;
            Type = type;
            Amount = amount;
            DamageType = damageType;
            ResourceType = resourceType;
            Position = position ?? target?.Position ?? default;
            EffectName = effectName ?? string.Empty;
            SkillPresentation = skillPresentation;
            Source = source;
            SourcePosition = source?.Position;
            MapObject = mapObject;
            TargetVfx = targetVfx;
            CueDelaySeconds = cueDelaySeconds;
            ActionId = actionId;
            IsCritical = isCritical;
        }

        public BattleUnit Target { get; }
        public CombatFeedbackType Type { get; }
        public int Amount { get; }
        public DamageType? DamageType { get; }
        public ResourceType? ResourceType { get; }
        public GridPosition Position { get; }
        public string EffectName { get; }
        public CombatSkillPresentation? SkillPresentation { get; }
        public BattleUnit Source { get; }
        public GridPosition? SourcePosition { get; }
        public SummonedCombatMapObject MapObject { get; }
        public CombatTargetVfxKind? TargetVfx { get; }
        public float CueDelaySeconds { get; }
        // Presentation correlation only; no saved state or gameplay timing.
        public string ActionId { get; }
        public bool IsCritical { get; }
    }

    public enum CombatTargetVfxKind
    { Heal, CutBlood, CutFragments, Fire, Tremor, Blunt, EnergyImpact, EnergyGround,
        FireGround, PierceBlood, SmokeGround, SmokeHead, ArrowGround,
        ArcaneWard, HolyGrace, Rally, HolyImpact, HolyBlind, ArcaneFocus,
        PullDust, Shout, KnifeRecover, DestructionMark, WeakPointMark, Sacrifice, Transfusion,
        DeathWish, Offering, Fatigue, Tonic, FangContact, SpectralContact, AncientContact, SpectralLanding,
        BoneWard, SpectralDeparture, SpectralReveal, LifeSiphon,
        ToxicContact, ToxicGround, RootContact, GrowthGround, FlareHead, FlareGround,
        RoyalCommand, SlayerWard, HammerCharge, ShadowImpact, ShadowGround, BarkWard, WoodImpact, WoodGround, MineBurst, TrapGround, TrapContact, BanditOrder }
}
