using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Core.Utilities
{
    public static class DungeonStairPathUtility
    {
        public static bool AreAdjacent(Vector2Int a, Vector2Int b)
            => System.Math.Abs((long)a.x - b.x) + System.Math.Abs((long)a.y - b.y) == 1;

        // Only the unambiguous one-step legacy connection can be inferred.
        public static bool TryResolve(Vector2Int first, Vector2Int second, IReadOnlyList<Vector2Int> authored,
            out Vector2Int[] path, out string error)
        {
            path = null; error = null;
            if (authored == null || authored.Count == 0)
            {
                if (!AreAdjacent(first, second)) { error = "Wskaż przebieg długich schodów po kolejnych polach."; return false; }
                path = new[] { first, second }; return true;
            }
            if (authored.Count < 2 || authored[0] != first || authored[authored.Count - 1] != second)
            { error = "Trasa schodów musi zaczynać i kończyć się na wskazanych powierzchniach."; return false; }
            for (var i = 1; i < authored.Count; i++)
                if (!AreAdjacent(authored[i - 1], authored[i]))
                { error = $"Pola schodów {i - 1} i {i} nie są sąsiadami przez krawędź."; return false; }
            path = new Vector2Int[authored.Count];
            for (var i = 0; i < path.Length; i++) path[i] = authored[i];
            return true;
        }
    }
}
