using UnityEngine;
using DragLab.Domain;

namespace DragLab.Data
{
    [CreateAssetMenu(fileName = "Event", menuName = "DragLab/Career Event")]
    public sealed class CareerEventDefinition : ScriptableObject
    {
        public string stableId;
        public string displayName;
        public string rivalName;
        public VehicleDefinition opponent;
        public Tune opponentTune = new Tune();
        public float distanceM = 402.336f;
        public int reputationRequired;
        public int cashWin = 500;
        public int cashLoss = 100;
        public int reputationWin = 2;
        public string tier = "F", eventKind = "Race", prerequisite;
        public int entryFee, firstWinBonus;
        public float minimumHp, maximumHp, maximumDisplacementL, minimumMassKg, maximumMassKg;
        public bool restrictDrive, naturallyAspiratedOnly, turboOnly;
        public DriveType requiredDrive;
        public string requiredModel;
    }
}
