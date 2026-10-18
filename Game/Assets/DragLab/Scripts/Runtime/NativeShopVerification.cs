using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        Button PartCard(string id)=>shell.ScrollToPart(id.StartsWith("partCard:",StringComparison.Ordinal)?id.Substring("partCard:".Length):id);
        static object ClipMember(object owner,string name)
        {
            if(owner==null)return null;
            for(var type=owner.GetType();type!=null;type=type.BaseType){
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly;
                var field=type.GetField(name,flags);if(field!=null)return field.GetValue(owner);
                var property=type.GetProperty(name,flags);if(property!=null)return property.GetValue(owner);
            }
            return null;
        }
        int OwnedClipCount()
        {
            int count=0,inspected=0;
            foreach(var element in shell.Root.panel.visualTree.Query<VisualElement>().ToList()){
                var data=ClipMember(element,"renderData")??ClipMember(element,"renderChainData");
                var allocation=ClipMember(data,"clipRectID");if(allocation==null)continue;inspected++;
                if(ClipMember(allocation,"ownedState")?.ToString()=="Owned")count++;
            }
            return inspected==0?-1:count;
        }
        IEnumerator ClipBudgetChecks(WindowsKeyboardProbe input)
        {
            string profile=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            foreach(bool touch in new[]{false,true}){
                NativeMobile.SimulatedTouch=touch;shell.Refresh();yield return Click(input,"navBuild");yield return new WaitForSecondsRealtime(.2f);
                for(int category=0;category<5;category++){
                    yield return Click(input,"partsTab"+category);yield return Click(input,"partsFilters");
                    yield return PartDialogClick(input,"partCompatibility:All");yield return PartDialogClick(input,"partStockStatus:All");yield return Click(input,"dialogApply");yield return new WaitForSecondsRealtime(.3f);
                    var grid=shell.Root.Q("partsGrid");int cards=grid.Query<Button>(className:"part-card").ToList().Count,clips=OwnedClipCount();
                    report.AppendLine("Clip budget "+(touch?"touch":"desktop")+" category "+category+": cards="+cards+" clips="+clips+" / "+shell.Root.Q<Label>("partsCount").text);
                    if(category==0)yield return Capture(touch?"clip-budget-touch":"clip-budget-desktop");
                    Check(cards<=64,"Only a bounded viewport of cards is attached for the complete catalogue");
                    Check(clips>=0&&clips<4096,"Actual panel-owned clip allocations stay below half the shader limit");
                    var scroll=shell.Root.Q<ScrollView>("partsScroll");float end=scroll.verticalScroller.highValue;
                    var expected=shell.PartBrowser.Entries.Where(e=>NativePartBrowser.Section(e.Definition.category)==(NativePartSection)category&&shell.PartQuery.Matches(e,game.Profile.cash)).ToList();expected.Sort(shell.PartQuery.Compare);
                    foreach(float fraction in new[]{.5f,1f,0f}){
                        scroll.scrollOffset=new Vector2(0,end*fraction);yield return new WaitForSecondsRealtime(.2f);
                        var shown=grid.Query<Button>(className:"part-card").ToList();
                        Check(shown.Count<=64&&OwnedClipCount()<4096,"Scroll recycles clipped controls at "+fraction);
                        Check(shown.Select(c=>c.name).SequenceEqual(expected.Skip(shell.PartWindowFirst).Take(shown.Count).Select(e=>"partCard:"+e.Id)),"Window retains exact sorted product identities at "+fraction);
                        if(fraction==1&&expected.Count>0)Check(shown.Last().name=="partCard:"+expected.Last().Id,"The final product remains reachable through native scrolling / first="+shell.PartWindowFirst+" shown="+shown.Count+" total="+expected.Count+" offset="+scroll.scrollOffset.y+" cached end="+end+" current end="+scroll.verticalScroller.highValue+" grid="+grid.layout.height);
                    }
                }
                yield return Click(input,"navGarage");yield return new WaitForSecondsRealtime(.2f);
                Check(shell.Root.Q("partsGrid").childCount==0,"Leaving Build releases its materialized card controls");
            }
            Check(profile==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Large-catalogue rendering does not change or save player state");
            Check(errors==0,"Catalogue tours and touch rendering produce no allocator errors");
        }
        IEnumerator ShopStabilityChecks(WindowsKeyboardProbe input)
        {
            var model=shell.PartBrowser;int evaluated=model.EvaluationCount,queries=shell.PartQueryApplyCount,cards=shell.PartCardBuildCount;string state=JsonUtility.ToJson(game.Profile);
            var visible=shell.Root.Q("partsGrid").Query<Button>(className:"part-card").ToList().Where(e=>!e.ClassListContains("hidden")).ToArray();var rects=visible.Select(e=>e.worldBound).ToArray();yield return new WaitForSecondsRealtime(.7f);
            Check(evaluated==0&&model.EvaluationCount==evaluated&&shell.PartQueryApplyCount==queries&&shell.PartCardBuildCount==cards,"Ordinary shop browsing performs no full-part dynos, query churn or card creation while idle");
            Check(visible.Select((e,i)=>e.worldBound==rects[i]).All(v=>v),"Visible shop cards keep their order and position after the page opens");Check(visible.Length<model.Entries.Count/2,"The shop builds only matching card controls rather than the whole foreign catalogue");
            report.AppendLine("Shop idle: "+visible.Length+" rendered cards / "+model.Entries.Count+" offers / "+model.EvaluationCount+" full comparisons / "+shell.PartCacheHits+" cache hits");
            yield return Click(input,"partsTab3");yield return WaitParts();var tire=shell.PartBrowser.Entries.First(e=>e.Source=="New"&&e.Definition.id==game.Profile.catalog.Id(game.Profile.SelectedCar.vehicleId,PartCategory.Tires,1));var card=PartCard("partCard:"+tire.Id);shell.Root.Q<ScrollView>("partsScroll").ScrollTo(card);yield return new WaitForSecondsRealtime(.15f);Check(card.Query<Label>().ToList().Any(l=>l.text.Contains("Grip factor"))&&card.Query<Label>().ToList().Any(l=>l.text.Contains("Reference lifespan")),"Tire cards expose grip and reference lifespan immediately");yield return Capture("shop-tire-specs");yield return Click(input,card.name);Check(shell.Root.Q("partKeySpecs").Query<Label>().ToList().Any(l=>l.text=="Reference lifespan"),"Part details reuse the category-specific tire summary");yield return Click(input,"partAdd");yield return PartDialogClick(input,"partPurchaseDestination:Install");Check(shell.Root.Q<Button>("partPurchaseMode:Individual").enabledSelf,"Single-tire buying remains accessible when a different compound was marked Install");yield return PartDialogClick(input,"partPurchaseMode:Individual");Check(shell.Root.Q<Button>("partPurchaseDestination:Install").ClassListContains("selected")&&!shell.Root.Q<Button>("dialogAddtocart").enabledSelf,"A different compatible compound keeps Install and requires explicit corners");yield return PartDialogClick(input,"partPurchaseDestination:Store");Check(shell.Root.Q<Button>("dialogAddtocart").enabledSelf,"Single tire storage remains available without a fitted position");yield return Capture("single-tire-purchase");yield return Click(input,"dialogAddtocart");yield return null;yield return new WaitForSecondsRealtime(.2f);Check(game.CartCount==1,"Native tire confirmation stages its line / modal "+shell.ModalOpen+" / count "+game.CartCount);Check(game.CartLines.Single().action==CartAction.NewUnits&&game.CartLines.Single().role=="tire"&&game.CartLines.Single().quantity==1&&!game.CartLines.Single().install,"One loose tire stages as one unit at its actual unit price");game.ClearNativeCart();yield return MixedTirePurchaseChecks(input,tire);yield return Click(input,"partBack");var rim=shell.PartBrowser.Entries.First(e=>e.Source=="New"&&e.Definition.id==game.Profile.catalog.Id(game.Profile.SelectedCar.vehicleId,PartCategory.Wheels,1));var rimCard=PartCard("partCard:"+rim.Id);shell.Root.Q<ScrollView>("partsScroll").ScrollTo(rimCard);yield return new WaitForSecondsRealtime(.15f);yield return Click(input,rimCard.name);yield return MixedTirePurchaseChecks(input,rim);yield return Click(input,"partBack");yield return Click(input,"partsTab0");yield return WaitParts();Check(state==JsonUtility.ToJson(game.Profile),"Shop browsing, summaries and tire staging do not change the real car or resources");
        }
#endif
    }
}
