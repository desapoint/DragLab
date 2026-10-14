using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator ResponsivenessChecks(WindowsKeyboardProbe input)
        {
            if(!shell.Visible)yield return Key(input,0x77);
            var p=game.Profile;p.cash=1000000;p.reputation=1000;p.revision++;Check(ProfileStore.TrySave(p),"UI audit uses a durable isolated profile");string state=JsonUtility.ToJson(p);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            int first=Time.frameCount;yield return Click(input,"navBuild");yield return WaitParts();yield return new WaitForSecondsRealtime(.5f);report.AppendLine("UI segment shop entry frames "+first+"–"+Time.frameCount);
            int builds=shell.PartBrowserBuildCount,queries=shell.PartQueryApplyCount,cards=shell.PartCardBuildCount;yield return new WaitForSecondsRealtime(.5f);Check(builds==shell.PartBrowserBuildCount&&queries==shell.PartQueryApplyCount&&cards==shell.PartCardBuildCount,"Idle shop performs no rebuild, requery or card creation");
            first=Time.frameCount;var search=shell.Root.Q<TextField>("partsSearch");search.value="piston";yield return null;search.value="";yield return null;report.AppendLine("UI segment search frames "+first+"–"+Time.frameCount);
            var offer=shell.PartBrowser.Entries.First(e=>e.Source=="New"&&e.Compatibility=="Compatible"&&e.Definition.category==PartCategory.HeadIntake&&e.Definition.stage==1);var card=shell.Root.Q("partCard:"+offer.Id);shell.Root.Q<ScrollView>("partsScroll").ScrollTo(card);yield return new WaitForSecondsRealtime(.1f);
            first=Time.frameCount;yield return Click(input,card);yield return new WaitForSecondsRealtime(.2f);Check(shell.Root.Q("partDetailDyno")!=null,"Part details retain actual dyno and comparison");report.AppendLine("UI segment part details frames "+first+"–"+Time.frameCount);yield return Click(input,"partBack");
            Check(game.StageNativePurchase(offer.Definition.id,null,1,true),"Audit stages one fitting cart installation");first=Time.frameCount;yield return Click(input,"partsCart");yield return new WaitForSecondsRealtime(.2f);Check(shell.Root.Q("cartDyno")!=null,"Combined cart retains authoritative preview");report.AppendLine("UI segment cart frames "+first+"–"+Time.frameCount);yield return Click(input,"cartBack");game.ClearNativeCart();
            first=Time.frameCount;yield return Click(input,"navTuning");yield return new WaitForSecondsRealtime(.2f);var number=shell.Root.Q<FloatField>("tuneNumber:launch");number.value+=100;yield return null;yield return Click(input,"tuningRevert");report.AppendLine("UI segment tuning frames "+first+"–"+Time.frameCount);
            first=Time.frameCount;yield return Click(input,"settingsButton");yield return new WaitForSecondsRealtime(.2f);report.AppendLine("UI segment settings frames "+first+"–"+Time.frameCount);foreach(var id in new[]{"fuelUsage","fluidDepletion","fluidAging","conditionEffects"}){Check(shell.Root.Q<Button>(id+"0").Q<Label>().text==(id=="conditionEffects"?"Assisted":"None")&&shell.Root.Q<Button>(id+"2").Q<Label>().text==(id=="conditionEffects"?"Full":"Realistic"),id+" orders assisted left to full simulation right");}
            Check(shell.Root.Q<Slider>("guaranteedFittingParts").inverted,"Market guarantee slider becomes harder toward the right");
            yield return RevealClick(input,"fuelUsage0");Check(shell.DraftDriver.fuelUsage==DragLab.Domain.FuelUsageMode.None,"Left fuel choice maps to None without changing saved enum values");yield return RevealClick(input,"fuelUsage2");Check(shell.DraftDriver.fuelUsage==DragLab.Domain.FuelUsageMode.Realistic,"Right fuel choice maps to realistic consumption");yield return RevealClick(input,"conditionEffects2");Check(shell.DraftDriver.conditionEffects==ConditionEffectMode.Full,"Right condition effects maps to Full");yield return RevealClick(input,"serviceAssist1");Check(shell.DraftDriver.serviceAssist==ServiceAssist.Manual,"Manual fluid service is the harder right choice");
            yield return Capture("responsiveness-settings");yield return Click(input,"cancelSettings");
            Check(RacingSurface.GeometryCacheHits>50,"Repeated native controls reuse identical geometry without changing hover or live state");
            Check(state==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Browsing, searching, cart staging and reverted tuning do not change or save player assets");
            game.StageNativePurchase(offer.Definition.id,null,1,false);int expected=game.NativeCartTotal(),cash=game.Profile.cash;var quote=game.QuoteNativeCart();var timer=System.Diagnostics.Stopwatch.StartNew();
            ProfileStore.SaveMeasurement=(phase,ms)=>report.AppendLine("UI durable action "+phase+" ms "+ms.ToString("F2"));MechanicsTransactions.Measurement=(phase,ms)=>report.AppendLine("UI transaction "+phase+" ms "+ms.ToString("F2"));bool committed;try{committed=game.CommitNativeCart(quote);}finally{ProfileStore.SaveMeasurement=null;MechanicsTransactions.Measurement=null;}
            report.AppendLine("UI durable transaction total ms "+timer.Elapsed.TotalMilliseconds.ToString("F2"));Check(committed&&game.Profile.cash==cash-expected,"Real runtime purchase publishes once after the durable save succeeds");Check(ProfileStore.Load().cash==game.Profile.cash,"Compressed runtime save reload matches the committed balance");
        }
#endif
    }
}
