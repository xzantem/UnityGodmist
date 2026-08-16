using System;
using Godmist.Core.Enums;

namespace Godmist.Runtime.Effects
{
    [Serializable]
    public class PassiveEffectSourceRuntime
    {
        public PassiveEffectSourceType SourceType;
        public string SourceId = string.Empty;
        public string SourceCharacterId = string.Empty;
    }
}