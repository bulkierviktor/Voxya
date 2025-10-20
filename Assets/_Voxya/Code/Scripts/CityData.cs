using System;
using UnityEngine;

[Serializable]
public class CityData
{
    // Centro de la ciudad en coordenadas de chunk (enteras almacenadas como floats)
    public Vector2 centerChunk;

    // Radio en chunks (2..4)
    public int radiusChunks;

    // Centro de la ciudad en coordenadas de mundo (XZ)
    public Vector2 WorldCenterXZ(int chunkSize)
    {
        float cx = centerChunk.x * chunkSize + chunkSize * 0.5f;
        float cz = centerChunk.y * chunkSize + chunkSize * 0.5f;
        return new Vector2(cx, cz);
    }
}