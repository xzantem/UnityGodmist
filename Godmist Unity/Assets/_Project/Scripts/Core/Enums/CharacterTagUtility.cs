using System;

namespace Godmist.Core.Enums
{
    public static class CharacterTagUtility
    {
        /// <summary>
        /// Parses authored condition references while keeping Humanoid as a
        /// backwards-compatible alias for the canonical Human tag.
        /// </summary>
        public static bool TryParse(string value, out CharacterTag tag)
        {
            tag = default;
            if (string.IsNullOrWhiteSpace(value)) return false;

            if (string.Equals(value.Trim(), "Humanoid", StringComparison.OrdinalIgnoreCase))
            {
                tag = CharacterTag.Human;
                return true;
            }

            return Enum.TryParse(value.Trim(), ignoreCase: true, out tag)
                && Enum.IsDefined(typeof(CharacterTag), tag);
        }

        public static string ToConditionReference(CharacterTag tag) => tag.ToString();
    }
}
