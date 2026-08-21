using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class GameSessionRestoreFactoryTests
    {
        [Test]
        public void TryCreate_RestoresEmptyValidSession()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();

            try
            {
                var saveData = GameSaveData.CreateNew("Restore test");
                saveData.Party.MaxActiveMembers = 5;
                saveData.CampaignId = "campaign.restore";

                var restoreFactory = new GameSessionRestoreFactory();

                var result = restoreFactory.TryCreate(
                    saveData,
                    catalog,
                    out GameSession restoredSession);

                Assert.That(result, Is.True);
                Assert.That(restoredSession, Is.Not.Null);
                Assert.That(restoredSession.Gold, Is.EqualTo(0));
                Assert.That(restoredSession.CampaignId, Is.EqualTo("campaign.restore"));
                Assert.That(
                    restoredSession.PlayerParty.MaxActiveMembers,
                    Is.EqualTo(5));
                Assert.That(
                    restoredSession.CurrentMode,
                    Is.EqualTo(
                        Godmist.Core.Enums.GameModeState.Exploration));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void TryCreate_RestoresExplorationPosition()
        {
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();

            try
            {
                var saveData = GameSaveData.CreateNew("Pozycja Arungardu");
                saveData.Party.MaxActiveMembers = 5;
                saveData.HasExplorationPosition = true;
                saveData.ExplorationPositionX = 14.5f;
                saveData.ExplorationPositionY = -7.25f;

                var restoreFactory = new GameSessionRestoreFactory();

                var result = restoreFactory.TryCreate(
                    saveData,
                    catalog,
                    out GameSession restoredSession);

                Assert.That(result, Is.True);
                Assert.That(restoredSession.HasExplorationPosition, Is.True);
                Assert.That(
                    restoredSession.ExplorationPosition,
                    Is.EqualTo(new Vector2(14.5f, -7.25f)));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void TryCreate_RoundTripsSkillTreeInvestmentsThroughJson()
        {
            var characterDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var skillTree = CreateSkillTree("tree.save", "node.power", maxRanks: 2);
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();

            try
            {
                SetAutoProperty(characterDefinition, "Id", "character.save");
                SetAutoProperty(classData, "Id", "class.save");
                SetAutoProperty(classData, "ResourceType", ResourceType.Momentum);
                classData.SkillTrees.Add(skillTree);

                catalog.CharacterDefinitions.Add(characterDefinition);
                catalog.CharacterClasses.Add(classData);

                var character = new CharacterInstanceFactory().Create(
                    characterDefinition,
                    classData);
                character.AvailableSkillPoints = 5;

                var progression = new SkillTreeProgressionSystem();
                Assert.That(
                    progression.TryInvest(
                        character,
                        "tree.save",
                        "node.power"),
                    Is.True);
                Assert.That(
                    progression.TryInvest(
                        character,
                        "tree.save",
                        "node.power"),
                    Is.True);

                var session = new GameSession(
                    GameModeState.Exploration,
                    "campaign.skill-tree-save");
                Assert.That(session.TryRegisterCharacter(character), Is.True);
                Assert.That(
                    session.TryAddCharacterToActiveParty("character.save"),
                    Is.True);

                var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(
                    session,
                    "Skill tree save");

                Assert.That(snapshot.Characters, Has.Count.EqualTo(1));
                Assert.That(
                    snapshot.Characters[0].AvailableSkillPoints,
                    Is.EqualTo(3));
                Assert.That(snapshot.Characters[0].SkillTreeProgress, Has.Count.EqualTo(1));
                Assert.That(
                    snapshot.Characters[0].SkillTreeProgress[0].TreeId,
                    Is.EqualTo("tree.save"));
                Assert.That(
                    snapshot.Characters[0].SkillTreeProgress[0].NodeInvestments[0].NodeId,
                    Is.EqualTo("node.power"));
                Assert.That(
                    snapshot.Characters[0].SkillTreeProgress[0].NodeInvestments[0].Ranks,
                    Is.EqualTo(2));

                var serializer = new GameSaveJsonSerializer();
                var json = serializer.Serialize(snapshot);
                Assert.That(
                    serializer.TryDeserialize(json, out var restoredSaveData),
                    Is.True);

                var result = new GameSessionRestoreFactory().TryCreate(
                    restoredSaveData,
                    catalog,
                    out var restoredSession);

                Assert.That(result, Is.True);
                Assert.That(
                    restoredSession.CharactersById.TryGetValue(
                        "character.save",
                        out var restoredCharacter),
                    Is.True);
                Assert.That(restoredCharacter.AvailableSkillPoints, Is.EqualTo(3));
                Assert.That(restoredCharacter.SkillTreeProgress, Has.Count.EqualTo(1));
                Assert.That(
                    restoredCharacter.SkillTreeProgress[0].TreeId,
                    Is.EqualTo("tree.save"));
                Assert.That(
                    restoredCharacter.SkillTreeProgress[0].NodeInvestments,
                    Has.Count.EqualTo(1));
                Assert.That(
                    restoredCharacter.SkillTreeProgress[0].NodeInvestments[0].NodeId,
                    Is.EqualTo("node.power"));
                Assert.That(
                    restoredCharacter.SkillTreeProgress[0].NodeInvestments[0].Ranks,
                    Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(skillTree);
                Object.DestroyImmediate(classData);
                Object.DestroyImmediate(characterDefinition);
            }
        }

        [Test]
        public void TryCreate_SanitizesStaleSkillTreeProgressWithoutDroppingValidSaveData()
        {
            var characterDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var validTree = CreateSkillTreeWithNodes(
                "tree.valid",
                new List<SkillTreeNodeDefinition>
                {
                    CreateSkillTreeNode("node.valid", 2),
                    CreateSkillTreeNode("node.capped", 2),
                    CreateSkillTreeNode("node.negative", 2)
                });
            var limitNodes = new List<SkillTreeNodeDefinition>();
            for (var index = 0; index < 51; index++)
            {
                limitNodes.Add(CreateSkillTreeNode($"node.limit.{index}", 1));
            }

            var limitTree = CreateSkillTreeWithNodes("tree.limit", limitNodes);

            try
            {
                SetAutoProperty(characterDefinition, "Id", "character.stale-progress");
                SetAutoProperty(classData, "Id", "class.stale-progress");
                SetAutoProperty(classData, "ResourceType", ResourceType.Momentum);
                classData.SkillTrees.Add(validTree);
                classData.SkillTrees.Add(limitTree);
                catalog.CharacterDefinitions.Add(characterDefinition);
                catalog.CharacterClasses.Add(classData);

                var saveData = GameSaveData.CreateNew("Stale skill tree progress");
                saveData.Party.MaxActiveMembers = Party.DefaultMaxActiveMembers;
                saveData.Party.ActiveLeaderId = "character.stale-progress";
                saveData.Party.ActiveMemberIds.Add("character.stale-progress");
                saveData.Characters.Add(new CharacterSaveData
                {
                    CharacterId = "character.stale-progress",
                    ClassId = "class.stale-progress",
                    Level = 1,
                    CurrentHealth = 1,
                    LifeState = CharacterLifeState.Alive,
                    AvailabilityState = CharacterAvailabilityState.Available,
                    ResourcePool = new ResourcePoolSaveData
                    {
                        ResourceType = ResourceType.Momentum
                    },
                    Resistances = new ResistanceSetSaveData(),
                    SkillTreeProgress = new List<SkillTreeProgressSaveData>
                    {
                        new SkillTreeProgressSaveData
                        {
                            TreeId = "tree.valid",
                            NodeInvestments = new List<SkillTreeNodeInvestmentSaveData>
                            {
                                new SkillTreeNodeInvestmentSaveData
                                {
                                    NodeId = "node.valid",
                                    Ranks = 1
                                },
                                new SkillTreeNodeInvestmentSaveData
                                {
                                    NodeId = "node.unknown",
                                    Ranks = 3
                                },
                                new SkillTreeNodeInvestmentSaveData
                                {
                                    NodeId = "node.capped",
                                    Ranks = 0
                                }
                            }
                        },
                        new SkillTreeProgressSaveData
                        {
                            TreeId = "tree.valid",
                            NodeInvestments = new List<SkillTreeNodeInvestmentSaveData>
                            {
                                new SkillTreeNodeInvestmentSaveData
                                {
                                    NodeId = "node.valid",
                                    Ranks = 3
                                },
                                new SkillTreeNodeInvestmentSaveData
                                {
                                    NodeId = "node.capped",
                                    Ranks = 9
                                },
                                new SkillTreeNodeInvestmentSaveData
                                {
                                    NodeId = "node.negative",
                                    Ranks = -1
                                }
                            }
                        },
                        new SkillTreeProgressSaveData
                        {
                            TreeId = "tree.missing",
                            NodeInvestments = new List<SkillTreeNodeInvestmentSaveData>
                            {
                                new SkillTreeNodeInvestmentSaveData
                                {
                                    NodeId = "node.valid",
                                    Ranks = 1
                                }
                            }
                        },
                        new SkillTreeProgressSaveData
                        {
                            TreeId = "tree.limit",
                            NodeInvestments = CreateSkillTreeSaveInvestments(
                                "node.limit.",
                                51,
                                ranks: 1)
                        }
                    }
                });

                Assert.That(
                    new GameSessionRestoreFactory().TryCreate(
                        saveData,
                        catalog,
                        out var restoredSession),
                    Is.True);

                var restoredCharacter = restoredSession.CharactersById[
                    "character.stale-progress"];
                Assert.That(restoredCharacter.SkillTreeProgress, Has.Count.EqualTo(2));

                var restoredValidTree = restoredCharacter.SkillTreeProgress[0];
                Assert.That(restoredValidTree.TreeId, Is.EqualTo("tree.valid"));
                Assert.That(restoredValidTree.NodeInvestments, Has.Count.EqualTo(2));
                Assert.That(
                    restoredValidTree.NodeInvestments[0].Ranks,
                    Is.EqualTo(2));
                Assert.That(
                    restoredValidTree.NodeInvestments[1].Ranks,
                    Is.EqualTo(2));

                var restoredLimitTree = restoredCharacter.SkillTreeProgress[1];
                Assert.That(restoredLimitTree.TreeId, Is.EqualTo("tree.limit"));
                Assert.That(
                    restoredLimitTree.NodeInvestments,
                    Has.Count.EqualTo(SkillTreeProgressionSystem.MaximumPointsPerTree));
                foreach (var investment in restoredLimitTree.NodeInvestments)
                {
                    Assert.That(investment, Is.Not.Null);
                    Assert.That(investment.Ranks, Is.EqualTo(1));
                }

                var legacySave = GameSaveData.CreateNew("Legacy empty skill tree progress");
                legacySave.Party.MaxActiveMembers = Party.DefaultMaxActiveMembers;
                legacySave.Party.ActiveLeaderId = "character.stale-progress";
                legacySave.Party.ActiveMemberIds.Add("character.stale-progress");
                legacySave.Characters.Add(new CharacterSaveData
                {
                    CharacterId = "character.stale-progress",
                    ClassId = "class.stale-progress",
                    Level = 1,
                    CurrentHealth = 1,
                    LifeState = CharacterLifeState.Alive,
                    AvailabilityState = CharacterAvailabilityState.Available,
                    ResourcePool = new ResourcePoolSaveData
                    {
                        ResourceType = ResourceType.Momentum
                    },
                    Resistances = new ResistanceSetSaveData(),
                    SkillTreeProgress = null
                });

                Assert.That(
                    new GameSessionRestoreFactory().TryCreate(
                        legacySave,
                        catalog,
                        out var legacySession),
                    Is.True);
                Assert.That(
                    legacySession.CharactersById["character.stale-progress"]
                        .SkillTreeProgress,
                    Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(limitTree);
                Object.DestroyImmediate(validTree);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(classData);
                Object.DestroyImmediate(characterDefinition);
            }
        }

        [Test]
        public void TryCreate_MergesDuplicateSkillTreeRecordsInDefinitionOrder()
        {
            var characterDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var nodes = new List<SkillTreeNodeDefinition>();
            for (var index = 0; index < 52; index++)
            {
                nodes.Add(CreateSkillTreeNode($"node.canonical.{index}", 1));
            }

            var tree = CreateSkillTreeWithNodes("tree.canonical", nodes);

            try
            {
                SetAutoProperty(characterDefinition, "Id", "character.canonical");
                SetAutoProperty(classData, "Id", "class.canonical");
                SetAutoProperty(classData, "ResourceType", ResourceType.Momentum);
                classData.SkillTrees.Add(tree);
                catalog.CharacterDefinitions.Add(characterDefinition);
                catalog.CharacterClasses.Add(classData);

                var firstSave = CreateSkillTreeSave(
                    "Canonical skill tree A",
                    "character.canonical",
                    "class.canonical",
                    new List<SkillTreeProgressSaveData>
                    {
                        new SkillTreeProgressSaveData
                        {
                            TreeId = "tree.canonical",
                            NodeInvestments = CreateSkillTreeSaveInvestmentsRange(
                                "node.canonical.",
                                0,
                                31,
                                ranks: 1,
                                reverse: false)
                        },
                        new SkillTreeProgressSaveData
                        {
                            TreeId = "tree.canonical",
                            NodeInvestments = CreateSkillTreeSaveInvestmentsRange(
                                "node.canonical.",
                                30,
                                52,
                                ranks: 1,
                                reverse: true)
                        }
                    });
                var secondSave = CreateSkillTreeSave(
                    "Canonical skill tree B",
                    "character.canonical",
                    "class.canonical",
                    new List<SkillTreeProgressSaveData>
                    {
                        new SkillTreeProgressSaveData
                        {
                            TreeId = "tree.canonical",
                            NodeInvestments = CreateSkillTreeSaveInvestmentsRange(
                                "node.canonical.",
                                30,
                                52,
                                ranks: 1,
                                reverse: false)
                        },
                        new SkillTreeProgressSaveData
                        {
                            TreeId = "tree.canonical",
                            NodeInvestments = CreateSkillTreeSaveInvestmentsRange(
                                "node.canonical.",
                                0,
                                31,
                                ranks: 1,
                                reverse: true)
                        }
                    });

                Assert.That(
                    new GameSessionRestoreFactory().TryCreate(
                        firstSave,
                        catalog,
                        out var firstSession),
                    Is.True);
                Assert.That(
                    new GameSessionRestoreFactory().TryCreate(
                        secondSave,
                        catalog,
                        out var secondSession),
                    Is.True);

                var firstProgress = firstSession.CharactersById[
                    "character.canonical"].SkillTreeProgress;
                var secondProgress = secondSession.CharactersById[
                    "character.canonical"].SkillTreeProgress;
                Assert.That(firstProgress, Has.Count.EqualTo(1));
                Assert.That(secondProgress, Has.Count.EqualTo(1));
                Assert.That(
                    SkillTreeProgressSignature(firstProgress[0]),
                    Is.EqualTo(SkillTreeProgressSignature(secondProgress[0])));
                Assert.That(firstProgress[0].NodeInvestments, Has.Count.EqualTo(50));
                Assert.That(
                    firstProgress[0].NodeInvestments[0].NodeId,
                    Is.EqualTo("node.canonical.0"));
                Assert.That(
                    firstProgress[0].NodeInvestments[49].NodeId,
                    Is.EqualTo("node.canonical.49"));
                Assert.That(
                    firstProgress[0].NodeInvestments[49].Ranks,
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(classData);
                Object.DestroyImmediate(characterDefinition);
            }
        }

        [Test]
        public void TryCreate_PreservesValidHighTierPrerequisiteNodeByDefinitionOrder()
        {
            var characterDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var prerequisiteNode = CreateSkillTreeNode("node.prerequisite", 1);
            var highTierNode = CreateSkillTreeNode("node.high-tier", 1);
            var requirement = new SkillTreeNodeRequirement();
            SetAutoProperty(requirement, "NodeId", "node.prerequisite");
            SetAutoProperty(requirement, "MinimumRanks", 1);
            SetAutoProperty(
                highTierNode,
                "Tier",
                3);
            SetAutoProperty(
                highTierNode,
                "Requirements",
                new List<SkillTreeNodeRequirement> { requirement });
            var tree = CreateSkillTreeWithNodes(
                "tree.high-tier",
                new List<SkillTreeNodeDefinition>
                {
                    prerequisiteNode,
                    highTierNode
                });

            try
            {
                SetAutoProperty(characterDefinition, "Id", "character.high-tier");
                SetAutoProperty(classData, "Id", "class.high-tier");
                SetAutoProperty(classData, "ResourceType", ResourceType.Momentum);
                classData.SkillTrees.Add(tree);
                catalog.CharacterDefinitions.Add(characterDefinition);
                catalog.CharacterClasses.Add(classData);

                var saveData = CreateSkillTreeSave(
                    "High-tier skill tree",
                    "character.high-tier",
                    "class.high-tier",
                    new List<SkillTreeProgressSaveData>
                    {
                        new SkillTreeProgressSaveData
                        {
                            TreeId = "tree.high-tier",
                            NodeInvestments = new List<SkillTreeNodeInvestmentSaveData>
                            {
                                new SkillTreeNodeInvestmentSaveData
                                {
                                    NodeId = "node.high-tier",
                                    Ranks = 1
                                },
                                new SkillTreeNodeInvestmentSaveData
                                {
                                    NodeId = "node.prerequisite",
                                    Ranks = 1
                                }
                            }
                        }
                    });

                Assert.That(
                    new GameSessionRestoreFactory().TryCreate(
                        saveData,
                        catalog,
                        out var restoredSession),
                    Is.True);
                var progress = restoredSession.CharactersById[
                    "character.high-tier"].SkillTreeProgress[0];
                Assert.That(progress.NodeInvestments, Has.Count.EqualTo(2));
                Assert.That(
                    progress.NodeInvestments[0].NodeId,
                    Is.EqualTo("node.prerequisite"));
                Assert.That(progress.NodeInvestments[0].Ranks, Is.EqualTo(1));
                Assert.That(
                    progress.NodeInvestments[1].NodeId,
                    Is.EqualTo("node.high-tier"));
                Assert.That(progress.NodeInvestments[1].Ranks, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(classData);
                Object.DestroyImmediate(characterDefinition);
            }
        }

        [Test]
        public void ActiveSkillLoadout_EnforcesCapacityDuplicatesAndBasicAttack()
        {
            var loadout = new ActiveSkillLoadout();
            var knownSkillIds = new[]
            {
                "skill.basic",
                "skill.one",
                "skill.two",
                "skill.three",
                "skill.four",
                "skill.five",
                "skill.six",
                "skill.seven"
            };

            Assert.That(
                loadout.TryInitialize(
                    knownSkillIds,
                    "skill.basic",
                    new[]
                    {
                        "skill.basic",
                        "skill.one",
                        "skill.two",
                        "skill.three",
                        "skill.four"
                    }),
                Is.True);
            Assert.That(loadout.Capacity, Is.EqualTo(5));
            Assert.That(
                loadout.TryAddSkill("skill.one", knownSkillIds),
                Is.False);
            Assert.That(
                loadout.TryAddSkill("skill.five", knownSkillIds),
                Is.False);
            Assert.That(loadout.TryRemoveSkill("skill.basic"), Is.False);

            Assert.That(
                loadout.TrySetCapacityModifier("necklace.extra_slots", 2),
                Is.True);
            Assert.That(loadout.Capacity, Is.EqualTo(7));
            Assert.That(
                loadout.TryAddSkill("skill.five", knownSkillIds),
                Is.True);
            Assert.That(
                loadout.TryAddSkill("skill.six", knownSkillIds),
                Is.True);
            Assert.That(
                loadout.TryAddSkill("skill.seven", knownSkillIds),
                Is.False);
        }

        [Test]
        public void ActiveSkillLoadout_PreservesOrderAndRejectsInvalidRequests()
        {
            var loadout = new ActiveSkillLoadout();

            Assert.That(
                loadout.TryInitialize(
                    new[] { "skill.basic", "skill.one", "skill.two" },
                    "skill.basic",
                    new[] { "skill.two", "skill.basic", "skill.one" }),
                Is.True);
            Assert.That(
                loadout.ActiveSkillIds,
                Is.EqualTo(new[] { "skill.basic", "skill.two", "skill.one" }));
            Assert.That(
                loadout.TryMoveSkill("skill.one", 0),
                Is.False);
            Assert.That(
                loadout.ActiveSkillIds,
                Is.EqualTo(new[] { "skill.basic", "skill.two", "skill.one" }));
            Assert.That(
                loadout.TryInitialize(
                    new[] { "skill.basic", "skill.one" },
                    "skill.basic",
                    new[] { "skill.basic", "skill.basic" }),
                Is.False);
            Assert.That(
                loadout.TryInitialize(
                    new[] { "skill.basic", "skill.one" },
                    "skill.basic",
                    new[] { "skill.one" }),
                Is.False);
        }

        [Test]
        public void ActiveSkillLoadout_ReplacesKnownInactiveSkillAtTargetIndexAtomically()
        {
            var loadout = new ActiveSkillLoadout();
            var knownSkillIds = new[]
            {
                "skill.basic",
                "skill.target",
                "skill.other",
                "skill.replacement"
            };

            Assert.That(
                loadout.TryInitialize(
                    knownSkillIds,
                    "skill.basic",
                    new[] { "skill.basic", "skill.target", "skill.other" }),
                Is.True);
            var capacityBeforeReplace = loadout.Capacity;

            Assert.That(
                loadout.TryReplaceSkill(
                    "skill.target",
                    "skill.replacement",
                    knownSkillIds),
                Is.True);
            Assert.That(
                loadout.ActiveSkillIds,
                Is.EqualTo(new[]
                {
                    "skill.basic",
                    "skill.replacement",
                    "skill.other"
                }));
            Assert.That(loadout.Capacity, Is.EqualTo(capacityBeforeReplace));

            var stateAfterSuccess = loadout.ActiveSkillIds;
            Assert.That(
                loadout.TryReplaceSkill(
                    "skill.basic",
                    "skill.target",
                    knownSkillIds),
                Is.False);
            Assert.That(
                loadout.TryReplaceSkill(
                    "skill.other",
                    "skill.replacement",
                    knownSkillIds),
                Is.False);
            Assert.That(
                loadout.TryReplaceSkill(
                    "skill.missing",
                    "skill.target",
                    knownSkillIds),
                Is.False);
            Assert.That(
                loadout.TryReplaceSkill(
                    "skill.other",
                    "skill.unknown",
                    knownSkillIds),
                Is.False);
            Assert.That(loadout.ActiveSkillIds, Is.EqualTo(stateAfterSuccess));
        }

        [Test]
        public void ActiveSkillLoadout_PinsBasicAttackAtIndexZeroAcrossInitializeAndMove()
        {
            var loadout = new ActiveSkillLoadout();
            var knownSkillIds = new[]
            {
                "skill.basic",
                "skill.one",
                "skill.two"
            };

            Assert.That(
                loadout.TryInitialize(
                    knownSkillIds,
                    "skill.basic",
                    new[] { "skill.two", "skill.basic", "skill.one" }),
                Is.True);
            Assert.That(
                loadout.ActiveSkillIds,
                Is.EqualTo(new[] { "skill.basic", "skill.two", "skill.one" }));
            Assert.That(loadout.TryMoveSkill("skill.basic", 1), Is.False);
            Assert.That(loadout.TryMoveSkill("skill.two", 0), Is.False);
            Assert.That(
                loadout.ActiveSkillIds,
                Is.EqualTo(new[] { "skill.basic", "skill.two", "skill.one" }));
        }

        [Test]
        public void SkillLoadoutSystem_RejectsChangesDuringCombat()
        {
            var characterDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var basicSkill = CreateSkill("skill.basic");
            var extraSkill = CreateSkill("skill.extra");
            var session = new GameSession(GameModeState.Exploration, "campaign.loadout");

            try
            {
                SetAutoProperty(characterDefinition, "Id", "character.loadout");
                SetAutoProperty(
                    characterDefinition,
                    "StartingSkills",
                    new List<SkillData> { basicSkill, extraSkill });
                SetAutoProperty(classData, "Id", "class.loadout");
                SetAutoProperty(classData, "ResourceType", ResourceType.Momentum);
                SetAutoProperty(classData, "BasicAttackSkill", basicSkill);

                var character = new CharacterInstanceFactory().Create(
                    characterDefinition,
                    classData);
                Assert.That(session.TryRegisterCharacter(character), Is.True);

                var loadoutSystem = new SkillLoadoutSystem();
                Assert.That(
                    loadoutSystem.TrySetActiveSkills(
                        session,
                        "character.loadout",
                        new[] { "skill.basic", "skill.extra" }),
                    Is.True);

                Assert.That(
                    session.TrySetActiveCombat(
                        new CombatSession { CombatId = "combat.loadout" }),
                    Is.True);
                Assert.That(
                    loadoutSystem.TrySetActiveSkills(
                        session,
                        "character.loadout",
                        new[] { "skill.extra", "skill.basic" }),
                    Is.False);
                Assert.That(
                    character.ActiveSkillLoadout.ActiveSkillIds,
                    Is.EqualTo(new[] { "skill.basic", "skill.extra" }));
            }
            finally
            {
                Object.DestroyImmediate(extraSkill);
                Object.DestroyImmediate(basicSkill);
                Object.DestroyImmediate(classData);
                Object.DestroyImmediate(characterDefinition);
            }
        }

        [Test]
        public void SkillLoadoutSystem_ReplacesSkillAndGuardsBasicInvalidAndCombat()
        {
            var characterDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var basicSkill = CreateSkill("skill.basic");
            var targetSkill = CreateSkill("skill.target");
            var replacementSkill = CreateSkill("skill.replacement");
            var session = new GameSession(GameModeState.Exploration, "campaign.loadout-replace");

            try
            {
                SetAutoProperty(characterDefinition, "Id", "character.loadout-replace");
                SetAutoProperty(
                    characterDefinition,
                    "StartingSkills",
                    new List<SkillData> { targetSkill, replacementSkill });
                SetAutoProperty(classData, "Id", "class.loadout-replace");
                SetAutoProperty(classData, "ResourceType", ResourceType.Momentum);
                SetAutoProperty(classData, "BasicAttackSkill", basicSkill);

                var character = new CharacterInstanceFactory().Create(
                    characterDefinition,
                    classData);
                Assert.That(session.TryRegisterCharacter(character), Is.True);

                var loadoutSystem = new SkillLoadoutSystem();
                Assert.That(
                    loadoutSystem.TrySetActiveSkills(
                        session,
                        character.CharacterId,
                        new[] { "skill.basic", "skill.target" }),
                    Is.True);
                var capacityBeforeReplace = character.ActiveSkillLoadout.Capacity;

                Assert.That(
                    loadoutSystem.TryReplaceSkill(
                        session,
                        character.CharacterId,
                        "skill.target",
                        "skill.replacement"),
                    Is.True);
                Assert.That(
                    character.ActiveSkillLoadout.ActiveSkillIds,
                    Is.EqualTo(new[] { "skill.basic", "skill.replacement" }));
                Assert.That(
                    character.ActiveSkillLoadout.Capacity,
                    Is.EqualTo(capacityBeforeReplace));

                Assert.That(
                    loadoutSystem.TryMoveSkill(
                        session,
                        character.CharacterId,
                        "skill.basic",
                        1),
                    Is.False);
                Assert.That(
                    loadoutSystem.TryMoveSkill(
                        session,
                        character.CharacterId,
                        "skill.replacement",
                        0),
                    Is.False);

                var stateAfterSuccess = character.ActiveSkillLoadout.ActiveSkillIds;
                Assert.That(
                    loadoutSystem.TryReplaceSkill(
                        session,
                        character.CharacterId,
                        "skill.basic",
                        "skill.target"),
                    Is.False);
                Assert.That(
                    loadoutSystem.TryReplaceSkill(
                        session,
                        character.CharacterId,
                        "skill.replacement",
                        "skill.basic"),
                    Is.False);
                Assert.That(
                    loadoutSystem.TryReplaceSkill(
                        session,
                        character.CharacterId,
                        "skill.missing",
                        "skill.target"),
                    Is.False);
                Assert.That(
                    loadoutSystem.TryReplaceSkill(
                        session,
                        character.CharacterId,
                        "skill.replacement",
                        "skill.unknown"),
                    Is.False);
                Assert.That(
                    character.ActiveSkillLoadout.ActiveSkillIds,
                    Is.EqualTo(stateAfterSuccess));

                Assert.That(
                    session.TrySetActiveCombat(
                        new CombatSession { CombatId = "combat.loadout-replace" }),
                    Is.True);
                Assert.That(
                    loadoutSystem.TryReplaceSkill(
                        session,
                        character.CharacterId,
                        "skill.replacement",
                        "skill.target"),
                    Is.False);
                Assert.That(
                    character.ActiveSkillLoadout.ActiveSkillIds,
                    Is.EqualTo(stateAfterSuccess));
            }
            finally
            {
                Object.DestroyImmediate(replacementSkill);
                Object.DestroyImmediate(targetSkill);
                Object.DestroyImmediate(basicSkill);
                Object.DestroyImmediate(classData);
                Object.DestroyImmediate(characterDefinition);
            }
        }

        [Test]
        public void CharacterInstanceFactory_InitializesLoadoutFromExplicitBasicAttack()
        {
            var characterDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var basicSkill = CreateSkill("skill.basic");
            var firstSkill = CreateSkill("skill.first");
            var secondSkill = CreateSkill("skill.second");
            var thirdSkill = CreateSkill("skill.third");

            try
            {
                SetAutoProperty(characterDefinition, "Id", "character.factory");
                SetAutoProperty(
                    characterDefinition,
                    "StartingSkills",
                    new List<SkillData>
                    {
                        firstSkill,
                        secondSkill,
                        thirdSkill
                    });
                SetAutoProperty(classData, "Id", "class.factory");
                SetAutoProperty(classData, "ResourceType", ResourceType.Momentum);
                SetAutoProperty(classData, "BasicAttackSkill", basicSkill);

                var character = new CharacterInstanceFactory().Create(
                    characterDefinition,
                    classData);

                Assert.That(character.ActiveSkillLoadout.IsInitialized, Is.True);
                Assert.That(
                    character.ActiveSkillLoadout.ActiveSkillIds,
                    Is.EqualTo(new[]
                    {
                        "skill.basic",
                        "skill.first",
                        "skill.second",
                        "skill.third"
                    }));
                Assert.That(
                    character.Skills.ConvertAll(skill => skill.SkillId),
                    Is.EqualTo(new[]
                    {
                        "skill.first",
                        "skill.second",
                        "skill.third",
                        "skill.basic"
                    }));
                Assert.That(
                    character.GetActiveSkills(),
                    Is.EqualTo(new[] { basicSkill, firstSkill, secondSkill, thirdSkill }));
            }
            finally
            {
                Object.DestroyImmediate(thirdSkill);
                Object.DestroyImmediate(secondSkill);
                Object.DestroyImmediate(firstSkill);
                Object.DestroyImmediate(basicSkill);
                Object.DestroyImmediate(classData);
                Object.DestroyImmediate(characterDefinition);
            }
        }

        [Test]
        public void SaveRestore_RoundTripsActiveSkillLoadoutAndLegacySaveMigrates()
        {
            var characterDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var basicSkill = CreateSkill("skill.basic");
            var firstSkill = CreateSkill("skill.first");
            var secondSkill = CreateSkill("skill.second");
            var thirdSkill = CreateSkill("skill.third");

            try
            {
                SetAutoProperty(characterDefinition, "Id", "character.save-loadout");
                SetAutoProperty(
                    characterDefinition,
                    "StartingSkills",
                    new List<SkillData>
                    {
                        firstSkill,
                        secondSkill,
                        thirdSkill
                    });
                SetAutoProperty(classData, "Id", "class.save-loadout");
                SetAutoProperty(classData, "ResourceType", ResourceType.Momentum);
                SetAutoProperty(classData, "BasicAttackSkill", basicSkill);
                catalog.CharacterDefinitions.Add(characterDefinition);
                catalog.CharacterClasses.Add(classData);
                catalog.SkillDefinitions.Add(basicSkill);
                catalog.SkillDefinitions.Add(firstSkill);
                catalog.SkillDefinitions.Add(secondSkill);
                catalog.SkillDefinitions.Add(thirdSkill);

                var character = new CharacterInstanceFactory().Create(
                    characterDefinition,
                    classData);
                var session = new GameSession(
                    GameModeState.Exploration,
                    "campaign.save-loadout");
                Assert.That(session.TryRegisterCharacter(character), Is.True);
                Assert.That(
                    session.TryAddCharacterToActiveParty(character.CharacterId),
                    Is.True);

                var loadoutSystem = new SkillLoadoutSystem();
                Assert.That(
                    loadoutSystem.TrySetActiveSkills(
                        session,
                        character.CharacterId,
                        new[]
                        {
                            "skill.second",
                            "skill.basic",
                            "skill.first"
                        }),
                    Is.True);
                Assert.That(
                    loadoutSystem.TrySetCapacityModifier(
                        session,
                        character.CharacterId,
                        "necklace.extra_slots",
                        2),
                    Is.True);
                Assert.That(
                    loadoutSystem.TrySetActiveSkills(
                        session,
                        character.CharacterId,
                        new[]
                        {
                            "skill.first",
                            "skill.basic",
                            "skill.second"
                        }),
                    Is.True);
                Assert.That(character.ActiveSkillLoadout.Capacity, Is.EqualTo(7));

                var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(
                    session,
                    "Loadout save");
                var serializer = new GameSaveJsonSerializer();
                Assert.That(
                    serializer.TryDeserialize(
                        serializer.Serialize(snapshot),
                        out var restoredSaveData),
                    Is.True);
                Assert.That(restoredSaveData.Characters[0].HasActiveSkillLoadout, Is.True);
                Assert.That(
                    restoredSaveData.Characters[0].ActiveSkillIds,
                    Is.EqualTo(new[]
                    {
                        "skill.first",
                        "skill.basic",
                        "skill.second"
                    }));

                Assert.That(
                    new GameSessionRestoreFactory().TryCreate(
                        restoredSaveData,
                        catalog,
                        out var restoredSession),
                    Is.True);
                var restoredCharacter = restoredSession.CharactersById[
                    "character.save-loadout"];
                Assert.That(
                    restoredCharacter.ActiveSkillLoadout.ActiveSkillIds,
                    Is.EqualTo(new[]
                    {
                        "skill.first",
                        "skill.basic",
                        "skill.second"
                    }));
                Assert.That(restoredCharacter.ActiveSkillLoadout.Capacity, Is.EqualTo(7));

                var legacySave = GameSaveData.CreateNew("Legacy loadout");
                legacySave.Party.MaxActiveMembers = Party.DefaultMaxActiveMembers;
                legacySave.Party.ActiveLeaderId = "character.save-loadout";
                legacySave.Party.ActiveMemberIds.Add("character.save-loadout");
                legacySave.Characters.Add(new CharacterSaveData
                {
                    CharacterId = "character.save-loadout",
                    ClassId = "class.save-loadout",
                    Level = 1,
                    CurrentHealth = 1,
                    LifeState = CharacterLifeState.Alive,
                    AvailabilityState = CharacterAvailabilityState.Available,
                    ResourcePool = new ResourcePoolSaveData
                    {
                        ResourceType = ResourceType.Momentum
                    },
                    Resistances = new ResistanceSetSaveData(),
                    SkillIds = new List<string>
                    {
                        "skill.second",
                        "skill.first",
                        "skill.third"
                    }
                });

                Assert.That(
                    new GameSessionRestoreFactory().TryCreate(
                        legacySave,
                        catalog,
                        out var migratedSession),
                    Is.True);
                var migratedCharacter = migratedSession.CharactersById[
                    "character.save-loadout"];
                Assert.That(
                    migratedCharacter.Skills.ConvertAll(skill => skill.SkillId),
                    Is.EqualTo(new[]
                    {
                        "skill.second",
                        "skill.first",
                        "skill.third",
                        "skill.basic"
                    }));
                Assert.That(
                    migratedCharacter.ActiveSkillLoadout.ActiveSkillIds,
                    Is.EqualTo(new[]
                    {
                        "skill.basic",
                        "skill.second",
                        "skill.first",
                        "skill.third"
                    }));
            }
            finally
            {
                Object.DestroyImmediate(thirdSkill);
                Object.DestroyImmediate(secondSkill);
                Object.DestroyImmediate(firstSkill);
                Object.DestroyImmediate(basicSkill);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(classData);
                Object.DestroyImmediate(characterDefinition);
            }
        }

        [Test]
        public void LegacySaveWithoutConfiguredBasicAttackFallsBackToKnownSkills()
        {
            var characterDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var classData = ScriptableObject.CreateInstance<CharacterClassData>();
            var catalog = ScriptableObject.CreateInstance<GameContentCatalog>();
            var firstSkill = CreateSkill("skill.first");
            var secondSkill = CreateSkill("skill.second");

            try
            {
                SetAutoProperty(characterDefinition, "Id", "character.legacy-fallback");
                SetAutoProperty(
                    characterDefinition,
                    "StartingSkills",
                    new List<SkillData> { firstSkill, secondSkill });
                SetAutoProperty(classData, "Id", "class.legacy-fallback");
                SetAutoProperty(classData, "ResourceType", ResourceType.Momentum);
                catalog.CharacterDefinitions.Add(characterDefinition);
                catalog.CharacterClasses.Add(classData);
                catalog.SkillDefinitions.Add(firstSkill);
                catalog.SkillDefinitions.Add(secondSkill);

                var legacySave = GameSaveData.CreateNew("Legacy fallback");
                legacySave.Party.MaxActiveMembers = Party.DefaultMaxActiveMembers;
                legacySave.Party.ActiveLeaderId = "character.legacy-fallback";
                legacySave.Party.ActiveMemberIds.Add("character.legacy-fallback");
                legacySave.Characters.Add(new CharacterSaveData
                {
                    CharacterId = "character.legacy-fallback",
                    ClassId = "class.legacy-fallback",
                    Level = 1,
                    CurrentHealth = 1,
                    LifeState = CharacterLifeState.Alive,
                    AvailabilityState = CharacterAvailabilityState.Available,
                    ResourcePool = new ResourcePoolSaveData
                    {
                        ResourceType = ResourceType.Momentum
                    },
                    Resistances = new ResistanceSetSaveData(),
                    SkillIds = new List<string>
                    {
                        "skill.first",
                        "skill.second"
                    }
                });

                Assert.That(
                    new GameSessionRestoreFactory().TryCreate(
                        legacySave,
                        catalog,
                        out var restoredSession),
                    Is.True);
                var restoredCharacter = restoredSession.CharactersById[
                    "character.legacy-fallback"];
                Assert.That(restoredCharacter.ActiveSkillLoadout.IsInitialized, Is.False);
                Assert.That(
                    restoredCharacter.GetActiveSkills(),
                    Is.EqualTo(new[] { firstSkill, secondSkill }));
            }
            finally
            {
                Object.DestroyImmediate(secondSkill);
                Object.DestroyImmediate(firstSkill);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(classData);
                Object.DestroyImmediate(characterDefinition);
            }
        }

        private static SkillTreeDefinition CreateSkillTree(
            string treeId,
            string nodeId,
            int maxRanks)
        {
            var tree = ScriptableObject.CreateInstance<SkillTreeDefinition>();
            var node = new SkillTreeNodeDefinition();

            SetAutoProperty(tree, "Id", treeId);
            SetAutoProperty(
                tree,
                "Nodes",
                new List<SkillTreeNodeDefinition> { node });
            SetAutoProperty(node, "Id", nodeId);
            SetAutoProperty(node, "Tier", 1);
            SetAutoProperty(node, "MaxRanks", maxRanks);
            SetAutoProperty(
                node,
                "Requirements",
                new List<SkillTreeNodeRequirement>());

            return tree;
        }

        private static SkillTreeDefinition CreateSkillTreeWithNodes(
            string treeId,
            List<SkillTreeNodeDefinition> nodes)
        {
            var tree = ScriptableObject.CreateInstance<SkillTreeDefinition>();
            SetAutoProperty(tree, "Id", treeId);
            SetAutoProperty(tree, "Nodes", nodes);
            return tree;
        }

        private static SkillTreeNodeDefinition CreateSkillTreeNode(
            string nodeId,
            int maxRanks)
        {
            var node = new SkillTreeNodeDefinition();
            SetAutoProperty(node, "Id", nodeId);
            SetAutoProperty(node, "Tier", 1);
            SetAutoProperty(node, "MaxRanks", maxRanks);
            SetAutoProperty(
                node,
                "Requirements",
                new List<SkillTreeNodeRequirement>());
            return node;
        }

        private static List<SkillTreeNodeInvestmentSaveData>
            CreateSkillTreeSaveInvestments(
                string nodeIdPrefix,
                int count,
                int ranks)
        {
            var result = new List<SkillTreeNodeInvestmentSaveData>();
            for (var index = 0; index < count; index++)
            {
                result.Add(new SkillTreeNodeInvestmentSaveData
                {
                    NodeId = $"{nodeIdPrefix}{index}",
                    Ranks = ranks
                });
            }

            return result;
        }

        private static List<SkillTreeNodeInvestmentSaveData>
            CreateSkillTreeSaveInvestmentsRange(
                string nodeIdPrefix,
                int startIndex,
                int endIndexExclusive,
                int ranks,
                bool reverse)
        {
            var result = new List<SkillTreeNodeInvestmentSaveData>();
            var count = endIndexExclusive - startIndex;
            for (var offset = 0; offset < count; offset++)
            {
                var index = reverse
                    ? endIndexExclusive - offset - 1
                    : startIndex + offset;
                result.Add(new SkillTreeNodeInvestmentSaveData
                {
                    NodeId = $"{nodeIdPrefix}{index}",
                    Ranks = ranks
                });
            }

            return result;
        }

        private static GameSaveData CreateSkillTreeSave(
            string displayName,
            string characterId,
            string classId,
            List<SkillTreeProgressSaveData> skillTreeProgress)
        {
            var saveData = GameSaveData.CreateNew(displayName);
            saveData.Party.MaxActiveMembers = Party.DefaultMaxActiveMembers;
            saveData.Party.ActiveLeaderId = characterId;
            saveData.Party.ActiveMemberIds.Add(characterId);
            saveData.Characters.Add(new CharacterSaveData
            {
                CharacterId = characterId,
                ClassId = classId,
                Level = 1,
                CurrentHealth = 1,
                LifeState = CharacterLifeState.Alive,
                AvailabilityState = CharacterAvailabilityState.Available,
                ResourcePool = new ResourcePoolSaveData
                {
                    ResourceType = ResourceType.Momentum
                },
                Resistances = new ResistanceSetSaveData(),
                SkillTreeProgress = skillTreeProgress
            });
            return saveData;
        }

        private static string SkillTreeProgressSignature(
            SkillTreeProgress progress)
        {
            var result = progress?.TreeId ?? string.Empty;
            foreach (var investment in progress?.NodeInvestments ?? new())
            {
                result += $"|{investment?.NodeId}:{investment?.Ranks}";
            }

            return result;
        }

        private static SkillData CreateSkill(string skillId)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            SetAutoProperty(skill, "SkillId", skillId);
            return skill;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                $"Nie znaleziono pola backing field dla {propertyName}.");

            field.SetValue(target, value);
        }
    }
}
