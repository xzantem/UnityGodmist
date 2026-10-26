using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView
    {
        private void BuildDifficultTerrainDecorations()
        {
            if(!catacombsArtPresentationEnabled || UsesVegetationPresentation || boundMap==null)return;
            var sprite=LoadPresentationSprite("catacombs-rubble");if(sprite==null)return;
            foreach(var cell in boundMap.DifficultTerrainCells)
            {
                var decoration=CreateDecoration("Loose difficult-terrain stones",sprite,transform,CellToWorldCenter(cell),
                    GetFloorFeedbackDepthSortingOrder(),new Color(.65f,.62f,.57f,1),.35f,FloorFeedbackSortingLayerID);
                decoration.Cell=cell;decoration.Object.layer=gameObject.layer;
                decoration.Renderer.flipX=(VegetationHash(cell)&1u)!=0;
                presentationDecorations.Add(decoration);floorSortedPresentations.Add(decoration.Renderer);
            }
        }
    }
}
