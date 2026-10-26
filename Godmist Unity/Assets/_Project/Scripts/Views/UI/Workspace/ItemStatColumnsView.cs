using TMPro;
using UnityEngine;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>Aligns marked equipment values with the right edge without changing glossary links.</summary>
    [ExecuteAlways]
    public sealed class ItemStatColumnsView : MonoBehaviour
    {
        public const string MarkerId = "item-stat-row";
        public const string Marker = "<link=\"" + MarkerId + "\">\u200B</link>";
        [SerializeField] private TMP_Text detailsText;

        private void OnEnable() { if (detailsText != null) detailsText.OnPreRenderText += AlignValues; }
        private void OnDisable() { if (detailsText != null) detailsText.OnPreRenderText -= AlignValues; }

        private void AlignValues(TMP_TextInfo info)
        {
            if (info == null || detailsText == null) return;
            var right = detailsText.rectTransform.rect.xMax - detailsText.margin.z;
            for (var i = 0; i < info.linkCount; i++)
            {
                var link = info.linkInfo[i];
                if (link.GetLinkID() != MarkerId || link.linkTextLength == 0) continue;
                var marker = link.linkTextfirstCharacterIndex;
                if (marker < 0 || marker >= info.characterCount) continue;
                var line = info.lineInfo[info.characterInfo[marker].lineNumber];
                var first = marker;
                while (first <= line.lastCharacterIndex && info.characterInfo[first].character != ':') first++;
                first++;
                while (first <= line.lastCharacterIndex && !info.characterInfo[first].isVisible) first++;
                if (first > line.lastCharacterIndex) continue;
                var last = line.lastCharacterIndex;
                while (last >= first && !info.characterInfo[last].isVisible) last--;
                if (last < first) continue;
                var offset = right - info.characterInfo[last].topRight.x;
                if (offset < 0) continue; // Keep wrapped or unusually long comparison rows readable.
                for (var c = first; c <= last; c++)
                {
                    var character = info.characterInfo[c];
                    if (character.isVisible)
                    {
                        var vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                        for (var v = 0; v < 4; v++) vertices[character.vertexIndex + v].x += offset;
                    }
                    character.bottomLeft.x += offset; character.topLeft.x += offset;
                    character.bottomRight.x += offset; character.topRight.x += offset;
                    character.origin += offset; character.xAdvance += offset;
                    info.characterInfo[c] = character;
                }
            }
        }
    }
}
