using System;
using System.Collections;
using System.IO;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator RaceHudChecks(WindowsKeyboardProbe input)
        {
            game.Profile.driving.shifting = ShiftMode.Sequential;
            game.Profile.driving.launch = LaunchMode.Assisted;
            game.Profile.driving.throttleAssist = false;
            game.Profile.driving.warmupBeforeRace = false;
            game.StartPractice(); yield return null; yield return new WaitForSecondsRealtime(.1f);
            var hud = game.RaceHud; var state = game.Race.player.state;
            Check(hud.Visible && !shell.Visible, "Native instruments open over the track without the full-screen menu/navigation");
            Check(hud.Gear.Readout == "N" && state.rpm > 0, "Staged car shows neutral and its actual running idle");
            game.enabled = false;
            double rpm = state.rpm, speed = state.speed; int gear = state.gear;
            string player = JsonUtility.ToJson(game.Profile); byte[] disk = File.ReadAllBytes(ProfileStore.FilePath);
            state.rpm = 4321; state.speed = 100 / 3.6; state.gear = 3; hud.Refresh();
            Check(hud.Rpm.Readout == "4321" && hud.Speed.Readout == "100" && hud.Gear.Readout == "3", "Snapshot instruments display exact authoritative RPM, road speed and engaged gear");
            BuildLimits.Capture(game.Race.player, out double safe, out _, out _, out _);
            Check(Math.Abs(hud.Rpm.Scale.Redline - Math.Min(safe, game.Race.player.spec.redlineRpm)) < .01, "Red band starts at the lowest safe hardware/spec redline, separately from shift/cut settings");
            Check(hud.Rpm.Scale.SweepDegrees < 360 && hud.Rpm.Scale.Maximum > game.Race.player.spec.redlineRpm, "Analog scale has a partial-circle sweep and headroom above redline");
            Check(!hud.Root.Query<Label>().ToList().Any(l => l.text.Contains("Shift") || l.text.Contains("target")), "New RPM dial contains no suggested shift marker or target");
            yield return new WaitForSecondsRealtime(.2f);
            int faces = hud.Rpm.FacePaints, needles = hud.Rpm.NeedlePaints;
            state.rpm = 4488; hud.Refresh(); yield return new WaitForSecondsRealtime(.2f);
            Check(hud.Rpm.FacePaints == faces && hud.Rpm.NeedlePaints > needles, "Changing RPM repaints the needle without rebuilding the fixed dial geometry");
            state.rpm = 4321; hud.Refresh();
            bool failedEngine=state.engineFailed,failedDrive=state.drivetrainFailed,failedBrakes=state.brakesFailed;
            state.engineFailed=state.drivetrainFailed=state.brakesFailed=true;hud.Refresh();
            string faults=hud.Root.Q<Label>("raceWarning").text;
            Check(faults.Contains("ENGINE FAILED")&&faults.Contains("NO DRIVE PATH")&&faults.Contains("BRAKE FAILURE"),"Concurrent essential engine, driveline and brake warnings remain visible together");
            state.engineFailed=failedEngine;state.drivetrainFailed=failedDrive;state.brakesFailed=failedBrakes;hud.Refresh();
            foreach (var kind in new[] { TransmissionType.Manual, TransmissionType.DCT, TransmissionType.Automatic, TransmissionType.CVT })
            {
                hud.Gear.SetState(0,kind); Check(hud.Gear.Readout == "N", kind + " neutral remains neutral");
                hud.Gear.SetState(2,kind); Check(hud.Gear.Readout == (kind == TransmissionType.CVT ? "D" : "2"), kind + " gear display does not invent CVT stepped gears");
            }
            hud.enabled=false;
            var manual=new TransmissionSpec{type=TransmissionType.Manual,manualLayout=ManualGearboxLayout.HPattern};
            hud.Gear.SetState(2,manual,6,0,1);hud.Gear.SetState(3,manual,6,.5,1);
            Check(hud.Gear.HPattern&&Math.Abs(hud.Gear.Knob.y-92)<.01&&hud.Gear.Knob.x>hud.Gear.Gate(2).x&&hud.Gear.Knob.x<hud.Gear.Gate(3).x,"H-pattern shift travels through the neutral crossgate using real handover progress");
            yield return Capture("race-dashboard-h-shift");
            hud.Gear.SetState(3,manual,6,0,1);Check((hud.Gear.Knob-hud.Gear.Gate(3)).sqrMagnitude<.01,"Completed H-pattern shift settles into the correct gate");
            foreach(var kind in new[]{TransmissionType.Manual,TransmissionType.DCT,TransmissionType.Automatic,TransmissionType.CVT})
            {hud.Gear.SetState(2,new TransmissionSpec{type=kind,manualLayout=ManualGearboxLayout.Sequential},6,.1,.3);Check(!hud.Gear.HPattern,"Non-H hardware does not draw a manual H gate: "+kind);yield return Capture("race-dashboard-"+kind);}
            hud.enabled=true;hud.Refresh();
            Check(RaceDashboard.Top(260,1280)<RaceDashboard.Top(720,1280)-15,"Dashboard has a shallow instrument-cluster rise rather than a rectangular top");
            var preferences = SharedUnitSettings.Current; var originalUnit = preferences.speed;
            foreach (SpeedUnit unit in Enum.GetValues(typeof(SpeedUnit)))
            {
                preferences.speed = unit; hud.Refresh();
                Check(hud.Speed.Readout == DisplayUnits.Speed(state.speed,unit).ToString("F0") && hud.Speed.Scale.Unit == DisplayUnits.SpeedLabel(unit), "Dial and exact readout share applied " + unit + " units");
            }
            preferences.speed = originalUnit; hud.Refresh();
            var originalScale = hud.Rpm.Scale;
            hud.Rpm.Configure(new RaceGaugeScale(originalScale.Maximum,1000,1000,"RPM",originalScale.Redline,180,180));
            Check(Math.Abs(hud.Rpm.Scale.Angle(0)-180)<.01 && Math.Abs(hud.Rpm.Scale.Angle(originalScale.Maximum)-360)<.01, "The same gauge renderer accepts a semicircular scale without changing telemetry");
            yield return Capture("race-gauges-semicircle"); hud.Rpm.Configure(originalScale);
            yield return Capture("race-gauges-1280");
            foreach (int width in new[] { 1024, 1920 })
            {
                Screen.SetResolution(width,width==1024?768:1080,false);yield return new WaitForSecondsRealtime(.4f);hud.Refresh();
                Check(hud.Speed.worldBound.xMax < hud.Rpm.worldBound.xMin && hud.Rpm.worldBound.xMax < hud.Gear.worldBound.xMin, "Left dials and right shifter stay separated at " + width);
                float pedalTop = (Screen.height - 91 * RaceInputAdapter.Scale(Screen.width,Screen.height)) / hud.Panel.scale;
                Check(hud.Dashboard.worldBound.yMax <= hud.Root.worldBound.yMax && hud.Rpm.worldBound.yMin < hud.Dashboard.worldBound.yMin && hud.Gear.worldBound.yMax <= hud.Root.worldBound.yMax, "Low dashboard stays bounded with the instrument cluster overflowing at " + width);
                yield return Capture("race-gauges-"+width);
            }
            var target = new RenderTexture(3840,2160,24);target.Create();hud.Panel.targetTexture=target;hud.Refresh();yield return new WaitForSecondsRealtime(.3f);
            Check(hud.Panel.scale==2 && hud.Rpm.worldBound.xMax<hud.Gear.worldBound.xMin,"True 4K target uses sharp vector geometry and shared 2× logical scaling");
            var active=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(3840,2160,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"race-gauges-4k.png"),image.EncodeToPNG());RenderTexture.active=active;hud.Panel.targetTexture=null;target.Release();Destroy(target);Destroy(image);
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);
            Check(player==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Rendering, themes/scales, units and resizing never change assets or save the profile");
            state.rpm=rpm;state.speed=speed;state.gear=gear;game.enabled=true;
            yield return Key(input,0x77);Check(!hud.Visible,"F8 restores the original whole race interface");yield return Key(input,0x77);Check(hud.Visible,"F8 returns to the instruments on the same live race");
            input.Key(0x57,true);yield return new WaitForSecondsRealtime(.4f);
            Check(game.Race.player.state.throttle>.9 && hud.Rpm.Value>game.Race.player.spec.idleRpm,"Held W revs the staged authoritative engine and updates the live dial");
            while(game.Race.time<3.1)yield return null;
            yield return Key(input,0x45);Check(game.Race.player.state.launched && game.Race.player.state.gear>=1,"E engages and launches from neutral with the new HUD");
            yield return new WaitForSecondsRealtime(.5f);yield return Key(input,0x45);
            Check(game.Race.player.state.throttle>.9 && game.Race.player.state.rpm>0,"Keyboard shifting preserves held throttle and rotating engine RPM");
            while(game.Race.player.state.shiftRemaining>0)yield return null;
            yield return new WaitForSecondsRealtime(.05f);
            float scale=RaceInputAdapter.Scale(Screen.width,Screen.height);var rect=RaceInputAdapter.ControlRect(RaceInputAdapter.UpControl,Screen.width/scale,Screen.height/scale);
            int oldGear=game.Race.player.state.gear;input.MouseButtonAt((int)(rect.center.x*scale),(int)(rect.center.y*scale),true);yield return new WaitForSecondsRealtime(.1f);input.MouseButtonAt((int)(rect.center.x*scale),(int)(rect.center.y*scale),false);yield return new WaitForSecondsRealtime(.2f);
            Check(game.Race.player.state.gear==oldGear && game.Race.player.state.throttle>.9,"Hidden legacy shift buttons have no invisible hit targets; held keyboard throttle remains live");
            input.Key(0x57,false);yield return Capture("race-gauges-driving");
            yield return Key(input,0x1B);Check(!hud.Visible && game.ComparisonRoute=="Pause","Pause hides new instruments and retains the existing pause workflow");yield return Key(input,0x1B);Check(hud.Visible,"Resuming restores instruments without replacing the race session");
            game.ReturnToGarage();yield return null;Check(!hud.Visible,"Leaving the race removes the instrument overlay");
            game.Profile.driving.shifting=ShiftMode.Automatic;game.Profile.driving.launch=LaunchMode.Automatic;
            game.StartPractice();game.verificationInput=()=>new DriverInput{throttle=1};
            float finishDeadline=Time.realtimeSinceStartup+65;
            while(game.Race.phase!=RacePhase.Finished&&Time.realtimeSinceStartup<finishDeadline)yield return null;
            game.verificationInput=null;yield return null;
            Check(game.ComparisonRoute=="Results"&&!hud.Visible&&game.Race.player.state.finished,"Finishing a real authoritative pass hides instruments and preserves the existing results workflow");
            yield return Capture("race-gauges-results");game.ReturnToGarage();
        }
#endif
    }
}
