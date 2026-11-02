#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
    // Literal diagnostic boundaries captured at K baseline 99ad759.
    // CartReservations source SHA256: 17B8BF9FCA5994AB13569B42BDF38447634FA52BB127CC221AAD667DDEAF7F17
    // MarketCatalogLayout source SHA256: 936471352EE0524AF476FD76956A29943AA9C329371AF662B0684F6024DECFB9
    // These methods intentionally retain Unity JsonUtility behavior and are not production helpers.
    internal static class LegacyCartCanonicalBoundaryReference
    {
        internal static string Signature(IEnumerable<PartCartLine> rows)
            => JsonUtility.ToJson(new PartCart.Lines { entries = rows.Select(l => l?.Copy()).ToList() });

        internal static bool SameCart(PlayerProfile p, string carId, IEnumerable<PartCartLine> rows)
            => p.inventory.cart != null && p.inventory.cart.carId == carId &&
               Signature(p.inventory.cart.lines) == Signature(rows);

        internal static string ProductCacheMiss(PartDefinition d)
        {
            var copy = d.Copy();
            copy.id = "";
            return JsonUtility.ToJson(copy);
        }
    }
}
#endif
