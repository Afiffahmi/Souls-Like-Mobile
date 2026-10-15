using System.Collections.Generic;
using UnityEngine;
namespace Nusantara.World.Editor
{
    public sealed class WorldSurfaceDetailLibrary : ScriptableObject
    {
        public List<WorldSurfaceStyle> entries=new List<WorldSurfaceStyle>();
        public Texture2DArray textures;
        public Texture2D parameters;
    }
}
