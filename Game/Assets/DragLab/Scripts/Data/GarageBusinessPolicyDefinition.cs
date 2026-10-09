using UnityEngine;
using DragLab.Domain.Mechanics;
namespace DragLab.Data
{
    [CreateAssetMenu(fileName="GarageBusinessPolicy",menuName="DragLab/Garage Business Policy")]
    public sealed class GarageBusinessPolicyDefinition:ScriptableObject
    { public GarageBusinessPolicy policy=new GarageBusinessPolicy(); }
}
