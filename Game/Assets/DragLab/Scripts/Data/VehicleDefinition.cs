using UnityEngine;
using DragLab.Domain;

namespace DragLab.Data
{
    [CreateAssetMenu(fileName = "Vehicle", menuName = "DragLab/Vehicle Definition")]
    public sealed class VehicleDefinition : ScriptableObject
    {
        public VehicleSpec spec = new VehicleSpec();
        public Color defaultPaint = new Color(.32f, .8f, .72f);
        [Min(0)] public int purchasePrice;
        [Min(0)] public int reputationRequired;
        public Tune CreateStockTune()
        {
            return new Tune { finalDrive = spec.finalDrive, launchRpm = 3200, shiftRpm = spec.redlineRpm - 200 };
        }
    }
}
