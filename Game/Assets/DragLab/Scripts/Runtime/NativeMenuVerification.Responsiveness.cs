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
            var p=game.Profile;p.cash=1000000;p.reputation=1000;p.revision++;Check(ProfileStore.TrySave(p),"UI audit uses a durable isolated profile");string state=JsonUtility.ToJson(p);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            int first=Time.frameCount;yield return Click(input,"navBuild");yield return WaitParts();yield return new WaitForSecondsRealtime(.5f);report.AppendLine("UI segment shop entry frames "+first+"–"+Time.frameCount);
            int builds=shell.PartBrowserBuildCount,queries=shell.PartQueryApplyCount,cards=shell.PartCardBuildCount;yield return new WaitForSecondsRealtime(.5f);Check(builds==shell.PartBrowserBuildCount&&queries==shell.PartQueryApplyCount&&cards==shell.PartCardBuildCount,"Idle shop performs no rebuild, requery or card creation");
            first=Time.frameCount;var search=shell.Root.Q<TextField>("partsSearch");search.value="piston";yield return null;search.value="";yield return null;report.AppendLine("UI segment search frames "+first+"–"+Time.frameCount);
            var offer=shell.PartBrowser.Entries.First(e=>e.Source=="New"&&e.Compatibility=="Compatible"&&e.Definition.category==PartCategory.HeadIntake&&e.Definition.stage==1);var card=shell.Root.Q("partCard:"+offer.Id);shell.Root.Q<ScrollView>("partsScroll").ScrollTo(card);yield return new WaitForSecondsRealtime(.1f);
            first=Time.frameCount;yield return Click(input,card);yield return new WaitForSecondsRealtime(.2f);Check(shell.Root.Q("partDetailDyno")!=null,"Part details retain actual dyno and comparison");report.AppendLine("UI segment part details frames "+first+"–"+Time.frameCount);yield return Click(input,"partBack");
            Check(game.StageNativePurchase(offer.Definition.id,null,1,true),"Audit stages one fitting cart installation");first=Time.frameCount;yield return Click(input,"partsCart");yield return new WaitForSecondsRealtime(.2f);Check(shell.Root.Q("cartDyno")!=null,"Combined cart retains authoritative preview");report.AppendLine("UI segment cart frames "+first+"–"+Time.frameCount);yield return Click(input,"cartBack");game.ClearNativeCart();
            first=Time.frameCount;yield return Click(input,"navTuning");yield return new WaitForSecondsRealtime(.2f);var number=shell.Root.Q<FloatField>("tuneNumber:launch");number.value+=100;yield return null;yield return Click(input,"tuningRevert");report.AppendLine("UI segment tuning frames "+first+"–"+Time.frameCount);
            first=Time.frameCount;yield return Click(input,"settingsButton");yield return new WaitForSecondsRealtime(.2f);report.AppendLine("UI segment settings frames "+first+"–"+Time.frameCount);yield return Capture("responsiveness-settings");yield return Click(input,"cancelSettings");
            Check(RacingSurface.GeometryCacheHits>50,"Repeated native controls reuse identical geometry without changing hover or live state");
            Check(state==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Browsing, searching, cart staging and reverted tuning do not change or save player assets");
        }
#endif
    }
}
