using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator WaitForInspection()
        {
            yield return new WaitForEndOfFrame();yield return null;
            float deadline=Time.realtimeSinceStartup+15;
            while(game.InspectionBusy&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!game.InspectionBusy,"Latest copied-car probe completes before deadline");
        }
        IEnumerator ShopRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log=new StringBuilder("Shop/cart/inventory, wear graph, definitive saves and opponent audio / actual Windows mouse / isolated profile.\n");
            Action<bool,string> require=(ok,label)=>{Check(ok,label);log.AppendLine("PASS: "+label);};
            var p=game.Profile;var car=p.SelectedCar;p.cash=100000;p.reputation=100;
            var original=p.parts.Find(i=>i.instanceId==car.installed.slots.Find(s=>s.slot=="HeadIntake").partInstanceId);original.condition.health=.7f;string originalId=original.instanceId;
            var rings=p.parts.Find(i=>i.instanceId==car.installed.slots.Find(s=>s.slot=="PistonRings").partInstanceId);foreach(var e in rings.condition.elements){e.health=.65f;e.fault=ElementFault.CompressionLeak;e.severity=.2f;}
            ProfileStore.Save(p);game.ReturnToGarage();yield return WaitForInspection();
            require(game.HealthyInspection!=null&&game.HealthyInspection.peakHp>game.SavedInspection.peakHp,"Current wear/faults show a higher-power no-wear reference without repairing the car");
            yield return Capture("current-versus-no-wear");
            string bytes=File.ReadAllText(ProfileStore.FilePath,Encoding.UTF8);long revision=p.revision;int count=p.parts.Count,cash=p.cash;
            string head=p.catalog.Id(car.vehicleId,PartCategory.HeadIntake,1),cooling=p.catalog.Id(car.vehicleId,PartCategory.Cooling,1);
            using(var mouse=new WindowsKeyboardProbe()) {
                FramePacingProbe.Phase="shop-cart-clicks";
                game.ShowParts(PartCategory.HeadIntake);game.SelectPartsPage("Shop");yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse,"cart-fit:"+head);yield return ClickPart(mouse,"cart-plus:"+head);
                game.ShowParts(PartCategory.Cooling);yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"cart-fit:"+cooling);yield return WaitForInspection();
                require(game.CartCount==3&&game.ProposedInspection!=null&&game.ProposedInspection.source.build.spec.fittedParts.Any(d=>d.id==head)&&game.ProposedInspection.source.build.spec.fittedParts.Any(d=>d.id==cooling),"Mouse cart changes aggregate installed hardware across categories and quantities");
                require(game.Profile.revision==revision&&game.Profile.cash==cash&&game.Profile.parts.Count==count&&File.ReadAllText(ProfileStore.FilePath,Encoding.UTF8)==bytes,"Adding, quantities and cumulative previews never save, spend or change the ledger");
                double expectedPower=game.ProposedInspection.peakHp;yield return Capture("cumulative-cart-stats");
                yield return ClickPart(mouse,"cart-checkout");yield return WaitForInspection();
                require(game.CartCount==0&&game.Profile.parts.Count==count+3&&game.Profile.cash<cash&&game.Profile.economyReceipts.Last().operation=="Parts cart"&&game.SavedInspection.peakHp==expectedPower,"Actual checkout durably buys all entries and matches cumulative dyno");
                var retained=game.Profile.parts.Find(i=>i.instanceId==originalId);
                require(retained.location==PartLocation.Inventory&&retained.condition.health==.7f,"Removed original remains in inventory with its existing wear");
                yield return ClickPart(mouse,"parts-page:Inventory");game.ShowParts(PartCategory.HeadIntake);yield return new WaitForSecondsRealtime(.2f);
                require(game.PartsPage=="Inventory"&&!game.partRects.ContainsKey("cart-add:"+head),"Inventory is a distinct ownership view, without catalog purchase rows");
                var spare=game.Profile.parts.First(i=>i.definitionId==head&&i.location==PartLocation.Inventory);
                yield return ClickPart(mouse,"preview-item:"+spare.instanceId);yield return Capture("inventory-retained-spares");
                yield return ClickPart(mouse,"install:"+spare.instanceId);
                require(game.Profile.SelectedCar.installed.slots.Any(s=>s.partInstanceId==spare.instanceId)&&ProfileStore.Load().parts.Any(i=>i.instanceId==spare.instanceId&&i.location!=PartLocation.Inventory),"Actual inventory installation reuses and saves the selected identity");
                FramePacingProbe.Phase="tuning-slider-preview";game.OpenTuning();yield return new WaitForSecondsRealtime(.2f);
                bytes=File.ReadAllText(ProfileStore.FilePath,Encoding.UTF8);
                yield return MoveTuneSlider(mouse,"radius",.65f);yield return MoveTuneSlider(mouse,"radius",.3f);
                require(game.DraftDirty&&File.ReadAllText(ProfileStore.FilePath,Encoding.UTF8)==bytes,"Tuning gestures update previews without a full save");game.ReturnToGarage();
                FramePacingProbe.Phase="driving-setting-held-gesture";game.ShowDrivingSettings();yield return new WaitForSecondsRealtime(.2f);
                game.ScrollToPart("driving-clutch");yield return new WaitForSecondsRealtime(.2f);
                var rect=game.partRects["driving-clutch"];int x=(int)(rect.x+rect.width*.65f),y=(int)rect.center.y;
                bytes=File.ReadAllText(ProfileStore.FilePath,Encoding.UTF8);mouse.MouseButtonAt(x,y,true);yield return new WaitForSecondsRealtime(1.2f);
                require(File.ReadAllText(ProfileStore.FilePath,Encoding.UTF8)==bytes,"Holding a settings slider beyond the old save delay does not write a full save");
                mouse.MouseButtonAt(x,y,false);yield return new WaitForSecondsRealtime(.3f);
                require(Math.Abs(ProfileStore.Load().driving.clutchReleaseSeconds-game.settings.clutchReleaseSeconds)<1e-6&&File.ReadAllText(ProfileStore.FilePath,Encoding.UTF8)!=bytes,"Completed settings gesture saves its final value");
                game.OpenAudioSettings();yield return new WaitForSecondsRealtime(.2f);
                rect=AudioSettingsView.VerificationOpponentSliderRect;x=(int)(rect.x+rect.width*.3f);y=(int)rect.center.y;
                mouse.MouseButtonAt(x,y,true);yield return new WaitForSecondsRealtime(.08f);mouse.MouseButtonAt(x,y,false);yield return new WaitForSecondsRealtime(.2f);
                require(SharedAudioSettings.OpponentVolume>.2&&SharedAudioSettings.OpponentVolume<.4,"Actual opponent volume slider changes the shared preference");
                float opponentVolume=SharedAudioSettings.OpponentVolume;SharedAudioSettings.Reload();require(Math.Abs(SharedAudioSettings.OpponentVolume-opponentVolume)<1e-6,"Opponent slider final value survives reload");
                FramePacingProbe.Phase="opponent-audio-race";game.ReturnToGarage();game.StartPractice();yield return new WaitForSecondsRealtime(.8f);
                require(game.OpponentAudio.Ready&&game.OpponentAudio.OutputRms>1e-5&&game.OpponentAudio.CurrentVolume<game.Audio.CurrentVolume,"Opponent has audible independent engine output with distance/volume attenuation");
                SharedAudioSettings.SetOpponentVolume(0);yield return new WaitForSecondsRealtime(.8f);
                require(game.OpponentAudio.OutputRms<1e-5&&game.Audio.OutputRms>1e-5,"Opponent mute silences its engine while player audio remains audible");
                SharedAudioSettings.SetOpponentVolume(1);yield return new WaitForSecondsRealtime(.3f);game.PauseRace(true);yield return new WaitForSecondsRealtime(.2f);
                require(game.OpponentAudio.CurrentVolume==0&&game.Audio.CurrentVolume==0,"Pause silences both cars");game.ReturnToGarage();
            }
            require(errors==0,"Shop, background inspection, settings and opponent DSP have no runtime/GUI errors");
            File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),log+"Runtime errors: "+errors+"\n",new UTF8Encoding(false));
#else
            yield return null;throw new InvalidOperationException("Shop probe requires Windows development player.");
#endif
        }
    }
}
