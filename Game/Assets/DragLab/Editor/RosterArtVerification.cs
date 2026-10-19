using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Presentation;
using UnityEngine;
using UnityEditor;

namespace DragLab.Editor
{
    public static class RosterArtVerification
    {
        public static void Run()
        {
            // Repair first imports made before the explicit Texture2D invariant.
            // PNGs with minimal metadata otherwise inherited a cube shape in this Editor.
            foreach (string path in Directory.GetFiles("Assets/DragLab/Resources/Sprites/Vehicles/roster", "*.png", SearchOption.AllDirectories)) {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureShape != TextureImporterShape.Texture2D) { importer.textureShape = TextureImporterShape.Texture2D; importer.SaveAndReimport(); }
            }
            int checks = 0; var log = new StringBuilder(); var textures = new Dictionary<string, Texture2D>();
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; log.AppendLine("PASS: " + message); }
            var definitions = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            foreach (var definition in definitions) {
                string before = JsonUtility.ToJson(definition.spec); string id = definition.spec.id;
                string art = VehicleArt.Resolve(definition.spec);
                Check(art == "roster/" + definition.spec.modelFamily.ToLowerInvariant(), id + ": resolves its own model body.");
                var legacy = definition.spec.Copy(); legacy.modelFamily = null;
                Check(VehicleArt.Resolve(legacy) == art, id + ": old snapshots without modelFamily retain identity.");
                var obj = new GameObject("Raster body verification");
                try {
                    var view = obj.AddComponent<LayeredCarView>(); view.Build(definition.spec, definition.defaultPaint, 0, false);
                    var paint = obj.transform.Find("body-paint").GetComponent<SpriteRenderer>();
                    var fixtures = obj.transform.Find("body-fixtures").GetComponent<SpriteRenderer>();
                    var wheel = obj.transform.Find("front-wheel"); var rear = obj.transform.Find("rear-wheel");
                    var rotor = obj.transform.Find("front-rotor"); var caliper = obj.transform.Find("front-caliper");
                    if (textures.TryGetValue(art, out var same)) Check(same == paint.sprite.texture, id + ": trims share their model texture.");
                    else { Check(!textures.Values.Contains(paint.sprite.texture), id + ": distinct model has a distinct texture."); textures.Add(art, paint.sprite.texture); }
                    Check(paint.sortingOrder > wheel.GetComponent<SpriteRenderer>().sortingOrder && fixtures.sortingOrder > paint.sortingOrder, id + ": body occludes wheels; fixtures remain above paint.");
                    var tune = definition.CreateStockTune(); view.ApplySetup(definition.spec, tune);
                    Check(view.FrontTireSizeScale == 1 && view.RearTireSizeScale == 1 && view.ContactHeight > 0, id + ": stock wheel scale and ground contact are valid.");
                    var fixtureColor = fixtures.color; var fixtureSprite = fixtures.sprite;
                    foreach (var color in new[] { Color.red, Color.blue, Color.white, Color.black, Color.yellow }) {
                        view.SetPaint(color);
                        Check(paint.color == color && fixtures.color == fixtureColor && fixtures.sprite == fixtureSprite, id + ": repaint changes only the paint renderer / " + color);
                    }
                    tune.frontWheelDiameter = .8f; tune.rearWheelDiameter = 1.2f;
                    view.ApplySetup(definition.spec, tune); var position = wheel.localPosition; var size = wheel.localScale;
                    view.ApplySetup(definition.spec, tune);
                    Check(Math.Abs(view.FrontTireSizeScale - .8f) < .0001 && Math.Abs(view.RearTireSizeScale - 1.2f) < .0001 && wheel.localPosition == position && wheel.localScale == size, id + ": staggered axle tuning is stable across repeated application.");
                    view.Animate(new CarState { frontWheelAngleDegrees = 90, rearWheelAngleDegrees = 180 });
                    Check(Quaternion.Angle(wheel.localRotation, rotor.localRotation) < .001 && Quaternion.Angle(caliper.localRotation, Quaternion.identity) < .001 && Quaternion.Angle(rear.localRotation, Quaternion.Euler(0, 0, -180)) < .001, id + ": independent axle rotation and stationary caliper survive new geometry.");
                    Check(JsonUtility.ToJson(definition.spec) == before, id + ": rendering preserves authored physics and stable IDs.");
                } finally { UnityEngine.Object.DestroyImmediate(obj); }
            }
            Check(definitions.Length == 52 && textures.Count == 25, "All 52 configurations resolve 25 distinct model bodies.");
            Check(VehicleArt.Resolve(new VehicleSpec { id = "unknown-fixture", modelFamily = "Uncatalogued", artId = "hatch" }) == "hatch", "Unknown/custom model preserves its legacy art fallback.");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/roster-cel-v1")); Directory.CreateDirectory(folder);
            log.AppendLine("Checks: " + checks); File.WriteAllText(Path.Combine(folder, "verification.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("ROSTER ART PASS: " + checks);
        }
    }
}
