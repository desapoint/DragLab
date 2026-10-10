using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        VisualElement[] VisibleOwnedCards()=>shell.Root.Q("ownedCarGrid").Children().Where(c=>c.ClassListContains("owned-car-card")&&!c.ClassListContains("hidden")).ToArray();
        IEnumerator CarFilterClick(WindowsKeyboardProbe input,string id)
        {
            shell.Root.Q<ScrollView>("modalContent").ScrollTo(shell.Root.Q(id));yield return new WaitForSecondsRealtime(.1f);yield return Click(input,id);
        }
        IEnumerator CarFilterChecks(WindowsKeyboardProbe input)
        {
            var profile=game.Profile;string baseline=JsonUtility.ToJson(profile),disk=System.IO.File.ReadAllText(ProfileStore.FilePath);
            int builds=shell.OwnedGridBuildCount,probes=shell.OwnedRatingRunCount;
            var entries=shell.OwnedEntries;
            Check(entries.Count==profile.cars.Count&&entries.All(e=>e.Type!="Unspecified"),"Authored body types cover every owned model without deriving type from temporary sprite art");
            Check(entries.Where(e=>e.Rating!=null).All(e=>shell.Root.Q<Label>("carClass:"+e.Id).text.Contains("PP "+e.Rating.points)),"Cards show PP from the same completed quarter-mile rating as rank");
            yield return Click(input,"ownedCarSearch");yield return Key(input,0x4A);yield return Key(input,0x55);yield return Key(input,0x4E);
            Check(shell.Root.Q<TextField>("ownedCarSearch").value.ToLowerInvariant()=="jun"&&VisibleOwnedCards().Length==entries.Count(e=>new OwnedCarQuery {Search="jun"}.Matches(e)),"Real keyboard search filters named/model instances as text is entered");
            yield return Capture("car-search");yield return Click(input,"ownedCarClear");
            Check(VisibleOwnedCards().Length==entries.Count&&shell.OwnedQuery.Search=="","Clear restores the complete grid without selecting another car");
            yield return Click(input,"ownedCarFilters");yield return CarFilterClick(input,"ownedFilterType:Coupe");
            Check(shell.OwnedQuery.Type=="All"&&VisibleOwnedCards().Length==entries.Count,"Filter dialog choices stay draft until Apply");
            yield return Capture("car-filters");yield return Click(input,"dialogCancel");
            Check(shell.OwnedQuery.Type=="All","Cancel discards filter draft");
            yield return Click(input,"ownedCarFilters");yield return CarFilterClick(input,"ownedFilterType:Coupe");yield return Click(input,"dialogApply");
            Check(VisibleOwnedCards().Select(c=>c.name).SequenceEqual(entries.Where(e=>e.Type=="Coupe").Select(e=>"ownedCar:"+e.Id)),"Body-type Apply shows exact matching instances in stable owned order");
            yield return Click(input,"settingsButton");yield return Click(input,"cancelSettings");
            Check(shell.OwnedQuery.Type=="Coupe"&&VisibleOwnedCards().Length==entries.Count(e=>e.Type=="Coupe"),"Settings Cancel retains search/filter state");
            yield return Key(input,0x77);Check(!shell.Visible,"F8 can compare the legacy Garage while a native filter is applied");yield return Key(input,0x77);
            Check(shell.OwnedQuery.Type=="Coupe"&&VisibleOwnedCards().Length==entries.Count(e=>e.Type=="Coupe"),"F8 whole-interface comparison retains the native filtered view");
            yield return Click(input,"ownedCarFilters");yield return Click(input,"dialogReset");
            Check(shell.OwnedQuery.Type=="Coupe"&&shell.ModalOpen&&shell.Root.Q<Label>("ownedFilterPreview").text.StartsWith(entries.Count+" /"),"Reset previews cleared filter criteria without publishing until Apply");yield return Click(input,"dialogCancel");
            yield return Click(input,"ownedCarSort");yield return CarFilterClick(input,"ownedSortBy:HP");yield return CarFilterClick(input,"ownedSortDirection:Descending");yield return Click(input,"dialogApply");
            var expected=entries.Where(shell.OwnedQuery.Matches).ToList();expected.Sort(shell.OwnedQuery.Compare);
            Check(VisibleOwnedCards().Select(c=>c.name).SequenceEqual(expected.Select(e=>"ownedCar:"+e.Id)),"Real sort controls order the filtered grid by applied HP descending");
            yield return Capture("car-sorted-hp");
            yield return Click(input,"ownedCarFilters");yield return CarFilterClick(input,"ownedFilterType:All");
            var min=shell.Root.Q<TextField>("ownedFilterMin2");var max=shell.Root.Q<TextField>("ownedFilterMax2");
            min.value="500";max.value="100";Check(!shell.Root.Q<Button>("dialogApply").enabledSelf&&shell.Root.Q<Label>("ownedFilterError").text.Length>0,"Inverted ranges disable Apply instead of silently exchanging values");
            min.value="NaN";max.value="";Check(!shell.Root.Q<Button>("dialogApply").enabledSelf,"Non-finite numeric filter input rejects");
            min.value="200";max.value="";Check(shell.Root.Q<Button>("dialogApply").enabledSelf,"Blank upper bound means unrestricted rather than zero");
            yield return CarFilterClick(input,"ownedFilterMin2");yield return Capture("car-filter-ranges");yield return Click(input,"dialogApply");
            Check(Math.Abs(shell.OwnedQuery.Minimum[2].Value-200/DisplayUnits.Torque(1,SharedUnitSettings.Current.torque))<.001&&VisibleOwnedCards().Length==entries.Count(shell.OwnedQuery.Matches),"Torque range converts selected display units back to canonical Nm");
            yield return Click(input,"ownedCarClear");
            yield return Click(input,"ownedCarFilters");yield return CarFilterClick(input,"ownedFilterType:SUV");yield return Click(input,"dialogApply");
            Check(VisibleOwnedCards().Length==0&&!shell.Root.Q("ownedCarNoMatches").ClassListContains("hidden")&&profile.cars.Count==entries.Count,"Zero matches show a recoverable filter state without fabricating cars or emptying ownership");
            yield return Capture("car-no-matches");yield return Click(input,"ownedCarClear");
            foreach(string drive in new[] {"FWD","RWD","AWD","4×4"}) {
                var query=new OwnedCarQuery {Drive=drive};
                Check(entries.Where(query.Matches).All(e=>drive=="4×4"?e.LockedCenter:e.Drive==drive),"Drivetrain query uses applied coupling semantics: "+drive);
            }
            foreach(string status in new[] {"Ready","Needs service","Reserved","Unavailable","No fuel"}) {
                var query=new OwnedCarQuery {Status=status};Check(entries.Count(query.Matches)==entries.Count(e=>e.Status==status),"Readiness filter preserves actual availability: "+status);
            }
            foreach(var sort in Enum.GetValues(typeof(OwnedCarSort)).Cast<OwnedCarSort>())foreach(bool descending in new[] {false,true}) {
                var query=new OwnedCarQuery {Sort=sort,Descending=descending};var ordered=entries.ToList();ordered.Sort(query.Compare);
                Check(ordered.Zip(ordered.Skip(1),(a,b)=>query.Compare(a,b)<=0).All(ok=>ok),"Sort is consistent with stable ties/unavailable values: "+sort+" / "+descending);
            }
            foreach(string rank in new[] {"F","E","D","C","B","A","S"})Check(entries.Count(new OwnedCarQuery {Rank=rank}.Matches)==entries.Count(e=>e.Rating?.performanceClass==rank),"Rank filter uses authoritative completed classification: "+rank);
            var combined=new OwnedCarQuery {Drive="RWD",Type="Coupe",Minimum=new double?[] {null,100,100,null,null},Maximum=new double?[] {null,null,null,3000,100}};
            Check(entries.Where(combined.Matches).All(e=>e.Drive=="RWD"&&e.Type=="Coupe"&&e.Hp>=100&&e.Torque>=100&&e.Weight<=3000),"Category and numeric filters intersect on the same applied snapshot");
            var noRating=new OwnedCarEntry {Id="unknown",Name="Unknown",Order=100,RatingPending=true};
            var known=new OwnedCarEntry {Id="known",Name="Known",Order=0,Rating=new BenchmarkResult {performanceClass="B",points=600}};
            foreach(bool descending in new[] {false,true})Check(new OwnedCarQuery {Sort=OwnedCarSort.PP,Descending=descending}.Compare(noRating,known)>0&&!new OwnedCarQuery {Minimum=new double?[] {0,null,null,null,null}}.Matches(noRating),"Unavailable PP stays last and cannot pass a numeric bound: "+descending);
            var locked=new OwnedCarEntry {Drive="AWD",LockedCenter=true};Check(new OwnedCarQuery {Drive="AWD"}.Matches(locked)&&new OwnedCarQuery {Drive="4×4"}.Matches(locked)&&new OwnedCarQuery {Search="4x4"}.Matches(locked),"Locked-center 4×4 is an explicit searchable subset of the modeled AWD family");
            Check(new OwnedCarQuery {Sort=OwnedCarSort.Drivetrain}.Compare(locked,new OwnedCarEntry {Drive="AWD"})!=0,"Drivetrain sorting distinguishes locked-center 4×4 from other AWD setups");
            Check(shell.OwnedGridBuildCount==builds&&shell.OwnedRatingRunCount==probes,"Search/filter/sort gestures reuse cards and ratings without new inspection runs");
            Check(JsonUtility.ToJson(profile)==baseline&&System.IO.File.ReadAllText(ProfileStore.FilePath)==disk,"Search/filter/sort/cancel/clear preserve player assets, selected car and save file");
            Check(shell.Root.Q("ownedCarToolbar").Query<Button>().ToList().All(b=>b.tabIndex==-1),"New browsing controls preserve no Tab traversal");
        }
        void CarFilterToolbarLayout(int width)
        {
            var toolbar=shell.Root.Q("ownedCarToolbar");var field=shell.Root.Q("ownedCarSearch");var filters=shell.Root.Q("ownedCarFilters");var sort=shell.Root.Q("ownedCarSort");var count=shell.Root.Q("ownedCarCount");
            Check(!toolbar.ClassListContains("hidden")&&field.worldBound.xMax<=filters.worldBound.xMin+1&&filters.worldBound.xMax<=sort.worldBound.xMin+1&&count.worldBound.xMax<=toolbar.worldBound.xMax+1,"Search/filter/sort/count anchors fit without overlap at "+width);
        }
#endif
    }
}
