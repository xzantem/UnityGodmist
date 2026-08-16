using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Godmist.Views.UI
{
    [CreateAssetMenu(fileName = "CombatTooltipGlossary", menuName = "Godmist/Combat/Tooltip Glossary")]
    public class CombatTooltipGlossary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string Id;
            public Sprite Icon;
            public string Title;
            [Tooltip("Dodatkowe formy nazwy wykrywane w opisach, np. 'jednostkę', 'jednostki'.")]
            public List<string> Aliases = new();
            [TextArea] public string Description;
        }

        [SerializeField] private List<Entry> entries = new();

        public bool TryGet(string id, out Entry result)
        {
            foreach (var entry in entries)
            {
                if (entry != null && string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    result = entry;
                    return true;
                }
            }
            result = null;
            return false;
        }

        public string Linkify(string richText)
        {
            if (string.IsNullOrWhiteSpace(richText) || entries == null) return richText ?? string.Empty;
            var candidates = new List<(Entry Entry, string Term)>();
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;
                AddCandidate(candidates, entry, entry.Title);
                if (entry.Aliases == null) continue;
                foreach (var alias in entry.Aliases) AddCandidate(candidates, entry, alias);
            }

            candidates.Sort((left, right) => right.Term.Length.CompareTo(left.Term.Length));
            var result = new StringBuilder(richText.Length + 32);
            var linkDepth = 0;
            for (var index = 0; index < richText.Length;)
            {
                if (richText[index] == '<')
                {
                    var tagEnd = richText.IndexOf('>', index);
                    if (tagEnd >= 0)
                    {
                        var tag = richText.Substring(index, tagEnd - index + 1);
                        if (tag.StartsWith("<link", StringComparison.OrdinalIgnoreCase)) linkDepth++;
                        else if (tag.StartsWith("</link", StringComparison.OrdinalIgnoreCase)) linkDepth = Mathf.Max(0, linkDepth - 1);
                        result.Append(tag);
                        index = tagEnd + 1;
                        continue;
                    }
                }
                if (linkDepth == 0 && TryMatchCandidate(richText, index, candidates, out var entry, out var length))
                {
                    result.Append("<link=\"").Append(entry.Id).Append("\"><color=#7EDBFF>")
                        .Append(richText, index, length).Append("</color></link>");
                    index += length;
                    continue;
                }
                result.Append(richText[index++]);
            }
            return result.ToString();
        }

        private static void AddCandidate(List<(Entry Entry, string Term)> candidates, Entry entry, string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return;
            foreach (var candidate in candidates)
                if (candidate.Entry == entry && string.Equals(candidate.Term, term, StringComparison.OrdinalIgnoreCase)) return;
            candidates.Add((entry, term));
        }

        private static bool TryMatchCandidate(string text, int start, List<(Entry Entry, string Term)> candidates, out Entry entry, out int length)
        {
            foreach (var candidate in candidates)
            {
                length = candidate.Term.Length;
                if (start + length <= text.Length
                    && string.Compare(text, start, candidate.Term, 0, length, StringComparison.OrdinalIgnoreCase) == 0
                    && HasTermBoundaries(text, start, length))
                {
                    entry = candidate.Entry;
                    return true;
                }
            }
            entry = null;
            length = 0;
            return false;
        }

        private static bool HasTermBoundaries(string text, int start, int length)
        {
            var beforeIsWord = start > 0 && char.IsLetterOrDigit(text[start - 1]);
            var after = start + length;
            var afterIsWord = after < text.Length && char.IsLetterOrDigit(text[after]);
            return !beforeIsWord && !afterIsWord;
        }
    }
}
