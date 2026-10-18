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
        static void MobilePointer(VisualElement target,TouchPhase phase,Vector2 position)
        {
            var t=new Touch{fingerId=0,phase=phase,position=position};
            if(phase==TouchPhase.Began){using(var e=PointerDownEvent.GetPooled(t,EventModifiers.None))target.SendEvent(e);}
            else if(phase==TouchPhase.Moved){using(var e=PointerMoveEvent.GetPooled(t,EventModifiers.None))target.SendEvent(e);}
            else {using(var e=PointerUpEvent.GetPooled(t,EventModifiers.None))target.SendEvent(e);}
        }
        IEnumerator MobileTap(VisualElement target)
        {
            Check(target!=null&&target.enabledInHierarchy,"Touch target exists and is enabled: "+target?.name);
            for(var p=target.parent;p!=null;p=p.parent)if(p is ScrollView view)view.ScrollTo(target);
            yield return null;
            MobilePointer(target,TouchPhase.Began,target.worldBound.center);yield return null;
            MobilePointer(target,TouchPhase.Ended,target.worldBound.center);yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator MobileTap(string id)=>MobileTap(shell.Root.Q(id));
        IEnumerator MobileAccessChecks(WindowsKeyboardProbe mouse)
        {
            NativeMobile.SimulatedTouch=true;Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);shell.Refresh();
            Check(shell.Visible&&VisibleDealerCards().Length==3,"Touch fresh game reaches all fixed starter choices");
            yield return MobileTap(shell.Root.Q("dealerCar:starter:starter-hatch"));
            yield return MobileTap("dealerBuy");yield return MobileTap("dialogConfirm");
            Check(game.Profile.cars.Count==1&&!game.Profile.firstPurchasePending,"Starter purchase completes using touch events alone");
            yield return MobileTap("renameOwnedCar");
            var name=shell.Root.Q<TextField>("ownedCarName");
            Check(name!=null,"Owned car naming has an editable on-screen text field");
            name.value="Phone test";yield return MobileTap("dialogSave");
            Check(shell.Root.Q<Label>("ownedDetailTitle").text=="Phone test","Touch name confirmation saves the car label");
            yield return Capture("mobile-owned");
            var plot=shell.Root.Query<NativeDynoPlot>().ToList().First(p=>p.resolvedStyle.display!=DisplayStyle.None);
            var point=plot.worldBound.center;MobilePointer(plot,TouchPhase.Began,point);yield return null;
            Check(plot.HoverRpm.HasValue,"Touch press reveals dyno point values without hover");
            MobilePointer(plot,TouchPhase.Moved,point+new Vector2(20,0));MobilePointer(plot,TouchPhase.Ended,point+new Vector2(20,0));yield return null;
            Check(plot.HoverRpm.HasValue,"Dyno readings remain available after lifting a finger");
            yield return MobileTap("maintenanceFuel");var quantity=shell.Root.Q<FloatField>("maintenanceQuantity");quantity.value=1;
            yield return MobileTap("dialogConfirm");Check(!shell.ModalOpen,"Touch fuel purchase confirms and closes");
            foreach(var size in new[]{new Vector2Int(854,480),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2400,1080)}){
                Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.3f);
                NativeMobile.SimulatedSafeArea=new Rect(48,24,Screen.width-80,Screen.height-36);shell.Refresh();yield return null;
                var b=shell.Root.worldBound;var scale=shell.Panel.scale;
                Check(Math.Abs(b.xMin*scale-48)<2&&Math.Abs(b.yMax*scale-(Screen.height-24))<2,"Menu clears simulated notch/gesture insets at "+size);
                foreach(var id in new[]{"navGarage","navBuild","navTuning","navRace","navBusiness","navData","settingsButton"}){
                    var button=shell.Root.Q(id).worldBound;Check(button.xMin>=b.xMin&&button.xMax<=b.xMax+1,"Mobile navigation stays reachable: "+size+" / "+id);
                }
                yield return Capture("mobile-"+size.x);
            }
            NativeMobile.SimulatedSafeArea=null;Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);
            yield return MobileTap("navBuild");Check(game.ScreenName=="Parts","Touch opens parts shop");
            yield return MobileTap("partsFilters");Check(shell.ModalOpen,"Touch opens shop filters");yield return MobileTap("dialogCancel");
            var part=shell.Root.Query<Button>().ToList().First(b=>b.name!=null&&b.name.StartsWith("partCard:")&&!b.ClassListContains("hidden"));
            yield return MobileTap(part);yield return MobileTap("partAdd");yield return MobileTap("dialogAddtocart");
            Check(game.CartCount>0,"Touch purchase dialog reserves a part in the cart");
            game.SuspendForMobile();var reloaded=ProfileStore.Load();
            Check(reloaded.inventory.cart.lines.Count==game.CartLines.Count&&reloaded.cash==game.Profile.cash,"Mobile backgrounding durably retains cart reservations and cash");
            yield return MobileTap("navTuning");var range=shell.Root.Q<NativeTuningRange>("tuneRange:launch");
            float baseline=range.Number.value;yield return MobileTap("fineUp:launch");
            Check(Math.Abs(range.Number.value-baseline-range.FineStep)<.01&&game.DraftDirty,"Touch fine step edits only the tuning draft");
            yield return MobileTap("resetRange:launch");Check(Math.Abs(range.Number.value-baseline)<.01,"Touch reset replaces the middle-click shortcut");
            Check(range.Number.keyboardType==TouchScreenKeyboardType.NumbersAndPunctuation&&!range.Number.hideMobileInput,"Precision numeric fields request an editable mobile keyboard");
            range.Allow(false);Check(!range.Q("fineUp:launch").enabledInHierarchy,"Touch helpers respect installed hardware tuning permissions");range.Allow(true);
            yield return Capture("mobile-tuning");
            var view=shell.Root.Q<ScrollView>("tuningAreaEngine");var content=view.contentContainer;Vector2 drag=view.contentViewport.worldBound.center;
            float scrollBefore=view.scrollOffset.y;MobilePointer(content,TouchPhase.Began,drag);yield return null;
            for(int i=1;i<=4;i++){MobilePointer(content,TouchPhase.Moved,drag-new Vector2(0,30*i));yield return null;}
            MobilePointer(content,TouchPhase.Ended,drag-new Vector2(0,120));yield return new WaitForSecondsRealtime(.1f);
            Check(view.scrollOffset.y>scrollBefore,"Tuning lists scroll with a finger drag without a mouse wheel");
            yield return MobileTap("settingsButton");yield return MobileTap("tabSound");
            float volume=SharedAudioSettings.MasterVolume;var slider=shell.Root.Q<Slider>("masterVolume");var dragger=slider.Q(className:"unity-base-slider__drag-container");
            var a=dragger.worldBound;MobilePointer(dragger,TouchPhase.Began,new Vector2(a.xMin+a.width*.8f,a.center.y));yield return null;
            MobilePointer(dragger,TouchPhase.Moved,new Vector2(a.xMin+a.width*.7f,a.center.y));yield return null;
            MobilePointer(dragger,TouchPhase.Ended,new Vector2(a.xMin+a.width*.7f,a.center.y));yield return null;
            Check(Math.Abs(slider.value-volume)>.01,"Shared sliders accept touch drag input");
            Check(shell.SettingsDirty&&SharedAudioSettings.MasterVolume==volume,"Touch settings remain unapplied drafts");
            yield return MobileTap("applySettings");yield return MobileTap("dialogApply");
            Check(!shell.SettingsDirty&&Math.Abs(SharedAudioSettings.MasterVolume-slider.value)<.001,"Touch Apply confirmation persists settings");
            yield return MobileTap("cancelSettings");Check(game.ScreenName=="Tuning","Cancel returns to the previous menu on touch");
            yield return MobileTap("navBusiness");Check(!shell.Visible&&!InterfaceComparison.IsReplacement("Business"),"Unfinished Business replacement falls back to the working touchable page");
            shell.TouchBack();yield return new WaitForSecondsRealtime(.2f);Check(shell.Visible&&game.ScreenName=="Garage","Android Back can leave the legacy business page");
            yield return MobileTap("navData");Check(!shell.Visible&&!InterfaceComparison.IsReplacement("Stats"),"Unfinished Data page also remains accessible through its legacy presentation");
            shell.TouchBack();yield return new WaitForSecondsRealtime(.2f);
            yield return MobileTap("touchCompare");Check(!shell.Visible&&!InterfaceComparison.IsReplacement("Garage"),"Footer touch action exposes legacy presentation without F8");
            float compareScale=NativeMobile.Scale(Screen.width,Screen.height);
            int compareX=(int)(Screen.width-90*compareScale-8),compareY=(int)(4+22*compareScale);
            mouse.MouseButtonAt(compareX,compareY,true);yield return null;yield return new WaitForSecondsRealtime(.08f);
            mouse.MouseButtonAt(compareX,compareY,false);yield return new WaitForSecondsRealtime(.2f);
            Check(shell.Visible&&InterfaceComparison.IsReplacement("Garage"),"Legacy comparison button returns on a complete press/release gesture");
            game.Profile.driving.launch=LaunchMode.Manual;game.Profile.driving.shifting=ShiftMode.ManualClutch;game.Profile.driving.clutchAssist=false;game.Profile.driving.warmupBeforeRace=true;yield return MobileTap("navRace");yield return MobileTap("raceMode:Practice");yield return MobileTap("raceStage");yield return MobileTap("dialogStage");
            Check(game.ScreenName=="Race"&&game.RaceHud.Visible,"Touch staging enters the authoritative physical race");
            game.enabled=false;var hud=game.RaceHud;
            foreach(int width in new[]{854,1280,1920}){
                Screen.SetResolution(width,width==854?480:width*9/16,false);yield return new WaitForSecondsRealtime(.2f);
                NativeMobile.SimulatedSafeArea=new Rect(48,24,Screen.width-80,Screen.height-36);hud.Refresh();yield return null;
                var pedals=hud.Pedals.worldBound;var dials=hud.Root.Q("raceDials").worldBound;var gear=hud.Gear.worldBound;
                yield return Capture("mobile-race-"+width);
                Check(dials.xMax<=pedals.xMin&&pedals.xMax<=gear.xMin,"Required three-pedal set clears gauges and shifter at mobile width "+width+" / "+dials+" / "+pedals+" / "+gear);
                Check(hud.Root.Q("racePause").worldBound.yMin*hud.Panel.scale>=12,"Race pause clears top safe inset at "+width);
            }
            NativeMobile.SimulatedSafeArea=null;Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.2f);
            yield return MobileTap(hud.Root.Q("raceLineLock"));Check(game.Race.player.state.warmupLineLock,"Touch warm-up line lock uses the existing simulation state");
            yield return MobileTap(hud.Root.Q("raceReady"));Check(!game.Race.warmup,"Touch Ready stages the countdown without Enter");
            PedalTouch(hud.Pedals.Gas,1,TouchPhase.Began);PedalTouch(hud.Pedals.Brake,2,TouchPhase.Began);
            PedalTouch(hud.Pedals.Clutch,3,TouchPhase.Began);Check(game.RaceControls.Peek().throttle==1&&game.RaceControls.Peek().brake==1&&game.RaceControls.Peek().clutch==1,"Touch gas and brake operate together at mobile scale");
            ShiftSwipe(hud.ShiftFlick,-45);yield return null;Check(game.RaceControls.Peek().throttle==1,"Touch shift leaves the accelerator held");
            yield return MobileTap(hud.Root.Q("racePause"));Check(game.NativeRacePause&&game.RaceControls.Peek().throttle==0,"Touch pause clears held pedals");yield return Capture("mobile-pause");
            yield return MobileTap("pauseSound");Check(shell.Root.Q("tuneRange:pauseMaster")!=null,"Touch pause exposes contained sound controls");yield return MobileTap("dialogBack");
            yield return MobileTap("pauseQuit");yield return MobileTap("dialogQuitrace");
            Check(game.ScreenName=="Garage"&&MechanicsOwnership.Validate(game.Profile,game.Profile.catalog)==null,"Touch quit retains a valid saved ownership state");
            game.enabled=true;NativeMobile.SimulatedTouch=false;
        }
#endif
    }
}
