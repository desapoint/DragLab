using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        static void ShiftTouch(RaceShiftFlick flick,int finger,TouchPhase phase,Vector2 offset)
        {
            var touch=new Touch{fingerId=finger,phase=phase,position=flick.worldBound.center+offset};
            if(phase==TouchPhase.Began){using(var e=PointerDownEvent.GetPooled(touch,EventModifiers.None))flick.SendEvent(e);}
            else if(phase==TouchPhase.Moved){using(var e=PointerMoveEvent.GetPooled(touch,EventModifiers.None))flick.SendEvent(e);}
            else if(phase==TouchPhase.Canceled){using(var e=PointerCancelEvent.GetPooled(touch,EventModifiers.None))flick.SendEvent(e);}
            else {using(var e=PointerUpEvent.GetPooled(touch,EventModifiers.None))flick.SendEvent(e);}
        }
        static void ShiftSwipe(RaceShiftFlick flick,float y,float x=0)
        {
            ShiftTouch(flick,0,TouchPhase.Began,Vector2.zero);ShiftTouch(flick,0,TouchPhase.Moved,new Vector2(x,y));ShiftTouch(flick,0,TouchPhase.Ended,new Vector2(x,y));
        }
        Touch PhysicalTouch(VisualElement target,int finger,TouchPhase phase,Vector2 offset=default)
        {
            var point=(target.worldBound.center+offset)*game.RaceHud.Panel.scale;
            return new Touch{fingerId=finger,phase=phase,position=new Vector2(point.x,Screen.height-point.y)};
        }
        IEnumerator PhysicalShiftHoldChecks()
        {
            var hud=game.RaceHud;var controls=game.RaceControls;hud.ReleaseInput();controls.Clear();NativeMobile.SimulatedPhysicalRaceTouches=true;
            var gas=PhysicalTouch(hud.Pedals.Gas,0,TouchPhase.Began);hud.PollTouches(new[]{gas});gas.phase=TouchPhase.Stationary;
            Check(controls.Peek().throttle==1,"First physical finger presses and holds gas");
            PedalTouch(hud.Pedals.Gas,0,TouchPhase.Ended);
            Check(controls.Peek().throttle==1,"UI pointer releases cannot clear a still-held physical finger");
            var clutch=PhysicalTouch(hud.Pedals.Clutch,1,TouchPhase.Began);hud.PollTouches(new[]{gas,clutch});clutch.phase=TouchPhase.Stationary;
            for(int pass=0;pass<3;pass++){
                int shiftFinger=pass==2?127:2;var shift=PhysicalTouch(hud.ShiftFlick,shiftFinger,TouchPhase.Began);hud.PollTouches(new[]{gas,clutch,shift});
                shift=PhysicalTouch(hud.ShiftFlick,shiftFinger,TouchPhase.Moved,new Vector2(0,-45));hud.PollTouches(new[]{shift,gas,clutch});
                controls.Poll(false);var input=controls.Consume();
                Check(input.shiftUp&&input.throttle==1&&input.clutch==1,"Physical flick "+pass+" preserves first-finger gas and clutch despite touch order changes");
                shift.phase=TouchPhase.Ended;hud.PollTouches(new[]{gas,shift,clutch});
                for(int frame=0;frame<5;frame++){hud.PollTouches(new[]{gas,clutch});controls.Poll(false);hud.Refresh();yield return null;}
                Check(controls.Consume().throttle==1&&!controls.Consume().shiftUp,"Lifting the shifter finger leaves gas held across frames "+pass);
            }
            clutch.phase=TouchPhase.Ended;hud.PollTouches(new[]{gas,clutch});Check(controls.Peek().throttle==1&&controls.Peek().clutch==0,"Lifting only clutch leaves physical gas held");
            gas=PhysicalTouch(hud.Pedals.Gas,0,TouchPhase.Moved,new Vector2(0,-50));hud.PollTouches(new[]{gas});Check(Math.Abs(controls.Peek().throttle-.5)<.01,"Physical held pedal still supports feathering");
            gas.phase=TouchPhase.Ended;hud.PollTouches(new[]{gas});Check(controls.Peek().throttle==0,"Lifting gas releases it without a stuck throttle");
            gas=PhysicalTouch(hud.Pedals.Gas,107,TouchPhase.Began);hud.PollTouches(new[]{gas});hud.PollTouches(Array.Empty<Touch>());Check(controls.Peek().throttle==0,"Disappeared physical fingers are released even without an Ended event");
            gas=PhysicalTouch(hud.Pedals.Gas,5,TouchPhase.Began);var shiftFirst=PhysicalTouch(hud.ShiftFlick,0,TouchPhase.Began);hud.PollTouches(new[]{shiftFirst,gas});
            shiftFirst=PhysicalTouch(hud.ShiftFlick,0,TouchPhase.Moved,new Vector2(0,45));gas.phase=TouchPhase.Stationary;hud.PollTouches(new[]{shiftFirst,gas});
            Check(controls.Consume().shiftDown&&controls.Peek().throttle==1,"Shifter-first physical touch order also preserves a second gas finger");
            shiftFirst.phase=TouchPhase.Canceled;hud.PollTouches(new[]{gas,shiftFirst});Check(controls.Peek().throttle==1&&!hud.ShiftFlick.Held,"Canceling a physical shifter contact does not release gas");
            hud.ReleaseInput();Check(controls.Peek().throttle==0,"HUD interruption clears physical contacts");NativeMobile.SimulatedPhysicalRaceTouches=false;
        }
        IEnumerator PollPedalKeys(float seconds)
        {
            float until=Time.unscaledTime+seconds;
            do{game.RaceControls.Poll(false);game.RaceHud.Refresh();yield return null;}while(Time.unscaledTime<until);
        }
        IEnumerator FlickChecks(WindowsKeyboardProbe keyboard)
        {
            var driver=game.Profile.driving;driver.shifting=ShiftMode.ManualClutch;driver.launch=LaunchMode.Manual;driver.clutchAssist=driver.throttleAssist=driver.warmupBeforeRace=false;
            game.StartPractice();yield return new WaitForSecondsRealtime(.15f);game.enabled=false;
            var hud=game.RaceHud;var controls=game.RaceControls;
            yield return PollPedalKeys(.15f);yield return Capture("keyboard-pedals-released");
            Check(hud.ShiftFlick.resolvedStyle.display==DisplayStyle.None&&hud.Root.Q("raceShiftUp").resolvedStyle.display!=DisplayStyle.None,"Desktop retains its existing shift buttons without a flick hitbox");
            keyboard.Key(0x57,true);yield return PollPedalKeys(.16f);
            Check(hud.Pedals.Gas.Depression>.95f&&hud.Pedals.Brake.Depression<.05f,"Real W presses animate only the accelerator");
            keyboard.Key(0x53,true);keyboard.Key(0xA0,true);yield return PollPedalKeys(.16f);
            Check(hud.Pedals.Clutch.Depression>.95f&&hud.Pedals.Brake.Depression>.95f&&hud.Pedals.Gas.Depression>.95f,"Real W/S/Left Shift animate all three desktop pedals");yield return Capture("keyboard-pedals-pressed");
            keyboard.Key(0x45,true);yield return PollPedalKeys(.04f);keyboard.Key(0x45,false);
            Check(controls.Consume().shiftUp&&controls.Peek().throttle==1&&hud.Pedals.Gas.Depression>.95f,"Desktop shift input preserves held throttle and pedal pose");
            keyboard.Key(0xA0,false);yield return PollPedalKeys(.16f);
            Check(hud.Pedals.Clutch.Depression<.05f&&hud.Pedals.Gas.Depression>.95f,"Releasing Shift springs back only the clutch pedal");
            keyboard.Key(0x57,false);keyboard.Key(0x53,false);yield return PollPedalKeys(.16f);
            Check(hud.Pedals.Gas.Depression<.05f&&hud.Pedals.Brake.Depression<.05f,"Keyboard release restores the pedal faces");
            NativeMobile.SimulatedTouch=true;hud.Refresh();yield return null;
            Check(hud.Root.Q("raceShiftUp").resolvedStyle.display==DisplayStyle.None&&hud.Root.Q("raceShiftDown").resolvedStyle.display==DisplayStyle.None,"Mobile has no up/down shift buttons");
            Check(hud.ShiftFlick.resolvedStyle.display!=DisplayStyle.None&&!hud.ShiftFlick.focusable,"Mobile shifter receives flick gestures without focus navigation");
            Check(!DrivingPreferences.ReverseTouchShifting,"Existing preference files default to flick up for upshift");
            yield return PhysicalShiftHoldChecks();
            PedalTouch(hud.Pedals.Gas,1,TouchPhase.Began);PedalTouch(hud.Pedals.Clutch,2,TouchPhase.Began);
            ShiftTouch(hud.ShiftFlick,0,TouchPhase.Began,Vector2.zero);ShiftTouch(hud.ShiftFlick,0,TouchPhase.Moved,new Vector2(0,-40));
            var held=controls.Consume();Check(held.shiftUp&&!held.shiftDown&&held.throttle==1&&held.clutch==1,"Up flick queues upshift with two pedal fingers held");
            ShiftTouch(hud.ShiftFlick,0,TouchPhase.Moved,new Vector2(0,-100));ShiftTouch(hud.ShiftFlick,0,TouchPhase.Ended,new Vector2(0,-100));
            Check(!controls.Consume().shiftUp,"Continued movement/release cannot repeat a shift");
            ShiftSwipe(hud.ShiftFlick,45);Check(controls.Consume().shiftDown,"Down flick queues downshift");
            ShiftSwipe(hud.ShiftFlick,-10);var tiny=controls.Consume();Check(!tiny.shiftUp&&!tiny.shiftDown,"Taps and small movements do not shift");
            ShiftSwipe(hud.ShiftFlick,-5,80);var horizontal=controls.Consume();Check(!horizontal.shiftUp&&!horizontal.shiftDown,"Horizontal swipes do not shift");
            ShiftTouch(hud.ShiftFlick,0,TouchPhase.Began,Vector2.zero);ShiftTouch(hud.ShiftFlick,0,TouchPhase.Canceled,Vector2.zero);
            Check(!hud.ShiftFlick.Held&&!controls.Consume().shiftUp,"Canceled gestures release capture without shifting");
            ShiftTouch(hud.ShiftFlick,0,TouchPhase.Began,Vector2.zero);ShiftTouch(hud.ShiftFlick,6,TouchPhase.Began,Vector2.zero);ShiftTouch(hud.ShiftFlick,6,TouchPhase.Moved,new Vector2(0,-80));
            Check(!controls.Consume().shiftUp,"Extra shifter fingers cannot hijack the captured gesture");ShiftTouch(hud.ShiftFlick,6,TouchPhase.Ended,Vector2.zero);hud.ShiftFlick.Release();
            ShiftTouch(hud.ShiftFlick,0,TouchPhase.Began,Vector2.zero);game.HandleApplicationInterruption();hud.Refresh();
            Check(!hud.ShiftFlick.Held&&controls.Peek().throttle==0,"Pause/focus interruption releases flick and pedal captures");
            game.PauseRace(false);game.ReturnToGarage();game.enabled=true;yield return null;
            string profileBefore=JsonUtility.ToJson(game.Profile);
            yield return MobileTap("settingsButton");yield return MobileTap("tabControls");
            string diskBefore=File.ReadAllText(DrivingPreferences.FilePath);
            yield return MobileTap("touchShiftDirection1");
            Check(shell.SettingsDirty&&!DrivingPreferences.ReverseTouchShifting&&File.ReadAllText(DrivingPreferences.FilePath)==diskBefore,"Direction choice stays an unapplied draft");
            yield return MobileTap("cancelSettings");Check(!DrivingPreferences.ReverseTouchShifting,"Cancel discards the proposed direction");
            yield return MobileTap("settingsButton");yield return MobileTap("tabControls");yield return MobileTap("touchShiftDirection1");yield return MobileTap("applySettings");
            using(var locked=new FileStream(DrivingPreferences.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)){
                yield return MobileTap("dialogApply");Check(shell.ModalOpen&&!DrivingPreferences.ReverseTouchShifting,"Failed preference write never publishes reversed controls");
            }
            Check(PreferenceCommit.Recover(out var recovery)&&recovery==null,"Direction failure retains recoverable preferences");yield return MobileTap("modalClose");
            yield return MobileTap("applySettings");yield return MobileTap("dialogApply");
            Check(DrivingPreferences.ReverseTouchShifting&&!shell.SettingsDirty,"Confirmed Apply publishes reversed flick direction");
            DrivingPreferences.ReloadControls();Check(DrivingPreferences.ReverseTouchShifting,"Reversed controls reload from the separate preferences file");
            DrivingPreferences.SaveIfChanged(game.Profile);Check(JsonUtility.FromJson<DrivingPreferences.Data>(File.ReadAllText(DrivingPreferences.FilePath)).reverseTouchShifting,"Normal driving preference writes preserve touch direction");
            Check(PreferenceCommit.TryApply(game.Profile,SharedUnitSettings.Current,SharedAudioSettings.MasterVolume,SharedAudioSettings.OpponentVolume,out var audioError)&&DrivingPreferences.ReverseTouchShifting,"Audio/default preference commits preserve touch direction / "+audioError);
            Check(JsonUtility.ToJson(game.Profile)==profileBefore&&!profileBefore.Contains("reverseTouchShifting"),"Control direction changes neither progression nor captured simulation driver fields");
            yield return Capture("flick-controls-setting");yield return MobileTap("cancelSettings");
            game.StartPractice();yield return new WaitForSecondsRealtime(.15f);game.enabled=false;hud=game.RaceHud;controls=game.RaceControls;
            PedalTouch(hud.Pedals.Gas,1,TouchPhase.Began);PedalTouch(hud.Pedals.Clutch,2,TouchPhase.Began);
            ShiftSwipe(hud.ShiftFlick,45);var reversed=controls.Consume();Check(reversed.shiftUp&&!reversed.shiftDown,"Reversed down flick queues upshift");
            var car=game.Race.player;car.Launch(0);Check(car.Shift(1,reversed)&&car.state.gear==2,"Flick uses the real clutch-gated transmission shift");
            car.state.shiftRemaining=0;PedalTouch(hud.Pedals.Clutch,2,TouchPhase.Ended);
            ShiftSwipe(hud.ShiftFlick,-45);var noClutch=controls.Consume();Check(noClutch.shiftDown&&!car.Shift(-1,noClutch),"Reversed up flick cannot bypass the clutch requirement");
            var kind=car.transmission.type;car.transmission.type=TransmissionType.CVT;
            ShiftSwipe(hud.ShiftFlick,-45);Check(!controls.Consume().shiftDown,"CVT has no downshift from a reversed up flick");
            ShiftSwipe(hud.ShiftFlick,45);Check(controls.Consume().shiftUp,"CVT retains its existing Drive/restart input through a flick");car.transmission.type=kind;
            ShiftTouch(hud.ShiftFlick,0,TouchPhase.Began,Vector2.zero);InterfaceComparison.SetReplacement("Race",false);hud.Refresh();Check(!hud.ShiftFlick.Held,"F8 releases an in-progress flick");InterfaceComparison.SetReplacement("Race",true);hud.Refresh();
            yield return Capture("mobile-flick-shifter");game.enabled=true;game.ReturnToGarage();NativeMobile.SimulatedTouch=false;
        }
#endif
    }
}
