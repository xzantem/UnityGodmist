using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Views.Combat;
using UnityEngine;

namespace Godmist.Views.UI
{
    public class CombatPartyRailView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private Transform entryRoot;
        [SerializeField] private CombatPartyMemberView entryTemplate;
        [Header("Compact Drawer")]
        [SerializeField, Min(0f)] private float verticalPadding = 12f;
        [SerializeField, Min(0f)] private float minimumDrawerHeight = 0f;

        public string SelectedCharacterId { private get; set; }

        private readonly List<CombatPartyMemberView> entries = new();

        private void Awake()
        {
            var layout = entryRoot != null
                ? entryRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>()
                : null;
            if (layout != null)
            {
                layout.childAlignment = TextAnchor.MiddleCenter;
            }
        }

        private void Update()
        {
            var session = sandboxBootstrap?.Session;
            if (session?.Units == null || entryRoot == null || entryTemplate == null)
            {
                SetEntriesVisible(false);
                return;
            }

            var playerUnits = new List<BattleUnit>();
            foreach (var unit in session.Units)
            {
                if (unit?.Character != null && unit.IsPlayerSide)
                {
                    playerUnits.Add(unit);
                }
            }

            while (entries.Count < playerUnits.Count)
            {
                entries.Add(Instantiate(entryTemplate, entryRoot));
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var visible = index < playerUnits.Count;
                entries[index].gameObject.SetActive(visible);
                if (visible)
                {
                    entries[index].Bind(playerUnits[index], playerUnits[index].Character.CharacterId == SelectedCharacterId, playerUnits[index] == session.ActiveUnit);
                }
            }

            ResizeDrawerToVisibleEntries(playerUnits.Count);
        }


        private void SetEntriesVisible(bool visible)
        {
            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    entry.gameObject.SetActive(visible);
                }
            }

            ResizeDrawerToVisibleEntries(visible ? entries.Count : 0);
        }

        private void ResizeDrawerToVisibleEntries(int visibleEntryCount)
        {
            var drawer = GetComponentInParent<CombatEdgeDrawer>();
            var drawerRect = drawer != null
                ? drawer.transform as RectTransform
                : null;
            if (drawerRect == null)
            {
                return;
            }

            var spacing = entryRoot != null
                ? entryRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>()?.spacing ?? 0f
                : 0f;
            var contentHeight = 0f;
            for (var index = 0; index < visibleEntryCount && index < entries.Count; index++)
            {
                if (entries[index] == null)
                {
                    continue;
                }

                var entryRect = entries[index].transform as RectTransform;
                if (entryRect != null)
                {
                    contentHeight += entryRect.rect.height;
                }
            }

            if (visibleEntryCount > 1)
            {
                contentHeight += spacing * (visibleEntryCount - 1);
            }

            var targetHeight = Mathf.Max(
                minimumDrawerHeight,
                contentHeight + verticalPadding * 2f);
            if (Mathf.Approximately(drawerRect.rect.height, targetHeight))
            {
                return;
            }

            drawerRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                targetHeight);
        }
    }
}
