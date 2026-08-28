using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public class DungeonSpikeTrapView : DungeonObjectView
    {
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private Color armedColor = new(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private Color triggeredColor = new(0.45f, 0.45f, 0.45f, 1f);

        private ExplorationPartyController partyController;
        private DungeonFloorInstance floor;
        private Tilemap floorTilemap;
        private SpikeTrapDungeonObjectInstance trap;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonFloorInstance nextFloor,
            SpikeTrapDungeonObjectInstance nextTrap,
            Tilemap nextFloorTilemap,
            Vector3 worldPosition)
        {
            if (!TryBind(nextTrap, worldPosition)) return;

            partyController = nextPartyController;
            floor = nextFloor;
            trap = nextTrap;
            floorTilemap = nextFloorTilemap;
            visualRenderer ??= GetComponent<SpriteRenderer>();
            if (visualRenderer != null)
            {
                visualRenderer.sortingOrder = Mathf.Max(
                    visualRenderer.sortingOrder,
                    6);
            }
            RefreshVisual();
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen
                || trap == null
                || floor == null
                || partyController == null
                || floorTilemap == null)
            {
                return;
            }

            // The floor changes every trap in the activation group at once,
            // while each view owns its own renderer. Refresh already-triggered
            // views as well so the whole group is visibly triggered.
            if (trap.IsTriggered)
            {
                RefreshVisual();
                return;
            }

            var session = GameBootstrap.Instance?.Session;

            if (session == null
                || session.CurrentMode != GameModeState.Exploration
                || session.ActiveCombat != null)
            {
                return;
            }

            var trapCell = new Vector3Int(trap.Position.x, trap.Position.y, 0);
            if (!IsActivePartyMemberOnCell(trapCell)) return;

            if (!floor.TryTriggerSpikeTrap(
                    trap.InstanceId,
                    out var triggeredTraps))
            {
                return;
            }

            foreach (var triggeredTrap in triggeredTraps)
            {
                foreach (var characterView in partyController.ActiveMemberViews)
                {
                    if (characterView == null
                        || !session.PlayerParty.ActiveMemberIds.Contains(
                            characterView.CharacterId)
                        || floorTilemap.WorldToCell(characterView.Position)
                            != new Vector3Int(
                                triggeredTrap.Position.x,
                                triggeredTrap.Position.y,
                                0)
                        || session == null
                        || !session.CharactersById.TryGetValue(
                            characterView.CharacterId,
                            out var character))
                    {
                        continue;
                    }

                    ApplyDamage(character, triggeredTrap.Damage);
                }
            }

            RefreshVisual();
            Debug.Log(
                $"Dungeon spike trap '{trap.InstanceId}' triggered "
                + $"({triggeredTraps.Count} trap(s)).");
        }

        private bool IsActivePartyMemberOnCell(Vector3Int cell)
        {
            var session = GameBootstrap.Instance?.Session;
            if (session?.PlayerParty == null) return false;

            foreach (var characterView in partyController.ActiveMemberViews)
            {
                if (characterView == null
                    || !session.PlayerParty.ActiveMemberIds.Contains(
                        characterView.CharacterId)
                    || floorTilemap.WorldToCell(characterView.Position) != cell)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void RefreshVisual()
        {
            if (visualRenderer != null)
            {
                visualRenderer.color = trap != null && trap.IsTriggered
                    ? triggeredColor
                    : armedColor;
            }
        }

        private static void ApplyDamage(CharacterInstance character, int damage)
        {
            if (character == null
                || character.LifeState != CharacterLifeState.Alive
                || character.DebugIsInvulnerable)
            {
                return;
            }

            character.CurrentHealth = Mathf.Max(
                0,
                character.CurrentHealth - Mathf.Max(1, damage));

            if (character.CurrentHealth == 0)
            {
                character.LifeState = CharacterLifeState.Downed;
            }
        }
    }
}
