# Streaming Biomes and Cities Implementation

## Overview
This implementation adds streaming chunk generation, biomes, and cities to the Voxya voxel game engine. The system is designed to be modular, minimal, and compatible with existing code.

## Parameters (as specified)
- **Chunk Settings**: chunkSize=16, chunkHeight=100
- **View Distance**: viewDistanceInChunks default 3 (can be set to 4)
- **Biomes**: Plains, Hills, Desert, Snow, Forest
- **Cities**: cityCount=5, distance min=40 max=80 chunks
- **First City**: Spawns 3-6 chunks from origin
- **City Radius**: Variable 2-4 chunks per city

## New Files

### 1. Biome.cs
Simple enum defining five biome types:
- Plains (flatter terrain, 0.8x height)
- Hills (hillier terrain, 1.5x height)
- Desert (slightly varied, 0.9x height)
- Snow (mountainous, 1.3x height)
- Forest (normal terrain, 1.0x height)

### 2. BiomeManager.cs
Manages biome selection based on chunk coordinates using:
- Perlin noise with seed offset for deterministic generation
- Distance from origin (spawn area favors Plains)
- Two noise values combined for variety
- Static method GetBiomeHeightMultiplier() for terrain height variation

### 3. WorldData.cs
Serializable data structure containing:
- World seed (int)
- List of CityData entries

CityData stores:
- position (Vector2 chunk coordinates)
- radius (int, in chunks)

### 4. CityManager.cs
Handles city generation and placement:
- Generates city positions deterministically from seed
- First city placed 3-6 chunks from origin
- Ensures minimum separation (minCityDistance) between cities
- Each city has a variable radius (2-4 chunks)
- Provides methods to check if chunk is in city area or city center

## Modified Files

### 1. Chunk.cs Changes
**Initialize signature updated:**
```csharp
public void Initialize(WorldGenerator world, Vector2 position, Biome biome)
```

**Changes:**
- Added `public Biome biome` field
- Added `[RequireComponent(typeof(MeshCollider))]`
- Changed GetTerrainHeight from static to instance method
- Applied biome height multiplier in GetTerrainHeight

**Preserved APIs:**
- GetBlock(int x, int y, int z) - unchanged signature

### 2. WorldGenerator.cs Changes
**Major rewrite from static to streaming generation:**

**New Fields:**
- playerTransform - reference for chunk streaming
- seed - world generation seed
- viewDistanceInChunks - render distance
- City parameters (count, distance, radius)
- cityPrefab - optional prefab (uses placeholder if null)
- Managers: BiomeManager, CityManager, WorldData
- activeChunks dictionary for loaded chunks
- Parent objects for organization

**New Methods:**
- ChunkUpdateCoroutine() - runs every 0.2s to check player movement
- UpdateChunksAroundPlayer() - loads/unloads chunks based on player position
- CreateChunkRoutine() - creates one chunk per frame (throttling)
- SpawnCity() - spawns city prefab or placeholder cube

**Preserved APIs:**
- GetChunk(int x, int z) - unchanged signature
- GetBlockAt(Vector3 worldPosition) - unchanged signature
- EnablePlayerAfterWorldGen() - kept for compatibility

**Behavior Changes:**
- Player controls enabled immediately (streaming doesn't need wait)
- Chunks created dynamically around player
- Chunks destroyed when outside view distance
- City placeholders (gold cubes, 10x20x10) spawned when cityPrefab is null

## Throttling Strategy
1. ChunkUpdateCoroutine runs every 0.2 seconds
2. CreateChunkRoutine yields one frame per chunk (max 1 chunk/frame)
3. Only updates when player moves to different chunk

## City Spawning
- CityManager generates cities deterministically on Start()
- When chunk is created, checks if it's in city radius
- If chunk is city center, spawns city:
  - If cityPrefab assigned: instantiate prefab
  - If cityPrefab null: create gold-colored placeholder cube (10x20x10)
- Cities parented under "Cities" GameObject
- Chunks parented under "Chunks" GameObject

## Compatibility Notes
1. **Preserved Public APIs**: GetChunk, GetBlockAt, GetBlock signatures unchanged
2. **Legacy Support**: chunkObjects dictionary maintained for compatibility
3. **Player Behavior**: EnablePlayerAfterWorldGen kept but now redundant
4. **No Breaking Changes**: Other scripts (PlayerController, ThirdPersonCamera) unaffected

## Usage in Unity Editor
1. Attach WorldGenerator to GameObject
2. Assign references:
   - player (PlayerController)
   - playerTransform (or it auto-detects from player)
   - chunkPrefab (existing chunk prefab)
   - cityPrefab (optional - null creates placeholder)
3. Adjust parameters as needed:
   - seed (for different worlds)
   - viewDistanceInChunks (3-4 recommended)
   - cityCount, distances, radii
4. Play - chunks stream around player automatically

## Technical Details
- Uses Unity coroutines for async chunk generation
- Deterministic generation via Random.InitState(seed)
- HashSet for efficient chunk tracking
- Vector2 for 2D chunk coordinates
- Biome noise sampling offset by seed for variety
