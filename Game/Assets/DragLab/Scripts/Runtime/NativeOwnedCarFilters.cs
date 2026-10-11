using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        readonly OwnedCarQuery savedOwnedQuery=new OwnedCarQuery(),dealerQuery=new OwnedCarQuery();
        OwnedCarQuery ownedQuery=>dealerMode?dealerQuery:savedOwnedQuery;
        readonly Dictionary<string,OwnedCarEntry> ownedEntries=new Dictionary<string,OwnedCarEntry>();
        Dictionary<string,OwnedCarEntry> BrowsingEntries=>dealerMode?dealerEntries:ownedEntries;
        string QuerySortName(OwnedCarSort sort)=>dealerMode&&sort==OwnedCarSort.OwnedOrder?"Model order":SortName(sort);
        VisualElement ownedToolbar;
        TextField ownedSearch;
        Label ownedCount,ownedNoMatches;
        Button ownedFilter,ownedSort,ownedClear;
        internal IReadOnlyList<OwnedCarEntry> OwnedEntries=>ownedEntries.Values.OrderBy(e=>e.Order).ToArray();
        internal OwnedCarQuery OwnedQuery=>ownedQuery;
        internal int OwnedQueryRefreshCount {get;private set;}
        internal int OwnedRatingRunCount {get;private set;}
        static string BodyName(CarBodyType body)=>body==CarBodyType.Unknown?"Unspecified":body.ToString();
        static string InductionName(InductionType type)=>type==InductionType.NaturallyAspirated?"NA":type==InductionType.Turbo||type==InductionType.TwinTurbo?"Turbo":type==InductionType.Supercharger||type==InductionType.TwinSupercharger?"Supercharged":"Twincharged";
        static string SortName(OwnedCarSort sort)=>sort==OwnedCarSort.OwnedOrder?"Owned order":sort.ToString();
        void InitializeOwnedCarFilters()
        {
            ownedToolbar=new VisualElement {name="ownedCarToolbar"};ownedToolbar.AddToClassList("owned-car-toolbar");
            root.Q(className:"workspace").Insert(0,ownedToolbar);
            ownedSearch=new TextField {name="ownedCarSearch",maxLength=128,tooltip="Search name, model, instance, type or configuration"};
            ownedSearch.AddToClassList("setting-text");ownedSearch.AddToClassList("owned-car-search");ownedToolbar.Add(ownedSearch);
            ownedSearch.RegisterValueChangedCallback(evt=>{ownedQuery.Search=evt.newValue;ApplyOwnedQuery(true);});
            ownedFilter=OwnedToolbarButton("ownedCarFilters","Filters","Tuning",()=>OpenOwnedCarFilters());
            ownedSort=OwnedToolbarButton("ownedCarSort","Owned order","Data",OpenOwnedCarSort);
            ownedClear=OwnedToolbarButton("ownedCarClear","×",null,ResetOwnedQuery);ownedClear.tooltip="Reset search, filters and sorting";ownedClear.AddToClassList("owned-query-clear");
            ownedCount=new Label {name="ownedCarCount"};ownedCount.AddToClassList("owned-car-count");ownedToolbar.Add(ownedCount);
            ownedNoMatches=new Label {name="ownedCarNoMatches",text="No matching cars"};ownedNoMatches.AddToClassList("car-empty");ownedNoMatches.AddToClassList("hidden");ownedScroll.Add(ownedNoMatches);
            var hint=new Label("Search cars") {name="ownedCarSearchHint",pickingMode=PickingMode.Ignore};hint.AddToClassList("owned-search-hint");ownedSearch.Add(hint);
            ownedSearch.RegisterCallback<FocusInEvent>(_=>hint.AddToClassList("hidden"));ownedSearch.RegisterCallback<FocusOutEvent>(_=>hint.EnableInClassList("hidden",!string.IsNullOrEmpty(ownedSearch.value)));
            ownedSearch.RegisterValueChangedCallback(_=>hint.EnableInClassList("hidden",!string.IsNullOrEmpty(ownedSearch.value)||ownedSearch.focusController?.focusedElement==ownedSearch));
        }
        Button OwnedToolbarButton(string id,string text,string icon,Action action)
        {
            var button=new Button(action) {name=id,text=text,tabIndex=-1,focusable=false};button.AddToClassList("action-button");ownedToolbar.Add(button);
            if(icon!=null)button.Add(new FilledMenuIcon(icon));AddSurface(button,RacingSurface.Kind.SecondaryAction);return button;
        }
        internal void ResetOwnedQuery()
        {
            ownedQuery.Search="";ownedQuery.Rank=ownedQuery.Drive=ownedQuery.Type=ownedQuery.Gearbox=ownedQuery.Induction=ownedQuery.Status=ownedQuery.Affordability="All";
            ownedQuery.Minimum=new double?[5];ownedQuery.Maximum=new double?[5];ownedQuery.Sort=OwnedCarSort.OwnedOrder;ownedQuery.Descending=false;
            ownedSearch.SetValueWithoutNotify("");ownedSearch.Q("ownedCarSearchHint").RemoveFromClassList("hidden");ApplyOwnedQuery(true);
            ownedSearch.Blur();
        }
        internal void ApplyOwnedQuery(bool resetScroll=false)
        {
            if(ownedGrid==null)return;
            OwnedQueryRefreshCount++;
            var entries=BrowsingEntries.Values.ToList();entries.Sort(ownedQuery.Compare);
            var grid=dealerMode?dealerGrid:ownedGrid;var noMatches=dealerMode?dealerNoMatches:ownedNoMatches;
            if(grid==null||noMatches==null)return;
            int visible=0;
            foreach(var entry in entries) {
                var card=grid.Q((dealerMode?"dealerCar:":"ownedCar:")+entry.Id);if(card==null)continue;
                bool matches=ownedQuery.Matches(entry);card.EnableInClassList("hidden",!matches);if(matches)visible++;
                card.BringToFront();
            }
            noMatches.EnableInClassList("hidden",visible>0||BrowsingEntries.Count==0);
            ownedCount.text=visible+" / "+BrowsingEntries.Count;
            bool waiting=BrowsingEntries.Values.Any(e=>e.RatingPending);
            bool ratingFiltered=ownedQuery.Rank!="All"||ownedQuery.Minimum[0].HasValue||ownedQuery.Maximum[0].HasValue;
            noMatches.text=waiting&&ratingFiltered?"Calculating ratings…":"No matching cars";
            ownedCount.tooltip=waiting?"Class and PP estimates are calculating on copied builds. Unknown values remain unavailable; filtered results update when ready.":dealerMode?"Matching / dealership models":"Matching / owned cars";
            ownedFilter.Q<Label>().text="Filters"+(ownedQuery.FilterCount>0?" · "+ownedQuery.FilterCount:"");ownedFilter.EnableInClassList("selected",ownedQuery.FilterCount>0);
            ownedSort.Q<Label>().text=QuerySortName(ownedQuery.Sort)+(ownedQuery.Sort==OwnedCarSort.OwnedOrder&&!ownedQuery.Descending?"":ownedQuery.Descending?" ↓":" ↑");
            ownedSort.EnableInClassList("selected",ownedQuery.Sort!=OwnedCarSort.OwnedOrder||ownedQuery.Descending);
            ownedClear.SetEnabled(!string.IsNullOrWhiteSpace(ownedQuery.Search)||ownedQuery.FilterCount>0||ownedQuery.Sort!=OwnedCarSort.OwnedOrder||ownedQuery.Descending);
            if(resetScroll)(dealerMode?dealerScroll:ownedScroll).scrollOffset=Vector2.zero;
        }
        void QueryChoices(VisualElement parent,string id,string label,string[] options,Func<string> get,Action<string> set,int perRow=5)
        {
            var block=new VisualElement();block.AddToClassList("owned-query-group");parent.Add(block);block.Add(new Label(label));
            var buttons=new List<Tuple<Button,string>>();
            void Update(){foreach(var pair in buttons)pair.Item1.EnableInClassList("selected",get()==pair.Item2);}
            for(int start=0;start<options.Length;start+=perRow) {
                var group=new VisualElement();group.AddToClassList("choice-group");group.AddToClassList("owned-query-choices");block.Add(group);
                int count=Math.Min(perRow,options.Length-start);
                for(int j=0;j<count;j++) {
                    string option=options[start+j];var button=new Button(()=>{set(option);Update();}) {name=id+":"+option,text=option,tabIndex=-1,focusable=false};button.AddToClassList("choice-button");
                    button.AddToClassList(j==0?"choice-first":j==count-1?"choice-last":"choice-middle");group.Add(button);
                    AddSurface(button,j==0?RacingSurface.Kind.ChoiceFirst:j==count-1?RacingSurface.Kind.ChoiceLast:RacingSurface.Kind.ChoiceMiddle);dialogSurfaces.Add(button.Q<RacingSurface>());buttons.Add(Tuple.Create(button,option));
                }
            }
            Update();
        }
        void OpenOwnedCarSort()
        {
            var draft=ownedQuery.Copy();
            OpenDialog("Sort cars",null,new[] {new DialogAction("Cancel",CloseDialog),new DialogAction("Apply",()=>{
                ownedQuery.Sort=draft.Sort;ownedQuery.Descending=draft.Descending;CloseDialog();ApplyOwnedQuery(true);
            },true)},populate:content=> {
                QueryChoices(content,"ownedSortBy","Sort by",Enum.GetValues(typeof(OwnedCarSort)).Cast<OwnedCarSort>().Select(QuerySortName).ToArray(),()=>QuerySortName(draft.Sort),value=>draft.Sort=Enum.GetValues(typeof(OwnedCarSort)).Cast<OwnedCarSort>().First(s=>QuerySortName(s)==value),3);
                QueryChoices(content,"ownedSortDirection","Order",new[] {"Ascending","Descending"},()=>draft.Descending?"Descending":"Ascending",value=>draft.Descending=value=="Descending");
            });
        }
        void OpenOwnedCarFilters(OwnedCarQuery initial=null)
        {
            var draft=(initial??ownedQuery).Copy();var minimum=new TextField[5];var maximum=new TextField[5];
            var preview=new Label {name="ownedFilterPreview"};var error=new Label {name="ownedFilterError"};error.AddToClassList("car-critical");
            double torqueScale=DisplayUnits.Torque(1,SharedUnitSettings.Current.torque);
            bool Validate() {
                for(int i=0;i<5;i++) {
                    bool Parse(TextField field,out double? value) {
                        value=null;if(string.IsNullOrWhiteSpace(field.value))return true;
                        if(!double.TryParse(field.value,NumberStyles.Float,CultureInfo.CurrentCulture,out double number)||double.IsNaN(number)||double.IsInfinity(number)||number<0||i==0&&number>999||i==4&&number>100)return false;
                        value=i==2?number/torqueScale:number;return true;
                    }
                    if(!Parse(minimum[i],out draft.Minimum[i])||!Parse(maximum[i],out draft.Maximum[i])||draft.Minimum[i].HasValue&&draft.Maximum[i].HasValue&&draft.Minimum[i]>draft.Maximum[i]) {
                        error.text="Enter valid ranges: minimum ≤ maximum.";root.Q<Button>("dialogApply").SetEnabled(false);return false;
                    }
                }
                error.text="";preview.text=BrowsingEntries.Values.Count(draft.Matches)+" / "+BrowsingEntries.Count+" cars";root.Q<Button>("dialogApply").SetEnabled(true);return true;
            }
            OpenDialog("Filter cars",null,new[] {
                new DialogAction("Reset",()=>{CloseDialog();OpenOwnedCarFilters(new OwnedCarQuery {Search=ownedQuery.Search,Sort=ownedQuery.Sort,Descending=ownedQuery.Descending});}),
                new DialogAction("Cancel",CloseDialog),new DialogAction("Apply",()=> {
                    if(!Validate())return;ownedQuery.Rank=draft.Rank;ownedQuery.Drive=draft.Drive;ownedQuery.Type=draft.Type;ownedQuery.Gearbox=draft.Gearbox;ownedQuery.Induction=draft.Induction;ownedQuery.Status=draft.Status;
                    ownedQuery.Affordability=draft.Affordability;
                    ownedQuery.Minimum=draft.Minimum;ownedQuery.Maximum=draft.Maximum;CloseDialog();ApplyOwnedQuery(true);
                },true)
            },populate:content=> {
                QueryChoices(content,"ownedFilterRank","Rank",new[] {"All","F","E","D","C","B","A","S"},()=>draft.Rank,value=>{draft.Rank=value;Validate();},8);
                QueryChoices(content,"ownedFilterDrive","Drivetrain",new[] {"All","FWD","RWD","AWD","4×4"},()=>draft.Drive,value=>{draft.Drive=value;Validate();});
                QueryChoices(content,"ownedFilterType","Type",new[] {"All","Hatchback","Coupe","Sedan","Pickup","SUV","Wagon","Van","Roadster","Unspecified"},()=>draft.Type,value=>{draft.Type=value;Validate();});
                QueryChoices(content,"ownedFilterGearbox","Transmission",new[] {"All","Manual","Sequential","DCT","Automatic","CVT"},()=>draft.Gearbox,value=>{draft.Gearbox=value;Validate();},3);
                QueryChoices(content,"ownedFilterInduction","Induction",new[] {"All","NA","Turbo","Supercharged","Twincharged"},()=>draft.Induction,value=>{draft.Induction=value;Validate();});
                if(dealerMode)QueryChoices(content,"ownedFilterAffordability","Affordability",new[] {"All","Affordable","Unaffordable"},()=>draft.Affordability,value=>{draft.Affordability=value;Validate();});
                QueryChoices(content,"ownedFilterStatus","Availability",dealerMode?new[] {"All","Available","Locked","Unaffordable","Unavailable"}:new[] {"All","Ready","Needs service","Reserved","Unavailable","No fuel"},()=>draft.Status,value=>{draft.Status=value;Validate();},3);
                string[] names={"PP","HP","Torque · "+DisplayUnits.TorqueLabel(SharedUnitSettings.Current.torque),"Weight · kg","Condition · %"};
                for(int i=0;i<5;i++) {
                    var row=new VisualElement();row.AddToClassList("owned-query-range");content.Add(row);var title=new Label(names[i]);title.AddToClassList("owned-query-range-title");row.Add(title);
                    double scale=i==2?torqueScale:1;
                    TextField Field(string edge,double? number) {
                        var field=new TextField {name="ownedFilter"+edge+i,value=number.HasValue?(number.Value*scale).ToString("0.##"):"",tooltip=edge+" (blank = no bound)",maxLength=16};field.AddToClassList("setting-number");field.RegisterValueChangedCallback(_=>Validate());return field;
                    }
                    row.Add(new Label("Min"));minimum[i]=Field("Min",draft.Minimum[i]);row.Add(minimum[i]);row.Add(new Label("Max"));maximum[i]=Field("Max",draft.Maximum[i]);row.Add(maximum[i]);
                }
                content.Add(preview);content.Add(error);
            });
            Validate();
            foreach(var button in root.Q("modalContent").Query<Button>().ToList())if(button.name=="ownedFilterDrive:4×4")button.tooltip="AWD with an effective locked center coupling. AWD includes all modeled AWD setups.";
        }
    }
}
