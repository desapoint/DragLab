using UnityEngine;
using DragLab.Domain.Mechanics;
namespace DragLab.Data
{
    [CreateAssetMenu(menuName = "DragLab/Operating Policy")]
    public sealed class OperatingPolicyDefinition : ScriptableObject { public OperatingPolicy policy = new OperatingPolicy(); }
}
