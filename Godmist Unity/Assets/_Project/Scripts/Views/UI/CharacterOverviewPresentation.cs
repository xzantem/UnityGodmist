using Godmist.Runtime.Characters;
using Godmist.Systems.Characters;

namespace Godmist.Views.UI
{
    public static class CharacterOverviewPresentation
    {
        public static string Experience(CharacterInstance character, CharacterProgressionSystem progression) =>
            character.Level >= CharacterProgressionSystem.MaximumLevel ? "maksimum"
            : $"{character.CurrentExperience}/{progression.GetExperienceRequiredForNextLevel(character.Level)}";
    }
}
