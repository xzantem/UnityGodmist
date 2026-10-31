using System.Collections.Generic;
using System.Globalization;
namespace Godmist.Systems.Combat
{
    // Optional observation of existing deterministic calculations. No RNG/state.
    public sealed class CombatValueExplanation
    {
        public readonly List<(string Key,string Value)> Rows = new();
        public void Add(string key,string value) => Rows.Add((key,value));
        public void Add(string key,float value) => Add(key,value.ToString("0.####",CultureInfo.GetCultureInfo("pl-PL")));
    }
}
