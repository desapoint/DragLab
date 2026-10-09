using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DragLab.Runtime
{
    public sealed class BenchPlotLines : IDisposable
    {
        Material material;
        float scale;
        public void Begin()
        {
            if (material == null)
            {
                material = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_Cull", (int)CullMode.Off); material.SetInt("_ZWrite", 0);
            }
            material.SetPass(0); scale = GUI.matrix.lossyScale.x;
            GL.PushMatrix(); GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0); GL.Begin(GL.TRIANGLES);
        }
        public void End() { GL.End(); GL.PopMatrix(); }
        public void Line(Vector2 from, Vector2 to, Color color, float width = 2)
        {
            GL.Color(color);
            Vector2 direction = to - from;
            direction = direction.sqrMagnitude < .000001f ? Vector2.right : direction.normalized;
            Vector2 cap = direction * (width * .5f), side = new Vector2(-direction.y, direction.x) * (width * .5f);
            Vector2 a = from - cap - side, b = from - cap + side, c = to + cap + side, d = to + cap - side;
            Vertex(a); Vertex(b); Vertex(c); Vertex(a); Vertex(c); Vertex(d);
        }
        void Vertex(Vector2 p) { GL.Vertex3(p.x * scale, p.y * scale, 0); }
        public void Series(int count, float width, Func<int, Vector2> point, Color color, float thickness)
        {
            if (count == 0) return;
            var indices = ReducedIndices(count, width, point);
            Vector2 previous = point(indices[0]);
            if (indices.Count == 1) Line(previous, previous, color, thickness);
            for (int i = 1; i < indices.Count; i++) { var next = point(indices[i]); Line(previous, next, color, thickness); previous = next; }
        }
        // Chronological buckets retain first/last and XY extrema. RPM sweeps
        // double back; sorting by X or using fixed stride loses that evidence.
        public static List<int> ReducedIndices(int count, float width, Func<int, Vector2> point)
        {
            int stride = Math.Max(1, (int)Math.Ceiling(count / Math.Max(1f, width * 2)));
            var output = new List<int>(); var candidates = new int[6];
            for (int first = 0; first < count; first += stride)
            {
                int last = Math.Min(count - 1, first + stride - 1), minX = first, maxX = first, minY = first, maxY = first;
                var low = point(first); var high = low;
                for (int i = first + 1; i <= last; i++)
                {
                    var p = point(i);
                    if (p.x < low.x) { low.x = p.x; minX = i; } if (p.x > high.x) { high.x = p.x; maxX = i; }
                    if (p.y < low.y) { low.y = p.y; minY = i; } if (p.y > high.y) { high.y = p.y; maxY = i; }
                }
                candidates[0] = first; candidates[1] = last; candidates[2] = minX; candidates[3] = maxX; candidates[4] = minY; candidates[5] = maxY;
                Array.Sort(candidates); foreach (int index in candidates) if (output.Count == 0 || output[output.Count - 1] != index) output.Add(index);
            }
            return output;
        }
        public void Dispose() { if (material != null) UnityEngine.Object.Destroy(material); }
    }
}
