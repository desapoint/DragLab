using System;
using System.Collections.Generic;
using DragLab.Data;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Presentation
{
    public static class VehicleArt
    {
        static readonly Dictionary<string, string> families = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly Dictionary<string, string> resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        public static string Resolve(VehicleSpec spec)
        {
            string family = spec.modelFamily;
            if (string.IsNullOrEmpty(family)) {
                if (families.Count == 0) foreach (var asset in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) families[asset.spec.id] = asset.spec.modelFamily;
                families.TryGetValue(spec.id, out family);
            }
            if (string.IsNullOrEmpty(family)) return spec.artId;
            if (!resolved.TryGetValue(family, out string id)) {
                string candidate = "roster/" + family.ToLowerInvariant();
                resolved[family] = id = Resources.Load<TextAsset>("Sprites/Vehicles/" + candidate + "/car-sprite") == null ? null : candidate;
            }
            return id ?? spec.artId;
        }
    }
}
