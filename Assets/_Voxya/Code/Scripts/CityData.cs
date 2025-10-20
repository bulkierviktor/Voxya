using System;
using UnityEngine;

[Serializable]
public class CityData
{
    // Centro de la ciudad en coordenadas de chunk (enteras almacenadas como floats)
    public Vector2 centerChunk;

    // Radio en chunks (2..4)
    public int radiusChunks;

    // Compatibilidad con código previo que usa 'position' y 'radius'
    public Vector2 position
    {
        get => centerChunk;
        set => centerChunk = value;
    }

    public int radius
    {
        get => radiusChunks;
        set => radiusChunks = value;
    }

    public CityData() { }

    public CityData(Vector2 centerChunk, int radiusChunks)
    {
        this.centerChunk = centerChunk;
        this.radiusChunks = radiusChunks;
    }

    // Centro de la ciudad en coordenadas de mundo (XZ)
    public Vector2 WorldCenterXZ(int chunkSize)
    {
        float cx = centerChunk.x * chunkSize + chunkSize * 0.5f;
        float cz = centerChunk.y * chunkSize + chunkSize * 0.5f;
        return new Vector2(cx, cz);
    }
}