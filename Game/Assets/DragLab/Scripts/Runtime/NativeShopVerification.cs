using System;
using System.Collections;
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
        void ShopCacheChecks()
        {
            NativeShopDomainChecks.Run(game.vehicles.ToArray(),Check);
        }
        IEnumerator ShopStabilityChecks(WindowsKeyboardProbe input)
        {
            ShopCacheChecks();var model=shell.PartBrowser;int evaluated=model.EvaluationCount,queries=shell.PartQueryApplyCount,cards=shell.PartCardBuildCount;string state=JsonUtility.ToJson(game.Profile);
            var visible=shell.Root.Q("partsGrid").Children().Where(e=>!e.ClassListContains("hidden")).ToArray();var rects=visible.Select(e=>e.worldBound).ToArray();yield return new WaitForSecondsRealtime(.7f);
            Check(evaluated==0&&model.EvaluationCount==evaluated&&shell.PartQueryApplyCount==queries&&shell.PartCardBuildCount==cards,"Ordinary shop browsing performs no full-part dynos, query churn or card creation while idle");
            Check(visible.Select((e,i)=>e.worldBound==rects[i]).All(v=>v),"Visible shop cards keep their order and position after the page opens");Check(visible.Length<model.Entries.Count/2,"The shop builds only matching card controls rather than the whole foreign catalogue");
            report.AppendLine("Shop idle: "+visible.Length+" rendered cards / "+model.Entries.Count+" offers / "+model.EvaluationCount+" full comparisons / "+shell.PartCacheHits+" cache hits");
            yield return Click(input,"partsTab3");yield return WaitParts();var tire=shell.PartBrowser.Entries.First(e=>e.Source=="New"&&e.Definition.id==game.Profile.catalog.Id(game.Profile.SelectedCar.vehicleId,PartCategory.Tires,1));var card=shell.Root.Q("partCard:"+tire.Id);shell.Root.Q<ScrollView>("partsScroll").ScrollTo(card);yield return new WaitForSecondsRealtime(.15f);Check(card.Query<Label>().ToList().Any(l=>l.text.Contains("Grip factor"))&&card.Query<Label>().ToList().Any(l=>l.text.Contains("Reference lifespan")),"Tire cards expose grip and reference lifespan immediately");yield return Capture("shop-tire-specs");yield return Click(input,card.name);Check(shell.Root.Q("partKeySpecs").Query<Label>().ToList().Any(l=>l.text=="Reference lifespan"),"Part details reuse the category-specific tire summary");yield return Click(input,"partAdd");yield return PartDialogClick(input,"partPurchaseDestination:Install");Check(shell.Root.Q<Button>("partPurchaseMode:Individual").enabledSelf,"Single-tire buying remains accessible when a different compound was marked Install");yield return PartDialogClick(input,"partPurchaseMode:Individual");Check(shell.Root.Q<Button>("partPurchaseDestination:Install").ClassListContains("selected")&&!shell.Root.Q<Button>("dialogAddtocart").enabledSelf,"A different compatible compound keeps Install and requires explicit corners");yield return PartDialogClick(input,"partPurchaseDestination:Store");Check(shell.Root.Q<Button>("dialogAddtocart").enabledSelf,"Single tire storage remains available without a fitted position");yield return Capture("single-tire-purchase");yield return Click(input,"dialogAddtocart");Check(game.CartLines.Single().action==CartAction.NewUnits&&game.CartLines.Single().role=="tire"&&game.CartLines.Single().quantity==1&&!game.CartLines.Single().install,"One loose tire stages as one unit at its actual unit price");game.ClearNativeCart();yield return MixedTirePurchaseChecks(input,tire);yield return Click(input,"partBack");var rim=shell.PartBrowser.Entries.First(e=>e.Source=="New"&&e.Definition.id==game.Profile.catalog.Id(game.Profile.SelectedCar.vehicleId,PartCategory.Wheels,1));var rimCard=shell.Root.Q("partCard:"+rim.Id);shell.Root.Q<ScrollView>("partsScroll").ScrollTo(rimCard);yield return new WaitForSecondsRealtime(.15f);yield return Click(input,rimCard.name);yield return MixedTirePurchaseChecks(input,rim);yield return Click(input,"partBack");yield return Click(input,"partsTab0");yield return WaitParts();Check(state==JsonUtility.ToJson(game.Profile),"Shop browsing, summaries and tire staging do not change the real car or resources");
        }
#endif
    }
}
