using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(menuName = "Godmist/Dungeons/Vegetation Presentation")]
    public sealed class DungeonVegetationPresentation : ScriptableObject
    {
        public static Matrix4x4 CenteredGroundTransform(Sprite sprite)
        {
            var scale = new Vector3(1f / sprite.bounds.size.x, 1f / sprite.bounds.size.y, 1);
            return Matrix4x4.TRS(-Vector3.Scale(sprite.bounds.center, scale), Quaternion.identity, scale);
        }
        [field: SerializeField] public TileBase GroundTile { get; private set; }
        [field: SerializeField] public Sprite[] TreeVariants { get; private set; } = System.Array.Empty<Sprite>();
        [field: SerializeField] public Sprite[] SmallTreeVariants { get; private set; } = System.Array.Empty<Sprite>();
        public static Sprite SelectTreeSprite(Sprite original, Sprite[] variants, uint noise)
        {
            // Use the new native mature art. Legacy sapling/dead slots 3/4
            // must not replace mature trees when space is constrained.
            var slot = noise % 72u;
            var index = slot == 11 ? 5
                : slot < 18 ? 2 : slot < 36 ? 1 : slot < 54 ? 0 : -1;
            return index >= 0 && variants != null && index < variants.Length && variants[index] != null
                ? variants[index] : original;
        }
        public static Sprite SelectDetailSprite(Sprite original, Sprite[] variants, uint noise)
        {
            if (variants == null || variants.Length == 0) return original;
            // Mix independently from mirror/colour bits; repeat binding stays stable.
            unchecked { noise ^= noise >> 16; noise *= 2246822519u; noise ^= noise >> 13; }
            var slot = (int)(noise % (uint)(variants.Length + 1));
            return slot == 0 ? original : variants[slot - 1] ?? original;
        }
        [field: SerializeField] public Sprite[] WoodlandFungiVariants { get; private set; } = System.Array.Empty<Sprite>();
        [field: SerializeField] public Sprite[] WoodlandFernVariants { get; private set; } = System.Array.Empty<Sprite>();
        [field: SerializeField] public Sprite[] ExposedRootsVariants { get; private set; } = System.Array.Empty<Sprite>();
        [field: SerializeField] public Sprite[] DeadwoodVariants { get; private set; } = System.Array.Empty<Sprite>();
        [field: SerializeField] public Sprite[] FallenLogVariants { get; private set; } = System.Array.Empty<Sprite>();
        [field: SerializeField] public Sprite[] SmallStoneVariants { get; private set; } = System.Array.Empty<Sprite>();
        [field: SerializeField] public Sprite[] LowShrubVariants { get; private set; } = System.Array.Empty<Sprite>();
        [field: SerializeField] public Sprite[] GroundCoverVariants { get; private set; } = System.Array.Empty<Sprite>();
        [field: SerializeField] public Sprite BoundaryTree { get; private set; }
        [field: SerializeField] public Sprite SoilSprite { get; private set; }
        [field: SerializeField] public Sprite GroundCover { get; private set; }
        [field: SerializeField] public Sprite LowShrub { get; private set; }
        [field: SerializeField] public Sprite[] LowHedgeSegments { get; private set; } = System.Array.Empty<Sprite>();
        [field: SerializeField] public Sprite DryShrub { get; private set; }
        [field: SerializeField] public Sprite Deadwood { get; private set; }
        [field: SerializeField] public Sprite ExposedRoots { get; private set; }
        [field: SerializeField] public Sprite WoodlandFungi { get; private set; }
        [field: SerializeField] public Sprite SmallStone { get; private set; }
        [field: SerializeField] public Sprite FallenLog { get; private set; }
        [field: SerializeField] public Sprite WoodlandFern { get; private set; }
        [field: SerializeField] public Color GroundTint { get; private set; } = Color.white;
        [field: SerializeField] public Color TreeTint { get; private set; } = Color.white;
        [field: SerializeField, Min(.1f)] public float TreeWidthInCells { get; private set; } = 1.5f;
        [field: SerializeField, Range(2, 8)] public int BoundaryDepthInCells { get; private set; } = 5;
        [field: SerializeField] public Vector2 TreeVisualOffsetInCells { get; private set; } = Vector2.zero;
    }
}
