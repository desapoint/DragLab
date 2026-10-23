using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        static void PedalTouch(RaceTouchPedal pedal,int finger,TouchPhase phase,float rise=0)
        {
            var touch=new Touch{fingerId=finger,phase=phase,position=pedal.worldBound.center+new Vector2(0,-rise)};
            if(phase==TouchPhase.Began){using(var e=PointerDownEvent.GetPooled(touch,EventModifiers.None))pedal.SendEvent(e);}
            else if(phase==TouchPhase.Moved){using(var e=PointerMoveEvent.GetPooled(touch,EventModifiers.None))pedal.SendEvent(e);}
            else if(phase==TouchPhase.Canceled){using(var e=PointerCancelEvent.GetPooled(touch,EventModifiers.None))pedal.SendEvent(e);}
            else {using(var e=PointerUpEvent.GetPooled(touch,EventModifiers.None))pedal.SendEvent(e);}
        }
        IEnumerator TouchPedalChecks(WindowsKeyboardProbe mouse)
        {
            var driver=game.Profile.driving;driver.shifting=ShiftMode.ManualClutch;driver.launch=LaunchMode.Manual;
            driver.clutchAssist=false;driver.throttleAssist=false;driver.warmupBeforeRace=false;
            game.StartPractice();yield return new WaitForSecondsRealtime(.15f);game.enabled=false;
            var hud=game.RaceHud;var controls=game.RaceControls;var car=game.Race.player;driver=car.driver;hud.Refresh();yield return null;
            yield return Capture("pedals-neutral");
            Check(hud.Visible&&hud.Pedals.Clutch.resolvedStyle.display!=DisplayStyle.None,"Required manual clutch appears beside brake and accelerator");
            foreach(var type in new[]{TransmissionType.DCT,TransmissionType.Automatic,TransmissionType.CVT}){
                var old=car.transmission.type;car.transmission.type=type;hud.Refresh();yield return null;
                Check(hud.Pedals.Clutch.resolvedStyle.display==DisplayStyle.None,type+" hides the friction-clutch pedal despite manual driver preferences");car.transmission.type=old;
            }
            driver.launch=LaunchMode.Assisted;driver.clutchAssist=true;hud.Refresh();yield return null;
            Check(hud.Pedals.Clutch.resolvedStyle.display==DisplayStyle.None,"Clutch assistance removes an unnecessary clutch pedal");
            driver.launch=LaunchMode.Manual;hud.Refresh();yield return null;
            Check(hud.Pedals.Clutch.resolvedStyle.display!=DisplayStyle.None,"Manual launch still exposes its required clutch with shift assistance enabled");driver.clutchAssist=false;driver.launch=LaunchMode.Assisted;driver.shifting=ShiftMode.Sequential;hud.Refresh();yield return null;
            Check(hud.Pedals.Clutch.resolvedStyle.display==DisplayStyle.None,"Sequential driving with assisted launch does not require a clutch pedal");
            driver.launch=LaunchMode.Manual;driver.shifting=ShiftMode.ManualClutch;hud.Refresh();yield return null;
            controls.Clear();PedalTouch(hud.Pedals.Gas,1,TouchPhase.Began);PedalTouch(hud.Pedals.Clutch,2,TouchPhase.Began);
            Check(controls.Peek().throttle==1&&controls.Peek().clutch==1,"Two independent touch pointers hold gas and clutch simultaneously");
            PedalTouch(hud.Pedals.Brake,3,TouchPhase.Began);Check(controls.Peek().throttle==1&&controls.Peek().clutch==1&&controls.Peek().brake==1,"Three fingers can operate all pedals independently");
            PedalTouch(hud.Pedals.Brake,3,TouchPhase.Canceled);
            controls.Poll(false);for(int i=0;i<8;i++)Check(controls.Consume().throttle==1&&controls.Peek().clutch==1,"Native contacts survive keyboard polling and repeated fixed ticks / "+i);
            hud.Refresh();yield return new WaitForSecondsRealtime(.16f);
            Check(hud.Pedals.Gas.Depression>.9f&&hud.Pedals.Clutch.Depression>.9f,"Both pedal faces animate down while held");yield return Capture("pedals-both-pressed");
            PedalTouch(hud.Pedals.Clutch,2,TouchPhase.Moved,50);
            Check(Math.Abs(controls.Peek().clutch-.5)<.01&&controls.Peek().throttle==1,"Sliding a held clutch upward feathers it without interrupting gas");
            yield return new WaitForSecondsRealtime(.12f);yield return Capture("pedals-half-clutch");
            PedalTouch(hud.Pedals.Clutch,2,TouchPhase.Ended);
            Check(controls.Peek().clutch==0&&controls.Peek().throttle==1,"Lifting one finger releases only its pedal");
            PedalTouch(hud.Pedals.Brake,2,TouchPhase.Began);Check(controls.Peek().brake==1&&controls.Peek().throttle==1,"Brake and accelerator have independent contacts");
            PedalTouch(hud.Pedals.Brake,2,TouchPhase.Canceled);Check(controls.Peek().brake==0&&controls.Peek().throttle==1,"Touch cancellation clears only the canceled contact");
            PedalTouch(hud.Pedals.Clutch,2,TouchPhase.Began);
            NativeMobile.SimulatedTouch=true;hud.Refresh();yield return null;
            ShiftSwipe(hud.ShiftFlick,-45);
            NativeMobile.SimulatedTouch=false;hud.Refresh();yield return null;
            var drive=controls.Consume();Check(drive.shiftUp&&drive.throttle==1&&drive.clutch==1,"Touch shifter queues one upshift without releasing held pedals");
            Check(!controls.Consume().shiftUp,"Touch shift is consumed once");car.Launch(0);Check(car.Shift(1,drive)&&car.state.gear==2,"Real manual transmission accepts the touch clutch input for its shift gate");
            PedalTouch(hud.Pedals.Gas,1,TouchPhase.Canceled);PedalTouch(hud.Pedals.Clutch,2,TouchPhase.Canceled);
            controls.BeginFrame(1,0,0);PedalTouch(hud.Pedals.Gas,1,TouchPhase.Began);PedalTouch(hud.Pedals.Gas,1,TouchPhase.Ended);
            Check(controls.Peek().throttle==1,"Releasing touch gas does not release an independently held keyboard throttle");controls.BeginFrame(0,0,0);
            var hit=hud.Pedals.Gas.worldBound;mouse.MouseButtonAt((int)hit.center.x,(int)hit.center.y,true);yield return new WaitForSecondsRealtime(.16f);
            Check(controls.Peek().throttle==1&&hud.Pedals.Gas.Depression>.9f,"Real mouse press uses the same captured pedal and animation");yield return Capture("pedals-mouse-down");
            mouse.MouseButtonAt(20,100,false);yield return new WaitForSecondsRealtime(.16f);Check(controls.Peek().throttle==0&&hud.Pedals.Gas.Depression<.1f,"Release outside the pedal springs back and cannot leave gas stuck");
            PedalTouch(hud.Pedals.Gas,1,TouchPhase.Began);InterfaceComparison.SetReplacement("Race",false);hud.Refresh();
            Check(controls.Peek().throttle==0&&!hud.Visible,"F8 presentation change releases native contacts");InterfaceComparison.SetReplacement("Race",true);hud.Refresh();
            PedalTouch(hud.Pedals.Gas,1,TouchPhase.Began);game.HandleApplicationInterruption();hud.Refresh();
            Check(controls.Peek().throttle==0&&controls.Peek().clutch==0&&controls.Peek().brake==0,"Application interruption clears every held pedal before pause");
            game.PauseRace(false);yield return null;
            foreach(int width in new[]{960,1280,1920}){
                Screen.SetResolution(width,width==960?540:width*9/16,false);yield return new WaitForSecondsRealtime(.35f);hud.Refresh();yield return null;
                var clutch=hud.Pedals.Clutch.worldBound;var brake=hud.Pedals.Brake.worldBound;var gas=hud.Pedals.Gas.worldBound;
                Check(hud.Rpm.worldBound.xMax<clutch.xMin&&clutch.xMax<brake.xMin&&brake.xMax<gas.xMin&&gas.xMax<hud.Gear.worldBound.xMin,"Pedal set sits left of shifter without overlapping gauges at "+width);
                Check(clutch.width>=72&&clutch.height>=150&&gas.yMax<=hud.Root.worldBound.yMax,"Pedal hit targets remain large and inside the screen at "+width);
                Check(hud.Pedals.Gas.focusable==false&&!hud.ShiftFlick.focusable&&hud.Root.Q("raceShiftUp")==null,"Driving controls never introduce Tab navigation at "+width);
                yield return Capture("pedals-"+width);
            }
            hud.enabled=false;hud.Root.AddToClassList("touch-wide");yield return null;Check(hud.Pedals.Gas.worldBound.width>=112,"High-resolution phone layout offers wider touch targets");hud.Root.RemoveFromClassList("touch-wide");hud.enabled=true;
            Screen.SetResolution(1280,720,false);game.enabled=true;game.ReturnToGarage();
        }
#endif
    }
}
