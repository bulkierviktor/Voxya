using UnityEditor;
using UnityEngine;
using Voxya.Voxel.Core;

namespace Voxya.Voxel.Editor
{
    public static class CreateConfigMenu
    {
        [MenuItem("Assets/Create/Voxya/Voxel/World Config")]
        public static void Create()
        {
            var asset = ScriptableObject.CreateInstance<VoxelWorldConfig>();
            ProjectWindowUtil.CreateAsset(asset, "VoxelWorldConfig.asset");
        }
    }
}