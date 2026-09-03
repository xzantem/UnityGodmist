using System;
using Godmist.Core.Enums;

namespace Godmist.Runtime.Effects
{
    [Serializable]
    public sealed class PassiveEffectRuntimeStateValue
    {
        public string Key = string.Empty;
        public PassiveEffectRuntimeValueType Type;
        public float NumberValue;
        public int IntegerValue;
        public string TextValue = string.Empty;
        public bool BooleanValue;

        public static PassiveEffectRuntimeStateValue Number(string key, float value)
        {
            return new PassiveEffectRuntimeStateValue
            {
                Key = key ?? string.Empty,
                Type = PassiveEffectRuntimeValueType.Number,
                NumberValue = value
            };
        }

        public static PassiveEffectRuntimeStateValue Integer(string key, int value)
        {
            return new PassiveEffectRuntimeStateValue
            {
                Key = key ?? string.Empty,
                Type = PassiveEffectRuntimeValueType.Integer,
                IntegerValue = value
            };
        }

        public static PassiveEffectRuntimeStateValue Text(string key, string value)
        {
            return new PassiveEffectRuntimeStateValue
            {
                Key = key ?? string.Empty,
                Type = PassiveEffectRuntimeValueType.Text,
                TextValue = value ?? string.Empty
            };
        }

        public static PassiveEffectRuntimeStateValue Boolean(string key, bool value)
        {
            return new PassiveEffectRuntimeStateValue
            {
                Key = key ?? string.Empty,
                Type = PassiveEffectRuntimeValueType.Boolean,
                BooleanValue = value
            };
        }
    }
}
