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
            Check(Camera.main.orthographicSize < PrototypeGame.MenuCameraSize && Camera.main.transform.position.y < 0, "Race framing enlarges the whole world and raises the road independently of the HUD");
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
            Check(hud.Root.IndexOf(hud.SteeringRim)>hud.Root.IndexOf(hud.Speed.parent),"Steering rim draws in front of both gauges");
            Check(RaceSteeringRim.Thickness>34&&RaceSteeringRim.LowerOffset>240,"Foreground wheel has a thicker black rim and lower position");
            var strip=UnityEngine.Object.FindFirstObjectByType<DragLab.Presentation.DragStripView>();
            Check(DragLab.Presentation.DragStripView.Project(8,-2).y==DragLab.Presentation.DragStripView.Project(2,-2).y,"Travel remains horizontal rather than tilting the road or cars");
            float nearTravel=DragLab.Presentation.DragStripView.Project(12,-3).x-DragLab.Presentation.DragStripView.Project(2,-3).x;
            float farTravel=DragLab.Presentation.DragStripView.Project(12,.6f).x-DragLab.Presentation.DragStripView.Project(2,.6f).x;
            Check(nearTravel>farTravel&&DragLab.Presentation.DragStripView.DepthScale(-.2f)<1,"Road texture, markings and far car share a foreshortened depth projection with faster near-ground motion");
            var meshes=strip.GetComponentsInChildren<MeshFilter>(true);
            var asphalt=meshes.First(f=>f.sharedMesh.name=="Perspective asphalt").sharedMesh;
            var roadVertices=asphalt.vertices;
            float nearWidth=(roadVertices[1]-roadVertices[0]).magnitude,farWidth=(roadVertices[49]-roadVertices[48]).magnitude;
            Check(farWidth<nearWidth*.65f&&roadVertices[48].x>roadVertices[0].x+10,"Asphalt geometry itself is a strongly narrowed and skewed trapezoid, not a rectangular mesh with different UVs");
            float markedDepth=DragLab.Presentation.DragStripView.ScreenLane(.82f)-DragLab.Presentation.DragStripView.ScreenLane(-4.8f);
            Check(markedDepth<2&&markedDepth>1.5f&&roadVertices[0].y<DragLab.Presentation.DragStripView.ScreenLane(-4.8f),"Road retains narrow ground-depth projection with extra asphalt coverage beneath the dashboard");
            Check(DragLab.Presentation.DragStripView.ScreenLane(DragLab.Presentation.DragStripView.OpponentLane)-DragLab.Presentation.DragStripView.ScreenLane(DragLab.Presentation.DragStripView.PlayerLane)<.7f,"Projected lanes move closer without flattening the upright car sprites");
            var nearCar=GameObject.Find("Player Car").GetComponent<DragLab.Presentation.LayeredCarView>();
            var farCar=GameObject.Find("Opponent Car").GetComponent<DragLab.Presentation.LayeredCarView>();
            game.PlaceRaceCars();
            var post=strip.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.sprite.name=="Start tree housing");
            var support=post.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.sprite.name=="Start tree support pole");
            var poleGround=DragLab.Presentation.DragStripView.Project(-3-strip.Travel,.8f);
            Check(Math.Abs(support.bounds.min.y-poleGround.y)<.001&&Math.Abs(support.bounds.center.x-poleGround.x)<.001,"Vertical support pole meets the exact projected ground point under the lights");
            Check(Math.Abs(post.bounds.min.y-poleGround.y-DragLab.Presentation.DragStripView.StartPoleHeight)<.001&&support.bounds.max.y>post.bounds.min.y,"Light housing is raised by the exposed pole height and its support overlaps without a gap");
            Check(post.bounds.size.y>2.5f&&post.bounds.size.x>.6f,"Enlarged start-light housing and lenses remain readable above the narrower road");
            Check(post.GetComponentsInChildren<SpriteRenderer>(true).All(r=>r.sortingOrder<farCar.GetComponentsInChildren<SpriteRenderer>(true).Min(c=>c.sortingOrder)),"Distant start-light post stays behind the car when the compressed lanes overlap");
            Check(Math.Abs(farCar.transform.localScale.x-.82f)<.001,"Rear car uses the same 82-percent scale as its projected lane");
            Check(Math.Abs(nearCar.transform.position.y-nearCar.ContactHeight-DragLab.Presentation.DragStripView.ScreenLane(DragLab.Presentation.DragStripView.PlayerLane))<.001&&Math.Abs(farCar.transform.position.y-farCar.ContactHeight*.82f-DragLab.Presentation.DragStripView.ScreenLane(DragLab.Presentation.DragStripView.OpponentLane))<.001,"Both cars are anchored by tire contacts on their depth plane rather than sprite centers");
            double oldOpponentDistance=game.Race.opponent.state.distance;
            float priorFarX=farCar.transform.position.x;game.Race.opponent.state.distance+=10;game.PlaceRaceCars();
            Check(Math.Abs(farCar.transform.position.x-priorFarX-10*.60f*.82f)<.001,"Actual rear-car displacement for ten physical metres matches its projected road scale");
            game.Race.opponent.state.distance=oldOpponentDistance;game.PlaceRaceCars();
            double oldDistance=state.distance,oldTime=game.Race.time;bool oldDq=state.disqualified;
            game.Race.time=2.1;strip.Render(game.Race);yield return Capture("race-lights-amber");
            state.distance=100;state.speed=70;strip.Render(game.Race);
            var movingPoleGround=DragLab.Presentation.DragStripView.Project(-3-strip.Travel,.8f);
            Check(Math.Abs(support.bounds.center.x-movingPoleGround.x)<.001&&Math.Abs(support.bounds.min.y-movingPoleGround.y)<.001&&support.bounds.center.x<poleGround.x,"Pole and raised lights scroll as one assembly while their foot stays on the projected road edge");
            Check(Math.Abs(strip.Travel-60)<.001&&strip.SpeedCue>.7f,"High-speed ground travel uses the shared larger distance scale and stronger near-road cues");
            Check(strip.LitLampCount==4,"Countdown illuminates both lanes' reached amber lamps");
            game.Race.time=3.1;strip.Render(game.Race);Check(strip.LitLampCount==2,"Green shows only the paired green lamps");yield return Capture("race-lights-green");
            state.disqualified=true;strip.Render(game.Race);Check(strip.LitLampCount==2,"Red light replaces green for a false start");yield return Capture("race-lights-red");
            state.disqualified=oldDq;game.Race.time=3.1;state.distance=game.Race.distance;game.Race.opponent.state.distance=game.Race.distance;
            game.PlaceRaceCars();strip.Render(game.Race);
            var finish=meshes.First(f=>f.sharedMesh.name=="Finish line").sharedMesh.vertices;
            Check(Math.Abs(finish[48].x-finish[0].x)>4&&Math.Abs(finish[49].x-finish[48].x)<Math.Abs(finish[1].x-finish[0].x)*.65f,"Finish ribbon is visibly diagonal across the road and narrows toward the far edge");
            var finishUvs=meshes.First(f=>f.sharedMesh.name=="Finish line").sharedMesh.uv;
            bool squareChecks=true;
            foreach(int sample in new[]{0,20,46})
            {
                Vector2 edge=(Vector2)(finish[sample+1]-finish[sample]),step=(Vector2)(finish[sample+2]-finish[sample]);
                var edgeUv=finishUvs[sample+1]-finishUvs[sample];var stepUv=finishUvs[sample+2]-finishUvs[sample];
                float determinant=edgeUv.x*stepUv.y-edgeUv.y*stepUv.x;
                var u=(edge*stepUv.y-step*edgeUv.y)/determinant;
                var v=(-edge*stepUv.x+step*edgeUv.x)/determinant;
                float ratio=u.magnitude/v.magnitude,skew=Math.Abs(Vector2.Dot(u.normalized,v.normalized));
                if(ratio<.85f||ratio>1.15f||skew>.15f)squareChecks=false;
            }
            Check(squareChecks,"Finish checker UVs reconstruct near-square orthogonal checks at near/middle/far depths instead of elongated diamonds");
            Check(finishUvs[48].y-finishUvs[0].y>8,"Finish checks repeat along the ribbon instead of stretching one short image over the road");
            foreach(var view in new[]{nearCar,farCar})
            {
                float lane=view==nearCar?DragLab.Presentation.DragStripView.PlayerLane:DragLab.Presentation.DragStripView.OpponentLane;
                float row=(DragLab.Presentation.DragStripView.ScreenLane(lane)-finish[0].y)/(finish[48].y-finish[0].y)*24;int lo=Mathf.FloorToInt(row),hi=Mathf.CeilToInt(row);
                float lineX=Mathf.Lerp((finish[lo*2].x+finish[lo*2+1].x)*.5f,(finish[hi*2].x+finish[hi*2+1].x)*.5f,row-lo);
                Check(Math.Abs(view.transform.position.x-lineX)<.001,"Equal physical finish distances align the actual car and skewed finish mesh in its own lane: "+view.name);
            }
            hud.Refresh();yield return Capture("race-trapezoid-finish-alignment");
            state.distance=oldDistance;game.Race.opponent.state.distance=oldOpponentDistance;game.Race.time=oldTime;state.speed=speed;game.PlaceRaceCars();strip.Render(game.Race);
            hud.Refresh();yield return Capture("race-trapezoid-start");
            var hardware=game.Race.player.tune;var car=game.Race.player;
            bool fitted=hardware.partShiftLight,adjustable=hardware.partShiftLightAdjustable,launched=state.launched;
            float threshold=hardware.shiftLightRpm;double remaining=state.shiftRemaining,oldRpm=state.rpm;int oldGearLight=state.gear;
            hardware.partShiftLight=false;state.rpm=car.spec.redlineRpm;state.launched=true;state.gear=2;state.shiftRemaining=0;hud.Refresh();
            Check(!hud.ShiftLamp.Installed&&!hud.ShiftLamp.Lit&&hud.ShiftLamp.ClassListContains("hidden"),"No shift-light hardware means no lamp or shift cue even at high RPM");
            hardware.partShiftLight=true;hardware.partShiftLightAdjustable=true;hardware.shiftLightRpm=4000;state.rpm=3900;hud.Refresh();
            Check(hud.ShiftLamp.Installed&&!hud.ShiftLamp.Lit,"Installed programmable light remains dark below its real threshold");
            state.rpm=4100;hud.Refresh();Check(hud.ShiftLamp.Lit,"Purchased light illuminates at the applied hardware threshold without adding a dial shift marker");
            state.gear=car.spec.gears.Length;hud.Refresh();Check(!hud.ShiftLamp.Lit,"Shift light does not request a nonexistent gear above top gear");
            hardware.partShiftLight=fitted;hardware.partShiftLightAdjustable=adjustable;hardware.shiftLightRpm=threshold;
            state.launched=launched;state.gear=oldGearLight;state.shiftRemaining=remaining;state.rpm=oldRpm;hud.Refresh();
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
            hud.Gear.SetState(3,manual,6,0,1);Check((hud.Gear.Knob-hud.Gear.Gate(3)).sqrMagnitude<.01,"Completed H-pattern shift settles into the correct gate");yield return Capture("race-shifter-upper-row");
            hud.Gear.SetState(2,manual,6,0,1);float exitLeft=hud.Gear.ShaftExit.x;hud.Gear.SetState(6,manual,6,0,1);
            Check(exitLeft<110&&hud.Gear.ShaftExit.x>110&&hud.Gear.ShaftExit.y==166,"Shaft exit moves within the boot around a lower pivot as the round knob changes gates");
            Check(hud.Gear.Gate(1).y==80&&hud.Gear.Gate(3).y==80&&hud.Gear.Gate(5).y==80&&hud.Gear.Gate(2).y==133&&hud.Gear.Gate(4).y==133&&hud.Gear.Gate(6).y==133,"Upper gear row is closer to the boot while lower gear positions stay unchanged");
            var timingSpec=JsonUtility.FromJson<VehicleSpec>(JsonUtility.ToJson(game.Race.player.spec));
            timingSpec.shiftSeconds=.6f;double slow=TransmissionDynamics.ShiftSeconds(manual,timingSpec,game.Race.player.tune);
            hud.Gear.SetState(2,manual,6,0,slow);hud.Gear.SetState(3,manual,6,slow-.08,slow);float slowTravel=(hud.Gear.Knob-hud.Gear.Gate(2)).magnitude;
            timingSpec.shiftSeconds=.15f;double fast=TransmissionDynamics.ShiftSeconds(manual,timingSpec,game.Race.player.tune);
            hud.Gear.SetState(2,manual,6,0,fast);hud.Gear.SetState(3,manual,6,Math.Max(0,fast-.08),fast);
            Check((hud.Gear.Knob-hud.Gear.Gate(2)).magnitude>slowTravel,"The same elapsed shift time advances a fast real gearbox farther than a slow gearbox");
            Vector2 held=hud.Gear.Knob;hud.Gear.SetState(3,manual,6,Math.Max(0,fast-.08),fast);
            Check(hud.Gear.Knob==held,"Shifter animation cannot advance when authoritative shift time is frozen");
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
                var roadEdge=Camera.main.WorldToViewportPoint(new Vector3(2,roadVertices[0].y,0));
                float roadTop=(1-roadEdge.y)*Screen.height/hud.Panel.scale;
                float dashTop=hud.Dashboard.worldBound.yMin+RaceDashboard.Top(roadEdge.x*hud.Root.worldBound.width,hud.Root.worldBound.width);
                Check(roadTop>=dashTop, "Asphalt extends beneath the dashboard without a background gap at " + width);
                Check(hud.Speed.contentRect.width<226 && hud.Rpm.contentRect.width<254, "Reduced dials retain their independent HUD sizing at " + width);
                foreach(var dial in new[]{hud.Speed,hud.Rpm})
                {
                    float radius=dial.contentRect.width*.40f;
                    var center=dial.worldBound.center;
                    bool clear=true;
                    foreach(var number in dial.Query<Label>(className:"gauge-number").ToList())
                    {
                        var b=number.worldBound;
                        foreach(var c in new[]{new Vector2(b.xMin,b.yMin),new Vector2(b.xMax,b.yMin),new Vector2(b.xMin,b.yMax),new Vector2(b.xMax,b.yMax)})
                            if((c-center).magnitude>radius+.5f)clear=false;
                    }
                    Check(clear,"All "+dial.name+" number boxes remain inside the tick marks at "+width);
                }
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
            yield return RacePauseChecks(input);
            game.ReturnToGarage();yield return null;Check(!hud.Visible,"Leaving the race removes the instrument overlay");
            game.Profile.driving.shifting=ShiftMode.Automatic;game.Profile.driving.launch=LaunchMode.Automatic;
            game.StartPractice();game.verificationInput=()=>new DriverInput{throttle=1};
            game.Race.opponent.driver.reactionSeconds=game.Race.player.driver.reactionSeconds+8;
            float finishDeadline=Time.realtimeSinceStartup+65;
            bool movingShot=false,fastShot=false,finishShot=false,playerFinishedShot=false;
            while(game.Race.phase!=RacePhase.Finished&&Time.realtimeSinceStartup<finishDeadline)
            {
                if(!finishShot&&game.Race.distance-game.Race.player.state.distance<4){finishShot=true;yield return Capture("race-trapezoid-finish-approach");}
                if(!movingShot&&game.Race.player.state.speed>=20){movingShot=true;yield return Capture("race-road-72kmh");}
                if(!fastShot&&game.Race.player.state.speed>=35){fastShot=true;yield return Capture("race-road-126kmh");}
                if(!playerFinishedShot&&game.Race.player.state.finished&&!game.Race.opponent.state.finished){
                    playerFinishedShot=true;strip.Billboard.Refresh(game.Race);
                    Check(strip.Billboard.Player.Finished&&!strip.Billboard.Opponent.Finished&&strip.Billboard.Opponent.Elapsed==""&&strip.Billboard.Opponent.Reaction=="","Player board publishes only at its finish; rival digits remain blank before its own beam");
                    Check(!game.Results.Visible,"Paper results do not announce a winner while the rival is still racing");
                    yield return Capture("race-board-player-finish");
                }
                yield return null;
            }
            game.verificationInput=null;yield return null;
            Check(game.ComparisonRoute=="Results"&&!hud.Visible&&game.Race.player.state.finished,"Finishing a real authoritative pass hides instruments and preserves the existing results workflow");
            Check(playerFinishedShot&&game.Results.Visible&&strip.Billboard.Player.Finished&&strip.Billboard.Opponent.Finished,"Both finish beams publish independently before the native timing slip opens");
            Check(string.IsNullOrEmpty(game.ResultNotice)&&game.Results.Root.Q("slipNotice").ClassListContains("hidden"),"Successful results omit redundant outcome/save-status text and its empty row");
            var focusRace=game.Race;double focusElapsed=focusRace.player.state.elapsed;int focusCash=game.Profile.cash;
            game.HandleApplicationInterruption();game.SendMessage("OnApplicationPause",true);yield return null;
            Check(!game.NativeRacePause&&game.Results.Visible&&game.Race==focusRace&&game.ComparisonRoute=="Results","Focus and application interruptions do not open pause over finished results");
            Check(game.Race.player.state.elapsed==focusElapsed&&game.Profile.cash==focusCash,"Finished interruption preserves frozen timing and cannot award twice");
            Check(strip.Billboard.Player.Elapsed==game.Race.player.state.elapsed.ToString("00.000",System.Globalization.CultureInfo.InvariantCulture)&&strip.Billboard.Opponent.Reaction==game.Race.opponent.state.reaction.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture).PadLeft(6),"Eight-segment board uses the two frozen authoritative ET/RT readings");
            Check(game.Results.Root.Q("timingSlip").worldBound.xMax<=Screen.width+1&&game.Results.Root.Q("resultAgain").worldBound.yMax<=Screen.height+1,"Paper slip and shared result actions fit the viewport");
            yield return Capture("race-gauges-results");
            yield return Click(input,game.Results.Root.Q<Foldout>("resultDetails").Q<Toggle>());
            yield return Capture("race-paper-details");
            yield return Key(input,0x77);Check(!game.Results.Visible&&game.ComparisonRoute=="Results","F8 restores the complete legacy results presentation");
            yield return Key(input,0x77);Check(game.Results.Visible,"F8 restores the paper timing slip without changing the settled race");
            var printRace=game.Race;string frozenBoard=strip.Billboard.Player.Elapsed;
            yield return new WaitForSecondsRealtime(.3f);
            Check(game.Race==printRace&&strip.Billboard.Player.Elapsed==frozenBoard,"Shutdown motion cannot change a posted finish time");
            float stopDeadline=Time.realtimeSinceStartup+40;while(game.Race.player.state.speed>.01&&Time.realtimeSinceStartup<stopDeadline)yield return null;
            Check(game.Race.player.state.speed<=.01&&game.Race.player.state.brake<=RaceSimulation.MaximumShutdownBrake+.00001,"Automatic shutdown reaches a real stop using partial braking");
            var carScreen=Camera.main.WorldToScreenPoint(game.PlayerView.transform.position);
            Check(carScreen.x<game.Results.Root.Q("timingSlip").worldBound.xMin,"Stopped car remains to the left of the fixed paper slip");
            yield return Capture("race-paper-stopped");
            foreach(int width in new[]{1024,1920}){
                Screen.SetResolution(width,width==1024?768:1080,false);yield return new WaitForSecondsRealtime(.4f);game.Results.Refresh();
                Check(game.Results.Root.Q("timingSlip").worldBound.xMax<=Screen.width+1&&game.Results.Root.Q("resultAgain").worldBound.yMax<=Screen.height+1,"Timing slip and action row stay bounded at "+width);
                var viewport=game.Results.Root.Q<ScrollView>("timingSlipScroll").contentViewport.worldBound;
                Check(game.Results.Root.Query<Label>(className:"slip-number").ToList().All(l=>l.worldBound.xMax<=viewport.xMax+1),"Both timing columns fit without right-edge clipping at "+width);
                yield return Capture("race-paper-"+width);
            }
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.4f);
            var paperTarget=new RenderTexture(3840,2160,24);paperTarget.Create();game.Results.Panel.targetTexture=paperTarget;game.Results.Refresh();yield return new WaitForSecondsRealtime(.3f);
            Check(game.Results.Panel.scale==2&&game.Results.Root.Q("resultAgain").worldBound.yMax<=1080,"Paper slip and action row use crisp shared 4K panel scaling");
            var oldTarget=RenderTexture.active;RenderTexture.active=paperTarget;var paperImage=new Texture2D(3840,2160,TextureFormat.RGB24,false);paperImage.ReadPixels(new Rect(0,0,3840,2160),0,0);paperImage.Apply();File.WriteAllBytes(Path.Combine(folder,"race-paper-4k.png"),paperImage.EncodeToPNG());RenderTexture.active=oldTarget;game.Results.Panel.targetTexture=null;paperTarget.Release();Destroy(paperTarget);Destroy(paperImage);
            var completed=game.Race;yield return Key(input,0x1B);yield return Key(input,0x52);yield return Key(input,0x47);
            Check(game.NativeRacePause&&game.Race==completed,"Results retry/garage shortcuts cannot bypass an open native pause dialog");
            yield return Click(input,"dialogResume");yield return Click(input,game.Results.Root.Q("resultGarage"));
            Check(game.ScreenName=="Garage"&&game.Race==null&&game.Profile.runCheckpoints.All(r=>r.settled),"Real results G shortcut returns to Garage and closes the worn-tire checkpoint");
            Check(Math.Abs(Camera.main.orthographicSize-PrototypeGame.MenuCameraSize)<.001 && Camera.main.transform.position.y==0, "Returning to Garage restores its original camera framing");
            Check(ProfileStore.Load().runCheckpoints.All(r=>r.settled),"Finished-race exit remains settled after durable profile reload");
            yield return ResultActionChecks(input);
        }
        IEnumerator ResultActionChecks(WindowsKeyboardProbe input)
        {
            var strip=GameObject.Find("Drag Strip").GetComponent<DragLab.Presentation.DragStripView>();
            game.Profile.driving.shifting=ShiftMode.Automatic;game.Profile.driving.launch=LaunchMode.Automatic;
            int retryCash=game.Profile.cash;game.StartPractice();game.verificationInput=()=>new DriverInput{throttle=1};
            Check(game.Race!=null,"Result action fixture stages the real car / "+game.ResultNotice);
            float retryDeadline=Time.realtimeSinceStartup+65;
            SqliteStateStore.FailureInjection=stage=>{if(stage=="before-commit"&&game.Race?.phase==RacePhase.Finished)throw new IOException("Isolated result-save failure");};
            try{
                while(game.Race.phase!=RacePhase.Finished&&Time.realtimeSinceStartup<retryDeadline)yield return null;
                yield return null;
                Check(game.Results.Visible&&game.ResultNotice.StartsWith("Result save failed")&&!game.Results.Root.Q("slipNotice").ClassListContains("hidden"),"Actual failed result save stays visible on the slip with retry retained");
                yield return Capture("race-result-save-error");
            }finally{SqliteStateStore.FailureInjection=null;}
            game.verificationInput=null;float settlementDeadline=Time.realtimeSinceStartup+3;
            while(!string.IsNullOrEmpty(game.ResultNotice)&&Time.realtimeSinceStartup<settlementDeadline)yield return null;
            yield return new WaitForEndOfFrame();Check(game.Results.Visible,"A second actual pass opens the reusable paper results");
            Check(string.IsNullOrEmpty(game.ResultNotice)&&game.Results.Root.Q("slipNotice").ClassListContains("hidden"),"Successful settlement retry clears the actionable error without adding a success message / notice="+game.ResultNotice+" / storage="+ProfileStore.Status+" / hidden="+game.Results.Root.Q("slipNotice").ClassListContains("hidden"));
            var retryRace=game.Race;var extra=game.Results.Root.Q<Foldout>("resultDetails");extra.value=true;yield return null;
            var export=game.Results.Root.Q("resultExport");game.Results.Root.Q<ScrollView>("timingSlipScroll").ScrollTo(export);yield return new WaitForSecondsRealtime(.15f);yield return Click(input,export);
            string csv=Path.Combine(Path.GetDirectoryName(ProfileStore.FilePath),"last-run.csv");
            Check(File.Exists(csv)&&File.ReadAllText(csv)==retryRace.telemetry.Csv(),"Paper telemetry action exports the exact authoritative trace");
            yield return new WaitForEndOfFrame();Check(!game.Results.Root.Q("slipNotice").ClassListContains("hidden")&&game.ResultNotice.StartsWith("Exported:"),"Explicit telemetry export feedback remains visible");
            yield return Click(input,game.Results.Root.Q("resultAgain"));
            report.AppendLine("MEASURE: retry changed="+(game.Race!=retryRace)+" / phase="+game.Race?.phase+" / visible="+game.Results.Visible+" / note="+game.ResultNotice);
            yield return Capture("race-paper-retry-outcome");
            Check(game.Race!=retryRace&&game.Race.phase==RacePhase.Countdown&&!game.Results.Visible,"Race again starts one new session and removes the old slip / "+game.ResultNotice);
            Check(game.IsPractice&&game.Profile.cash==retryCash,"Practice retry keeps its mode and cannot duplicate rewards");
            Check(!strip.Billboard.Player.Finished&&!strip.Billboard.Opponent.Finished&&strip.Billboard.Player.Elapsed==""&&strip.Billboard.Opponent.Reaction=="","A new race clears both board lanes independently of the previous winner");
            yield return Capture("race-board-retry-reset");game.ReturnToGarage();yield return null;
            Check(game.Race==null&&ProfileStore.Load().runCheckpoints.All(r=>r.settled),"Leaving the staged retry closes its durable checkpoint without replaying a payout");
        }
#endif
    }
}
