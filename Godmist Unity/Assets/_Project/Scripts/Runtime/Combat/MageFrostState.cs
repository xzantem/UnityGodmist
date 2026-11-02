using System.Collections.Generic;
namespace Godmist.Runtime.Combat
{
    // Participant-owned combat state; never serialized into a character save.
    public sealed class MageFrostState
    {
        public CombatSession Session;
        public int HealthLost, ShieldCooldown;
        public bool HadEmergencyShield;
        public string AuraSourceId;
        public readonly Dictionary<string,HashSet<string>> Contacts=new();
        public readonly List<MageFrostArea> Areas=new();
        public readonly HashSet<string> ObservedDeaths=new();
    }
    public sealed class MageFrostArea
    {
        public GridPosition Center;
        public int Turns;
        public bool Snowstorm;
        public int DamageRank;
        public float Attack;
        public bool MovementPenalty;
        public MageFrostArea Copy()=> (MageFrostArea)MemberwiseClone();
    }
    public readonly struct MageFrostContact
    {
        public readonly int FrozenTurns, StunTurns, Health;
        public readonly bool CanProc;
        public readonly string ActionId;
        public MageFrostContact(int frozen,int stun,int health,bool canProc,string actionId)
        {FrozenTurns=frozen;StunTurns=stun;Health=health;CanProc=canProc;ActionId=actionId??string.Empty;}
    }
}
