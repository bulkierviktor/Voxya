using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class MinimapRegionGizmos : MonoBehaviour
{
    public WorldGenerator world;
    [Min(1)] public int regionRadius = 12;
    public Color regionGridColor = new Color(0f, 0f, 0f, 0.15f);
    public Color cityColor = new Color(1f, 0.5f, 0f, 0.9f);
    public float cityMarkerSizeMeters = 0.75f;

    void OnDrawGizmos()
    {
        if (world == null || world.RegionIndex == null || world.playerTransform == null) return;

        var index = world.RegionIndex;
        int bx = Mathf.FloorToInt(world.playerTransform.position.x / Chunk.blockSize);
        int bz = Mathf.FloorToInt(world.playerTransform.position.z / Chunk.blockSize);
        Vector2Int currentRegion = index.WorldBlocksToRegion(new Vector2Int(bx, bz));

        for (int dx = -regionRadius; dx <= regionRadius; dx++)
            for (int dz = -regionRadius; dz <= regionRadius; dz++)
            {
                var r = new Vector2Int(currentRegion.x + dx, currentRegion.y + dz);
                var info = index.GetRegion(r);

                DrawRegionBounds(r, index.regionSizeBlocks);

                if (info.hasCity)
                {
                    Vector2Int cityBlocks = index.RegionToWorldBlocks(r, info.cityLocalOffsetBlocks);
                    Vector3 posMeters = new Vector3(cityBlocks.x * Chunk.blockSize, 2f * Chunk.blockSize, cityBlocks.y * Chunk.blockSize);
                    Gizmos.color = cityColor;
                    Gizmos.DrawSphere(posMeters, cityMarkerSizeMeters);
                }
            }
    }

    private void DrawRegionBounds(Vector2Int region, int regionSizeBlocks)
    {
        Gizmos.color = regionGridColor;
        float x0 = region.x * regionSizeBlocks * Chunk.blockSize;
        float z0 = region.y * regionSizeBlocks * Chunk.blockSize;
        float size = regionSizeBlocks * Chunk.blockSize;

        Vector3 p0 = new Vector3(x0, 0f, z0);
        Vector3 p1 = new Vector3(x0 + size, 0f, z0);
        Vector3 p2 = new Vector3(x0 + size, 0f, z0 + size);
        Vector3 p3 = new Vector3(x0, 0f, z0 + size);

        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p0);
    }
}