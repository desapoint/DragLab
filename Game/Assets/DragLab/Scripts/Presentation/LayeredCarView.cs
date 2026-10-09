using System;
using System.Collections.Generic;
using UnityEngine;
using DragLab.Domain;
using DragLab.Data;
using DragLab.Domain.Mechanics;

namespace DragLab.Presentation
{
    // Manifest coordinates have an upper-left origin; Unity uses y-up units.
    public sealed class LayeredCarView : MonoBehaviour
    {
        const float PixelsPerUnit = 200;
        struct WheelBinding { public Transform transform; public bool front; }
        struct GeometryBinding { public Transform transform; public SpriteRenderer renderer; public string id; public Vector3 position, scale; }
        readonly List<GeometryBinding> geometry = new List<GeometryBinding>();
        float nominalRadiusM, artworkRadius;
        TireSmokeView smoke;
        Vector3 frontWheel,rearWheel;
        DriveType drive;
        public int SlipParticleCount=>smoke==null?0:smoke.ActiveCount;
        public int FrontSmokeEmitted=>smoke==null?0:smoke.FrontEmitted;
        public int RearSmokeEmitted=>smoke==null?0:smoke.RearEmitted;
        public float TireSizeScale { get; private set; } = 1;
        readonly List<WheelBinding> rotating = new List<WheelBinding>();
        readonly List<SpriteRenderer> paintLayers = new List<SpriteRenderer>();
        [Serializable] class Manifest { public CanvasInfo canvas; public AnchorSet anchors; public Layer[] layers; }
        [Serializable] class CanvasInfo { public int width; public int height; }
        [Serializable] class AnchorSet { public Anchor frontWheel; public Anchor rearWheel; }
        [Serializable] class Anchor { public float x; public float y; public float radius; }
        [Serializable] class Layer { public string id; public string file; public int z; public Placement placement; public bool rotateWithWheel; }
        [Serializable] class Placement { public string mode; public string anchor; public float x; public float y; public float[] offset; public float scale = 1; public float[] pivotNormalized; }

        public void Build(string artId, Color paint, int laneOrder = 0)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            rotating.Clear(); paintLayers.Clear(); geometry.Clear(); TireSizeScale = 1;
            nominalRadiusM = 0;
            foreach (var definition in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
                if (definition.spec.artId == artId) { nominalRadiusM = definition.spec.wheelRadiusM; break; }
            string root = "Sprites/Vehicles/" + artId + "/";
            TextAsset json = Resources.Load<TextAsset>(root + "car-sprite");
            if (json == null) throw new InvalidOperationException("Missing car manifest: " + artId);
            var manifest = JsonUtility.FromJson<Manifest>(json.text);
            artworkRadius = manifest.anchors.rearWheel.radius / PixelsPerUnit;
            frontWheel=new Vector3((manifest.anchors.frontWheel.x-manifest.canvas.width*.5f)/PixelsPerUnit,(manifest.canvas.height*.5f-manifest.anchors.frontWheel.y)/PixelsPerUnit,0);
            rearWheel=new Vector3((manifest.anchors.rearWheel.x-manifest.canvas.width*.5f)/PixelsPerUnit,(manifest.canvas.height*.5f-manifest.anchors.rearWheel.y)/PixelsPerUnit,0);
            foreach (var layer in manifest.layers)
            {
                var obj = new GameObject(layer.id);
                obj.transform.SetParent(transform, false);
                var renderer = obj.AddComponent<SpriteRenderer>();
                renderer.sprite = Resources.Load<Sprite>(root + System.IO.Path.GetFileNameWithoutExtension(layer.file));
                if (renderer.sprite == null) throw new InvalidOperationException("Sprite must import as Single: " + layer.file);
                renderer.sortingOrder = laneOrder + layer.z;
                if (layer.placement.mode == "anchor")
                {
                    var a = layer.placement.anchor == "frontWheel" ? manifest.anchors.frontWheel : manifest.anchors.rearWheel;
                    var offset = layer.placement.offset;
                    obj.transform.localPosition = new Vector3((a.x + offset[0] - manifest.canvas.width * .5f) / PixelsPerUnit,
                        (manifest.canvas.height * .5f - a.y - offset[1]) / PixelsPerUnit, 0);
                    obj.transform.localScale = Vector3.one * layer.placement.scale;
                    if (layer.rotateWithWheel) rotating.Add(new WheelBinding { transform = obj.transform, front = layer.id.StartsWith("front-") });
                }
                else obj.transform.localPosition = new Vector3(layer.placement.x / PixelsPerUnit, -layer.placement.y / PixelsPerUnit, 0);
                geometry.Add(new GeometryBinding { transform = obj.transform, renderer = renderer, id = layer.id, position = obj.transform.localPosition, scale = obj.transform.localScale });
                if (layer.id == "body-paint") { paintLayers.Add(renderer); renderer.color = paint; }
            }
            var smokeObject=new GameObject("Contact tire smoke");smokeObject.transform.SetParent(transform,false);
            smoke=smokeObject.AddComponent<TireSmokeView>();
            int smokeOrder=laneOrder;foreach(var layer in manifest.layers)smokeOrder=Mathf.Max(smokeOrder,laneOrder+layer.z+1);
            smoke.Initialize(smokeOrder);UpdateSmokeContacts();
        }

        // Geometry follows the same resolved hardware/tune as physics, including draft previews.
        // Scale from immutable authored transforms, never from the previous frame.
        public void ApplySetup(VehicleSpec spec, Tune tune)
        {
            drive=spec.drive;
            if (nominalRadiusM <= 0) nominalRadiusM = spec.wheelRadiusM;
            TireSizeScale = (float)(spec.wheelRadiusM * tune.WheelDiameter / nominalRadiusM);
            if (float.IsNaN(TireSizeScale) || float.IsInfinity(TireSizeScale) || TireSizeScale <= 0) TireSizeScale = 1;
            float lift = artworkRadius * (TireSizeScale - 1);
            bool wheels = true, brakes = true, body = true;
            if (tune.partsResolved) {
                wheels = HasPart(spec, PartCategory.Wheels) && HasPart(spec, PartCategory.Tires);
                brakes = HasPart(spec, PartCategory.Brakes);
                body = HasPart(spec, PartCategory.Body);
            }
            foreach (var part in geometry) {
                bool wheel = part.id.EndsWith("-wheel", StringComparison.Ordinal);
                bool brake = part.id.EndsWith("-rotor", StringComparison.Ordinal) || part.id.EndsWith("-caliper", StringComparison.Ordinal);
                part.transform.localScale = wheel ? part.scale * TireSizeScale : part.scale;
                // Lift body and axle centers equally, leaving the contact patch and shadow on the ground.
                part.transform.localPosition = part.position + (part.id == "shadow" ? Vector3.zero : Vector3.up * lift);
                part.renderer.enabled = wheel ? wheels : brake ? brakes : part.id == "shadow" || body;
            }
            UpdateSmokeContacts();
        }
        void UpdateSmokeContacts(){if(smoke!=null){var offset=Vector3.up*(artworkRadius*(TireSizeScale-1)-artworkRadius*TireSizeScale);smoke.SetContacts(frontWheel+offset,rearWheel+offset);}}
        public void ClearSlipEffects(){if(smoke!=null)smoke.Clear();}
        static bool HasPart(VehicleSpec spec, PartCategory category)
        { return spec.fittedParts != null && Array.Exists(spec.fittedParts, part => part.category == category); }

        public void SetPaint(Color color) { foreach (var layer in paintLayers) layer.color = color; }
        public void Animate(CarState state, bool effectsActive=true, bool acousticSlip=false)
        {
            foreach (var part in rotating)
                part.transform.localRotation = Quaternion.Euler(0, 0, -(float)((part.front ? state.frontWheelAngleDegrees : state.rearWheelAngleDegrees) % 360));
            // Calipers are deliberately absent from rotating: they follow the car only.
            if(smoke!=null&&effectsActive)smoke.Step(state,Time.unscaledDeltaTime,acousticSlip,drive);
        }
    }
}
