# TerrainGenerator
This repository contains a procedural generator of infinite terrain, implemented in Unity 6 using the URP. Built for [Acerola's Dirt Jam](https://itch.io/jam/acerola-dirt-jam).
My submission can be found [here](https://mobaster.itch.io/procedural-terrain-generator).

Below is a video showcase of the implementation. Click the image to be redirected to the YouTube video.

[![Watch the video](https://github.com/GuglielmoMazzesiDaniele/ProceduralTerrain/blob/main/Jam%20Submission%20Photos/Water.png)](https://www.youtube.com/watch?v=utK_U0SPAtk)

## Featues

As of **September 24, 2025**, the generator includes the following features:

- **Chunk System** — To avoid re-generating the entire terrain when the camera moves, the world is divided into reusable chunks. Each chunk is a fixed-resolution flat mesh that’s repositioned as needed and locally regenerated. Instead of unique geometry per tile, every chunk samples its own heightmap, updated on demand via a compute shader, to displace vertices on the GPU.
  ![Chunk System](https://github.com/GuglielmoMazzesiDaniele/ProceduralTerrain/blob/main/Jam%20Submission%20Photos/Chunk%20System.PNG)
  
- **Terrain LODs** — To avoid spending detail where the camera won’t notice, the system uses multiple LODs. As distance increases, chunks switch to larger world-space sizes while keeping the same quad resolution. You render far terrain with far fewer vertices, dramatically extending view range without increasing mesh complexity.
![LODs](https://github.com/GuglielmoMazzesiDaniele/ProceduralTerrain/blob/main/Jam%20Submission%20Photos/LODs.PNG)

- **Biomes** — Terrain heightmaps are generated with fractal Brownian motion (fBM) as the base signal. I apply a set of lightweight post-filters to that noise to carve out distinct landforms, then classify the result into biomes. A separate 2D “continental” noise (encoding temperature and moisture) feeds a look-up table (LUT) that maps large world regions to their target biome. Each biome defines its own color palette (and other visual parameters), and transitions are controlled with a tunable smoothstep—letting you choose sharp borders or soft gradients between biomes. The whole setup is data-driven, so adding, removing, or tweaking biomes is fast: update the LUT and palette, and the world adapts.
![Biomes](https://github.com/GuglielmoMazzesiDaniele/ProceduralTerrain/blob/main/Jam%20Submission%20Photos/2%20Biomes.png)

- **Shadows** — Using Unity’s rendering hooks, the terrain writes into cascaded shadow maps via a custom shadow-caster and depth pass, allowing high-quality shadows around the camera. The current implementation is capped to ~1.5 km shadow distance, and there’s a visible ring/seam artifact at cascade boundaries (a dark circle where levels meet).
![Shadows](https://github.com/GuglielmoMazzesiDaniele/ProceduralTerrain/blob/main/Jam%20Submission%20Photos/Cascade%20Shadowmaps.png)

- **Fog Render Pass** — Using Unity’s Scriptable Render Pipeline, I added a custom render feature that injects a full-screen pass (post after opaques) to compute depth-based fog. The pass samples the camera depth to accumulate distance fog, and I experimented with screen-space contact shadows (SSCS) to modulate fog visibility—approximating the effect of distant shadows. The fog tint inherits the main directional light’s color, while its density is driven by a time-scrolled noise texture to suggest slow, drifting volumes. The comparison screenshot shows the scene with vs. without the fog pass; the current maximum fog distance is ~7.5 km.
![Fog](https://github.com/GuglielmoMazzesiDaniele/ProceduralTerrain/blob/main/Jam%20Submission%20Photos/Fog.png)

As of **September 24, 2025**, the generator includes the following WIP features, which are not production-ready but in a presentable state:

- **Procedural Scattering** — Foliage, rocks, and props are what make biomes feel alive. Built on the existing chunk system, the scattering prototype uses GPU indirect instancing and compute-shader culling to draw tens of thousands of instances efficiently. The system predates terrain LODs, and merging the two (distribution rules + LOD-aware culling) is still pending. The screenshots show the GPU culling debug view and a basic render using placeholder cubes.
![Procedural Scattering](https://github.com/GuglielmoMazzesiDaniele/ProceduralTerrain/blob/main/Jam%20Submission%20Photos/Procedural%20Scattering.PNG)

- **Water** — Good water is hard. I tried mesh-based approaches and full-screen passes; none hit the quality/perf balance yet. For now, a simple depth-based full-screen shader fakes a water plane.
![Water](https://github.com/GuglielmoMazzesiDaniele/ProceduralTerrain/blob/main/Jam%20Submission%20Photos/Water.png)

- **Job System Refactoring** — I’m refactoring the generator to use Unity’s Job System and the Burst Compiler to build the terrain mesh on the CPU in parallel. This WIP branch has reached feature parity with the “main” version for core shapes; it can already produce comparable terrain, but the aim is broader: switch to Unity’s Mesh API (e.g., MeshDataArray, NativeArray<T>) so the result can plug directly into physics (colliders, raycasts, and collision detection) without additional conversions.
