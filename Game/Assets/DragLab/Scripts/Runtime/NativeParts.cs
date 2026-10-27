using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        sealed class PartActionOrigin
        {
            internal int DataGeneration,PresentationGeneration,WindowGeneration,DetailGeneration;
            internal int DetailDataGeneration,DetailPresentationGeneration;
            internal string EntryId,Source;
            internal string DetailId;
        }
        VisualElement partsPage,partsGrid,partsDetail;
        ScrollView partsScroll,partsDetailScroll;
        TextField partsSearch;
        Label partsCount,partsEmpty,partsNotice,partsReference;
        Button partsFilter,partsSort,partsCart;
        NativePartSection partsSection;
        bool partsPlanned;
        string partsStateKey,partsDetailId;
        List<NativePartEntry> partMatches=new List<NativePartEntry>();
        int partSectionCount;
        VisualElement partsBefore,partsAfter;
        int partWindowFirst=-1,partWindowLast=-1;
        const float partRowHeight=246;
        bool renderingPartWindow;
        internal int PartWindowFirst=>partWindowFirst;
        PlayerProfile partsProfile;
        string partsContextKey;
        readonly Dictionary<string,NativePartBrowser> partsBrowsers=new Dictionary<string,NativePartBrowser>();
        Vector2 partsReturnScroll;
        NativePartBrowser partsBrowser;
        readonly Dictionary<string,NativePartQuery> partQueries=new Dictionary<string,NativePartQuery>();
        readonly List<RacingSurface> partGridSurfaces=new List<RacingSurface>(),partDetailSurfaces=new List<RacingSurface>();
        readonly Dictionary<string,Dictionary<string,bool>> partDetailCollapsed=new Dictionary<string,Dictionary<string,bool>>();
        readonly Dictionary<string,bool> partDetailDifferences=new Dictionary<string,bool>();
        readonly Dictionary<string,Label[]> partCardLabels=new Dictionary<string,Label[]>();
        readonly Dictionary<string,Button> partCards=new Dictionary<string,Button>();
        readonly Dictionary<string,Action> partCardActions=new Dictionary<string,Action>();
        readonly Dictionary<string,Action> partDetailActionsByName=new Dictionary<string,Action>();
        readonly NativePartPreviewCache partPreviewCache=new NativePartPreviewCache();
        readonly NativePartSpecificationCache partSpecCache=new NativePartSpecificationCache();
        List<NativePartEntry> partPending=new List<NativePartEntry>();int partPendingIndex;
        NativePartBrowserBuild activePartBuild;
        NativePartsBaseReadInputs initialPartInputs;
        NativePartsBaseReadSet acceptedPartFacts;
        NativePartPreviewCache.TypedBinding acceptedPartBinding;
        NativePartsIdleSnapshot partIdleSnapshot;
        NativePartQueryBuild partQueryBuild;
        NativePartWindowBuild partWindowBuild;
        NativePartWindowBuild partWindowAwaitingLayout,partWindowLayoutFallback;
        int partDataGeneration,partPresentationGeneration,partPreparationFrame=-1;
        int partWindowGeneration;
        int partWindowLayoutAttachFrame=-1;
        int partWindowLayoutValidationAttempts;
        long partWindowLayoutAttachStarted;
        int partPublishedPresentationGeneration=-1;
        bool partActionsEnabled,partDetailActionsReady,partWindowLayoutHoldRequested,partWindowLayoutBlockedByModal;
        const int MaxPartWindowLayoutValidationAttempts=4;
        Action partDetailPurchaseAction;
        PartDetailWork partDetailWork;
        sealed class PartDetailWork
        {
            internal string Id,Source,Page;
            internal int DataGeneration,PresentationGeneration,WindowGeneration,DetailGeneration,Phase,LayoutWaitFrames;
            internal NativePartBrowser Browser;
            internal NativePartEntry Entry,Offer;
            internal PartActionOrigin Origin;
            internal bool Remember,Legacy,PreserveExisting;
            internal Vector2 Offset;
            internal float ViewportWidth,ViewportHeight;
            internal long Started;
            internal VisualElement Top,Facts,Specifications,Actions;
            internal VisualElement Root;
            internal ScrollView Scroll;
            internal Action PurchaseAction;
            internal readonly List<RacingSurface> Surfaces=new List<RacingSurface>();
            internal readonly Dictionary<string,Action> ActionCallbacks=new Dictionary<string,Action>();
        }
        bool partStepHoldRequested,partStepHeld;
        bool partAwaitingCatalog,partLastWindowAccepted,partsLegacyActive,partBuildFailed;
        string partBuildFailure;
        int partDetailDataGeneration=-1,partDetailPresentationGeneration=-1,partDetailGeneration;
        float partAcceptedViewportWidth=-1,partAcceptedViewportHeight=-1,partAcceptedScrollY=float.NaN;
        string partCurrentStatus,partCurrentPlanError;
        internal bool PartComparisonPending=>partPendingIndex<partPending.Count||partQueryBuild!=null&&!partQueryBuild.Complete;
        internal int PartCardBuildCount{get;private set;}
        internal int PartQueryApplyCount{get;private set;}
        internal int PartCacheHits=>partPreviewCache.Hits;
        internal bool PartBrowserPreparationPending=>!partBuildFailed&&(partDetailWork!=null||activePartBuild!=null&&!activePartBuild.Complete&&!activePartBuild.Failed||partQueryBuild!=null&&!partQueryBuild.Complete||partWindowBuild!=null&&!partWindowBuild.Complete||partWindowAwaitingLayout!=null||partWindowLayoutBlockedByModal||!partsLegacyActive&&partsDetailId!=null&&(partDetailDataGeneration!=partDataGeneration||partDetailPresentationGeneration!=partPresentationGeneration)&&!ModalOpen||!partsLegacyActive&&Visible&&partsPage!=null&&!partsPage.ClassListContains("hidden")&&!cartPageOpen&&!enginePageOpen&&!partActionsEnabled&&!(partsDetailId!=null&&partDetailActionsReady&&partDetailDataGeneration==partDataGeneration&&partDetailPresentationGeneration==partPresentationGeneration));
        internal int PartBrowserDataGeneration=>partDataGeneration;
        internal int PartBrowserPresentationGeneration=>partPresentationGeneration;
        internal int PartBrowserWindowGeneration=>partWindowGeneration;
        internal int PartBrowserWindowCardCount=>partCards.Count;
        internal bool PartBrowserWindowLayoutPending=>partWindowAwaitingLayout!=null;
        internal int PartBrowserWindowLayoutGeneration=>partWindowAwaitingLayout?.WindowGeneration??-1;
        internal int PartBrowserWindowLayoutAttachedCardCount=>partWindowAwaitingLayout?.Cards.Count??0;
        internal int PartBrowserWindowLayoutValidationAttemptsForChecks=>partWindowLayoutValidationAttempts;
        internal string PartBrowserPreparationFailureForChecks=>partBuildFailure;
        internal bool PartBrowserWindowGeometryCurrentForChecks
        {
            get
            {
                if(!partLastWindowAccepted||partPublishedPresentationGeneration!=partPresentationGeneration||partsScroll==null)return false;
                float scrollY=partsScroll.scrollOffset.y,width=partsScroll.contentViewport.layout.width,height=partsScroll.contentViewport.layout.height;
                return FinitePartLayout(scrollY)&&FinitePartLayout(width)&&FinitePartLayout(height)&&width>0&&height>0&&
                    Math.Abs(scrollY-partAcceptedScrollY)<.01f&&Math.Abs(width-partAcceptedViewportWidth)<.01f&&
                    Math.Abs(height-partAcceptedViewportHeight)<.01f;
            }
        }
        internal string PartBrowserDetailId=>partsDetailId;
        internal int PartBrowserDetailGeneration=>partDetailGeneration;
        internal int PartBrowserDetailDataGeneration=>partDetailDataGeneration;
        internal int PartBrowserDetailPresentationGeneration=>partDetailPresentationGeneration;
        internal int PartBrowserDetailPhase=>partDetailWork?.Phase??-1;
        internal bool PartBrowserDetailCurrent=>partsDetailId!=null&&partDetailActionsReady&&partDetailDataGeneration==partDataGeneration&&partDetailPresentationGeneration==partPresentationGeneration&&acceptedPartFacts!=null;
        internal bool PartBrowserActionsEnabled=>partActionsEnabled;
        internal bool PartBrowserStepHeld=>partStepHeld;
        internal string PartBrowserPreparationState=>partBuildFailed?"Failed":partDetailWork!=null?"Detail:"+partDetailWork.Phase:activePartBuild!=null&&!activePartBuild.Complete&&!activePartBuild.Failed?activePartBuild.State.ToString():PartComparisonPending?"Query":partWindowAwaitingLayout!=null?"WindowLayout":partWindowLayoutBlockedByModal?"WindowPaused":(partWindowBuild!=null&&!partWindowBuild.Complete||partWindowLayoutFallback!=null)?"Window":partsBrowser!=null?"Ready":"Idle";
        internal double PartPriceFactoryMilliseconds{get;private set;}
        internal double PartPriceFactoryMaximumMilliseconds{get;private set;}
        internal int PartPriceFactoryCallCount{get;private set;}
        internal int PartPriceFactoryMaximumCallsPerFrame{get;private set;}
        int partPriceFactoryFrame=-1,partPriceFactoryFrameCalls;
        internal double PartQueryMilliseconds{get;private set;}
        internal double PartSortMilliseconds{get;private set;}
        internal double PartWindowCreateMilliseconds{get;private set;}
        internal double PartWindowAttachMilliseconds{get;private set;}
        internal double PartWindowLayoutWaitMilliseconds{get;private set;}
        internal int PartWindowLayoutWaitFrames{get;private set;}
        internal double PartCurrentValidationMilliseconds{get;private set;}
        internal double PartDetailBuildMilliseconds{get;private set;}
        internal double PartDetailOpenToReadyMilliseconds{get;private set;}
        internal double PartDetailEvaluationMilliseconds{get;private set;}
        internal double PartDetailFactsMilliseconds{get;private set;}
        internal double PartDetailActionsMilliseconds{get;private set;}
        internal double PartDetailStatsMilliseconds{get;private set;}
        internal double PartDetailAttachMilliseconds{get;private set;}
        internal double PartDetailPublishMilliseconds{get;private set;}
        internal double PartDetailLayoutWaitMilliseconds{get;private set;}
        internal int PartDetailLayoutWaitFrames{get;private set;}
        internal double PartDetailHealthyMilliseconds{get;private set;}
        internal double PartDetailPriceMilliseconds{get;private set;}
        internal int PartActionGuardRejectedCount{get;private set;}
        internal int PartBuildFailureCount{get;private set;}
        internal long PartConsumerJsonCallCount=>RuntimeDataEncodingProbe.PartConsumerJsonCallCount;
        internal long PartConsumerLegacyKeyCallCount=>RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount;
        internal string PartConsumerLastOperation=>RuntimeDataEncodingProbe.LastOperation;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void HoldPartBrowserAfterNextStep(){partStepHoldRequested=true;partStepHeld=false;}
        internal void HoldPartBrowserAfterWindowAttachForChecks(){partWindowLayoutHoldRequested=true;partStepHeld=false;}
        internal void ResumePartBrowserPreparation(){partStepHoldRequested=false;partWindowLayoutHoldRequested=false;partStepHeld=false;}
        internal void RetryPartBrowserPreparation(){partBuildFailed=false;partBuildFailure=null;partIdleSnapshot=null;partWindowLayoutBlockedByModal=false;partWindowLayoutValidationAttempts=0;}
        internal void FailPartBrowserForChecks(string reason)=>FailPartBrowserBuild(reason);
        internal Action CapturePartCardActionForChecks(string id)
        {
            return partCardActions.TryGetValue(id,out var action)?action:null;
        }
        internal Action CapturePartDetailActionForChecks()=>partDetailPurchaseAction;
        internal int PartRegisteredSurfaceCountForChecks=>surfaces.Count;
        internal Func<int> CaptureAttachedPartWindowSurfaceRegistrationForChecks()
        {
            // Observe these exact pending surfaces after cancellation. A modal
            // legitimately adds unrelated surfaces to the global registry.
            var captured=partWindowAwaitingLayout?.Cards.Select(card=>card.Surface).ToArray()??Array.Empty<RacingSurface>();
            return ()=>captured.Count(surface=>surfaces.Contains(surface));
        }
        internal string PartActionLastRejectedReasonForChecks{get;private set;}
        internal string PartWindowContextForChecks=>"accepted="+partLastWindowAccepted+",publishedPresentation="+partPublishedPresentationGeneration+
            ",scroll="+(partsScroll==null?"none":partsScroll.scrollOffset.y.ToString("R"))+",acceptedScroll="+partAcceptedScrollY.ToString("R")+
            ",viewport="+(partsScroll==null?"none":partsScroll.contentViewport.layout.width.ToString("R")+"x"+partsScroll.contentViewport.layout.height.ToString("R"))+
            ",acceptedViewport="+partAcceptedViewportWidth.ToString("R")+"x"+partAcceptedViewportHeight.ToString("R")+
            ",layoutPending="+(partWindowAwaitingLayout!=null)+",layoutGeneration="+(partWindowAwaitingLayout?.WindowGeneration??-1)+
            ",layoutAttachedCards="+(partWindowAwaitingLayout?.Cards.Count??0)+",layoutFallback="+(partWindowLayoutFallback?.WindowGeneration??-1)+
            ",layoutAttempts="+partWindowLayoutValidationAttempts+",layoutPausedForModal="+partWindowLayoutBlockedByModal;
        internal string PartWindowLayoutContextForChecks
        {
            get
            {
                var ticket=partWindowAwaitingLayout??partWindowLayoutFallback;
                return ticket==null?"none":"data="+ticket.DataGeneration+",presentation="+ticket.PresentationGeneration+
                    ",window="+ticket.WindowGeneration+",range="+ticket.First+":"+ticket.Last+
                    ",ticketScroll="+ticket.ScrollY.ToString("R")+",ticketViewport="+ticket.ViewportWidth.ToString("R")+"x"+ticket.ViewportHeight.ToString("R")+
                    ",attachFrame="+partWindowLayoutAttachFrame+",current="+PartWindowContextForChecks;
            }
        }
        internal string PartWindowLayoutGeometryForChecks
        {
            get
            {
                var ticket=partWindowAwaitingLayout??partWindowLayoutFallback;
                if(ticket==null||partsScroll==null)return "no attached window";
                float scrollY=partsScroll.scrollOffset.y,width=partsScroll.contentViewport.layout.width;
                float height=partsScroll.contentViewport.layout.height,contentHeight=partsScroll.contentContainer.layout.height;
                bool valid=AttachedPartWindowHasFiniteGeometry(ticket,width,height,scrollY,contentHeight);
                string range="unavailable";
                if(FinitePartLayout(scrollY)&&FinitePartLayout(height)&&height>0)
                {var current=NativeCardWindow.Calculate(partMatches.Count,scrollY,height,partRowHeight);range=current.First+":"+current.Last;}
                return "valid="+valid+",attempts="+partWindowLayoutValidationAttempts+",currentRange="+range+
                    ",attachedRange="+ticket.First+":"+ticket.Last+",scroll="+scrollY.ToString("R")+",ticketScroll="+ticket.ScrollY.ToString("R")+
                    ",viewport="+width.ToString("R")+"x"+height.ToString("R")+",ticketViewport="+ticket.ViewportWidth.ToString("R")+"x"+ticket.ViewportHeight.ToString("R")+
                    ",contentHeight="+contentHeight.ToString("R")+",panel="+(ticket.StagedGrid.panel!=null)+
                    ",cards="+ticket.Cards.Count+"/"+(ticket.Last-ticket.First);
            }
        }
        internal Action CapturePartDetailActionForChecks(string actionName)=>partDetailActionsByName.TryGetValue(actionName,out var action)?action:null;
#endif
        NativePartQuery PartsQuery {get{string key=game.PartsPage+"/"+partsSection;if(!partQueries.TryGetValue(key,out var q)){q=new NativePartQuery();partQueries[key]=q;}return q;}}
        internal NativePartBrowser PartBrowser=>partsBrowser;
        internal NativePartQuery PartQuery=>PartsQuery;
        internal bool PartsPlanned=>partsPlanned;
        internal int PartBrowserBuildCount{get;private set;}
        internal void FilterShopCategory(PartCategory category)
        {
            partsSection=NativePartBrowser.Section(category);
            var query=new NativePartQuery();query.Categories.Add(category);partQueries["Shop/"+partsSection]=query;
            InvalidatePartPresentation();
        }
        void InitializeParts()
        {
            partsPage=new VisualElement{name="partsPage"};partsPage.AddToClassList("parts-page");partsPage.AddToClassList("hidden");root.Q(className:"workspace").Add(partsPage);
            var heading=new VisualElement();heading.AddToClassList("car-details-heading");partsPage.Add(heading);var name=new Label{name="partsCarName",enableRichText=false};name.AddToClassList("car-detail-title");heading.Add(name);
            partsCart=PartButton(heading,"partsCart","Cart",OpenNativeCart);
            PartButton(heading,"partsTireSwap","Swap stored tires",()=>{
                if(partsLegacyActive){OpenTireInventorySwap();return;}
                if(!TryFreshPartFence(out _,out var reason)){RejectPartAction(reason);return;}
                OpenTireInventorySwap(PartContextOrigin());
            });partsCart.Insert(1,new FilledMenuIcon("Business"));
            PartButton(heading,"partsEngines","Engines",()=>{enginePageOpen=true;engineDetailId=null;engineViewKey=null;Refresh();}).Insert(1,new FilledMenuIcon("Build"));
            var sources=new VisualElement();sources.AddToClassList("owned-query-choices");sources.AddToClassList("choice-group");partsPage.Add(sources);
            sources.AddToClassList("parts-sources");
            PartChoices(sources,"partsSource",new[]{"Installed","Inventory","Shop"},()=>game.PartsPage,v=>{game.SelectPartsPage(v);partsDetailId=null;partsStateKey=null;});
            var guidance=new Label{name="partsGuidance"};guidance.AddToClassList("commerce-guidance");partsPage.Add(guidance);
            var tabs=new VisualElement{name="partsTabs"};tabs.AddToClassList("settings-tabs");tabs.AddToClassList("parts-tabs");partsPage.Add(tabs);
            for(int i=0;i<NativePartBrowser.SectionNames.Length;i++){int index=i;var b=new Button(()=>{partsSection=(NativePartSection)index;partsDetailId=null;InvalidatePartPresentation();}){name="partsTab"+i,text=NativePartBrowser.SectionNames[i],focusable=false,tabIndex=-1};b.AddToClassList("category-button");tabs.Add(b);AddSurface(b,i==0?RacingSurface.Kind.FirstTab:RacingSurface.Kind.Tab);}
            var toolbar=new VisualElement{name="partsToolbar"};toolbar.AddToClassList("owned-car-toolbar");partsPage.Add(toolbar);
            partsSearch=new TextField{name="partsSearch",maxLength=128};partsSearch.AddToClassList("setting-text");partsSearch.AddToClassList("owned-car-search");toolbar.Add(partsSearch);partsSearch.RegisterValueChangedCallback(e=>{PartsQuery.Search=e.newValue;ApplyPartQuery();});
            var hint=new Label("Search parts"){pickingMode=PickingMode.Ignore};hint.AddToClassList("owned-search-hint");partsSearch.Add(hint);partsSearch.RegisterCallback<FocusInEvent>(_=>hint.AddToClassList("hidden"));partsSearch.RegisterCallback<FocusOutEvent>(_=>hint.EnableInClassList("hidden",partsSearch.value!=""));partsSearch.RegisterValueChangedCallback(e=>hint.EnableInClassList("hidden",e.newValue!=""));
            partsFilter=PartButton(toolbar,"partsFilters","Filters",()=>OpenPartFilters());partsFilter.Insert(1,new FilledMenuIcon("Tuning"));partsSort=PartButton(toolbar,"partsSort","Price",OpenPartSort);
            var clear=PartButton(toolbar,"partsClear","×",()=>{partQueries[game.PartsPage+"/"+partsSection]=new NativePartQuery();partsSearch.SetValueWithoutNotify("");ApplyPartQuery();});clear.AddToClassList("owned-query-clear");
            partsCount=new Label{name="partsCount"};partsCount.AddToClassList("owned-car-count");toolbar.Add(partsCount);
            partsReference=new Label{name="partsReference"};partsReference.AddToClassList("tuning-readout");partsPage.Add(partsReference);
            partsScroll=new ScrollView{name="partsScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};partsScroll.AddToClassList("owned-cars-scroll");partsPage.Add(partsScroll);
            partsGrid=new VisualElement{name="partsGrid"};partsGrid.AddToClassList("owned-cars-grid");partsScroll.Add(partsGrid);
            partsBefore=new VisualElement{name="partsWindowBefore",pickingMode=PickingMode.Ignore};partsAfter=new VisualElement{name="partsWindowAfter",pickingMode=PickingMode.Ignore};
            foreach(var spacer in new[]{partsBefore,partsAfter}){spacer.style.width=Length.Percent(100);spacer.style.flexShrink=0;}
            partsEmpty=new Label{name="partsEmpty"};partsEmpty.AddToClassList("car-empty");partsScroll.Add(partsEmpty);
            partsDetail=new VisualElement{name="partsDetail"};partsDetail.AddToClassList("car-details-page");partsDetail.AddToClassList("hidden");partsPage.Add(partsDetail);
            partsDetailScroll=new ScrollView{name="partsDetailScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};partsDetailScroll.AddToClassList("car-details-scroll");partsDetail.Add(partsDetailScroll);
            partsNotice=new Label{name="partsNotice",enableRichText=false};partsNotice.AddToClassList("car-critical");partsPage.Add(partsNotice);
        }
        Button PartButton(VisualElement parent,string id,string label,Action action,bool primary=false,bool detail=false)
        {var b=new Button(action){name=id,text=label,focusable=false,tabIndex=-1};b.AddToClassList("action-button");if(primary)b.AddToClassList("primary-action");parent.Add(b);if(detail){AddPartDetailSurface(b,primary?RacingSurface.Kind.Action:RacingSurface.Kind.SecondaryAction);if(partDetailWork!=null)partDetailWork.ActionCallbacks[id]=action;else partDetailActionsByName[id]=action;}else AddSurface(b,primary?RacingSurface.Kind.Action:RacingSurface.Kind.SecondaryAction);return b;}
        void PartChoices(VisualElement parent,string id,string[] options,Func<string> get,Action<string> set)
        {
            var buttons=new List<Button>();void Update(){for(int i=0;i<buttons.Count;i++)buttons[i].EnableInClassList("selected",get()==options[i]);}
            for(int i=0;i<options.Length;i++){string value=options[i];var b=new Button(()=>{set(value);Update();}){name=id+":"+value,text=value,focusable=false,tabIndex=-1};b.AddToClassList("choice-button");b.AddToClassList(i==0?"choice-first":i==options.Length-1?"choice-last":"choice-middle");parent.Add(b);AddSurface(b,i==0?RacingSurface.Kind.ChoiceFirst:i==options.Length-1?RacingSurface.Kind.ChoiceLast:RacingSurface.Kind.ChoiceMiddle);buttons.Add(b);}Update();
        }
        void RefreshParts(string route)
        {
            partsPage.EnableInClassList("hidden",route!="Parts"||cartPageOpen||enginePageOpen);
            if(route!="Parts"||cartPageOpen||enginePageOpen){CancelPartBrowserPreparation();ReleasePartCards();return;}
            var p=game.Profile;game.PrepareNativeCart();var car=p.SelectedCar;
            root.Q<Label>("partsCarName").text=car==null?"No car":VehicleCollection.DisplayName(p.catalog,car);
            partsCart.Q<Label>().text="Cart · "+game.CartCount;partsCart.SetEnabled(car!=null);
            if(partsStateKey!=game.PartsPage)partsSearch.SetValueWithoutNotify(PartsQuery.Search);
            root.Q<Label>("partsGuidance").text=game.PartsPage=="Shop"?"Choose a part, then buy for storage or choose where to install it. Review and confirm in your cart.":game.PartsPage=="Inventory"?"Your spare parts. Fit individual units directly, or add complete-part installations, moves and sales to your cart.":"Parts fitted to this car. Browse a part to inspect its condition, find replacements or plan its removal.";
            for(int i=0;i<5;i++)root.Q<Button>("partsTab"+i).EnableInClassList("selected",i==(int)partsSection);
            foreach(var b in partsPage.Q(className:"parts-sources").Children().OfType<Button>())b.EnableInClassList("selected",b.name=="partsSource:"+game.PartsPage);

            // Nonempty planned previews remain on the existing exact legacy route
            // until the separately required typed PreviewReadOnly work lands.
            if(game.CartCount>0){RefreshLegacyParts(p);return;}
            RefreshBoundedParts(p);
            partsReference.text=(partsPlanned?"Planned build":"Current build")+(partCurrentPlanError==null?"":" · "+partCurrentPlanError);
        }

        void RefreshLegacyParts(PlayerProfile p)
        {
            CancelPartBrowserPreparation();partIdleSnapshot=null;acceptedPartFacts=null;acceptedPartBinding=null;partsLegacyActive=true;partActionsEnabled=true;SetPartCardsEnabled(true);
            var car=p.SelectedCar;
            string key=p.revision+"/"+car?.instanceId+"/"+partsPlanned+"/"+JsonUtility.ToJson(SharedUnitSettings.Current)+"/"+JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()});
            if(partsProfile!=p||partsContextKey!=key){partsProfile=p;partsContextKey=key;partsStateKey=null;partsBrowsers.Clear();}
            if(partsStateKey!=game.PartsPage){partsStateKey=game.PartsPage;if(!partsBrowsers.TryGetValue(game.PartsPage,out partsBrowser)){var prices=game.PartsPage=="Inventory"?game.NativeSalePrices():(Func<string,bool,int>)game.NativeSalePrice;partsBrowser=new NativePartBrowser(p,game.CartLines,partsPlanned,game.PartsPage,game.NativeUsedPrice,prices,partPreviewCache);partsBrowsers[game.PartsPage]=partsBrowser;PartBrowserBuildCount++;}RenderLegacyPartGrid();if(partsDetailId!=null)OpenPartDetail(partsDetailId,false);}
            if(partsDetailId==null&&!ModalOpen&&PartComparisonPending){long start=Stopwatch.GetTimestamp();do{var e=partPending[partPendingIndex++];partsBrowser.Evaluate(e);UpdatePartCard(e);}while(PartComparisonPending&&(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency<3);if(!PartComparisonPending)ApplyPartQuery();else partsCount.text=partPendingIndex+" / "+partPending.Count;}
            if(partsDetailId==null&&partPendingIndex>=partPending.Count)RenderLegacyPartWindow();
            partCurrentPlanError=partsBrowser.PlanError;
        }

        void RefreshBoundedParts(PlayerProfile p)
        {
            using(RuntimeDataEncodingProbe.Measure("parts/refresh"))RefreshBoundedPartsCore(p);
        }

        void RefreshBoundedPartsCore(PlayerProfile p)
        {
            if(partBuildFailed)
            {
                if(partIdleSnapshot!=null&&partIdleSnapshot.Matches(game,game.PartsPage,partsPlanned))
                {ShowPartsPending(string.IsNullOrEmpty(partBuildFailure)?"Parts could not be refreshed. Retry when the source data changes.":"Parts could not be refreshed. "+partBuildFailure);return;}
                partBuildFailed=false;partBuildFailure=null;partIdleSnapshot=null;
            }
            // Refresh can also be called by UI actions or capture tooling before
            // LateUpdate. Keep one preparation chunk per rendered frame across
            // all callers, without delaying cheap invalidation or action fences.
            if(partIdleSnapshot!=null&&!partIdleSnapshot.Matches(game,game.PartsPage,partsPlanned))
                InvalidateAcceptedPartData("Parts context changed.");
            if(partPreparationFrame==Time.frameCount)return;
            partPreparationFrame=Time.frameCount;
            if(ModalOpen&&(partWindowAwaitingLayout!=null||partWindowBuild!=null||partWindowLayoutFallback!=null))
            {
                CancelPartQueryAndWindow();partWindowLayoutBlockedByModal=true;
                ShowPartsPending("Waiting for the current dialog…");return;
            }
            if(partWindowLayoutBlockedByModal)
            {
                if(ModalOpen){ShowPartsPending("Waiting for the current dialog…");return;}
                partWindowLayoutBlockedByModal=false;
            }
            if(partDetailWork!=null){if(partStepHeld)return;AdvancePartDetailWork();return;}
            if(partIdleSnapshot==null)
            {
                partIdleSnapshot=null;StartPartBrowserBuild(p);
                ShowPartsPending(partAwaitingCatalog?"Preparing catalog…":"Preparing parts…");return;
            }
            if(partAwaitingCatalog)
            {
                if(!game.RequestNativePartsLayoutWarmup(p.catalog)){ShowPartsPending("Preparing catalog…");return;}
                partAwaitingCatalog=false;partIdleSnapshot=null;StartPartBrowserBuild(p);
                ShowPartsPending("Preparing parts…");return;
            }
            if(activePartBuild==null)
            {
                if(partsBrowser==null||acceptedPartFacts==null||!partActionsEnabled){StartPartBrowserBuild(p);ShowPartsPending(partAwaitingCatalog?"Preparing catalog…":"Preparing parts…");return;}
                else {BeginPartQuery();return;}
            }
            if(partStepHeld){ShowPartsPending("Preparing parts…");return;}
            if(!activePartBuild.Complete)
            {
                NativePartsBaseReadInputs fresh;
                try{fresh=CapturePartInputs(p,game.PartsPage,partsPlanned);}catch(Exception error){FailPartBrowserBuild(error.Message);return;}
                int beforeCapture=activePartBuild.CaptureItems,beforePrep=activePartBuild.PreparationItems;
                bool complete=activePartBuild.Advance(fresh,512,2.0);
                partCurrentPlanError=activePartBuild.FailureReason;
                if(activePartBuild.Failed){if(activePartBuild.InputsChanged)InvalidateAcceptedPartData(activePartBuild.FailureReason);else FailPartBrowserBuild(activePartBuild.FailureReason);return;}
                MaybeHoldPartStep(activePartBuild.CaptureItems>beforeCapture||activePartBuild.PreparationItems>beforePrep||complete);
                if(partStepHeld){ShowPartsPending("Preparing parts…");return;}
                if(!complete){ShowPartsPending("Preparing parts…");return;}
                AcceptPartBrowser(activePartBuild,fresh);return;
            }
            if(partsDetailId!=null)
            {
                if((partDetailDataGeneration!=partDataGeneration||partDetailPresentationGeneration!=partPresentationGeneration)&&!ModalOpen)
                {
                    if(!TryFreshPartFence(out _,out var detailReason)){InvalidateAcceptedPartData(detailReason);return;}
                    if(partsBrowser.Entries.Any(candidate=>candidate.Id==partsDetailId)){OpenPartDetail(partsDetailId,false,true);return;}
                    partsDetailId=null;partDetailDataGeneration=-1;partDetailPresentationGeneration=-1;partDetailActionsReady=false;ShowPartsBrowsing(true);partsScroll.RemoveFromClassList("hidden");partsDetail.AddToClassList("hidden");BeginPartQuery();
                }
                return;
            }
            if(partWindowAwaitingLayout!=null){if(!partStepHeld)AdvancePartWindowLayout();return;}
            if(partWindowBuild!=null){if(!partStepHeld)AdvancePartWindow();return;}
            if(partQueryBuild!=null){AdvancePartQuery();return;}
            if(!partActionsEnabled&&partsBrowser!=null){if(!TryFreshPartFence(out _,out var reason)){InvalidateAcceptedPartData(reason);return;}BeginPartQuery();return;}
            if(partsBrowser!=null&&partLastWindowAccepted&&partPublishedPresentationGeneration==partPresentationGeneration)
            {
                EnsurePartWindowTicket();
                if(partWindowBuild!=null){if(!partStepHeld)AdvancePartWindow();return;}
                return;
            }
            if(partsBrowser!=null){if(!TryFreshPartFence(out _,out var reason)){InvalidateAcceptedPartData(reason);return;}BeginPartQuery();return;}
        }

        NativePartsBaseReadInputs CapturePartInputs(PlayerProfile p,string page,bool planned)
        {
            long start=Stopwatch.GetTimestamp();var prices=new List<NativePartsPriceFact>();
            if(page=="Shop")
            {
                foreach(var stock in p.inventory?.dealer??new List<DealerStock>())
                    if(stock!=null&&(stock.IsPart||stock.IsUnit))prices.Add(new NativePartsPriceFact("Used",stock.Id,game.NativeUsedPrice(stock)));
            }
            else if(page=="Inventory")
            {
                Func<string,bool,int> sale=null;var car=p.SelectedCar;var installedIds=new HashSet<string>((car?.installed?.slots??new List<SlotBinding>()).Where(slot=>slot!=null).Select(slot=>slot.partInstanceId),StringComparer.Ordinal);
                foreach(var part in p.parts??new List<PartInstance>())
                    if(part!=null&&!installedIds.Contains(part.instanceId)&&part.location==PartLocation.Inventory)
                        prices.Add(new NativePartsPriceFact("OwnedPart",part.instanceId,string.IsNullOrEmpty(part.instanceId)?null:(sale??(sale=game.NativeSalePrices()))(part.instanceId,false)));
                foreach(var unit in p.looseEngineElements??new List<LooseEngineElement>())
                    prices.Add(new NativePartsPriceFact("OwnedUnit",unit?.element?.id,string.IsNullOrEmpty(unit?.element?.id)?null:(sale??(sale=game.NativeSalePrices()))(unit.element.id,true)));
            }
            double elapsed=ElapsedPartMs(start);PartPriceFactoryMilliseconds+=elapsed;
            PartPriceFactoryMaximumMilliseconds=Math.Max(PartPriceFactoryMaximumMilliseconds,elapsed);PartPriceFactoryCallCount++;
            if(partPriceFactoryFrame!=Time.frameCount){partPriceFactoryFrame=Time.frameCount;partPriceFactoryFrameCalls=0;}
            PartPriceFactoryMaximumCallsPerFrame=Math.Max(PartPriceFactoryMaximumCallsPerFrame,++partPriceFactoryFrameCalls);
            return new NativePartsBaseReadInputs(p,page,planned,game.CartLines,SharedUnitSettings.Current,NativePartsOwnerContext.Capture(),prices);
        }

        void StartPartBrowserBuild(PlayerProfile p)
        {
            bool preserveRequestedHold=partStepHoldRequested;CancelPartBrowserPreparation();partStepHoldRequested=preserveRequestedHold;partBuildFailed=false;partBuildFailure=null;partsLegacyActive=false;partDataGeneration++;partActionsEnabled=false;partDetailActionsReady=false;SetPartCardsEnabled(false);SetPartDetailMutationsEnabled(false);
            partIdleSnapshot=new NativePartsIdleSnapshot(game,game.PartsPage,partsPlanned);partsProfile=p;partsStateKey=null;partLastWindowAccepted=false;
            if(!game.RequestNativePartsLayoutWarmup(p.catalog)){partAwaitingCatalog=true;ShowPartsPending("Preparing catalog…");return;}
            try{initialPartInputs=CapturePartInputs(p,game.PartsPage,partsPlanned);activePartBuild=NativePartBrowserBuild.Begin(initialPartInputs,partPreviewCache);}
            catch(Exception error){FailPartBrowserBuild(error.Message);}
            ShowPartsPending("Preparing parts…");
        }

        void AcceptPartBrowser(NativePartBrowserBuild build,NativePartsBaseReadInputs fresh)
        {
            if(build==null||!build.Complete||build.Model==null||build.CompletedReadSet==null||build.CacheBinding==null)
            {FailPartBrowserBuild("Parts model was not fully validated.");return;}
            acceptedPartFacts=build.CompletedReadSet;acceptedPartBinding=build.CacheBinding;partsBrowser=build.Model;partsBrowsers[game.PartsPage]=partsBrowser;
            PartBrowserBuildCount++;partCurrentPlanError=partsBrowser.PlanError;partsStateKey=game.PartsPage;partActionsEnabled=false;
            CancelPartQueryAndWindow();partMatches=new List<NativePartEntry>();ReleasePartCards();
        }

        void BeginPartQuery()
        {
            if(partsBrowser==null||acceptedPartFacts==null||partQueryBuild!=null)return;
            PartQueryApplyCount++;partQueryBuild=new NativePartQueryBuild(partsBrowser,PartsQuery,partsSection,game.Profile.cash,partPresentationGeneration);
            partWindowBuild=null;partActionsEnabled=false;SetPartCardsEnabled(false);ShowPartsPending("Preparing parts…");
        }

        void AdvancePartQuery()
        {
            if(partQueryBuild==null||partStepHeld)return;
            NativePartsBaseReadInputs fresh;string reason;
            if(!TryFreshPartFence(out fresh,out reason)){InvalidateAcceptedPartData(reason);return;}
            double sortBefore=partQueryBuild.SortMilliseconds;long start=Stopwatch.GetTimestamp();bool complete=partQueryBuild.Advance(512,2.0);PartQueryMilliseconds+=ElapsedPartMs(start);PartSortMilliseconds+=partQueryBuild.SortMilliseconds-sortBefore;
            if(partStepHoldRequested&&partQueryBuild.LastStepItems>0){partStepHoldRequested=false;partStepHeld=true;}
            if(!complete){ShowPartsPending("Filtering parts…");return;}
            // Advance is synchronous/read-only with respect to live owner and
            // pricing inputs. Recheck full typed facts on this same stack;
            // regenerate external prices again only after a yield/new chunk.
            if(!ValidatePartFence(fresh,out reason)){InvalidateAcceptedPartData(reason);return;}
            partSectionCount=partQueryBuild.SectionCount;partMatches=partQueryBuild.TakeResult();partQueryBuild=null;partPending.Clear();partPendingIndex=0;
            partsCount.text=partMatches.Count+" / "+partSectionCount;partsSort.Q<Label>().text=PartsQuery.Sort+(PartsQuery.Descending?" ↓":" ↑");
            partsEmpty.text=game.Profile.SelectedCar==null?"Select a car in Garage.":"No matching parts";partsEmpty.EnableInClassList("hidden",partMatches.Count>0);
            partWindowBuild=null;partLastWindowAccepted=false;EnsurePartWindowTicket();
        }

        bool TryFreshPartFence(out NativePartsBaseReadInputs fresh,out string reason)
        {
            using(RuntimeDataEncodingProbe.Measure("parts/readset-fence"))
            {
                fresh=null;reason=null;if(acceptedPartFacts==null)return false;
                try{fresh=CapturePartInputs(game.Profile,game.PartsPage,partsPlanned);return ValidatePartFence(fresh,out reason);}
                catch(Exception error){reason=error.Message;return false;}
            }
        }
        bool ValidatePartFence(NativePartsBaseReadInputs fresh,out string reason)
        {
            if(acceptedPartFacts==null){reason="Parts data is no longer accepted.";return false;}
            long start=Stopwatch.GetTimestamp();
            try{return acceptedPartFacts.MatchesCurrent(fresh,out reason);}
            catch(Exception error){reason=error.Message;return false;}
            finally{PartCurrentValidationMilliseconds+=ElapsedPartMs(start);}
        }

        void InvalidateAcceptedPartData(string reason)
        {
            if(partDetailWork!=null)CancelPartDetailWork();
            MarkPartDetailStale();
            partBuildFailed=false;partBuildFailure=null;
            partDataGeneration++;partActionsEnabled=false;partDetailActionsReady=false;SetPartCardsEnabled(false);SetPartDetailMutationsEnabled(false);
            acceptedPartFacts=null;acceptedPartBinding=null;partIdleSnapshot=null;activePartBuild=null;CancelPartQueryAndWindow();partAwaitingCatalog=false;
        }

        void FailPartBrowserBuild(string reason)
        {
            if(partDetailWork!=null)CancelPartDetailWork();
            MarkPartDetailStale();
            activePartBuild?.Cancel(reason);activePartBuild=null;initialPartInputs=null;acceptedPartFacts=null;acceptedPartBinding=null;partActionsEnabled=false;partDetailActionsReady=false;SetPartDetailMutationsEnabled(false);CancelPartQueryAndWindow();partPending.Clear();partPendingIndex=0;PartBuildFailureCount++;
            partBuildFailed=true;partBuildFailure=reason;if(partIdleSnapshot==null)partIdleSnapshot=new NativePartsIdleSnapshot(game,game.PartsPage,partsPlanned);
            SetPartCardsEnabled(false);ShowPartsPending(string.IsNullOrEmpty(reason)?"Parts could not be refreshed. Retry when the source data changes.":"Parts could not be refreshed. "+reason);
        }

        void ShowPartsPending(string message)
        {
            if(partsCount!=null)partsCount.text=message;
            // Once a card-window ticket exists, its empty-state sibling is
            // part of ScrollView geometry. Keep the finalized text/visibility
            // stable through card work, attachment, measurement and test holds.
            if(partWindowBuild!=null||partWindowAwaitingLayout!=null||partWindowLayoutFallback!=null)return;
            if(partsEmpty!=null){partsEmpty.text=message;partsEmpty.RemoveFromClassList("hidden");}
            // Keep an attached, disabled fallback window mounted while a changed
            // scroll/range is rebuilt. Hiding it would remove the scrollbar,
            // change the viewport width, then reintroduce it on the next attach.
            if(partsGrid!=null)partsGrid.EnableInClassList("hidden",partCards.Count==0&&partWindowLayoutFallback==null&&partWindowAwaitingLayout==null);
        }

        void ShowPartWindowPending(string message)
        {
            if(partsCount!=null)partsCount.text=message;
            if(partsGrid!=null)partsGrid.EnableInClassList("hidden",partCards.Count==0&&partWindowLayoutFallback==null&&partWindowAwaitingLayout==null);
        }

        void SetPartCardsEnabled(bool enabled)
        {partActionsEnabled=enabled;if(partsGrid!=null)partsGrid.SetEnabled(enabled);foreach(var button in partCards.Values)button.SetEnabled(enabled);}
        void SetPartDetailMutationsEnabled(bool enabled)
        {
            partsDetail?.Q<VisualElement>("partDetailActions")?.SetEnabled(enabled);
            partsDetail?.Q<Button>("partPosition")?.SetEnabled(enabled);
        }
        void MarkPartDetailStale()
        {
            if(partsDetail==null||partsDetail.ClassListContains("hidden"))return;
            partsDetail.Query<Button>().ForEach(button=>button.SetEnabled(false));partsDetail.Q<Button>("partBack")?.SetEnabled(true);
            if(partsDetail.Q<Label>("partDetailUpdating")==null){var updating=new Label("This detail is out of date. Updating…"){name="partDetailUpdating",enableRichText=false};updating.AddToClassList("part-description");updating.AddToClassList("car-warning");partsDetail.Insert(0,updating);}
        }

        void CancelPartQueryAndWindow()
        {
            partQueryBuild=null;
            partWindowLayoutHoldRequested=false;partWindowLayoutBlockedByModal=false;
            if(partWindowBuild!=null){partWindowGeneration++;ReleasePartSurfaces(partWindowBuild.Cards.Select(card=>card.Surface));partWindowBuild.Cards.Clear();partWindowBuild=null;}
            CancelAttachedPartWindow();
        }

        void RecordPartWindowLayoutWait()
        {
            if(partWindowLayoutAttachStarted==0)return;
            PartWindowLayoutWaitMilliseconds+=(Stopwatch.GetTimestamp()-partWindowLayoutAttachStarted)*1000.0/Stopwatch.Frequency;
            PartWindowLayoutWaitFrames+=Math.Max(0,Time.frameCount-partWindowLayoutAttachFrame);
            partWindowLayoutAttachStarted=0;partWindowLayoutAttachFrame=-1;
        }

        void CancelAttachedPartWindow()
        {
            var awaiting=partWindowAwaitingLayout;var fallback=partWindowLayoutFallback;
            if(awaiting==null&&fallback==null)return;
            RecordPartWindowLayoutWait();
            partWindowGeneration++;
            partWindowAwaitingLayout=null;partWindowLayoutFallback=null;
            partWindowLayoutHoldRequested=false;
            foreach(var ticket in new[]{awaiting,fallback})if(ticket!=null)
            {
                ReleasePartSurfaces(ticket.Cards.Select(card=>card.Surface));
                ticket.StagedGrid.RemoveFromHierarchy();ticket.StagedGrid.Clear();ticket.Cards.Clear();
            }
            partLastWindowAccepted=false;partActionsEnabled=false;
            if(partsGrid!=null)partsGrid.EnableInClassList("hidden",true);
        }

        void CancelPartBrowserPreparation()
        {
            if(partDetailWork!=null)CancelPartDetailWork();
            bool hadData=activePartBuild!=null||acceptedPartFacts!=null||partQueryBuild!=null||partWindowBuild!=null;
            if(hadData)partDataGeneration++;
            activePartBuild?.Cancel();activePartBuild=null;initialPartInputs=null;partAwaitingCatalog=false;
            acceptedPartFacts=null;acceptedPartBinding=null;partIdleSnapshot=null;
            CancelPartQueryAndWindow();partStepHeld=false;partStepHoldRequested=false;partActionsEnabled=false;partBuildFailed=false;partBuildFailure=null;
        }

        void MaybeHoldPartStep(bool advanced)
        {if(advanced&&partStepHoldRequested){partStepHoldRequested=false;partStepHeld=true;}}

        static double ElapsedPartMs(long start)=>(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
        void ApplyPartQuery(){if(partsLegacyActive){ApplyLegacyPartQuery();return;}InvalidatePartPresentation();}
        void InvalidatePartPresentation()
        {
            if(partDetailWork!=null)CancelPartDetailWork();
            partPresentationGeneration++;CancelPartQueryAndWindow();partActionsEnabled=false;partDetailActionsReady=false;SetPartDetailMutationsEnabled(false);SetPartCardsEnabled(false);
            ShowPartsPending("Preparing parts…");
        }
        void EnsurePartWindowTicket()
        {
            if(partsBrowser==null||acceptedPartFacts==null||partsDetailId!=null||partsScroll==null||partsScroll.ClassListContains("hidden"))return;
            float scrollY=partsScroll.scrollOffset.y,height=partsScroll.contentViewport.layout.height,width=partsScroll.contentViewport.layout.width;
            if(partLastWindowAccepted&&partPublishedPresentationGeneration==partPresentationGeneration&&partWindowFirst>=0&&
                Math.Abs(partAcceptedScrollY-scrollY)<.01f&&Math.Abs(partAcceptedViewportHeight-height)<.01f&&Math.Abs(partAcceptedViewportWidth-width)<.01f)return;
            if(partWindowBuild!=null&&partWindowBuild.DataGeneration==partDataGeneration&&partWindowBuild.PresentationGeneration==partPresentationGeneration&&
                Math.Abs(partWindowBuild.ScrollY-scrollY)<.01f&&Math.Abs(partWindowBuild.ViewportHeight-height)<.01f&&Math.Abs(partWindowBuild.ViewportWidth-width)<.01f)return;
            if(partWindowBuild!=null){ReleasePartSurfaces(partWindowBuild.Cards.Select(card=>card.Surface));partWindowBuild.Cards.Clear();partWindowBuild=null;}
            var window=NativeCardWindow.Calculate(partMatches.Count,scrollY,height,partRowHeight);
            partWindowBuild=new NativePartWindowBuild(partDataGeneration,partPresentationGeneration,++partWindowGeneration,window,scrollY,height,width);
            partActionsEnabled=false;SetPartCardsEnabled(false);ShowPartWindowPending("Preparing cards…");
        }
        void AdvancePartWindow()
        {
            var ticket=partWindowBuild;if(ticket==null||ticket.Complete||partStepHeld)return;
            float scrollY=partsScroll.scrollOffset.y,height=partsScroll.contentViewport.layout.height,width=partsScroll.contentViewport.layout.width;
            if(ticket.DataGeneration!=partDataGeneration||ticket.PresentationGeneration!=partPresentationGeneration||ticket.WindowGeneration!=partWindowGeneration)
            {partWindowGeneration++;partLastWindowAccepted=false;partActionsEnabled=false;SetPartCardsEnabled(false);ReleasePartSurfaces(ticket.Cards.Select(card=>card.Surface));ticket.Cards.Clear();partWindowBuild=null;return;}
            if(Math.Abs(ticket.ScrollY-scrollY)>=.01f||Math.Abs(ticket.ViewportHeight-height)>=.01f||Math.Abs(ticket.ViewportWidth-width)>=.01f)
            {
                if(!TryFreshPartFence(out _,out var changedReason)){InvalidateAcceptedPartData(changedReason);return;}
                partWindowGeneration++;partLastWindowAccepted=false;partActionsEnabled=false;SetPartCardsEnabled(false);
                ReleasePartSurfaces(ticket.Cards.Select(card=>card.Surface));ticket.Cards.Clear();partWindowBuild=null;
                EnsurePartWindowTicket();return;
            }
            NativePartsBaseReadInputs fresh;string reason;if(!TryFreshPartFence(out fresh,out reason)){InvalidateAcceptedPartData(reason);return;}
            if(ticket.CardsComplete){PublishPartWindow(ticket,fresh);return;}
            int index=ticket.First+ticket.Cards.Count;var entry=partMatches[index];long start=Stopwatch.GetTimestamp();var card=CreateStagedPartCard(entry,ticket.DataGeneration,ticket.PresentationGeneration);double elapsed=ElapsedPartMs(start);
            ticket.Add(card,elapsed);PartWindowCreateMilliseconds+=elapsed;
            if(partStepHoldRequested&&ticket.LastStepItems>0){partStepHoldRequested=false;partStepHeld=true;}
        }
        NativePartWindowBuild.Card CreateStagedPartCard(NativePartEntry entry,int dataGeneration,int presentationGeneration)
        {
            var id=entry.Id;var source=entry.Source;int windowGeneration=partWindowBuild.WindowGeneration;Action click=()=>OpenPartDetailFromCard(id,source,dataGeneration,presentationGeneration,windowGeneration);var button=new Button(click){name="partCard:"+id,focusable=false,tabIndex=-1,tooltip=entry.Definition.name};
            button.AddToClassList("owned-car-card");button.AddToClassList("part-card");button.AddToClassList("clickable-panel");
            var surface=CreateStagedPartSurface(button,RacingSurface.Kind.Panel);
            var title=new Label(entry.DisplayName??entry.Definition.name){enableRichText=false,pickingMode=PickingMode.Ignore};title.AddToClassList("car-card-title");button.Add(title);
            var labels=new Label[8];var specifications=new VisualElement();specifications.AddToClassList("part-card-specifications");
            for(int i=0;i<labels.Length;i++){labels[i]=new Label{enableRichText=false,pickingMode=PickingMode.Ignore};labels[i].AddToClassList(i==0?"car-condition":"part-card-fact");if(i==1)button.Add(specifications);if(i>=1&&i<=4)specifications.Add(labels[i]);else button.Add(labels[i]);}
            UpdatePartCardLabels(entry,labels);
            return new NativePartWindowBuild.Card{Entry=entry,Button=button,Labels=labels,Surface=surface,Click=click};
        }
        void PublishPartWindow(NativePartWindowBuild ticket,NativePartsBaseReadInputs fresh)
        {
            // Only AdvancePartWindow calls this immediately after its fresh
            // price/owner fence, without yielding or invoking application code.
            if(!ValidatePartFence(fresh,out var reason)){InvalidateAcceptedPartData(reason);return;}
            if(ticket.DataGeneration!=partDataGeneration||ticket.PresentationGeneration!=partPresentationGeneration){InvalidatePartPresentation();return;}
            long start=Stopwatch.GetTimestamp();var oldGrid=partsGrid;
            if(partWindowLayoutFallback!=null)
            {
                ReleasePartSurfaces(partWindowLayoutFallback.Cards.Select(card=>card.Surface));
                partWindowLayoutFallback.StagedGrid.Clear();partWindowLayoutFallback.Cards.Clear();partWindowLayoutFallback=null;
            }
            ReleasePartCards();oldGrid.RemoveFromHierarchy();partsGrid=ticket.StagedGrid;partsBefore=ticket.BeforeElement;partsAfter=ticket.AfterElement;partsScroll.Insert(0,partsGrid);
            // The attached tree can change the ScrollView's client width (for
            // example when its scrollbar appears). Keep it disabled and keep
            // its surfaces/callbacks private until a later rendered-step fence
            // confirms the final layout and exact card range.
            partsGrid.SetEnabled(false);foreach(var card in ticket.Cards)card.Button.SetEnabled(false);
            partWindowLayoutFallback=null;partWindowAwaitingLayout=ticket;partWindowLayoutAttachFrame=Time.frameCount;partWindowLayoutAttachStarted=Stopwatch.GetTimestamp();
            partWindowLayoutValidationAttempts=0;
            partWindowFirst=partWindowLast=-1;partLastWindowAccepted=false;partActionsEnabled=false;
            ticket.MarkPublished();partWindowBuild=null;
            PartWindowAttachMilliseconds+=ElapsedPartMs(start);partsGrid.EnableInClassList("hidden",false);
            partsCount.text="Preparing cards…";
            if(partWindowLayoutHoldRequested){partWindowLayoutHoldRequested=false;partStepHeld=true;}
            else if(partStepHoldRequested){partStepHoldRequested=false;partStepHeld=true;}
        }

        static bool FinitePartLayout(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        bool AttachedPartWindowHasFiniteGeometry(NativePartWindowBuild ticket,float viewportWidth,float viewportHeight,float scrollY,float contentHeight)
        {
            if(!FinitePartLayout(viewportWidth)||!FinitePartLayout(viewportHeight)||viewportWidth<=0||viewportHeight<=0||
                !FinitePartLayout(scrollY)||scrollY<-.5f||!FinitePartLayout(contentHeight)||contentHeight<0||
                scrollY>Mathf.Max(0,contentHeight-viewportHeight)+1f||partsScroll==null||partsScroll.panel==null||
                ticket.StagedGrid.panel!=partsScroll.panel)return false;
            var viewportBounds=partsScroll.contentViewport.worldBound;
            var gridBounds=ticket.StagedGrid.worldBound;
            if(!FinitePartLayout(viewportBounds.x)||!FinitePartLayout(viewportBounds.y)||!FinitePartLayout(viewportBounds.width)||
                !FinitePartLayout(viewportBounds.height)||viewportBounds.width<=0||viewportBounds.height<=0||
                !FinitePartLayout(gridBounds.x)||!FinitePartLayout(gridBounds.y)||!FinitePartLayout(gridBounds.width)||
                !FinitePartLayout(gridBounds.height)||gridBounds.width<=0||gridBounds.height<0)return false;
            if(ticket.Cards.Count!=ticket.Last-ticket.First)return false;
            for(int i=0;i<ticket.Cards.Count;i++)
            {
                var card=ticket.Cards[i];int matchIndex=ticket.First+i;
                if(card?.Entry==null||card.Button==null||card.Surface==null||matchIndex<0||matchIndex>=partMatches.Count||
                    !ReferenceEquals(card.Entry,partMatches[matchIndex])||!ReferenceEquals(card.Button.parent,ticket.StagedGrid)||
                    !ReferenceEquals(card.Button.panel,partsScroll.panel))return false;
                var bounds=card.Button.worldBound;
                if(!FinitePartLayout(bounds.x)||!FinitePartLayout(bounds.y)||!FinitePartLayout(bounds.width)||
                    !FinitePartLayout(bounds.height)||bounds.width<=0||bounds.height<=0)return false;
            }
            return true;
        }

        void AdvancePartWindowLayout()
        {
            var ticket=partWindowAwaitingLayout;if(ticket==null)return;
            if(Time.frameCount<=partWindowLayoutAttachFrame)return;
            if(ModalOpen)
            {
                CancelPartQueryAndWindow();partWindowLayoutBlockedByModal=true;
                ShowPartsPending("Waiting for the current dialog…");return;
            }
            bool contextCurrent=ticket.DataGeneration==partDataGeneration&&ticket.PresentationGeneration==partPresentationGeneration&&
                ticket.WindowGeneration==partWindowGeneration&&ReferenceEquals(partsGrid,ticket.StagedGrid)&&partsBrowser!=null&&
                acceptedPartFacts!=null&&!partsLegacyActive&&partsDetailId==null&&game!=null&&game.ScreenName=="Parts"&&
                Visible&&InterfaceComparison.IsReplacement("Parts")&&game.CartCount==0&&!cartPageOpen&&!enginePageOpen&&
                partsPage!=null&&!partsPage.ClassListContains("hidden")&&partsScroll!=null&&!partsScroll.ClassListContains("hidden");
            if(!contextCurrent){CancelPartQueryAndWindow();return;}
            NativePartsBaseReadInputs fresh;string reason;
            if(!TryFreshPartFence(out fresh,out reason)){InvalidateAcceptedPartData(reason);return;}
            if(ticket.DataGeneration!=partDataGeneration||ticket.PresentationGeneration!=partPresentationGeneration||
                ticket.WindowGeneration!=partWindowGeneration||!ReferenceEquals(partsGrid,ticket.StagedGrid))
            {CancelPartQueryAndWindow();return;}

            float scrollY=partsScroll.scrollOffset.y;
            float viewportWidth=partsScroll.contentViewport.layout.width,viewportHeight=partsScroll.contentViewport.layout.height;
            float contentHeight=partsScroll.contentContainer.layout.height;
            if(!AttachedPartWindowHasFiniteGeometry(ticket,viewportWidth,viewportHeight,scrollY,contentHeight))
            {
                if(++partWindowLayoutValidationAttempts>=MaxPartWindowLayoutValidationAttempts)
                    FailPartBrowserBuild("Parts card layout did not settle after "+MaxPartWindowLayoutValidationAttempts+" rendered checks.");
                return;
            }
            var expected=NativeCardWindow.Calculate(partMatches.Count,scrollY,viewportHeight,partRowHeight);
            if(Math.Abs(ticket.ScrollY-scrollY)>=.01f||expected.First!=ticket.First||expected.Last!=ticket.Last)
            {
                // Scroll or range intent superseded this ticket. Keep the
                // disabled attached tree visible as a geometry fallback while
                // the replacement window is built, avoiding scrollbar churn.
                RecordPartWindowLayoutWait();partWindowAwaitingLayout=null;partWindowLayoutFallback=ticket;partLastWindowAccepted=false;partActionsEnabled=false;
                partWindowBuild=new NativePartWindowBuild(partDataGeneration,partPresentationGeneration,++partWindowGeneration,
                    expected,scrollY,viewportHeight,viewportWidth);
                ShowPartWindowPending("Preparing cards…");return;
            }

            foreach(var card in ticket.Cards)
            {
                partCards[card.Entry.Id]=card.Button;partCardActions[card.Entry.Id]=card.Click;
                partCardLabels[card.Entry.Id]=card.Labels;partGridSurfaces.Add(card.Surface);PartCardBuildCount++;
            }
            RegisterStagedPartSurfaces(ticket.Cards.Select(card=>card.Surface));
            partWindowFirst=ticket.First;partWindowLast=ticket.Last;partAcceptedScrollY=scrollY;
            partAcceptedViewportHeight=viewportHeight;partAcceptedViewportWidth=viewportWidth;
            partPublishedPresentationGeneration=ticket.PresentationGeneration;partLastWindowAccepted=true;
            partWindowAwaitingLayout=null;RecordPartWindowLayoutWait();
            partsCount.text=partMatches.Count+" / "+partSectionCount;
            partsGrid.EnableInClassList("hidden",false);SetPartCardsEnabled(true);ExcludeTabTraversal();
        }
        void OpenPartDetailFromCard(string id,string source,int dataGeneration,int presentationGeneration,int windowGeneration)
        {
            using(FramePacingProbe.Measure(10))
            using(RuntimeDataEncodingProbe.Measure("parts/card-open"))
            {
                var origin=new PartActionOrigin{EntryId=id,Source=source,DataGeneration=dataGeneration,PresentationGeneration=presentationGeneration,WindowGeneration=windowGeneration};
                if(!TryValidatePartAction(origin,out _,out var reason,true)){PartActionGuardRejectedCount++;RejectPartAction(reason);return;}
                OpenPartDetail(id,true,true);
            }
        }
        bool TryValidatePartAction(PartActionOrigin origin,out NativePartEntry entry,out string reason,bool requireCard=false)
        {
            using(RuntimeDataEncodingProbe.Measure("parts/action-validation"))return TryValidatePartActionCore(origin,out entry,out reason,requireCard);
        }
        bool TryValidatePartActionCore(PartActionOrigin origin,out NativePartEntry entry,out string reason,bool requireCard)
        {
            entry=null;reason=null;
            bool detailOrigin=origin?.DetailId!=null;
            bool detailContext=detailOrigin&&partsDetailId==origin.DetailId&&origin.DetailGeneration==partDetailGeneration&&partDetailActionsReady&&partDetailDataGeneration==partDataGeneration&&partDetailPresentationGeneration==partPresentationGeneration&&origin.DetailDataGeneration==partDetailDataGeneration&&origin.DetailPresentationGeneration==partDetailPresentationGeneration;
            bool listVisibleDuringDetailPreparation=partDetailWork!=null&&partsScroll!=null&&!partsScroll.ClassListContains("hidden");
            if(origin==null||partsLegacyActive||(detailOrigin&&!detailContext)||(!detailContext&&(!partActionsEnabled||partsDetailId!=null&&!listVisibleDuringDetailPreparation))||acceptedPartFacts==null||partsBrowser==null||
                origin.DataGeneration!=partDataGeneration||origin.PresentationGeneration!=partPresentationGeneration||(!detailOrigin&&origin.WindowGeneration!=partWindowGeneration)||
                game==null||game.ScreenName!="Parts"||!Visible||!InterfaceComparison.IsReplacement("Parts")||game.CartCount!=0||cartPageOpen||enginePageOpen||
                partsPage==null||partsPage.ClassListContains("hidden")||acceptedPartFacts.CapturedPage!=game.PartsPage||(!detailOrigin&&(!partLastWindowAccepted||partPublishedPresentationGeneration!=partPresentationGeneration)))
            {reason="This parts view has changed. Refresh it before continuing.";return false;}
            if(requireCard&&(detailContext||!partCards.ContainsKey(origin.EntryId))){reason="This card is no longer in the current window.";return false;}
            if(!detailOrigin&&(partsScroll==null||Math.Abs(partsScroll.scrollOffset.y-partAcceptedScrollY)>=.01f))
            {reason="The parts window has changed. Refresh it before continuing.";return false;}
            if(!detailOrigin)
            {
                float height=partsScroll.contentViewport.layout.height,width=partsScroll.contentViewport.layout.width;
                if(Math.Abs(height-partAcceptedViewportHeight)>=.01f||Math.Abs(width-partAcceptedViewportWidth)>=.01f)
                {reason="The parts window has changed. Refresh it before continuing.";return false;}
            }
            if(!TryFreshPartFence(out _,out reason))return false;
            if(origin.EntryId==null)return true;
            entry=partsBrowser.Entries.FirstOrDefault(candidate=>candidate.Id==origin.EntryId);
            if(entry==null||entry.Source!=origin.Source){entry=null;reason="This asset is no longer available in the current parts view.";return false;}
            return true;
        }
        PartActionOrigin PartOrigin(NativePartEntry entry)=>new PartActionOrigin{EntryId=entry.Id,Source=entry.Source,DataGeneration=partDataGeneration,PresentationGeneration=partPresentationGeneration,WindowGeneration=partWindowGeneration,DetailId=partsDetailId,DetailGeneration=partDetailGeneration,DetailDataGeneration=partDetailDataGeneration,DetailPresentationGeneration=partDetailPresentationGeneration};
        PartActionOrigin PartContextOrigin()=>new PartActionOrigin{DataGeneration=partDataGeneration,PresentationGeneration=partPresentationGeneration,WindowGeneration=partWindowGeneration,DetailId=partsDetailId,DetailGeneration=partDetailGeneration,DetailDataGeneration=partDetailDataGeneration,DetailPresentationGeneration=partDetailPresentationGeneration};
        Action GuardPartAction(NativePartEntry entry,Action action)
        {
            var origin=PartOrigin(entry);
            return ()=>{using(RuntimeDataEncodingProbe.Measure("parts/action-callback")){if(!TryValidatePartAction(origin,out _,out var reason)){PartActionGuardRejectedCount++;RejectPartAction(reason);return;}action?.Invoke();}};
        }
        void RejectPartAction(string reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PartActionLastRejectedReasonForChecks=reason;
#endif
            if(acceptedPartFacts!=null)InvalidateAcceptedPartData(reason);else{partActionsEnabled=false;SetPartCardsEnabled(false);ShowPartsPending("Parts changed. Updating…");}
        }
        void RenderLegacyPartGrid()
        {
            if(partsBrowser==null)return;ReleasePartCards();partsSearch.SetValueWithoutNotify(PartsQuery.Search);
            ShowPartsBrowsing(partsDetailId==null);partsScroll.EnableInClassList("hidden",partsDetailId!=null);partsDetail.EnableInClassList("hidden",partsDetailId==null);ApplyLegacyPartQuery();ExcludeTabTraversal();
        }
        void CreatePartCard(NativePartEntry e)
        {
            string id=e.Id;var card=new Button(()=>OpenPartDetail(id)){name="partCard:"+id,focusable=false,tabIndex=-1,tooltip=e.Definition.name};card.AddToClassList("owned-car-card");card.AddToClassList("part-card");card.AddToClassList("clickable-panel");partsGrid.Add(card);AddSurface(card,RacingSurface.Kind.Panel);partGridSurfaces.Add(card.Q<RacingSurface>());partCards[id]=card;PartCardBuildCount++;
            var title=new Label(e.DisplayName??e.Definition.name){enableRichText=false,pickingMode=PickingMode.Ignore};title.AddToClassList("car-card-title");card.Add(title);var labels=new Label[8];var specifications=new VisualElement();specifications.AddToClassList("part-card-specifications");for(int i=0;i<labels.Length;i++){labels[i]=new Label{enableRichText=false,pickingMode=PickingMode.Ignore};labels[i].AddToClassList(i==0?"car-condition":"part-card-fact");if(i==1)card.Add(specifications);if(i>=1&&i<=4)specifications.Add(labels[i]);else card.Add(labels[i]);}partCardLabels[id]=labels;UpdatePartCard(e);
        }
        void ReleasePartCards()
        {
            if(partsGrid==null)return;
            foreach(var surface in partGridSurfaces)surfaces.Remove(surface);
            partGridSurfaces.Clear();partsGrid.Clear();partCardLabels.Clear();partCards.Clear();partCardActions.Clear();partWindowFirst=partWindowLast=-1;
        }
        void RenderLegacyPartWindow(bool force=false)
        {
            if(renderingPartWindow||partsGrid==null||partMatches==null||!Visible||partsPage.ClassListContains("hidden")||partsScroll.ClassListContains("hidden"))return;
            var window=NativeCardWindow.Calculate(partMatches.Count,partsScroll.scrollOffset.y,partsScroll.contentViewport.layout.height,partRowHeight);
            if(!force&&partWindowFirst==window.First&&partWindowLast==window.Last)return;
            renderingPartWindow=true;
            try{
                var wanted=new HashSet<string>();for(int i=window.First;i<window.Last;i++)wanted.Add(partMatches[i].Id);
                foreach(string id in partCards.Keys.Where(id=>!wanted.Contains(id)).ToArray()){
                    var surface=partCards[id].Q<RacingSurface>();surfaces.Remove(surface);partGridSurfaces.Remove(surface);partCards.Remove(id);partCardLabels.Remove(id);
                }
                partsGrid.Clear();partsBefore.style.height=window.Before;partsAfter.style.height=window.After;
                if(partMatches.Count>0){
                    partsGrid.Add(partsBefore);
                    for(int i=window.First;i<window.Last;i++){var entry=partMatches[i];if(partCards.TryGetValue(entry.Id,out var card)){partsGrid.Add(card);UpdatePartCard(entry);}else CreatePartCard(entry);}
                    partsGrid.Add(partsAfter);
                }
                partWindowFirst=window.First;partWindowLast=window.Last;
                ExcludeTabTraversal();
            }finally{renderingPartWindow=false;}
        }
        internal Button ScrollToPart(string id)
        {
            int index=partMatches.FindIndex(e=>e.Id==id);if(index<0)return null;
            partsScroll.scrollOffset=new Vector2(0,index/2*partRowHeight);if(game.CartCount==0)InvalidatePartPresentation();else RenderLegacyPartWindow(true);
            return partCards.TryGetValue(id,out var card)?card:null;
        }
        string PartDelta(NativePartEntry e,int metric)
        {if(!e.Change[metric].HasValue)return e.Ready?"—":"…";double value=e.Change[metric].Value;if(metric==1||metric==4)value=DisplayUnits.Torque(value,SharedUnitSettings.Current.torque);return (value>0?"+":"")+value.ToString(metric==3?"0.#":"0.##")+" "+(metric==0?"HP":metric==1||metric==4?SharedUnitSettings.TorqueLabel:metric==2?"kg":metric==3?"pp":metric==5?"RPM":"W/°C");}
        PartStat[] PartSpecs(NativePartEntry e)
        {
            if(WheelFitments.HasOverrides(e.Part)){var wheels=Enumerable.Range(0,4).Select(i=>game.Profile.catalog.Find(WheelFitments.DefinitionId(e.Part,i))).ToArray();if(wheels.Select(d=>d.id).Distinct().Count()==1)return partSpecCache.Get(wheels[0]);return new[]{new PartStat("mass","Rim set mass",wheels.Sum(d=>d.massKg*.25),"kg",-1,"Actual four fitted rim specifications, before size scaling."),new PartStat("frontmass","Front rim / reference",(wheels[0].massKg+wheels[1].massKg)*.125,"kg",-1,"Mean front rim mass before size scaling."),new PartStat("rearmass","Rear rim / reference",(wheels[2].massKg+wheels[3].massKg)*.125,"kg",-1,"Mean rear rim mass before size scaling."),new PartStat("massscale","Mean rim mass multiplier",wheels.Average(d=>d.massScale),"×",-1,"Constituent mass multipliers; axle stats show actual sized inertia.")};}
            if(!TireFitments.HasOverrides(e.Part))return partSpecCache.Get(e.Definition);
            var defs=Enumerable.Range(0,4).Select(i=>game.Profile.catalog.Find(TireFitments.DefinitionId(e.Part,i))).ToArray();
            if(defs.Select(d=>d.id).Distinct().Count()==1)return partSpecCache.Get(defs[0]);
            return new[]{new PartStat("grip","Lowest grip factor",defs.Min(d=>d.grip),"×",1,"Lowest authored compound grip; inspect the actual corners below."),new PartStat("rollinglife","Shortest reference life",defs.Min(d=>d.tireLifeDistanceM/1000),"km",1,"Shortest constituent reference lifespan, not remaining distance."),new PartStat("slidinglife","Lowest slip-life budget",defs.Min(d=>d.wearEnergyJ/1e6),"MJ",1,"Lowest constituent sliding-work budget."),new PartStat("tireoptimal","Highest grip target",defs.Max(d=>d.tireOptimalC),"°C",0,"Hottest preferred temperature among the constituent tires.")};
        }
        void UpdatePartCard(NativePartEntry e)
        {
            if(!partCardLabels.TryGetValue(e.Id,out var labels))return;
            UpdatePartCardLabels(e,labels);
        }
        void UpdatePartCardLabels(NativePartEntry e,Label[] labels)
        {
            labels[0].text=e.Compatibility;
            var specs=PartSpecs(e);for(int i=0;i<4;i++){labels[i+1].text=i<specs.Length?PartStatText(specs[i]):"";labels[i+1].tooltip=i<specs.Length?specs[i].explanation:"";}
            double health=e.Unit?.element.health??(e.Part!=null&&e.Definition.category==PartCategory.Tires?TireDynamics.LowestHealth(e.Part.condition):e.Part?.condition.health)??1;bool failed=e.Unit!=null?e.Unit.element.Failed:e.Part!=null&&e.Definition.category==PartCategory.Tires?TireDynamics.AnyFailed(e.Part.condition):e.Part?.condition.failed??false;
            labels[5].tooltip=string.Join("\n",e.BrandNames.Select(b=>b+": "+PartBrands.Description(b)));
            labels[5].text=e.Source+" · "+string.Join(" / ",e.BrandNames.Select(PartBrands.Focus).Distinct())+" · "+PartSemantics.Friendly(e.Definition.category.ToString())+(PartSemantics.Wears(e.Definition.category)?" · "+(failed?"FAILED":health.ToString("P0")):"");labels[5].EnableInClassList("car-critical",failed);
            labels[6].text=(e.Price.HasValue?DisplayUnits.Money(e.Price.Value):"—")+(e.Definition.category==PartCategory.Tires||e.Definition.category==PartCategory.Wheels?e.Unit==null?" / set of 4":e.Definition.category==PartCategory.Wheels?" / wheel":" / tire":"")+" · "+(e.Definition.tuning!=PartTuningOptions.None||e.Definition.camTimingAdjustable?"Adjustable":"Fixed");
            labels[7].text=e.RequiresMatchingSet?"Matching-set preview":e.Source=="New"?e.StockUnits+" units in stock"+(e.RequiresExpress?" · Express":""):e.Unit!=null&&e.Change[3].HasValue?"Replacement condition "+PartDelta(e,3):"";

        }
        void ApplyLegacyPartQuery()
        {
            if(partsBrowser==null)return;PartQueryApplyCount++;
            var list=partsBrowser.Entries.Where(e=>NativePartBrowser.Section(e.Definition.category)==partsSection).ToList();
            partPending=PartsQuery.RequiresChanges?list.Where(e=>!e.Ready&&PartsQuery.Matches(e,game.Profile.cash,false)&&!partsBrowser.Restore(e)).ToList():new List<NativePartEntry>();partPendingIndex=0;
            partsGrid.EnableInClassList("hidden",PartComparisonPending);
            if(PartComparisonPending){partsCount.text="… / "+list.Count;partsEmpty.text="Calculating comparison…";partsEmpty.RemoveFromClassList("hidden");return;}
            var matching=list.Where(e=>PartsQuery.Matches(e,game.Profile.cash)).ToList();matching.Sort(PartsQuery.Compare);
            partMatches=matching;RenderLegacyPartWindow(true);
            partsCount.text=matching.Count+" / "+list.Count;partsEmpty.text=game.Profile.SelectedCar==null?"Select a car in Garage.":"No matching parts";partsEmpty.EnableInClassList("hidden",matching.Count>0);partsSort.Q<Label>().text=PartsQuery.Sort+(PartsQuery.Descending?" ↓":" ↑");

        }
        void ClearPartDetails(){foreach(var s in partDetailSurfaces)surfaces.Remove(s);partDetailSurfaces.Clear();foreach(var child in partsDetail.Children().Where(c=>c!=partsDetailScroll).ToArray())child.RemoveFromHierarchy();partsDetailScroll.Clear();}
        void DecoratePartDetail(VisualElement element,RacingSurface.Kind kind){AddPartDetailSurface(element,kind);}
        string PartStatText(PartStat stat)=>stat.label+": "+(stat.unit=="Nm"?SharedUnitSettings.Torque(stat.value,"0.##"):stat.unit=="bar"?SharedUnitSettings.Pressure(stat.value):stat.value.ToString(stat.unit=="km"?"N0":"0.##")+" "+stat.unit);
        string PartDescription(PartDefinition definition){string text=definition.description??"";int supplier=text.IndexOf(" Supplier: ",StringComparison.Ordinal);return SharedUnitSettings.TorqueText(supplier<0?text:text.Substring(0,supplier));}
        void ShowPartsBrowsing(bool show){root.Q("partsToolbar").EnableInClassList("hidden",!show);root.Q("partsTabs").EnableInClassList("hidden",!show);partsPage.Q(className:"parts-sources").EnableInClassList("hidden",!show);root.Q("partsGuidance").EnableInClassList("hidden",!show);partsReference.EnableInClassList("hidden",!show);}
        void BackToParts(){if(partDetailWork!=null)CancelPartDetailWork();partsDetailId=null;partDetailPurchaseAction=null;partDetailActionsReady=false;partDetailDataGeneration=-1;partDetailPresentationGeneration=-1;ShowPartsBrowsing(true);partsScroll.RemoveFromClassList("hidden");partsDetail.AddToClassList("hidden");partsScroll.scrollOffset=partsReturnScroll;ApplyPartQuery();}
        void ChoosePartPosition(NativePartEntry entry,PartActionOrigin origin=null)
        {
            if(origin!=null&&!TryValidatePartAction(origin,out _,out var initialReason)){RejectPartAction(initialReason);return;}
            bool Current(){if(origin==null)return true;if(TryValidatePartAction(origin,out _,out var reason))return true;RejectPartAction(reason);return false;}
            var positions=partsBrowser.Positions(entry);string selected=entry.Position;
            OpenDialog("Replacement position",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Apply",()=>{if(!Current())return;entry.Position=selected;entry.Ready=false;entry.Proposed=null;entry.Change=new double?[7];partsBrowser.Evaluate(entry);UpdatePartCard(entry);CloseDialog();OpenPartDetail(entry.Id,false);})},populate:content=>{
                var rows=new List<Tuple<Button,string>>();
                foreach(string position in positions){string target=position;var button=ReplacementPositionRow(content,entry.Definition,target,"partUnitPosition:"+partsBrowser.PositionLabel(entry,target),()=>{if(!Current())return;selected=target;foreach(var pair in rows)pair.Item1.EnableInClassList("selected",pair.Item2==selected);});button.EnableInClassList("selected",selected==target);rows.Add(Tuple.Create(button,target));}
            });
        }
        void PartDetailFact(VisualElement parent,string label,string value,string state=null)
        {
            var row=new VisualElement();row.AddToClassList("car-fact");parent.Add(row);
            var caption=new Label(label){enableRichText=false};caption.AddToClassList("car-fact-label");row.Add(caption);
            var text=new Label(value){enableRichText=false};text.AddToClassList("car-fact-value");if(state!=null)text.AddToClassList(state);row.Add(text);
        }
        void OpenPartDetail(string id,bool remember=true,bool alreadyFenced=false)
        {
            if(partsLegacyActive){OpenLegacyPartDetail(id,remember,alreadyFenced);return;}
            if(partsBrowser==null)return;
            if(partDetailWork!=null)CancelPartDetailWork();
            if(!alreadyFenced&&!TryFreshPartFence(out _,out var reason)){RejectPartAction(reason);return;}
            var entry=partsBrowser.Entries.Find(p=>p.Id==id);
            if(entry==null){partsDetailId=null;partDetailActionsReady=false;partDetailDataGeneration=-1;partDetailPresentationGeneration=-1;ApplyPartQuery();return;}
            var offset=remember?Vector2.zero:partsDetailScroll.scrollOffset;
            if(remember)partsReturnScroll=partsScroll.scrollOffset;
            int generation=checked(++partDetailGeneration);
            bool preserveVisibleDetail=partsDetail!=null&&!partsDetail.ClassListContains("hidden");
            partsDetailId=id;partDetailActionsReady=false;partDetailPurchaseAction=null;
            partDetailDataGeneration=partDataGeneration;partDetailPresentationGeneration=partPresentationGeneration;
            SetPartDetailMutationsEnabled(false);
            if(preserveVisibleDetail)MarkPartDetailStale();
            else{partsScroll.RemoveFromClassList("hidden");ShowPartsBrowsing(true);}
            var root=new VisualElement{name="partsDetailPending"};root.AddToClassList("car-details-page");root.style.position=Position.Absolute;root.style.left=0;root.style.right=0;root.style.top=0;root.style.bottom=0;var scroll=new ScrollView{name="partsDetailScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("car-details-scroll");root.Add(scroll);
            ThemeScrollView(scroll);
            partDetailWork=new PartDetailWork{Id=id,Source=entry.Source,Page=game.PartsPage,Entry=entry,Offer=entry,Browser=partsBrowser,DataGeneration=partDataGeneration,PresentationGeneration=partPresentationGeneration,WindowGeneration=partWindowGeneration,DetailGeneration=generation,Remember=remember,PreserveExisting=preserveVisibleDetail,Offset=offset,ViewportWidth=partsScroll.contentViewport.layout.width,ViewportHeight=partsScroll.contentViewport.layout.height,Root=root,Scroll=scroll,Started=Stopwatch.GetTimestamp()};
        }
        void OpenLegacyPartDetail(string id,bool remember=true,bool alreadyFenced=false)
        {
            long detailStart=Stopwatch.GetTimestamp();
            partDetailPurchaseAction=null;
            if(partsBrowser==null)return;
            if(!partsLegacyActive&&!alreadyFenced&&!TryFreshPartFence(out _,out var reason)){RejectPartAction(reason);return;}
            var e=partsBrowser.Entries.Find(p=>p.Id==id);if(e==null){partsDetailId=null;partDetailActionsReady=false;partDetailDataGeneration=-1;partDetailPresentationGeneration=-1;ApplyPartQuery();return;}
            long evaluationStart=Stopwatch.GetTimestamp();partsBrowser.Evaluate(e);PartDetailEvaluationMilliseconds+=ElapsedPartMs(evaluationStart);
            long factsStart=Stopwatch.GetTimestamp();var offer=e;if(partsLegacyActive)e=PurchasePreview(offer);PartActionOrigin actionOrigin=null;Action Guarded(Action action)=>actionOrigin==null?action:GuardPartAction(offer,action);if(remember)partsReturnScroll=partsScroll.scrollOffset;
            var offset=remember?Vector2.zero:partsDetailScroll.scrollOffset;
            partDetailGeneration=checked(partDetailGeneration+1);partsDetailId=id;partDetailActionsReady=false;partDetailDataGeneration=partDataGeneration;partDetailPresentationGeneration=partPresentationGeneration;actionOrigin=partsLegacyActive?null:PartOrigin(offer);ShowPartsBrowsing(false);ClearPartDetails();partsScroll.AddToClassList("hidden");partsDetail.RemoveFromClassList("hidden");
            var heading=new VisualElement{name="partDetailHeading"};heading.AddToClassList("part-detail-heading");partsDetail.Insert(0,heading);
            var back=PartButton(heading,"partBack","",BackToParts,detail:true);back.AddToClassList("car-back-button");back.Add(new FilledMenuIcon("Back"));back.tooltip="Back to parts";
            var title=new Label(e.DisplayName??e.Definition.name){name="partDetailTitle",enableRichText=false};title.AddToClassList("car-detail-title");title.AddToClassList("part-detail-title");heading.Add(title);
            var info=PartButton(heading,"partInfo","i",Guarded(()=>OpenDialog(e.Definition.name,SharedUnitSettings.TorqueText(PartBrands.Description(e.Definition.brand)+"\n"+e.Definition.description+"\n"+PartTuning.Summary(e.Definition)+"\n"+e.Reason+"\n"+string.Join("\n",PartSemantics.Stats(e.Definition).Select(PartStatText))),new[]{new DialogAction("Close",CloseDialog)})),detail:true);info.AddToClassList("part-info-button");info.tooltip="All part specifications";
            var top=new VisualElement{name="partDetailTop"};top.AddToClassList("part-detail-top");partsDetailScroll.Add(top);
            var facts=new VisualElement{name="partDetailFacts"};facts.AddToClassList("part-detail-facts");top.Add(facts);top.EnableInClassList("part-preview-unavailable",e.Proposed==null);DecoratePartDetail(facts,RacingSurface.Kind.Panel);
            PartDetailFact(facts,"Compatibility",e.Compatibility,e.Compatibility=="Compatible"?"car-ready":"car-warning");
            PartDetailFact(facts,"Brand",e.Definition.brand+" · "+PartBrands.Focus(e.Definition.brand));
            PartDetailFact(facts,"Specification",e.Definition.quality);
            PartDetailFact(facts,"Type",PartSemantics.Friendly(e.Definition.category.ToString())+" · "+e.Source);
            string set=e.Unit==null?InventoryUnits.SetLabel(game.Profile,game.Profile.SelectedCar,e.Definition):"Individual "+e.Unit.element.role;
            PartDetailFact(facts,"Includes",set.Replace("1 units","1 unit"));
            if(e.RequiresMatchingSet){
                PartDetailFact(facts,"Preview","With matching set","car-warning");
                var notice=new Label("Sold individually. Preview uses this unit plus new matching parts. A matching set is required before installing this unit."){name="partMatchingSetNotice",enableRichText=false};notice.AddToClassList("part-description");facts.Add(notice);
            }
            if(PartSemantics.Wears(e.Definition.category)){
                double health=e.Unit?.element.health??(e.Part!=null&&e.Definition.category==PartCategory.Tires?TireDynamics.LowestHealth(e.Part.condition):e.Part?.condition.health)??1;bool failed=e.Unit!=null?e.Unit.element.Failed:e.Part!=null&&e.Definition.category==PartCategory.Tires?TireDynamics.AnyFailed(e.Part.condition):e.Part?.condition.failed??false;
                PartDetailFact(facts,"Part condition",failed?"Failed · "+health.ToString("P0"):health.ToString("P0"),failed||health<=.25?"car-critical":health<=.5?"car-warning":"car-ready");
            }
            if(e.Source=="New"){var supply=PartsMarket.Quote(game.Profile,e.Definition,InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,e.Definition),PurchaseDraft(offer).Individual);PartDetailFact(facts,"Supply",PartsMarket.Available(game.Profile,e.Definition)+" units · "+supply.Summary);}
            PartDetailFact(facts,"Adjustment",e.Definition.tuning!=PartTuningOptions.None||e.Definition.camTimingAdjustable?"Adjustable":"Fixed");
            var description=new Label(PartDescription(e.Definition)){name="partDescription",enableRichText=false};description.AddToClassList("part-description");facts.Add(description);description.EnableInClassList("hidden",string.IsNullOrWhiteSpace(description.text));
            var specifications=new VisualElement{name="partKeySpecs"};specifications.AddToClassList("part-key-specs");partsDetailScroll.Add(specifications);
            foreach(var stat in PartSpecs(e))PartDetailFact(specifications,stat.label,PartStatText(stat).Substring(stat.label.Length+2));
            if(!string.IsNullOrWhiteSpace(e.Reason)){
                var reasons=e.Reason.Split('\n').Where(r=>!string.IsNullOrWhiteSpace(r)).Distinct().ToArray();
                if(e.Proposed==null){var availabilityLabel=new Label(SharedUnitSettings.TorqueText(e.Reason)){name="partAvailability",enableRichText=false};availabilityLabel.AddToClassList("part-description");availabilityLabel.AddToClassList("car-warning");facts.Add(availabilityLabel);}
                else {var warning=PartButton(facts,"partWarnings","Warnings · "+reasons.Length,Guarded(()=>OpenDialog("Warnings",SharedUnitSettings.TorqueText(e.Reason),new[]{new DialogAction("Close",CloseDialog)})),detail:true);warning.AddToClassList("part-warning-button");warning.AddToClassList(e.Proposed.warnings.Any(w=>w.critical)?"car-critical":"car-warning");}
            }
            bool fitting=e.Compatibility=="Compatible"||e.Compatibility=="Requires supporting changes";
            bool actualPosition=e.Unit==null||InventoryUnits.Positions(game.Profile,game.Profile.SelectedCar,e.Definition.id,e.Unit.element.role).Contains(e.Position);
            if(e.Source=="Owned"&&e.Unit!=null&&e.Position!=null){PartDetailFact(facts,"Position",partsBrowser.PositionLabel(e,e.Position));PartButton(facts,"partPosition","Change position",Guarded(()=>ChoosePartPosition(e,actionOrigin)),detail:true);if(!actualPosition){var notice=new Label("Apply the matching complete set before replacing this unit.");notice.AddToClassList("part-description");facts.Add(notice);}}
            if(e.Source=="New"&&e.Unit!=null&&e.Position!=null)PartDetailFact(facts,"Preview",string.Join(" · ",PurchaseDraft(offer).Positions.Count>0?PurchaseDraft(offer).Positions.Select(p=>InventoryUnits.PositionLabel(game.Profile,game.Profile.SelectedCar,e.Definition.id,p)):new[]{partsBrowser.PositionLabel(e,e.Position)}));
            var actions=new VisualElement{name="partDetailActions"};actions.AddToClassList("car-details-actions");actions.AddToClassList("part-detail-actions");partsDetail.Add(actions);
            var price=new Label(e.Price.HasValue?DisplayUnits.Money(e.Price.Value):""){name="partDetailPrice",enableRichText=false};price.AddToClassList("part-detail-price");actions.Add(price);
            var queuedLines=game.CartLines.Where(l=>e.Source=="New"?l.definitionId==e.Definition.id&&(l.action==CartAction.NewSet||l.action==CartAction.NewUnits):l.assetId==e.AssetId).ToArray();
            int queued=queuedLines.Sum(l=>l.quantity);
            var review=new VisualElement{name="partCartReview"};review.AddToClassList("part-cart-review");review.EnableInClassList("hidden",queued==0);partsDetail.Insert(partsDetail.IndexOf(actions),review);
            var queue=new Label(queued==0?"":"In cart · "+string.Join("\n",queuedLines.Select(CartLineSummary).Distinct())){name="partQueued",enableRichText=false};queue.AddToClassList("part-queued");review.Add(queue);
            PartButton(review,"partReviewCart","Review cart",OpenNativeCart,detail:true);
            if(game.PartsPage!="Installed"){
                if(e.Source=="New"||e.Source=="Used"){partDetailPurchaseAction=Guarded(()=>StagePurchase(offer,false,actionOrigin));PartButton(actions,"partAdd","Choose purchase",partDetailPurchaseAction,true,true).SetEnabled(game.Profile.reputation>=e.Definition.reputationRequired&&PartsMarket.FitsAny(game.Profile,e.Definition));}
                if(e.Source!="New"&&e.Source!="Used"){var fit=PartButton(actions,"partPlanInstall",e.Unit!=null&&e.Source=="Owned"?"Install unit":e.Source=="New"||e.Source=="Used"?"Add & install":"Plan install",Guarded(()=>{
                    if(game.NativeBlockPreparationPending(e.Definition)){OpenBlockPreparation(e,origin:actionOrigin);return;}
                    if(e.Unit!=null&&e.Source=="Owned"){long revision=game.Profile.revision;OpenDialog("Install unit?",partsBrowser.PositionLabel(e,e.Position),new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Install",()=>{if(actionOrigin!=null&&!TryValidatePartAction(actionOrigin,out _,out var reason)){RejectPartAction(reason);return;}if(game.InstallNativeUnit(e.Unit.element.id,e.Position,revision)){CloseDialog();partsStateKey=null;}else root.Q<ScrollView>("modalContent").Add(new Label(game.NativeNotice));},true)});}
                    else StagePurchase(offer,true,actionOrigin);
                }),true,true);fit.tooltip=e.Unit!=null&&e.Source=="Owned"?"Replace the selected unit after confirmation.":"Adds an installation plan to the cart; apply it at checkout.";fit.SetEnabled(fitting&&actualPosition&&(e.Unit==null||e.Position!=null)&&game.Profile.reputation>=e.Definition.reputationRequired);}
            }else PartButton(actions,"partPlanRemove","Remove via cart",Guarded(()=>{game.StageInventory(new PartCartLine{action=CartAction.Move,assetId=e.AssetId,definitionId=e.Definition.id,destination=InventoryKind.Storage,destinationId=SharedInventory.Garage});partsStateKey=null;}),detail:true);
            if(e.Source=="Owned"&&(e.Unit!=null||e.Part?.location==PartLocation.Inventory))PartButton(actions,"partPlanSale","Add sale to cart",Guarded(()=>{game.StageInventory(new PartCartLine{action=e.Unit==null?CartAction.SellPart:CartAction.SellUnit,assetId=e.AssetId,definitionId=e.Definition.id});partsStateKey=null;}),detail:true);
            if(e.Source=="Owned"||e.Source=="Installed"){
                PartDetailFact(facts,"Location",InventoryLocation(e.Location));
                PartButton(actions,"partMove","Move",Guarded(()=>OpenInventoryMove(e,actionOrigin)),detail:true).SetEnabled(game.Profile.SelectedCar!=null);
            }
            PartDetailFactsMilliseconds+=ElapsedPartMs(factsStart);
            long statsStart=Stopwatch.GetTimestamp();
            if(e.Proposed!=null&&partsBrowser.Reference!=null){
                NativeStatsPanel quick=null;
                quick=new NativeStatsPanel(partsBrowser.Reference,SharedUnitSettings.Current,new[]{NativeStatCatalog.PartSummary(partsBrowser.Reference,e.Proposed,new[]{e.Definition.category})},new Dictionary<string,bool>(),d=>OpenDialog(d.Label,d.InfoExplanation(quick.Current,quick.Proposed),new[]{new DialogAction("Close",CloseDialog)}),DecoratePartDetail,e.Proposed){name="partQuickStats"};quick.AddToClassList("part-quick-stats");quick.Q<Button>("statsDifferencesOnly").name="partQuickDifferences";
                var dyno=new VisualElement{name="partDetailDyno"};dyno.AddToClassList("part-detail-dyno");top.Add(dyno);DecoratePartDetail(dyno,RacingSurface.Kind.Panel);
                long healthyStart=Stopwatch.GetTimestamp();var healthy=partsLegacyActive?OwnedHealthy(game.Profile.SelectedCar):partsBrowser.HealthyCurrent;PartDetailHealthyMilliseconds+=ElapsedPartMs(healthyStart);
                dyno.Add(new NativeDynoPanel(new[]{new NativeDynoSeries("Reference",partsPlanned?"Planned build":"Current build",partsBrowser.Reference,NativeDynoStyle.Primary),new NativeDynoSeries("WithPart",e.RequiresMatchingSet?"With matching set":"With part",e.Proposed,NativeDynoStyle.Alternative),new NativeDynoSeries("NoWear","Current no wear",healthy,NativeDynoStyle.Reference)},SharedUnitSettings.Current.torque,"partDyno"));partsDetailScroll.Insert(1,quick);
                if(!partDetailCollapsed.TryGetValue(id,out var collapsed)){collapsed=new Dictionary<string,bool>();partDetailCollapsed[id]=collapsed;}
                var groups=NativeStatCatalog.PartEffects(partsBrowser.Reference,e.Proposed,new[]{e.Definition.category}).Select(g=>new NativeStatGroup(g.Id,g.Title,g.Definitions.ToArray()){InitiallyCollapsed=true,Children=g.Children});
                NativeStatsPanel panel=null;
                panel=new NativeStatsPanel(partsBrowser.Reference,SharedUnitSettings.Current,groups,collapsed,d=>OpenDialog(d.Label,d.InfoExplanation(panel.Current,panel.Proposed),new[]{new DialogAction("Close",CloseDialog)}),DecoratePartDetail,e.Proposed,partDetailDifferences.TryGetValue(id,out bool only)&&only){name="partStats"};panel.Q<Button>("statsDifferencesOnly").name="partDifferencesOnly";panel.DifferencesOnlyChanged=value=>partDetailDifferences[id]=value;partsDetailScroll.Add(panel);
                NativeStatsPanel specific=null;
                specific=new NativeStatsPanel(partsBrowser.Reference,SharedUnitSettings.Current,NativeStatCatalog.PartSpecific(partsBrowser.Reference,e.Proposed,new[]{e.Definition.category}),collapsed,d=>OpenDialog(d.Label,d.InfoExplanation(specific.Current,specific.Proposed),new[]{new DialogAction("Close",CloseDialog)}),DecoratePartDetail,e.Proposed,panel.DifferencesOnly){name="partSpecificStats"};
                specific.Q<Button>("statsDifferencesOnly").name="partSpecificDifferences";specific.Q<Button>("partSpecificDifferences").AddToClassList("hidden");
                specific.DifferencesOnlyChanged=value=>{partDetailDifferences[id]=value;if(panel.DifferencesOnly!=value)panel.SetDifferencesOnly(value);};
                panel.DifferencesOnlyChanged=value=>{partDetailDifferences[id]=value;if(specific.DifferencesOnly!=value)specific.SetDifferencesOnly(value);};
                partsDetailScroll.Insert(1,specific);specifications.AddToClassList("hidden");
            }
            PartDetailStatsMilliseconds+=ElapsedPartMs(statsStart);
            long priceStart=Stopwatch.GetTimestamp();if(offer.Source=="New")RefreshPurchasePrice(offer);PartDetailPriceMilliseconds+=ElapsedPartMs(priceStart);
            partDetailActionsReady=!partsLegacyActive;SetPartDetailMutationsEnabled(true);ExcludeTabTraversal();partsDetailScroll.schedule.Execute(()=>{if(partsDetailId==id)partsDetailScroll.scrollOffset=offset;});
            double legacyDetailMilliseconds=ElapsedPartMs(detailStart);
            PartDetailBuildMilliseconds+=legacyDetailMilliseconds;
            PartDetailOpenToReadyMilliseconds+=legacyDetailMilliseconds;
        }
        bool PartDetailTicketCurrent(PartDetailWork work)
        {
            bool viewportCurrent=work!=null&&work.Phase<6&&Math.Abs(partsScroll.contentViewport.layout.width-work.ViewportWidth)<.01f&&Math.Abs(partsScroll.contentViewport.layout.height-work.ViewportHeight)<.01f;
            if(work==null||partsLegacyActive||!Visible||game==null||game.ScreenName!="Parts"||!InterfaceComparison.IsReplacement("Parts")||ModalOpen||work.DetailGeneration!=partDetailGeneration||work.Id!=partsDetailId||work.DataGeneration!=partDataGeneration||work.PresentationGeneration!=partPresentationGeneration||work.WindowGeneration!=partWindowGeneration||work.Browser!=partsBrowser||game.PartsPage!=work.Page||partsPage==null||partsPage.ClassListContains("hidden")||(work.Phase<6&&!viewportCurrent))return false;
            var current=work.Browser.Entries.Find(candidate=>candidate.Id==work.Id);
            return current!=null&&ReferenceEquals(current,work.Entry)&&current.Source==work.Source;
        }
        void CancelPartDetailWork()
        {
            var work=partDetailWork;partDetailWork=null;
            if(work!=null)foreach(var surface in work.Surfaces)if(surface!=null)surfaces.Remove(surface);
            if(work!=null)work.Surfaces.Clear();
            partDetailPurchaseAction=null;partDetailActionsReady=false;partDetailDataGeneration=-1;partDetailPresentationGeneration=-1;
            if(work?.Root!=null)work.Root.RemoveFromHierarchy();
        }
        void AdvancePartDetailWork()
        {
            var work=partDetailWork;if(work==null)return;
            if(partStepHeld)return;
            if(!TryFreshPartFence(out _,out var reason)){CancelPartDetailWork();InvalidateAcceptedPartData(reason);return;}
            if(!PartDetailTicketCurrent(work)){CancelPartDetailWork();return;}
            long start=Stopwatch.GetTimestamp();
            try{switch(work.Phase)
            {
                case 0:
                    partsBrowser.Evaluate(work.Entry);PartDetailEvaluationMilliseconds+=ElapsedPartMs(start);work.Offer=work.Entry;work.Origin=PartOrigin(work.Offer);work.Phase++;break;
                case 1:BuildPartDetailBody(work);PartDetailFactsMilliseconds+=ElapsedPartMs(start);work.Phase++;break;
                case 2:BuildPartDetailActions(work);PartDetailActionsMilliseconds+=ElapsedPartMs(start);work.Phase++;break;
                case 3:BuildPartDetailStats(work);PartDetailStatsMilliseconds+=ElapsedPartMs(start);work.Phase++;break;
                case 4:RefreshPendingPartDetailPrice(work);PartDetailPriceMilliseconds+=ElapsedPartMs(start);work.Phase++;break;
                case 5:
                    if(work.Root.parent==null)partsPage.Add(work.Root);work.Root.style.visibility=Visibility.Hidden;work.Root.SetEnabled(false);PartDetailAttachMilliseconds+=ElapsedPartMs(start);work.Phase++;work.LayoutWaitFrames=0;break;
                default:
                    if(work.Root.layout.width<=0||work.Root.layout.height<=0)
                    {PartDetailLayoutWaitFrames++;PartDetailLayoutWaitMilliseconds+=ElapsedPartMs(start);if(++work.LayoutWaitFrames>3){CancelPartDetailWork();FailPartBrowserBuild("Part details did not complete layout.");}break;}
                    Vector2 publishOffset=work.PreserveExisting&&partsDetailScroll!=null?partsDetailScroll.scrollOffset:work.Offset;
                    if(partsDetail!=null){foreach(var surface in partDetailSurfaces)if(surface!=null)surfaces.Remove(surface);partDetailSurfaces.Clear();partsDetail.RemoveFromHierarchy();}
                    partsDetail=work.Root;partsDetail.name="partsDetail";partsDetail.style.position=Position.Relative;partsDetail.style.left=StyleKeyword.Null;partsDetail.style.right=StyleKeyword.Null;partsDetail.style.top=StyleKeyword.Null;partsDetail.style.bottom=StyleKeyword.Null;partsDetailScroll=work.Scroll;RegisterStagedPartSurfaces(work.Surfaces);partDetailSurfaces.AddRange(work.Surfaces);work.Surfaces.Clear();
                    partsDetail.style.visibility=Visibility.Visible;partsDetail.SetEnabled(true);ShowPartsBrowsing(false);partsScroll.AddToClassList("hidden");
                    partDetailDataGeneration=work.DataGeneration;partDetailPresentationGeneration=work.PresentationGeneration;
                    partDetailActionsByName.Clear();foreach(var pair in work.ActionCallbacks)partDetailActionsByName[pair.Key]=pair.Value;
                    partDetailPurchaseAction=work.PurchaseAction;partDetailActionsReady=true;SetPartDetailMutationsEnabled(true);ExcludeTabTraversal();
                    work.Scroll.scrollOffset=publishOffset;PartDetailOpenToReadyMilliseconds+=ElapsedPartMs(work.Started);PartDetailPublishMilliseconds+=ElapsedPartMs(start);partDetailWork=null;break;
            }}catch(Exception error){CancelPartDetailWork();FailPartBrowserBuild(error.Message);return;}
            finally{PartDetailBuildMilliseconds+=ElapsedPartMs(start);}
            if(partDetailWork==work&&partStepHoldRequested){partStepHoldRequested=false;partStepHeld=true;}
        }
        void AddPartDetailSurface(VisualElement owner,RacingSurface.Kind kind)
        {
            if(partDetailWork==null){AddSurface(owner,kind);partDetailSurfaces.Add(owner.Q<RacingSurface>());return;}
            var staged=CreateStagedPartSurface(owner,kind);partDetailWork.Surfaces.Add(staged);
        }
        void RefreshPendingPartDetailPrice(PartDetailWork work)
        {
            if(work?.Offer?.Source!="New")return;
            var draft=PurchaseDraft(work.Offer);var quote=PartsMarket.Quote(game.Profile,work.Offer.Definition,InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,work.Offer.Definition),draft.Individual,draft.Quantity);
            var label=work.Root.Q<Label>("partDetailPrice");if(label!=null)label.text=DisplayUnits.Money(quote.cost);
        }
        void BuildPartDetailBody(PartDetailWork work)
        {
            var e=work.Entry;string id=work.Id;Action Guarded(Action action)=>GuardPartAction(work.Offer,action);
            var heading=new VisualElement{name="partDetailHeading"};heading.AddToClassList("part-detail-heading");work.Root.Insert(0,heading);
            var back=PartButton(heading,"partBack","",BackToParts,detail:true);back.AddToClassList("car-back-button");back.Add(new FilledMenuIcon("Back"));back.tooltip="Back to parts";
            var title=new Label(e.DisplayName??e.Definition.name){name="partDetailTitle",enableRichText=false};title.AddToClassList("car-detail-title");title.AddToClassList("part-detail-title");heading.Add(title);
            var info=PartButton(heading,"partInfo","i",Guarded(()=>OpenDialog(e.Definition.name,SharedUnitSettings.TorqueText(PartBrands.Description(e.Definition.brand)+"\n"+e.Definition.description+"\n"+PartTuning.Summary(e.Definition)+"\n"+e.Reason+"\n"+string.Join("\n",PartSemantics.Stats(e.Definition).Select(PartStatText))),new[]{new DialogAction("Close",CloseDialog)})),detail:true);info.AddToClassList("part-info-button");info.tooltip="All part specifications";
            var top=new VisualElement{name="partDetailTop"};top.AddToClassList("part-detail-top");work.Scroll.Add(top);work.Top=top;
            var facts=new VisualElement{name="partDetailFacts"};facts.AddToClassList("part-detail-facts");top.Add(facts);top.EnableInClassList("part-preview-unavailable",e.Proposed==null);DecoratePartDetail(facts,RacingSurface.Kind.Panel);work.Facts=facts;
            PartDetailFact(facts,"Compatibility",e.Compatibility,e.Compatibility=="Compatible"?"car-ready":"car-warning");
            PartDetailFact(facts,"Brand",e.Definition.brand+" · "+PartBrands.Focus(e.Definition.brand));PartDetailFact(facts,"Specification",e.Definition.quality);
            PartDetailFact(facts,"Type",PartSemantics.Friendly(e.Definition.category.ToString())+" · "+e.Source);
            string set=e.Unit==null?InventoryUnits.SetLabel(game.Profile,game.Profile.SelectedCar,e.Definition):"Individual "+e.Unit.element.role;PartDetailFact(facts,"Includes",set.Replace("1 units","1 unit"));
            if(e.RequiresMatchingSet){PartDetailFact(facts,"Preview","With matching set","car-warning");var notice=new Label("Sold individually. Preview uses this unit plus new matching parts. A matching set is required before installing this unit."){name="partMatchingSetNotice",enableRichText=false};notice.AddToClassList("part-description");facts.Add(notice);}
            if(PartSemantics.Wears(e.Definition.category)){double health=e.Unit?.element.health??(e.Part!=null&&e.Definition.category==PartCategory.Tires?TireDynamics.LowestHealth(e.Part.condition):e.Part?.condition.health)??1;bool failed=e.Unit!=null?e.Unit.element.Failed:e.Part!=null&&e.Definition.category==PartCategory.Tires?TireDynamics.AnyFailed(e.Part.condition):e.Part?.condition.failed??false;PartDetailFact(facts,"Part condition",failed?"Failed · "+health.ToString("P0"):health.ToString("P0"),failed||health<=.25?"car-critical":health<=.5?"car-warning":"car-ready");}
            if(e.Source=="New"){var supply=PartsMarket.Quote(game.Profile,e.Definition,InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,e.Definition),PurchaseDraft(work.Offer).Individual);PartDetailFact(facts,"Supply",PartsMarket.Available(game.Profile,e.Definition)+" units · "+supply.Summary);}
            PartDetailFact(facts,"Adjustment",e.Definition.tuning!=PartTuningOptions.None||e.Definition.camTimingAdjustable?"Adjustable":"Fixed");
            var description=new Label(PartDescription(e.Definition)){name="partDescription",enableRichText=false};description.AddToClassList("part-description");facts.Add(description);description.EnableInClassList("hidden",string.IsNullOrWhiteSpace(description.text));
            var specifications=new VisualElement{name="partKeySpecs"};specifications.AddToClassList("part-key-specs");work.Scroll.Add(specifications);work.Specifications=specifications;
            foreach(var stat in PartSpecs(e))PartDetailFact(specifications,stat.label,PartStatText(stat).Substring(stat.label.Length+2));
            if(!string.IsNullOrWhiteSpace(e.Reason)){var reasons=e.Reason.Split('\n').Where(r=>!string.IsNullOrWhiteSpace(r)).Distinct().ToArray();if(e.Proposed==null){var availabilityLabel=new Label(SharedUnitSettings.TorqueText(e.Reason)){name="partAvailability",enableRichText=false};availabilityLabel.AddToClassList("part-description");availabilityLabel.AddToClassList("car-warning");facts.Add(availabilityLabel);}else{var warning=PartButton(facts,"partWarnings","Warnings · "+reasons.Length,Guarded(()=>OpenDialog("Warnings",SharedUnitSettings.TorqueText(e.Reason),new[]{new DialogAction("Close",CloseDialog)})),detail:true);warning.AddToClassList("part-warning-button");warning.AddToClassList(e.Proposed.warnings.Any(w=>w.critical)?"car-critical":"car-warning");}}
            if(e.Source=="Owned"&&e.Unit!=null&&e.Position!=null){PartDetailFact(facts,"Position",partsBrowser.PositionLabel(e,e.Position));PartButton(facts,"partPosition","Change position",Guarded(()=>ChoosePartPosition(e,work.Origin)),detail:true);if(!InventoryUnits.Positions(game.Profile,game.Profile.SelectedCar,e.Definition.id,e.Unit.element.role).Contains(e.Position)){var notice=new Label("Apply the matching complete set before replacing this unit.");notice.AddToClassList("part-description");facts.Add(notice);}}
            if(e.Source=="New"&&e.Unit!=null&&e.Position!=null)PartDetailFact(facts,"Preview",string.Join(" · ",PurchaseDraft(work.Offer).Positions.Count>0?PurchaseDraft(work.Offer).Positions.Select(p=>InventoryUnits.PositionLabel(game.Profile,game.Profile.SelectedCar,e.Definition.id,p)):new[]{partsBrowser.PositionLabel(e,e.Position)}));
        }
        void BuildPartDetailActions(PartDetailWork work)
        {
            var e=work.Entry;var offer=work.Offer;Action Guarded(Action action)=>GuardPartAction(offer,action);var actionOrigin=work.Origin;string id=work.Id;
            bool fitting=e.Compatibility=="Compatible"||e.Compatibility=="Requires supporting changes";bool actualPosition=e.Unit==null||InventoryUnits.Positions(game.Profile,game.Profile.SelectedCar,e.Definition.id,e.Unit.element.role).Contains(e.Position);
            var actions=new VisualElement{name="partDetailActions"};actions.AddToClassList("car-details-actions");actions.AddToClassList("part-detail-actions");work.Root.Add(actions);work.Actions=actions;
            var price=new Label(e.Price.HasValue?DisplayUnits.Money(e.Price.Value):""){name="partDetailPrice",enableRichText=false};price.AddToClassList("part-detail-price");actions.Add(price);
            var queuedLines=game.CartLines.Where(l=>e.Source=="New"?l.definitionId==e.Definition.id&&(l.action==CartAction.NewSet||l.action==CartAction.NewUnits):l.assetId==e.AssetId).ToArray();int queued=queuedLines.Sum(l=>l.quantity);
            var review=new VisualElement{name="partCartReview"};review.AddToClassList("part-cart-review");review.EnableInClassList("hidden",queued==0);work.Root.Insert(work.Root.IndexOf(actions),review);
            var queue=new Label(queued==0?"":"In cart · "+string.Join("\n",queuedLines.Select(CartLineSummary).Distinct())){name="partQueued",enableRichText=false};queue.AddToClassList("part-queued");review.Add(queue);PartButton(review,"partReviewCart","Review cart",OpenNativeCart,detail:true);
            if(game.PartsPage!="Installed"){
            if(e.Source=="New"||e.Source=="Used"){work.PurchaseAction=Guarded(()=>StagePurchase(offer,false,actionOrigin));PartButton(actions,"partAdd","Choose purchase",work.PurchaseAction,true,true).SetEnabled(game.Profile.reputation>=e.Definition.reputationRequired&&PartsMarket.FitsAny(game.Profile,e.Definition));}
                if(e.Source!="New"&&e.Source!="Used"){var fit=PartButton(actions,"partPlanInstall",e.Unit!=null&&e.Source=="Owned"?"Install unit":e.Source=="New"||e.Source=="Used"?"Add & install":"Plan install",Guarded(()=>{if(game.NativeBlockPreparationPending(e.Definition)){OpenBlockPreparation(e,origin:actionOrigin);return;}if(e.Unit!=null&&e.Source=="Owned"){long revision=game.Profile.revision;OpenDialog("Install unit?",partsBrowser.PositionLabel(e,e.Position),new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Install",()=>{if(actionOrigin!=null&&!TryValidatePartAction(actionOrigin,out _,out var reason)){RejectPartAction(reason);return;}if(game.InstallNativeUnit(e.Unit.element.id,e.Position,revision)){CloseDialog();partsStateKey=null;}else root.Q<ScrollView>("modalContent").Add(new Label(game.NativeNotice));},true)});}else StagePurchase(offer,true,actionOrigin);}),true,true);fit.tooltip=e.Unit!=null&&e.Source=="Owned"?"Replace the selected unit after confirmation.":"Adds an installation plan to the cart; apply it at checkout.";fit.SetEnabled(fitting&&actualPosition&&(e.Unit==null||e.Position!=null)&&game.Profile.reputation>=e.Definition.reputationRequired);}
            }else PartButton(actions,"partPlanRemove","Remove via cart",Guarded(()=>{game.StageInventory(new PartCartLine{action=CartAction.Move,assetId=e.AssetId,definitionId=e.Definition.id,destination=InventoryKind.Storage,destinationId=SharedInventory.Garage});partsStateKey=null;}),detail:true);
            if(e.Source=="Owned"&&(e.Unit!=null||e.Part?.location==PartLocation.Inventory))PartButton(actions,"partPlanSale","Add sale to cart",Guarded(()=>{game.StageInventory(new PartCartLine{action=e.Unit==null?CartAction.SellPart:CartAction.SellUnit,assetId=e.AssetId,definitionId=e.Definition.id});partsStateKey=null;}),detail:true);
            if(e.Source=="Owned"||e.Source=="Installed"){PartDetailFact(work.Facts,"Location",InventoryLocation(e.Location));PartButton(actions,"partMove","Move",Guarded(()=>OpenInventoryMove(e,actionOrigin)),detail:true).SetEnabled(game.Profile.SelectedCar!=null);}
        }
        void BuildPartDetailStats(PartDetailWork work)
        {
            var e=work.Entry;var id=work.Id;var top=work.Top;var specifications=work.Specifications;
            if(e.Proposed!=null&&partsBrowser.Reference!=null){NativeStatsPanel quick=null;quick=new NativeStatsPanel(partsBrowser.Reference,SharedUnitSettings.Current,new[]{NativeStatCatalog.PartSummary(partsBrowser.Reference,e.Proposed,new[]{e.Definition.category})},new Dictionary<string,bool>(),d=>OpenDialog(d.Label,d.InfoExplanation(quick.Current,quick.Proposed),new[]{new DialogAction("Close",CloseDialog)}),DecoratePartDetail,e.Proposed){name="partQuickStats"};quick.AddToClassList("part-quick-stats");quick.Q<Button>("statsDifferencesOnly").name="partQuickDifferences";
                var dyno=new VisualElement{name="partDetailDyno"};dyno.AddToClassList("part-detail-dyno");top.Add(dyno);DecoratePartDetail(dyno,RacingSurface.Kind.Panel);
                long healthyStart=Stopwatch.GetTimestamp();var healthy=partsBrowser.HealthyCurrent;PartDetailHealthyMilliseconds+=ElapsedPartMs(healthyStart);
                dyno.Add(new NativeDynoPanel(new[]{new NativeDynoSeries("Reference",partsPlanned?"Planned build":"Current build",partsBrowser.Reference,NativeDynoStyle.Primary),new NativeDynoSeries("WithPart",e.RequiresMatchingSet?"With matching set":"With part",e.Proposed,NativeDynoStyle.Alternative),new NativeDynoSeries("NoWear","Current no wear",healthy,NativeDynoStyle.Reference)},SharedUnitSettings.Current.torque,"partDyno"));work.Scroll.Insert(1,quick);
                if(!partDetailCollapsed.TryGetValue(id,out var collapsed)){collapsed=new Dictionary<string,bool>();partDetailCollapsed[id]=collapsed;}
                var groups=NativeStatCatalog.PartEffects(partsBrowser.Reference,e.Proposed,new[]{e.Definition.category}).Select(g=>new NativeStatGroup(g.Id,g.Title,g.Definitions.ToArray()){InitiallyCollapsed=true,Children=g.Children});NativeStatsPanel panel=null;
                panel=new NativeStatsPanel(partsBrowser.Reference,SharedUnitSettings.Current,groups,collapsed,d=>OpenDialog(d.Label,d.InfoExplanation(panel.Current,panel.Proposed),new[]{new DialogAction("Close",CloseDialog)}),DecoratePartDetail,e.Proposed,partDetailDifferences.TryGetValue(id,out bool only)&&only){name="partStats"};panel.Q<Button>("statsDifferencesOnly").name="partDifferencesOnly";panel.DifferencesOnlyChanged=value=>partDetailDifferences[id]=value;work.Scroll.Add(panel);
                NativeStatsPanel specific=null;specific=new NativeStatsPanel(partsBrowser.Reference,SharedUnitSettings.Current,NativeStatCatalog.PartSpecific(partsBrowser.Reference,e.Proposed,new[]{e.Definition.category}),collapsed,d=>OpenDialog(d.Label,d.InfoExplanation(specific.Current,specific.Proposed),new[]{new DialogAction("Close",CloseDialog)}),DecoratePartDetail,e.Proposed,panel.DifferencesOnly){name="partSpecificStats"};specific.Q<Button>("statsDifferencesOnly").name="partSpecificDifferences";specific.Q<Button>("partSpecificDifferences").AddToClassList("hidden");
                specific.DifferencesOnlyChanged=value=>{partDetailDifferences[id]=value;if(panel.DifferencesOnly!=value)panel.SetDifferencesOnly(value);};panel.DifferencesOnlyChanged=value=>{partDetailDifferences[id]=value;if(specific.DifferencesOnly!=value)specific.SetDifferencesOnly(value);};work.Scroll.Insert(1,specific);specifications.AddToClassList("hidden");}
        }
    }
}
