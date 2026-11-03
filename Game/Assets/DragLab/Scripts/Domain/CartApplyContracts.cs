using System;
using System.Collections.Generic;

namespace DragLab.Domain.Mechanics
{
    internal enum CartApplyBranch { SkippedSale, Move, NewSet, NewUnits, BuyUsed }

    internal sealed class CartApplyMalformedSourceException : InvalidOperationException
    {
        internal CartApplyMalformedSourceException(string memberPath)
            : base("Unsupported malformed source: " + memberPath) { }
    }

    internal readonly struct CartApplyIdentityRef : IEquatable<CartApplyIdentityRef>
    {
        internal readonly bool IsVirtual;
        internal readonly string ExistingId;
        internal readonly int TokenOrdinal;

        CartApplyIdentityRef(bool isVirtual, string existingId, int tokenOrdinal)
        { IsVirtual = isVirtual; ExistingId = existingId; TokenOrdinal = tokenOrdinal; }

        internal static CartApplyIdentityRef Existing(string id) => new CartApplyIdentityRef(false, id, -1);
        internal static CartApplyIdentityRef Virtual(int tokenOrdinal) => new CartApplyIdentityRef(true, null, tokenOrdinal);
        public bool Equals(CartApplyIdentityRef other) => IsVirtual == other.IsVirtual && TokenOrdinal == other.TokenOrdinal && ExistingId == other.ExistingId;
        public override bool Equals(object obj) => obj is CartApplyIdentityRef other && Equals(other);
        public override int GetHashCode() => IsVirtual ? unchecked((TokenOrdinal * 397) ^ 1) : ExistingId == null ? 0 : ExistingId.GetHashCode();
        public static bool operator ==(CartApplyIdentityRef left, CartApplyIdentityRef right) => left.Equals(right);
        public static bool operator !=(CartApplyIdentityRef left, CartApplyIdentityRef right) => !left.Equals(right);
    }

    internal sealed class CartApplyIdentity
    {
        internal readonly int TokenOrdinal, RowIndex, QuantityOrdinal;
        internal readonly string MemberPath, Kind;
        internal CartApplyIdentity(int tokenOrdinal, int rowIndex, int quantityOrdinal, string memberPath, string kind)
        { TokenOrdinal = tokenOrdinal; RowIndex = rowIndex; QuantityOrdinal = quantityOrdinal; MemberPath = memberPath; Kind = kind; }
    }

    internal sealed class CartApplyAllocatedIdentity
    {
        internal readonly CartApplyIdentityRef Identity;
        internal readonly string DomainFieldValue;
        internal CartApplyAllocatedIdentity(CartApplyIdentityRef identity, string domainFieldValue)
        { Identity = identity; DomainFieldValue = domainFieldValue; }
    }

    internal interface ICartIdentityAllocator
    {
        CartApplyAllocatedIdentity Allocate(string kind, int rowIndex, int quantityOrdinal, string memberPath);
    }

    internal sealed class CartApplyCatalogProduct
    {
        internal readonly PartDefinition Definition;
        internal readonly bool Found;
        internal readonly int Position, IdentityGroup;
        internal CartApplyCatalogProduct(PartDefinition definition, bool found, int position, int identityGroup)
        { Definition = definition; Found = found; Position = position; IdentityGroup = identityGroup; }
    }

    internal interface ICartCatalogRead : IDisposable
    {
        string[] Models();
        CartApplyCatalogProduct FindProduct(string id);
        PartDefinition FindDefinition(string id);
        Dictionary<PartCategory, string> Recipe(string model);
        VehicleSpec Spec(string vehicleId);
        BuildResolution Resolve(string vehicleId, IDictionary<PartCategory, string> installed, Tune adjustments = null);
        bool Fits(VehicleSpec spec, PartDefinition definition);
    }

    internal sealed class CartApplyQuote
    {
        internal readonly int Cost, Fee, LocalUnits, ExpressUnits;
        internal CartApplyQuote(int cost, int fee, int localUnits, int expressUnits)
        { Cost = cost; Fee = fee; LocalUnits = localUnits; ExpressUnits = expressUnits; }
    }

    internal sealed class CartApplySupplyDelta
    {
        readonly int[] positions;
        internal readonly int IdentityGroup, Before, After;
        internal readonly bool IsHold;
        internal int[] Positions => (int[])positions.Clone();
        internal CartApplySupplyDelta(int identityGroup, int before, int after, IEnumerable<int> positions, bool isHold)
        {
            IdentityGroup = identityGroup; Before = before; After = after; IsHold = isHold;
            this.positions = positions == null ? Array.Empty<int>() : new List<int>(positions).ToArray();
        }
    }

    internal sealed class CartApplyConsumeReceipt
    {
        readonly CartApplySupplyDelta[] stockDeltas, holdDeltas;
        internal readonly CartApplyQuote Quote;
        internal CartApplySupplyDelta[] StockDeltas => (CartApplySupplyDelta[])stockDeltas.Clone();
        internal CartApplySupplyDelta[] HoldDeltas => (CartApplySupplyDelta[])holdDeltas.Clone();
        internal CartApplyConsumeReceipt(CartApplyQuote quote, IEnumerable<CartApplySupplyDelta> stocks, IEnumerable<CartApplySupplyDelta> holds)
        {
            Quote = quote;
            stockDeltas = stocks == null ? Array.Empty<CartApplySupplyDelta>() : new List<CartApplySupplyDelta>(stocks).ToArray();
            holdDeltas = holds == null ? Array.Empty<CartApplySupplyDelta>() : new List<CartApplySupplyDelta>(holds).ToArray();
        }
    }

    internal interface ICartApplySupply
    {
        CartApplyQuote Quote(PartDefinition definition, int count, bool unit, int quantity = 1);
        CartApplyConsumeReceipt Consume(PartDefinition definition, int count, bool unit);
    }

    internal sealed class CartApplyDealerRead
    {
        internal readonly CartApplyIdentityRef Identity;
        internal readonly bool IsPart, IsUnit, IsOverlayAddition;
        internal readonly string DefinitionId;
        internal readonly int PaidPrice, SourceOrdinal;
        internal CartApplyDealerRead(CartApplyIdentityRef identity, bool isPart, bool isUnit, string definitionId, int paidPrice, int sourceOrdinal, bool isOverlayAddition)
        { Identity = identity; IsPart = isPart; IsUnit = isUnit; DefinitionId = definitionId; PaidPrice = paidPrice; SourceOrdinal = sourceOrdinal; IsOverlayAddition = isOverlayAddition; }
    }

    internal interface ICartApplyState : IDisposable
    {
        bool HasUnsettledRun { get; }
        bool HasCashRead { get; }
        int Cash { get; set; }
        int Reputation { get; }
        PartMoveMemberPolicy MemberPolicy { get; }
        ICartCatalogRead Catalog { get; }
        ICartApplySupply Supply { get; }
        ICartIdentityAllocator Identities { get; }
        int ErrorIndex { get; }
        bool CaptureEffects { get; }
        IReadOnlyList<CartApplyRowEffect> Effects { get; }
        IEnumerable<PartInstance> PartsInOrder { get; }
        IEnumerable<LooseEngineElement> UnitsInOrder { get; }
        PartMoveCarState FindCarState(string externalCarId);
        IDictionary<PartCategory, string> CarDefinitions(PartMoveCarState car);
        IDictionary<PartCategory, string> EngineDefinitions(PartMoveEngineState engine);
        PartInstance FindPart(CartApplyIdentityRef identity);
        PartInstance FindPartReadOnly(string externalId);
        PartInstance EditPart(PartInstance source);
        LooseEngineElement FindUnit(CartApplyIdentityRef identity);
        LooseEngineElement FindUnitReadOnly(string externalId);
        LooseEngineElement EditUnit(LooseEngineElement source);
        CartApplyDealerRead FindDealerStockReadOnly(string externalAssetId);
        IEnumerable<CartApplyDealerRead> DealerInOrder { get; }
        PartInstance TransferDealerPart(CartApplyDealerRead source);
        LooseEngineElement TransferDealerUnit(CartApplyDealerRead source);
        bool IsCandidateCreated(PartInstance part);
        bool IsCandidateCreated(LooseEngineElement unit);
        void MarkAffectedCar(PartMoveCarState car);
        void MarkChangedPart(PartInstance part);
        void MarkChangedUnit(LooseEngineElement unit);
        CartApplyIdentityRef IdentityFromOwnedField(string id);
        string OwnedFieldValue(CartApplyIdentityRef identity);
        void RemovePart(PartInstance source);
        void AppendPart(PartInstance value);
        void RemoveUnit(LooseEngineElement source);
        void AppendUnit(LooseEngineElement value);
        void RemoveDealerStock(CartApplyDealerRead source);
        void AppendDealerStock(DealerStock value);
        void BeginRow(int rowIndex);
        void CompleteRow(int nextIndex);
        void RecordEffect(CartApplyRowEffect effect);
    }
}
