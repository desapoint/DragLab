# Rendering

DragLab uses **URP 17.6.0 / the 2D Renderer** on Unity 6000.6.4f1. The pipeline and renderer assets live in `Game/Assets/DragLab/Rendering`. Graphics and all six Quality levels reference the same pipeline. It uses Render Graph, native render scale, two-sample MSAA, the existing Gamma color space, and no HDR, post-processing, camera depth or opaque copy textures.

The current art is unlit. Cars, track sprites, lamps and tire smoke explicitly share the resource `Rendering/SpriteUnlit`, using Unity's URP Sprite Unlit shader. A serialized material reference keeps the shader in packaged players; do not restore implicit Built-in sprite materials or rely only on `Shader.Find`.

The asphalt and starting/finish ribbons use `Rendering/VertexUnlit`. Its small URP shader preserves mesh vertex colors, texture UVs, transparent blending, two-sided geometry and sorting without writing depth. Unity's standard Mesh2D Unlit shader does not preserve the existing vertex shading and has different depth/culling behavior. Material instances retain their generated textures; shared sprite materials must never receive per-object texture or tint mutations.

Native UI Toolkit geometry/text and legacy IMGUI remain their existing interfaces. Debug bench plots remain intact. This migration adds no new visual theme, lighting effects, camera framing, physics, audio or save format. Crisp 4K UI still uses the existing panel scaling/render-target approach; it does not turn source car sprites into new 4K artwork.

There were no authored lights, lightmaps, reflection probes, PPv2 profiles, custom fullscreen effects or replacement-camera shaders in the three build scenes. Their cameras and world artwork are created at runtime. Unity's generated default Volume profile contains framework defaults; it is not an authored effect stack, and the current cameras do not enable post-processing.

`DragLab.Editor.RenderPipelineMigration.Verify()` re-reads the saved pipeline, renderer, materials, quality assignments and scene dependencies. Reassess this check when adding lighting, probes or effects. The authored assets and their `.meta` files must remain versioned.

For the migration's capture coverage and limitations, see [the report](../reports/render-pipeline-20261016/CHANGES.md). `tools/Capture-RenderPipelineInterfaces.ps1` captures isolated test players; `tools/Compare-RenderPipelineImages.py` uses Pillow/NumPy for same-size overlays, amplified differences and a review index. Matching images are never resized, registered, masked or normalized to conceal changes.

References: [Unity's 2D shader compatibility](https://docs.unity.com/en-us/engine/6000.5/manual/unity2d/2d-urp/2d-renderer-urp-features/shader-compatibility), [URP migration workflow](https://docs.unity3d.com/Manual/urp/upgrading-from-birp.html). Installed package source is the authority for the APIs used here.
