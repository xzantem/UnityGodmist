using Godmist.Core.Enums;
namespace Godmist.Views.UI
{
    public static class DungeonLengthPresentation
    {
        public static string Name(DungeonLength length) => length switch
        {
            DungeonLength.Short => "Krótka", DungeonLength.Normal => "Normalna",
            DungeonLength.Long => "Długa", _ => "Wyprawa"
        };
    }
}
