using UnityEngine;

namespace Nusantara.World
{
    [DefaultExecutionOrder(-10000)]
    public sealed class WorldEditorPreview : MonoBehaviour
    {
        void Awake() { if (Application.isPlaying) gameObject.SetActive(false); }
    }
}
