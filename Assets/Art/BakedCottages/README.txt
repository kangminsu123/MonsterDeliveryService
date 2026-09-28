Baked cottages — 10 individual assets

Drag each House_*.prefab into your level. Materials are already assigned to both FBX imports and prefabs.
Each house: 1 mesh / 1 material, 1,588–2,184 triangles, ground pivot Y=0, scale 1, front -Z. No colliders added.
Textures: 2048px Albedo (contact AO included), tangent-space Normal, Roughness and MetallicSmoothness (R=0, A=1-Roughness).
Unity Built-in Standard shader uses Albedo, Normal and MetallicSmoothness. Roughness is retained for other pipelines.
Blender procedural shaders were baked to PNG; FBX embeds Albedo, Normal and Roughness as well. Keep the supplied texture/material files with the assets.
Scene lighting is intentionally not baked into the color texture. Unity lighting controls the final appearance.
