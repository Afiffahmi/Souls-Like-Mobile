using UnityEngine;

namespace ElementalGems
{
    [ExecuteAlways, RequireComponent(typeof(UnityEngine.UI.GridLayoutGroup))]
    public sealed class GemGridLayout : MonoBehaviour
    {
        public int columns=2, rows=4;
        private void OnEnable() => Resize();
        private void OnRectTransformDimensionsChange() => Resize();
        private void Resize()
        {
            var grid=GetComponent<UnityEngine.UI.GridLayoutGroup>(); if(grid==null)return;
            var rect=((RectTransform)transform).rect;
            grid.cellSize=new Vector2(Mathf.Max(1,(rect.width-grid.spacing.x*(columns-1))/columns),Mathf.Max(1,(rect.height-grid.spacing.y*(rows-1))/rows));
        }
    }
}
