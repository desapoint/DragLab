using UnityEngine;
using DragLab.Domain.Mechanics;
namespace DragLab.Data
{
    [CreateAssetMenu(menuName = "DragLab/Valuation Policy")]
    public sealed class ValuationPolicyDefinition : ScriptableObject
    {
        public ValuationPolicy policy = ValuationPolicy.Defaults();
    }
}
