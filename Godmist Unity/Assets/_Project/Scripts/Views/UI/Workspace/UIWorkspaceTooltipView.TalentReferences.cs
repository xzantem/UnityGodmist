using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;

namespace Godmist.Views.UI.Workspace
{
    public sealed partial class UIWorkspaceTooltipView
    {
        private CharacterInstance talentReferenceCharacter;
        private string talentReferenceKnownSkills;
        private readonly List<CombatTooltipGlossary.Entry> talentReferenceEntries = new();
        private readonly Dictionary<string, SkillTreeNodeDefinition> talentReferenceNodes = new();
        private readonly Dictionary<string, SkillData> talentReferenceSkills = new();

        private static readonly Dictionary<string, string[]> TalentNameForms = new()
        {
            ["Szybki Odwet"] = new[] { "Szybkiego Odwetu", "Szybkim Odwetem" },
            ["Osłona w Ruchu"] = new[] { "Osłony w Ruchu", "Osłonę w Ruchu", "Osłonie w Ruchu" },
            ["Prowokacja"] = new[] { "Prowokacji", "Prowokację", "Prowokacją" },
            ["Taneczny Krok"] = new[] { "Tanecznego Kroku", "Tanecznym Krokiem" },
            ["Przedłużona Osłona"] = new[] { "Przedłużonej Osłony", "Przedłużoną Osłonę" },
            ["Swobodny Ruch"] = new[] { "Swobodnego Ruchu" },
            ["Wspólny Front"] = new[] { "Wspólnego Frontu", "Wspólnym Frontem" },
            ["Przypływ Adrenaliny"] = new[] { "Przypływu Adrenaliny" },
            ["Eskorta"] = new[] { "Eskorty" },
            ["Opanowanie"] = new[] { "Opanowania" },
            ["Ochrona Sztyletu"] = new[] { "Ochrony Sztyletu", "Ochroną Sztyletu" },
            ["Zwarty Szyk"] = new[] { "Zwartego Szyku" },
            ["Kontratak"] = new[] { "Kontrataku", "Kontratakiem" },
            ["Niezłomność"] = new[] { "Niezłomności" },
            ["Odbicie"] = new[] { "Odbicia", "Odbiciu" },
            ["Postawa Defensywna"] = new[] { "Postawy Defensywnej", "Postawę Defensywną" },
            ["Pragnienie Śmierci"] = new[] { "Pragnienia Śmierci", "Pragnieniem Śmierci" },
            ["Pragnienie Zemsty"] = new[] { "Pragnienia Zemsty" },
            ["Gniewny Odwet"] = new[] { "Gniewnego Odwetu" },
            ["Rozkaz Gotowości"] = new[] { "Rozkazu Gotowości", "Rozkazem Gotowości" },
            ["Rozkaz Ochrony"] = new[] { "Rozkazu Ochrony", "Rozkazem Ochrony" },
            ["Rozkaz Unicestwienia"] = new[] { "Rozkazu Unicestwienia", "Rozkazem Unicestwienia" },
            ["Unieruchomienie"] = new[] { "Unieruchomienia" },
            ["Wampiryzm"] = new[] { "Wampiryzmu" },
            ["Młyniec"] = new[] { "Młyńca", "Młyńcem" },
            ["Wyładowanie"] = new[] { "Wyładowania", "Wyładowaniem" },
            ["Przewidywanie"] = new[] { "Przewidywania" },
            ["Krok w Cień"] = new[] { "Kroku w Cień" },
            ["Podcięcie Gardła"] = new[] { "Podcięcia Gardła", "Podcięciem Gardła" },
            ["Podwójny Rzut"] = new[] { "Podwójnego Rzutu" },
            ["Przewrót w Przód"] = new[] { "Przewrotu w Przód" },
            ["Rykoszet"] = new[] { "Rykoszetu", "Rykoszetem" },
            ["Seria Ciosów"] = new[] { "Serii Ciosów" },
            ["Szybkie Ciosy"] = new[] { "Szybkich Ciosów", "Szybkimi Ciosami" },
            ["Słaby Punkt"] = new[] { "Słabego Punktu", "Słabym Punktem" },
            ["Tonik Pobudzający"] = new[] { "Toniku Pobudzającego" },
            ["Uderzenie Głowicą"] = new[] { "Uderzenia Głowicą" },
            ["Zatruty Sztylet"] = new[] { "Zatrutego Sztyletu", "Zatrutym Sztyletem" },
            ["Rzut Sztyletem"] = new[] { "Rzutu Sztyletem", "Rzutem Sztyletem" },
            ["Ofiara"] = new[] { "Ofiary", "Ofiarą" },
            ["Ognista Aura"] = new[] { "Ognistej Aury", "Ognistą Aurę" },
            ["Ognista Ofiara"] = new[] { "Ognistej Ofiary" },
            ["Ognista Strzała"] = new[] { "Ognistej Strzały" },
            ["Przejęcie Mocy"] = new[] { "Przejęcia Mocy", "Przejęciem Mocy" },
            ["Rozprzestrzenianie Płomieni"] = new[] { "Rozprzestrzeniania Płomieni" },
            ["Roztrojenie Energii"] = new[] { "Roztrojenia Energii" },
            ["Skupienie Magii"] = new[] { "Skupienia Magii" },
            ["Wyładowanie Energii"] = new[] { "Wyładowania Energii", "Wyładowaniem Energii" },
            ["Aura Leczenia"] = new[] { "Aury Leczenia", "Aurę Leczenia" },
            ["Błogosławieństwo"] = new[] { "Błogosławieństwa", "Błogosławieństwem" },
            ["Fala Przyciągająca"] = new[] { "Fali Przyciągającej" },
            ["Grupowe Uzdrowienie"] = new[] { "Grupowego Uzdrowienia" },
            ["Oślepiające Światło"] = new[] { "Oślepiającym Światłem", "Oślepiającego Światła" },
            ["Pole Osądu"] = new[] { "Pola Osądu", "Polem Osądu" },
            ["Udręczenie"] = new[] { "Udręczenia", "Udręczeniem" },
            ["Łańcuch Leczenia"] = new[] { "Łańcucha Leczenia" },
            ["Święta Regeneracja"] = new[] { "Świętej Regeneracji", "Świętą Regenerację" }
        };

        private static int TalentReferenceRank(CharacterInstance character, string nodeId) => character?.SkillTreeProgress?
            .SelectMany(p => p.NodeInvestments).Where(n => n.NodeId == nodeId)
            .Select(n => n.Ranks).DefaultIfEmpty(0).Max() ?? 0;

        private void RegisterTalentReferences(CharacterInstance character)
        {
            var root = GetRootTooltip();
            if (root != this) { root.RegisterTalentReferences(character); return; }
            var known = character?.GetKnownSkills()?.Where(s => s != null).ToArray() ?? Array.Empty<SkillData>();
            var signature = string.Join("|", known.Select(s => s.SkillId));
            if (ReferenceEquals(character, talentReferenceCharacter) && signature == talentReferenceKnownSkills) return;
            foreach (var old in talentReferenceEntries) calculationEntries.Remove(old.Id);
            talentReferenceEntries.Clear(); talentReferenceNodes.Clear(); talentReferenceSkills.Clear();
            talentReferenceCharacter = character; talentReferenceKnownSkills = signature;
            foreach (var node in character?.ClassData?.SkillTrees?.Where(t => t != null).SelectMany(t => t.Nodes)
                ?? Enumerable.Empty<SkillTreeNodeDefinition>())
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || talentReferenceNodes.ContainsKey(node.Id)) continue;
                talentReferenceNodes[node.Id] = node;
                var rank = TalentReferenceRank(character, node.Id);
                var entry = AddTalentReference(node.Id, node.DisplayName, node.Icon ?? node.GrantedSkill?.Icon,
                    (rank > 0 ? "Aktualne:\n" : "Następne:\n") + (SkillTreeTooltipCopy.ForRank(node.Id, Math.Max(1, rank)) ?? SkillTreeNodeView.BuildRankDescription(node, Math.Max(1, rank))));
                if (node.GrantedSkill != null && node.GrantedSkill.DisplayName != node.DisplayName)
                    entry.Aliases.Add(node.GrantedSkill.DisplayName);
            }
            var grantedIds = talentReferenceNodes.Values.Where(n => n.GrantedSkill != null).Select(n => n.GrantedSkill.SkillId).ToHashSet();
            foreach (var skill in known.Where(s => !grantedIds.Contains(s.SkillId)))
            {
                talentReferenceSkills[skill.SkillId] = skill;
                // Generic labels such as Leczenie describe a mechanic as well as a basic skill.
                // Keep the glossary meaning in prose; explicit skill IDs still resolve above.
                if (CombatTooltipGlossary.BuiltInEntries.Any(e => string.Equals(e.Title, skill.DisplayName, StringComparison.OrdinalIgnoreCase))) continue;
                AddTalentReference(skill.SkillId, skill.DisplayName, skill.Icon, skill.TooltipDescription);
            }
        }

        private CombatTooltipGlossary.Entry AddTalentReference(string id, string title, UnityEngine.Sprite icon, string description)
        {
            var entry = new CombatTooltipGlossary.Entry { Id = id, Title = title, Icon = icon, Description = description, MatchCase = true };
            if (TalentNameForms.TryGetValue(title, out var forms)) entry.Aliases.AddRange(forms);
            talentReferenceEntries.Add(entry); calculationEntries[id] = entry;
            return entry;
        }

        public string LinkTalentReferences(string text, CharacterInstance character, string excludedId = null)
        {
            RegisterTalentReferences(character);
            return CombatTooltipGlossary.LinkifyWithEntries(text, GetRootTooltip().talentReferenceEntries, excludedId);
        }

        public string LinkGuardianTalentReferences(string text, CharacterInstance character, string excludedId = null)
            => LinkTalentReferences(text, character, excludedId);

        private bool TryGetTalentReference(string id, out CombatTooltipGlossary.Entry entry)
        {
            var root = GetRootTooltip();
            entry = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            if (root.talentReferenceNodes.TryGetValue(id, out var node))
            {
                var character = root.talentReferenceCharacter;
                // Use the same renderer as the actual node; resolve only the hovered entry.
                var body = SkillTreeNodeView.BuildDetails(node, TalentReferenceRank(character, id), this, character);
                entry = new CombatTooltipGlossary.Entry { Id = id, Title = node.DisplayName,
                    Icon = node.Icon ?? node.GrantedSkill?.Icon, Description = body };
                return true;
            }
            if (root.talentReferenceSkills.TryGetValue(id, out var skill))
            {
                var character = root.talentReferenceCharacter;
                entry = new CombatTooltipGlossary.Entry { Id = id, Title = skill.DisplayName, Icon = skill.Icon,
                    Description = BuildLoadoutSkillCost(skill, character) + "\n\n" + BuildLoadoutSkillDetails(skill, character) };
                return true;
            }
            return false;
        }
    }
}
