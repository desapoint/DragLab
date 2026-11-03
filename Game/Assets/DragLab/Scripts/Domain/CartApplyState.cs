using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    internal sealed class LiveCartApplyState : ProfileInstalledPartTransitionState, ICartApplyState
    {
        MechanicalCatalog catalog;
        ICartCatalogRead catalogRead;
        ICartApplySupply supply;
        readonly bool hasCatalogOverride;
        static readonly IReadOnlyList<CartApplyRowEffect> emptyEffects = Array.Empty<CartApplyRowEffect>();
        readonly Dictionary<CartApplyDealerRead,DealerStock> dealerHandles = new Dictionary<CartApplyDealerRead,DealerStock>(ReferenceComparer<CartApplyDealerRead>.Instance);
        bool cashRead, disposed;
        int errorIndex;
        internal LiveCartApplyState(PlayerProfile owner) : base(owner,false) { }
        internal LiveCartApplyState(PlayerProfile owner,MechanicalCatalog catalogOverride) : base(owner, false) { catalog=catalogOverride;hasCatalogOverride=true; }
        internal bool IsCandidate => false;
        internal override bool HasUnsettledRun => SourceProfile.runCheckpoints.Any(run => !run.settled);
        bool ICartApplyState.HasUnsettledRun => HasUnsettledRun;
        public bool HasCashRead => cashRead;
        public int Cash { get { cashRead = true; return SourceProfile.cash; } set { cashRead = true; SourceProfile.cash = value; } }
        public int Reputation => SourceProfile.reputation;
        public PartMoveMemberPolicy MemberPolicy => PartMoveMemberPolicy.LegacyEnsure;
        public ICartCatalogRead Catalog
        {
            get { Check(); if (catalogRead == null) { if(!hasCatalogOverride)catalog = SourceProfile.catalog; catalogRead = new LiveCartCatalogRead(catalog); } return catalogRead; }
        }
        public ICartApplySupply Supply { get { Check(); return supply ?? (supply = new LiveCartApplySupply(SourceProfile)); } }
        public ICartIdentityAllocator Identities => LiveCartIdentityAllocator.Instance;
        public int ErrorIndex => errorIndex;
        public IReadOnlyList<CartApplyRowEffect> Effects => emptyEffects;
        public bool CaptureEffects => false;
        public IEnumerable<PartInstance> PartsInOrder { get { Check(); return SourceProfile.parts; } }
        public IEnumerable<LooseEngineElement> UnitsInOrder { get { Check(); return SourceProfile.looseEngineElements; } }
        public IEnumerable<CartApplyDealerRead> DealerInOrder
        { get { Check(); for (int i=0;i<SourceProfile.inventory.dealer.Count;i++) yield return DealerHandle(SourceProfile.inventory.dealer[i],i); } }
        public PartMoveCarState FindCarState(string id) => FindCar(id);
        IDictionary<PartCategory,string> ICartApplyState.CarDefinitions(PartMoveCarState car) => CarDefinitions(car);
        IDictionary<PartCategory,string> ICartApplyState.EngineDefinitions(PartMoveEngineState engine) => EngineDefinitions(engine);
        public PartInstance FindPart(CartApplyIdentityRef identity) => identity.IsVirtual ? null : FindPart(identity.ExistingId);
        public PartInstance FindPartReadOnly(string id) => SourceProfile.parts.Find(row=>row.instanceId==id);
        public PartInstance EditPart(PartInstance source) => source;
        public LooseEngineElement FindUnit(CartApplyIdentityRef identity) => identity.IsVirtual ? null : FindUnitReadOnly(identity.ExistingId);
        public LooseEngineElement FindUnitReadOnly(string id) => SourceProfile.looseEngineElements.Find(row=>row.element!=null&&row.element.id==id);
        public LooseEngineElement EditUnit(LooseEngineElement source) => source;
        public CartApplyDealerRead FindDealerStockReadOnly(string id)
        { for(int i=0;i<SourceProfile.inventory.dealer.Count;i++){var row=SourceProfile.inventory.dealer[i];if(row.Id==id)return DealerHandle(row,i);}return null; }
        CartApplyDealerRead DealerHandle(DealerStock row,int i)
        {
            var id=row?.Id; if(row==null||id==null)return null;var handle=new CartApplyDealerRead(CartApplyIdentityRef.Existing(id),row.IsPart,row.IsUnit,row.DefinitionId,row.paidPrice,i,false);dealerHandles[handle]=row;return handle;
        }
        DealerStock ResolveDealer(CartApplyDealerRead handle)
            => handle!=null&&dealerHandles.TryGetValue(handle,out var row)?row:null;
        public PartInstance TransferDealerPart(CartApplyDealerRead source) => ResolveDealer(source)?.part;
        public LooseEngineElement TransferDealerUnit(CartApplyDealerRead source) => ResolveDealer(source)?.unit;
        public bool IsCandidateCreated(PartInstance part) => false;
        public bool IsCandidateCreated(LooseEngineElement unit) => false;
        public void MarkAffectedCar(PartMoveCarState car)=>MarkAffected(car);
        public void MarkChangedPart(PartInstance part)=>MarkChanged(part);
        public void MarkChangedUnit(LooseEngineElement unit){if(unit?.element!=null)MarkUnitChanged(FindLooseUnit(unit.element.id));}
        public CartApplyIdentityRef IdentityFromOwnedField(string id) => CartApplyIdentityRef.Existing(id);
        public string OwnedFieldValue(CartApplyIdentityRef identity) => identity.IsVirtual ? throw new InvalidOperationException("Live state cannot resolve a virtual identity.") : identity.ExistingId;
        public void RemovePart(PartInstance source) => SourceProfile.parts.Remove(source);
        public void AppendPart(PartInstance value) => SourceProfile.parts.Add(value);
        public void RemoveUnit(LooseEngineElement source) => SourceProfile.looseEngineElements.Remove(source);
        public void AppendUnit(LooseEngineElement value) => SourceProfile.looseEngineElements.Add(value);
        public void RemoveDealerStock(CartApplyDealerRead source) { var row=ResolveDealer(source); if(row!=null)SourceProfile.inventory.dealer.Remove(row); }
        public void AppendDealerStock(DealerStock value) => SourceProfile.inventory.dealer.Add(value);
        public void BeginRow(int rowIndex) { if(disposed)throw new ObjectDisposedException(nameof(LiveCartApplyState));errorIndex=rowIndex; }
        public void CompleteRow(int nextIndex) { if(disposed)throw new ObjectDisposedException(nameof(LiveCartApplyState));errorIndex=nextIndex; }
        public void RecordEffect(CartApplyRowEffect effect) { if(disposed)throw new ObjectDisposedException(nameof(LiveCartApplyState)); }
        internal PlayerProfile Owner => SourceProfile;
        void Check() { if(disposed)throw new ObjectDisposedException(nameof(LiveCartApplyState)); }
        public override void Dispose()
        {
            if(disposed)return; disposed=true; catalogRead?.Dispose(); catalogRead=null; catalog=null; supply=null; dealerHandles.Clear(); cashRead=false;
            base.Dispose();
        }
    }

    internal sealed class LiveCartCatalogRead : ICartCatalogRead
    {
        MechanicalCatalog catalog;
        internal LiveCartCatalogRead(MechanicalCatalog catalog){this.catalog=catalog;}
        public string[] Models()=>catalog.parts.Where(d=>d.category==PartCategory.CylinderBlock).Select(d=>d.id.Split('/')[0]).Distinct().OrderBy(s=>s,StringComparer.Ordinal).ToArray();
        public CartApplyCatalogProduct FindProduct(string id)
        { var definition=catalog.Find(id); int position=-1,group=-1; if(definition!=null){for(int i=0;i<catalog.parts.Count;i++)if(ReferenceEquals(catalog.parts[i],definition)){position=i;break;}group=position;}return new CartApplyCatalogProduct(definition,definition!=null,position,group); }
        public PartDefinition FindDefinition(string id)=>catalog.Find(id);
        public Dictionary<PartCategory,string> Recipe(string model)=>catalog.Recipe(model);
        public VehicleSpec Spec(string id)=>catalog.Spec(id);
        public BuildResolution Resolve(string id,IDictionary<PartCategory,string> installed,Tune adjustments=null)=>catalog.Resolve(id,installed,adjustments);
        public bool Fits(VehicleSpec spec,PartDefinition definition)=>catalog.Fits(spec,definition);
        public void Dispose(){catalog=null;}
    }

    internal static class CartApplyCatalogReaders
    {
        internal static ICartCatalogRead Live(MechanicalCatalog catalog)=>new LiveCartCatalogRead(catalog);
        internal static ICartCatalogRead Scoped(MechanicalCatalog catalog)=>new CartApplyWorkingSet.ScopedCartCatalogRead(catalog);
    }

    internal sealed class LiveCartApplySupply : ICartApplySupply
    {
        PlayerProfile owner;
        internal LiveCartApplySupply(PlayerProfile owner){this.owner=owner;}
        public CartApplyQuote Quote(PartDefinition d,int count,bool unit,int quantity=1)
        { var q=PartsMarket.Quote(owner,d,count,unit,quantity);return new CartApplyQuote(q.cost,q.fee,q.localUnits,q.expressUnits); }
        public CartApplyConsumeReceipt Consume(PartDefinition d,int count,bool unit)
        { var q=PartsMarket.Consume(owner,d,count,unit);return new CartApplyConsumeReceipt(new CartApplyQuote(q.cost,q.fee,q.localUnits,q.expressUnits),null,null); }
    }
    internal sealed class LiveCartIdentityAllocator : ICartIdentityAllocator
    {
        internal static readonly LiveCartIdentityAllocator Instance=new LiveCartIdentityAllocator();
        public CartApplyAllocatedIdentity Allocate(string kind,int rowIndex,int quantityOrdinal,string memberPath)
        { string id=MechanicsOwnership.NewId(kind);return new CartApplyAllocatedIdentity(CartApplyIdentityRef.Existing(id),id); }
    }

    internal sealed class CartApplyWorkingSet : ProfileInstalledPartTransitionState,ICartApplyState
    {
        readonly List<PartInstance> partAdds=new List<PartInstance>();readonly HashSet<PartInstance> partDeletes=new HashSet<PartInstance>(ReferenceComparer<PartInstance>.Instance);
        readonly List<LooseEngineElement> unitAdds=new List<LooseEngineElement>();readonly HashSet<LooseEngineElement> unitDeletes=new HashSet<LooseEngineElement>(ReferenceComparer<LooseEngineElement>.Instance);
        readonly List<DealerStock> dealerAdds=new List<DealerStock>();readonly HashSet<DealerStock> dealerDeletes=new HashSet<DealerStock>(ReferenceComparer<DealerStock>.Instance);
        readonly Dictionary<CartApplyDealerRead,DealerStock> dealerHandles=new Dictionary<CartApplyDealerRead,DealerStock>(ReferenceComparer<CartApplyDealerRead>.Instance);
        readonly Dictionary<int,PartInstance> virtualParts=new Dictionary<int,PartInstance>();readonly Dictionary<int,LooseEngineElement> virtualUnits=new Dictionary<int,LooseEngineElement>();
        readonly Dictionary<string,CartApplyIdentityRef> symbols=new Dictionary<string,CartApplyIdentityRef>(StringComparer.Ordinal);
        readonly Dictionary<CartApplyIdentityRef,CartApplyIdentity> identityInfo=new Dictionary<CartApplyIdentityRef,CartApplyIdentity>();
        readonly List<CartApplyRowEffect> effects=new List<CartApplyRowEffect>();readonly List<CartApplyIdentity> identityList=new List<CartApplyIdentity>();
        readonly List<CartApplySupplyDelta> allDeltas=new List<CartApplySupplyDelta>();
        readonly CartApplyCaptureContext captureContext=new CartApplyCaptureContext();
        readonly Dictionary<LooseEngineElement,LooseEngineElement> unitEdits=new Dictionary<LooseEngineElement,LooseEngineElement>(ReferenceComparer<LooseEngineElement>.Instance);
        readonly Dictionary<LooseEngineElement,LooseEngineElement> unitEditSourcesByCopy=new Dictionary<LooseEngineElement,LooseEngineElement>(ReferenceComparer<LooseEngineElement>.Instance);
        readonly List<PartInstance> partWriteOrder=new List<PartInstance>();readonly HashSet<PartInstance> partWriteSet=new HashSet<PartInstance>(ReferenceComparer<PartInstance>.Instance);
        readonly List<LooseEngineElement> unitWriteOrder=new List<LooseEngineElement>();readonly HashSet<LooseEngineElement> unitWriteSet=new HashSet<LooseEngineElement>(ReferenceComparer<LooseEngineElement>.Instance);
        readonly List<DealerStock> dealerWriteOrder=new List<DealerStock>();readonly HashSet<DealerStock> dealerWriteSet=new HashSet<DealerStock>(ReferenceComparer<DealerStock>.Instance);
        MechanicalCatalog catalogSource;ICartCatalogRead catalogRead;ScopedMarketReadSet market;ICartApplySupply supply;
        CandidateIdentityAllocator allocator;Func<int,string> symbolProposer;bool cashRead,hasCashScalar,disposed,sourceIdsCaptured,childScopesClosed;int cashScalar,cashBefore,errorIndex,nextIdentity;
        HashSet<string> sourceIds;
        internal CartApplyWorkingSet(PlayerProfile owner,Func<int,string> symbolProposer=null):base(owner,true) { this.symbolProposer=symbolProposer; }
        bool ICartApplyState.HasUnsettledRun=>HasUnsettledRun;
        public bool HasCashRead=>cashRead;
        public int Cash { get { cashRead=true;if(!hasCashScalar){cashScalar=SourceProfile.cash;cashBefore=cashScalar;hasCashScalar=true;}return cashScalar;} set {cashRead=true;cashScalar=value;hasCashScalar=true;} }
        public int Reputation=>SourceProfile.reputation;
        public PartMoveMemberPolicy MemberPolicy=>PartMoveMemberPolicy.ProjectionRequireExisting;
        public bool CaptureEffects=>true;
        public int ErrorIndex=>errorIndex;
        public IReadOnlyList<CartApplyRowEffect> Effects=>effects;
        internal bool IsDisposed=>disposed;
        internal bool ChildrenDisposed=>disposed&&childScopesClosed&&market==null&&catalogRead==null&&catalogSource==null&&supply==null&&allocator==null&&symbolProposer==null&&
            partWriteOrder.Count==0&&partWriteSet.Count==0&&unitWriteOrder.Count==0&&unitWriteSet.Count==0&&dealerWriteOrder.Count==0&&dealerWriteSet.Count==0&&
            partAdds.Count==0&&partDeletes.Count==0&&unitAdds.Count==0&&unitDeletes.Count==0&&dealerAdds.Count==0&&dealerDeletes.Count==0&&dealerHandles.Count==0&&
            virtualParts.Count==0&&virtualUnits.Count==0&&symbols.Count==0&&identityInfo.Count==0&&identityList.Count==0&&effects.Count==0&&allDeltas.Count==0&&
            unitEdits.Count==0&&unitEditSourcesByCopy.Count==0&&sourceIds==null&&captureContext.Count==0&&SourceCachesCleared;
        internal int ActiveTokenCount=>symbols.Count;
        internal int? CashBeforeValue=>cashRead?(int?)cashBefore:null;
        internal int? CashAfterValue=>cashRead?(int?)cashScalar:null;
        internal int? CashDeltaValue=>cashRead?(int?)(cashScalar-cashBefore):null;
        public ICartCatalogRead Catalog
        {
            get { Check();if(catalogRead==null){catalogSource=SourceProfile.catalog;catalogRead=new ScopedCartCatalogRead(catalogSource);market=ScopedMarketReadSet.OpenBorrowed(SourceProfile,catalogRead);supply=new ScopedCartApplySupply(market);allocator=new CandidateIdentityAllocator(this);}return catalogRead; }
        }
        public ICartApplySupply Supply { get { _=Catalog;return supply; } }
        public ICartIdentityAllocator Identities { get { _=Catalog;return allocator; } }
        public IEnumerable<PartInstance> PartsInOrder { get { Check();return EnumerateParts(); } }
        IEnumerable<PartInstance> EnumerateParts(){foreach(var source in SourceProfile.parts)if(!partDeletes.Contains(source))yield return ReadCurrentPart(source);foreach(var added in partAdds)if(added!=null&&!partDeletes.Contains(added))yield return added;}
        public IEnumerable<LooseEngineElement> UnitsInOrder { get { Check();return EnumerateUnits(); } }
        IEnumerable<LooseEngineElement> EnumerateUnits(){foreach(var source in SourceProfile.looseEngineElements)if(!unitDeletes.Contains(source))yield return source!=null&&unitEdits.TryGetValue(source,out var edit)?edit:source;foreach(var added in unitAdds)if(added!=null&&!unitDeletes.Contains(added))yield return added;}
        public IEnumerable<CartApplyDealerRead> DealerInOrder { get { Check();return EnumerateDealer(); } }
        IEnumerable<CartApplyDealerRead> EnumerateDealer(){for(int i=0;i<SourceProfile.inventory.dealer.Count;i++){var source=SourceProfile.inventory.dealer[i];if(source!=null&&!dealerDeletes.Contains(source))yield return DealerHandle(source,i,false);}for(int i=0;i<dealerAdds.Count;i++)if(!dealerDeletes.Contains(dealerAdds[i]))yield return DealerHandle(dealerAdds[i],-1,true);}
        CartApplyDealerRead DealerHandle(DealerStock row,int ordinal,bool added)
        {var id=row?.Id;if(id==null)return null;var key=added?IdentityFromOwnedField(id):CartApplyIdentityRef.Existing(id);var handle=new CartApplyDealerRead(key,row.IsPart,row.IsUnit,row.DefinitionId,row.paidPrice,ordinal,added);dealerHandles[handle]=row;return handle;}
        public PartMoveCarState FindCarState(string id)=>FindCar(id);
        // The K transition surface reads merged inventory state: source rows first,
        // then operation-local additions, with tombstoned source rows hidden.
        internal override bool HasLooseUnit(string id)=>FindUnitReadOnly(id)!=null;
        internal override PartMoveLooseUnitState FindLooseUnit(string id)
        {var unit=FindUnitReadOnly(id);return unit==null?null:WrapLooseUnit(unit);}
        internal override PartInstance FindPart(string id)
        {return FindPart(CartApplyIdentityRef.Existing(id));}
        internal override IDictionary<PartCategory,string> CarDefinitions(PartMoveCarState car)
            =>MechanicsOwnership.Definitions(new CandidatePartList(this),car.Bindings);
        internal override IDictionary<PartCategory,string> EngineDefinitions(PartMoveEngineState engine)
            =>AssemblyOperations.Definitions(new CandidatePartList(this),engine.Bindings);
        IDictionary<PartCategory,string> ICartApplyState.CarDefinitions(PartMoveCarState car)=>CarDefinitions(car);
        IDictionary<PartCategory,string> ICartApplyState.EngineDefinitions(PartMoveEngineState engine)=>EngineDefinitions(engine);
        public PartInstance FindPart(CartApplyIdentityRef identity)
        {
            Check();if(identity.IsVirtual)return virtualParts.TryGetValue(identity.TokenOrdinal,out var created)&&!partDeletes.Contains(created)?created:null;
            var source=SourceProfile.parts.Find(p=>p.instanceId==identity.ExistingId&&!partDeletes.Contains(p));if(source!=null)return EditSourcePart(source);
            var added=partAdds.FirstOrDefault(p=>p!=null&&p.instanceId==identity.ExistingId&&!partDeletes.Contains(p)&&!IsCandidateCreated(p));if(added!=null)return added;
            return null;
        }
        public PartInstance FindPartReadOnly(string externalId)
        {var source=SourceProfile.parts.Find(p=>p.instanceId==externalId&&!partDeletes.Contains(p));if(source!=null)return ReadCurrentPart(source);return partAdds.FirstOrDefault(p=>p!=null&&p.instanceId==externalId&&!partDeletes.Contains(p)&&!IsCandidateCreated(p));}
        public PartInstance EditPart(PartInstance source)
        {Check();if(source==null)return null;if(IsCandidateCreated(source)||partAdds.Contains(source)){TouchPart(source);return source;}var copy=EditSourcePart(source);MarkChanged(copy);return copy;}
        public LooseEngineElement FindUnit(CartApplyIdentityRef identity)
        {Check();if(identity.IsVirtual)return virtualUnits.TryGetValue(identity.TokenOrdinal,out var created)&&!unitDeletes.Contains(created)?created:null;return FindUnitReadOnly(identity.ExistingId);}
        public LooseEngineElement FindUnitReadOnly(string id)
        {var source=SourceProfile.looseEngineElements.Find(u=>u.element.id==id&&!unitDeletes.Contains(u));if(source!=null)return unitEdits.TryGetValue(source,out var edit)?edit:source;return unitAdds.FirstOrDefault(u=>u?.element?.id==id&&!unitDeletes.Contains(u)&&!IsCandidateCreated(u));}
        public LooseEngineElement EditUnit(LooseEngineElement source)
        {Check();if(source==null)return null;if(IsCandidateCreated(source)||unitAdds.Contains(source)){TouchUnit(source);return source;}if(unitEditSourcesByCopy.ContainsKey(source)){TouchUnit(source);return source;}if(unitEdits.TryGetValue(source,out var prior)){TouchUnit(prior);return prior;}var copy=new LooseEngineElement{definitionId=source.definitionId,storageId=source.storageId,unitPrice=source.unitPrice,tireUnit=source.tireUnit,element=source.element?.Copy()};unitEdits[source]=copy;unitEditSourcesByCopy[copy]=source;TouchUnit(copy);return copy;}
        public CartApplyDealerRead FindDealerStockReadOnly(string id)
        {Check();for(int i=0;i<SourceProfile.inventory.dealer.Count;i++){var row=SourceProfile.inventory.dealer[i];if(!dealerDeletes.Contains(row)&&row.Id==id)return DealerHandle(row,i,false);}for(int i=0;i<dealerAdds.Count;i++){var row=dealerAdds[i];if(row!=null&&!dealerDeletes.Contains(row)&&row.Id==id)return DealerHandle(row,-1,true);}return null;}
        public PartInstance TransferDealerPart(CartApplyDealerRead source)
        {var row=DealerRow(source);var p=row?.part;if(p==null)return null;return p.Copy();}
        public LooseEngineElement TransferDealerUnit(CartApplyDealerRead source)
        {var row=DealerRow(source);var u=row?.unit;if(u==null)return null;return new LooseEngineElement{definitionId=u.definitionId,storageId=u.storageId,unitPrice=u.unitPrice,tireUnit=u.tireUnit,element=u.element?.Copy()};}
        DealerStock DealerRow(CartApplyDealerRead h)=>h!=null&&dealerHandles.TryGetValue(h,out var row)?row:null;
        public bool IsCandidateCreated(PartInstance value)=>value!=null&&symbols.TryGetValue(value.instanceId??"",out var identity)&&identity.IsVirtual;
        public bool IsCandidateCreated(LooseEngineElement value)=>value?.element!=null&&symbols.TryGetValue(value.element.id??"",out var identity)&&identity.IsVirtual;
        public void MarkAffectedCar(PartMoveCarState car)=>MarkAffected(car);
        public void MarkChangedPart(PartInstance part)=>MarkChanged(part);
        public void MarkChangedUnit(LooseEngineElement unit){if(unit!=null)EditUnit(unit);TouchUnit(unit);}
        internal override void MarkChanged(PartInstance part){if(part!=null){TouchPart(part);base.MarkChanged(part);}}
        void TouchPart(PartInstance value){var key=value==null?null:partAdds.Contains(value)?value:SourcePart(value);if(key!=null&&partWriteSet.Add(key))partWriteOrder.Add(key);}
        void TouchUnit(LooseEngineElement value){var key=value==null?null:unitAdds.Contains(value)?value:unitEditSourcesByCopy.TryGetValue(value,out var source)?source:value;if(key!=null&&unitWriteSet.Add(key))unitWriteOrder.Add(key);}
        void TouchDealer(DealerStock value){if(value!=null&&dealerWriteSet.Add(value))dealerWriteOrder.Add(value);}
        public CartApplyIdentityRef IdentityFromOwnedField(string id)=>id!=null&&symbols.TryGetValue(id,out var identity)?identity:CartApplyIdentityRef.Existing(id);
        public string OwnedFieldValue(CartApplyIdentityRef identity)
        {if(!identity.IsVirtual)return identity.ExistingId;foreach(var pair in symbols)if(pair.Value==identity)return pair.Key;throw new InvalidOperationException("Unknown virtual identity.");}
        public void RemovePart(PartInstance source){if(source==null)return;var added=partAdds.FirstOrDefault(v=>ReferenceEquals(v,source));if(added!=null){TouchPart(added);partDeletes.Add(added);}else{var original=SourcePart(source);if(SourceProfile.parts.Any(v=>ReferenceEquals(v,original))){TouchPart(original);partDeletes.Add(original);}}}
        public void AppendPart(PartInstance value){if(value==null)throw new ArgumentNullException(nameof(value));partAdds.Add(value);TouchPart(value);var identity=IdentityFromOwnedField(value.instanceId);if(identity.IsVirtual)virtualParts[identity.TokenOrdinal]=value;}
        public void RemoveUnit(LooseEngineElement source){if(source==null)return;var added=unitAdds.FirstOrDefault(v=>ReferenceEquals(v,source));if(added!=null){TouchUnit(added);unitDeletes.Add(added);}else{var original=unitEditSourcesByCopy.TryGetValue(source,out var sourceUnit)?sourceUnit:source;if(SourceProfile.looseEngineElements.Any(v=>ReferenceEquals(v,original))){TouchUnit(original);unitDeletes.Add(original);}}}
        public void AppendUnit(LooseEngineElement value){if(value==null)throw new ArgumentNullException(nameof(value));unitAdds.Add(value);TouchUnit(value);var identity=IdentityFromOwnedField(value.element?.id);if(identity.IsVirtual)virtualUnits[identity.TokenOrdinal]=value;}
        public void RemoveDealerStock(CartApplyDealerRead source){var row=DealerRow(source);if(row!=null){TouchDealer(row);dealerDeletes.Add(row);}}
        public void AppendDealerStock(DealerStock value){if(value==null)throw new ArgumentNullException(nameof(value));dealerAdds.Add(value);TouchDealer(value);}
        public void BeginRow(int index){CheckAlive();errorIndex=index;}
        public void CompleteRow(int next){CheckAlive();errorIndex=next;}
        public void RecordEffect(CartApplyRowEffect effect){CheckAlive();effects.Add(effect);if(effect?.SupplyDeltas!=null)allDeltas.AddRange(effect.SupplyDeltas);}
        CartApplyAllocatedIdentity Allocate(string kind,int row,int quantity,string member)
        {
            Check();if(!sourceIdsCaptured){sourceIds=CaptureSourceIds();sourceIdsCaptured=true;}
            int token=nextIdentity++;string basis=symbolProposer==null?"__cart_projection_"+token+"__":symbolProposer(token);if(basis==null)basis="__cart_projection_"+token+"__";string symbol=basis;int collision=0;while(sourceIds.Contains(symbol)||symbols.ContainsKey(symbol))symbol=basis+"_"+(++collision);
            var identity=CartApplyIdentityRef.Virtual(token);symbols.Add(symbol,identity);identityInfo.Add(identity,new CartApplyIdentity(token,row,quantity,member,kind));identityList.Add(identityInfo[identity]);return new CartApplyAllocatedIdentity(identity,symbol);
        }
        HashSet<string> CaptureSourceIds()
        {
            var ids=new HashSet<string>(StringComparer.Ordinal);if(SourceProfile==null)return ids;
            foreach(var p in (IEnumerable<PartInstance>)SourceProfile.parts??Enumerable.Empty<PartInstance>()){if(p==null)continue;ids.Add(p.instanceId);foreach(var e in p.condition?.elements??Array.Empty<EngineElement>())if(e!=null)ids.Add(e.id);}
            foreach(var u in (IEnumerable<LooseEngineElement>)SourceProfile.looseEngineElements??Enumerable.Empty<LooseEngineElement>())if(u?.element!=null)ids.Add(u.element.id);
            foreach(var c in (IEnumerable<OwnedCar>)SourceProfile.cars??Enumerable.Empty<OwnedCar>()){if(c==null)continue;ids.Add(c.instanceId);ids.Add(c.chassisId);ids.Add(c.engineAssemblyId);ids.Add(c.garageJobId);ids.Add(c.installed?.instanceId);foreach(var b in c.installed?.slots??new List<SlotBinding>())if(b!=null)ids.Add(b.partInstanceId);if(c.engineRebuild!=null){ids.Add(c.engineRebuild.id);ids.Add(c.engineRebuild.engineId);foreach(var target in c.engineRebuild.targets??new List<EngineRebuildTarget>())if(target!=null)ids.Add(target.partId);}foreach(var history in c.usedHistory??new List<string>())ids.Add(history);}
            foreach(var e in (IEnumerable<EngineAssemblyInstance>)SourceProfile.engines??Enumerable.Empty<EngineAssemblyInstance>()){if(e==null)continue;ids.Add(e.instanceId);ids.Add(e.parentVehicleId);foreach(var b in e.slots??new List<SlotBinding>())if(b!=null)ids.Add(b.partInstanceId);}
            foreach(var s in (IEnumerable<DealerStock>)SourceProfile.inventory?.dealer??Enumerable.Empty<DealerStock>()){if(s==null)continue;ids.Add(s.Id);if(s.part!=null){ids.Add(s.part.instanceId);foreach(var e in s.part.condition?.elements??Array.Empty<EngineElement>())if(e!=null)ids.Add(e.id);}if(s.unit?.element!=null)ids.Add(s.unit.element.id);}
            foreach(var storage in SourceProfile.inventory?.storage??new List<InventoryContainer>())if(storage!=null)ids.Add(storage.id);
            foreach(var run in SourceProfile.runCheckpoints??new List<RunCheckpoint>()){if(run==null)continue;ids.Add(run.runId);ids.Add(run.ownedVehicleId);foreach(var p in run.components??new List<PartInstance>()){if(p==null)continue;ids.Add(p.instanceId);foreach(var e in p.condition?.elements??Array.Empty<EngineElement>())if(e!=null)ids.Add(e.id);}}
            return ids;
        }
        internal CartApplyIdentity[] IdentityMap()=>identityList.ToArray();
        internal CartApplySupplyDelta[] SupplyDeltas()=>allDeltas.ToArray();
        internal CartApplyPartSnapshot[] ChangedPartSnapshots()
        {var rows=new List<CartApplyPartSnapshot>(partWriteOrder.Count);int insertion=0;for(int i=0;i<partWriteOrder.Count;i++){var key=partWriteOrder[i];bool added=partAdds.Contains(key),deleted=partDeletes.Contains(key);if(added&&deleted)continue;var value=added?key:ReadCurrentPart(key);rows.Add(new CartApplyPartSnapshot(IdentityFromOwnedField(key.instanceId),deleted,added?-1:SourcePartOrdinal(key),added?insertion++:-1,deleted?null:value,IdentityFromOwnedField,captureContext));}return rows.ToArray();}
        int SourcePartOrdinal(PartInstance source)=>SourceProfile?.parts==null?-1:SourceProfile.parts.FindIndex(p=>ReferenceEquals(p,source));
        internal CartApplyUnitSnapshot[] ChangedUnitSnapshots()
        {var rows=new List<CartApplyUnitSnapshot>(unitWriteOrder.Count);int insertion=0;for(int i=0;i<unitWriteOrder.Count;i++){var key=unitWriteOrder[i];bool added=unitAdds.Contains(key),deleted=unitDeletes.Contains(key);if(added&&deleted)continue;var value=added?key:unitEdits.TryGetValue(key,out var edit)?edit:key;rows.Add(new CartApplyUnitSnapshot(IdentityFromOwnedField(key.element?.id),deleted,added?-1:SourceUnitOrdinal(key),added?insertion++:-1,deleted?null:value,IdentityFromOwnedField,captureContext));}return rows.ToArray();}
        int SourceUnitOrdinal(LooseEngineElement source)=>SourceProfile?.looseEngineElements==null?-1:SourceProfile.looseEngineElements.FindIndex(u=>ReferenceEquals(u,source));
        internal CartApplyEngineSnapshot[] ChangedEngineSnapshots()=>ChangedEngines.Select(e=>new CartApplyEngineSnapshot(IdentityFromOwnedField(e.Id),false,SourceEngineOrdinal(e),-1,e.SnapshotCopy(),IdentityFromOwnedField)).ToArray();
        int SourceEngineOrdinal(PartMoveEngineState engine)=>SourceProfile?.engines==null?-1:SourceProfile.engines.FindIndex(e=>ReferenceEquals(e,engine.Source));
        internal CartApplyDealerSnapshot[] ChangedDealerSnapshots()
        {var rows=new List<CartApplyDealerSnapshot>(dealerWriteOrder.Count);int insertion=0;for(int i=0;i<dealerWriteOrder.Count;i++){var row=dealerWriteOrder[i];bool added=dealerAdds.Contains(row),deleted=dealerDeletes.Contains(row);if(added&&deleted)continue;rows.Add(new CartApplyDealerSnapshot(IdentityFromOwnedField(row.Id),deleted,added?-1:SourceDealerOrdinal(row),added?insertion++:-1,deleted?null:row,IdentityFromOwnedField,captureContext));}return rows.ToArray();}
        int SourceDealerOrdinal(DealerStock row)=>SourceProfile?.inventory?.dealer==null?-1:SourceProfile.inventory.dealer.FindIndex(value=>ReferenceEquals(value,row));
        internal CartApplyCarSnapshot[] ChangedCarSnapshots()
        {Func<string,CartApplyIdentityRef> map=IdentityFromOwnedField;return AffectedCars.Select(car=>new CartApplyCarSnapshot(IdentityFromOwnedField(car.Id),false,SourceCarOrdinal(car),-1,car.SnapshotCopy(),map)).ToArray();}
        int SourceCarOrdinal(PartMoveCarState car)=>SourceProfile?.cars==null?-1:SourceProfile.cars.FindIndex(value=>ReferenceEquals(value,car.Source));
        internal CartApplyOwnerSnapshot[] ChangedOwnerSnapshots()
        {return AffectedOwners.Select(owner=>owner.Car!=null?new CartApplyOwnerSnapshot(IdentityFromOwnedField(owner.Car.Id),false,SourceCarOrdinal(owner.Car),-1,"car",BindingRefs(owner.Car.Bindings,IdentityFromOwnedField)):new CartApplyOwnerSnapshot(IdentityFromOwnedField(owner.Engine.Id),false,SourceEngineOrdinal(owner.Engine),-1,"engine",BindingRefs(owner.Engine.Bindings,IdentityFromOwnedField))).ToArray();}
        static IEnumerable<CartApplyIdentityRef?> BindingRefs(IList<SlotBinding> bindings,Func<string,CartApplyIdentityRef> map)
            =>bindings==null?null:bindings.Select(binding=>binding==null?(CartApplyIdentityRef?)null:map(binding.partInstanceId));
        void CheckAlive(){if(disposed)throw new ObjectDisposedException(nameof(CartApplyWorkingSet));}
        void Check(){CheckAlive();if(SourceProfile==null)throw new NullReferenceException();}
        public override void Dispose(){if(disposed)return;disposed=true;market?.Dispose();bool marketClosed=market==null||market.IsDisposed;catalogRead?.Dispose();bool catalogClosed=catalogRead==null||catalogRead is ScopedCartCatalogRead scoped&&scoped.IsDisposed;childScopesClosed=marketClosed&&catalogClosed;market=null;catalogRead=null;catalogSource=null;supply=null;allocator?.Close();allocator=null;symbolProposer=null;partAdds.Clear();partDeletes.Clear();unitAdds.Clear();unitDeletes.Clear();dealerAdds.Clear();dealerDeletes.Clear();dealerHandles.Clear();virtualParts.Clear();virtualUnits.Clear();symbols.Clear();identityInfo.Clear();identityList.Clear();effects.Clear();allDeltas.Clear();unitEdits.Clear();unitEditSourcesByCopy.Clear();partWriteOrder.Clear();partWriteSet.Clear();unitWriteOrder.Clear();unitWriteSet.Clear();dealerWriteOrder.Clear();dealerWriteSet.Clear();sourceIds?.Clear();sourceIds=null;captureContext.Clear();base.Dispose();}
        sealed class CandidateIdentityAllocator:ICartIdentityAllocator
        {CartApplyWorkingSet state;internal CandidateIdentityAllocator(CartApplyWorkingSet value){state=value;}public CartApplyAllocatedIdentity Allocate(string kind,int rowIndex,int quantityOrdinal,string memberPath)=>state.Allocate(kind,rowIndex,quantityOrdinal,memberPath);internal void Close(){state=null;}}
        internal sealed class ScopedCartCatalogRead:ICartCatalogRead
        {
            MechanicalCatalog catalog;MechanicalCatalog.LookupScope scope;internal bool IsDisposed=>catalog==null&&scope==null;internal ScopedCartCatalogRead(MechanicalCatalog value){catalog=value;}
            MechanicalCatalog.LookupScope Scope=>scope??(scope=catalog.OpenLookupScope());
            public string[] Models()=>catalog.parts.Where(d=>d.category==PartCategory.CylinderBlock).Select(d=>d.id.Split('/')[0]).Distinct().OrderBy(s=>s,StringComparer.Ordinal).ToArray();
            public CartApplyCatalogProduct FindProduct(string id){var d=Scope.FindDefinition(id,out var found,out var position,out var group);return new CartApplyCatalogProduct(d,found,position,group);}
            public PartDefinition FindDefinition(string id)=>Scope.FindDefinition(id,out _,out _,out _);
            public Dictionary<PartCategory,string> Recipe(string model)=>catalog.Recipe(model);
            public VehicleSpec Spec(string id)=>catalog.Spec(id);
            public BuildResolution Resolve(string id,IDictionary<PartCategory,string> installed,Tune adjustments=null)=>catalog.ResolveCore(id,installed,adjustments,FindDefinition);
            public bool Fits(VehicleSpec spec,PartDefinition definition)=>catalog.Fits(spec,definition);
            public void Dispose(){scope?.Dispose();scope=null;catalog=null;}
        }
        sealed class ScopedCartApplySupply:ICartApplySupply
        {ScopedMarketReadSet market;internal ScopedCartApplySupply(ScopedMarketReadSet value){market=value;}public CartApplyQuote Quote(PartDefinition d,int count,bool unit,int quantity=1){var q=market.Quote(d,count,unit,quantity);return new CartApplyQuote(q.Cost,q.Fee,q.LocalUnits,q.ExpressUnits);}public CartApplyConsumeReceipt Consume(PartDefinition d,int count,bool unit){var result=market.Consume(d,count,unit);return new CartApplyConsumeReceipt(new CartApplyQuote(result.Quote.Cost,result.Quote.Fee,result.Quote.LocalUnits,result.Quote.ExpressUnits),Convert(result.StockDeltas,false),Convert(result.HoldDeltas,true));}static CartApplySupplyDelta[] Convert(IEnumerable<ScopedQuantityDelta> values,bool hold)=>values?.Select(v=>new CartApplySupplyDelta(v.IdentityGroup,v.Before,v.After,v.Positions,hold)).ToArray()??Array.Empty<CartApplySupplyDelta>();}
        sealed class CandidatePartList:IList<PartInstance>
        {readonly CartApplyWorkingSet owner;internal CandidatePartList(CartApplyWorkingSet value){owner=value;}public int Count=>owner.PartsInOrder.Count();public bool IsReadOnly=>true;public PartInstance this[int i]{get=>owner.PartsInOrder.ElementAt(i);set=>throw new NotSupportedException();}public int IndexOf(PartInstance value){int i=0;foreach(var p in owner.PartsInOrder){if(ReferenceEquals(value,p))return i;i++;}return -1;}public bool Contains(PartInstance v)=>IndexOf(v)>=0;public void CopyTo(PartInstance[] a,int i){foreach(var p in owner.PartsInOrder)a[i++]=p;}public IEnumerator<PartInstance> GetEnumerator()=>owner.PartsInOrder.GetEnumerator();System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();public void Add(PartInstance v)=>throw new NotSupportedException();public void Clear()=>throw new NotSupportedException();public void Insert(int i,PartInstance v)=>throw new NotSupportedException();public bool Remove(PartInstance v)=>throw new NotSupportedException();public void RemoveAt(int i)=>throw new NotSupportedException();}
    }
}
