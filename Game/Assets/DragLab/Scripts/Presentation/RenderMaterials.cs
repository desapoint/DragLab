using System;
using UnityEngine;

namespace DragLab.Presentation
{
    // Explicit resource references retain shaders in players and avoid implicit Built-in defaults.
    internal static class RenderMaterials
    {
        static Material sprite;
#if DRAGLAB_BUILTIN_CAPTURE
        // Only the migration baseline build defines this; it is absent from normal players.
        public static Material Sprite => sprite != null ? sprite : sprite = new Material(Shader.Find("Sprites/Default"));
        public static Material CreateVertex(string name,Texture texture) => new Material(Shader.Find("Sprites/Default")) { name=name,mainTexture=texture };
#else
        public static Material Sprite => sprite != null ? sprite : sprite = Load("SpriteUnlit");
        static Material Load(string name) => Resources.Load<Material>("Rendering/"+name)
            ?? throw new InvalidOperationException("Missing rendering material: "+name);
        public static Material CreateVertex(string name,Texture texture) =>
            new Material(Load("VertexUnlit")) { name=name,mainTexture=texture };
#endif
    }
}
