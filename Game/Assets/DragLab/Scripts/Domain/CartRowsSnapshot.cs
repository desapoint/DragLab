using System;
using System.Collections.Generic;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    // Owned immutable copy of the cart-line facts used by the frozen cart signature.
    // This type is dormant infrastructure; it does not replace CartReservations.Lines.
    internal sealed class CartRowsSnapshot
    {
        readonly CartRowValue[] rows;

        CartRowsSnapshot(CartRowValue[] ownedRows) { rows = ownedRows; }

        internal int Count => rows.Length;

        internal static CartRowsSnapshot Capture(IEnumerable<PartCartLine> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            var captured = new List<CartRowValue>();
            foreach (var row in source) captured.Add(new CartRowValue(row));
            return new CartRowsSnapshot(captured.ToArray());
        }

        // CartRowValue contains only immutable scalar/string facts and private arrays.
        internal CartRowValue GetRow(int index) => rows[index];

        internal bool RawEquals(CartRowsSnapshot other)
        {
            if (ReferenceEquals(other, null) || rows.Length != other.rows.Length) return false;
            for (int i = 0; i < rows.Length; i++)
                if (!rows[i].RawEquals(other.rows[i])) return false;
            return true;
        }

        internal bool CartSignatureEquals(CartRowsSnapshot other)
        {
            if (ReferenceEquals(other, null) || rows.Length != other.rows.Length) return false;
            for (int i = 0; i < rows.Length; i++)
                if (!rows[i].CartSignatureEquals(other.rows[i])) return false;
            return true;
        }
    }

    internal sealed class CartRowValue
    {
        readonly string[] positions;

        internal readonly bool IsNullRow;
        internal readonly string DefinitionId;
        internal readonly int Quantity;
        internal readonly bool Install;
        internal readonly bool PreparedBlock;
        internal readonly string PreparationOwner;
        internal readonly CartAction Action;
        internal readonly string AssetId;
        internal readonly string Role;
        internal readonly string DestinationId;
        internal readonly InventoryKind Destination;
        internal readonly bool PositionsWasNull;
        internal int PositionCount => positions.Length;

        internal CartRowValue(PartCartLine source)
        {
            IsNullRow = source == null;
            if (source == null)
            {
                // The raw marker remains null. Scalar accessors carry the concrete
                // field-initializer defaults so the effective view matches JsonUtility.
                Quantity = 1;
                Action = default(CartAction);
                Destination = default(InventoryKind);
                positions = Array.Empty<string>();
                return;
            }

            DefinitionId = source.definitionId;
            Quantity = source.quantity;
            Install = source.install;
            PreparedBlock = source.preparedBlock;
            PreparationOwner = source.preparationOwner;
            Action = source.action;
            AssetId = source.assetId;
            Role = source.role;
            DestinationId = source.destinationId;
            Destination = source.destination;
            PositionsWasNull = source.positions == null;
            if (source.positions == null)
            {
                positions = Array.Empty<string>();
                return;
            }
            positions = new string[source.positions.Count];
            for (int i = 0; i < positions.Length; i++) positions[i] = source.positions[i];
        }

        internal string PositionAt(int index) => positions[index];

        // Never return the private snapshot array.
        internal string[] CopyPositions() => (string[])positions.Clone();

        internal bool RawEquals(CartRowValue other)
        {
            if (ReferenceEquals(other, null) || IsNullRow != other.IsNullRow) return false;
            if (IsNullRow) return true;
            return string.Equals(DefinitionId, other.DefinitionId, StringComparison.Ordinal) &&
                   Quantity == other.Quantity && Install == other.Install && PreparedBlock == other.PreparedBlock &&
                   string.Equals(PreparationOwner, other.PreparationOwner, StringComparison.Ordinal) &&
                   Action == other.Action && string.Equals(AssetId, other.AssetId, StringComparison.Ordinal) &&
                   string.Equals(Role, other.Role, StringComparison.Ordinal) &&
                   string.Equals(DestinationId, other.DestinationId, StringComparison.Ordinal) &&
                   Destination == other.Destination && PositionsWasNull == other.PositionsWasNull &&
                   SamePositions(other, false);
        }

        internal bool CartSignatureEquals(CartRowValue other)
        {
            if (ReferenceEquals(other, null)) return false;
            var a = IsNullRow ? DefaultRow : this;
            var b = other.IsNullRow ? DefaultRow : other;
            return ProductStringSemantics.EffectiveEquals(a.DefinitionId, b.DefinitionId) &&
                   a.Quantity == b.Quantity && a.Install == b.Install && a.PreparedBlock == b.PreparedBlock &&
                   ProductStringSemantics.EffectiveEquals(a.PreparationOwner, b.PreparationOwner) &&
                   a.Action == b.Action && ProductStringSemantics.EffectiveEquals(a.AssetId, b.AssetId) &&
                   ProductStringSemantics.EffectiveEquals(a.Role, b.Role) &&
                   ProductStringSemantics.EffectiveEquals(a.DestinationId, b.DestinationId) &&
                   a.Destination == b.Destination && SamePositions(a, b, true);
        }

        static readonly CartRowValue DefaultRow = new CartRowValue();

        // Exact PartCartLine field initializers, without allocating/retaining a domain row.
        CartRowValue()
        {
            Quantity = 1;
            Action = default(CartAction);
            Destination = default(InventoryKind);
            positions = Array.Empty<string>();
        }

        bool SamePositions(CartRowValue other, bool effective)
        {
            if (positions.Length != other.positions.Length) return false;
            for (int i = 0; i < positions.Length; i++)
            {
                var a = positions[i]; var b = other.positions[i];
                if (effective)
                {
                    if (!ProductStringSemantics.EffectiveEquals(a, b)) return false;
                }
                else if (!string.Equals(a, b, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        static bool SamePositions(CartRowValue a, CartRowValue b, bool effective)
        {
            // PartCartLine.Copy converts a null positions list to an empty list.
            return a.SamePositions(b, effective);
        }
    }
}
