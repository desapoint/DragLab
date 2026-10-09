using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    public sealed class DrivingBenchRuntimeVerification : MonoBehaviour
    {
        PhysicalDrivingBench scene;
        string output;
        int errors;
        float started;
        public static bool Active { get { return Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-driving-verify") >= 0; } }
        public static void AttachIfRequested(PhysicalDrivingBench target)
        {
            if (!Active) return; var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--draglab-driving-verify");
            var probe = target.gameObject.AddComponent<DrivingBenchRuntimeVerification>(); probe.scene = target; probe.output = Path.GetFullPath(args[index + 1]);
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output); started = Time.realtimeSinceStartup;
            Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception) errors++; };
            var routines = new Stack<IEnumerator>(); routines.Push(Run());
            while (routines.Count > 0)
            {
                var routine = routines.Peek(); bool more;
                try { more = routine.MoveNext(); }
                catch (Exception exception) { Debug.LogError(exception); File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), "FAIL: " + exception + "\n"); Application.Quit(1); yield break; }
                if (!more) { routines.Pop(); continue; }
                if (routine.Current is IEnumerator nested) routines.Push(nested); else yield return routine.Current;
            }
            Application.Quit(0);
        }
        void Update() { if (started > 0 && Time.realtimeSinceStartup - started > 150) { File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), "FAIL: Physical bench timeout."); Application.Quit(2); } }
        IEnumerator Run()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var report = new System.Text.StringBuilder("Physical driving bench / actual Update, OnGUI, DSP and OS input.\n");
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); report.AppendLine("PASS: " + label); };
            string sentinelPath = Path.Combine(output, "profile-v1.json"); string sentinel = "Player save sentinel: physical bench must never read or write this."; File.WriteAllText(sentinelPath, sentinel);
            var cars = Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v => v.spec.id).ToArray(); scene.SelectVehicle(Array.FindIndex(cars, v => v.spec.id == "starter-hatch"));
            yield return new WaitForSecondsRealtime(.3f);
            check(!scene.Recording && scene.Session.snapshot.inputs.Count == 0 && scene.Session.Tick > 0, "Recording defaults off while the shared simulation runs normally.");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-driving-failure") >= 0)
            {
                using (var input = new WindowsKeyboardProbe())
                {
                    yield return Click(input, "money");
                    float until = Time.realtimeSinceStartup + 65;
                    while (!scene.Paused && Time.realtimeSinceStartup < until) yield return null;
                    check(scene.Paused && scene.Session.car.state.engineFailed, "Actual money-shift button runs ordinary controls through destructive clutch back-drive to engine failure.");
                    check(scene.Session.car.transmission.type == TransmissionType.Manual && scene.Session.samples.Any(s => s.signedClutchSlipRpm < -500) && scene.Session.samples.Any(s => s.engineDamage > 0 && s.throttle == 0), "Diagnostics capture negative synchronization mismatch and mechanical damage with gas released.");
                    yield return Capture("physical-engine-failure-diagnostics");
                    string original = TimedCsv(scene.Session); scene.Export(output);
                    yield return Click(input, "replay"); until = Time.realtimeSinceStartup + 65;
                    while (!scene.Paused && Time.realtimeSinceStartup < until) yield return null;
                    check(scene.Paused && original == TimedCsv(scene.Session), "Live destructive replay repeats every sample including failure.");
                    yield return Click(input, "reset");
                    check(!scene.Session.car.state.engineFailed && scene.Session.car.state.gear == 0, "Reset replaces only the isolated failed engine and returns to neutral idle.");
                    scene.SetRecording(false); scene.SetPaused(true);
                    int wear = scene.Session.snapshot.components.FindIndex(p => p.slot == "Clutch");
                    check(scene.SetStartingCondition(wear, .35f, 300), "Paused bench accepts per-part starting clutch health and heat.");
                    check(scene.Session.car.mechanics.parts.Find(p => p.slot == "Clutch").condition.health < .36 && scene.Session.car.state.clutchCapacityFactor < .5, "Assigned worn/hot clutch reduces actual shared capacity.");
                    yield return Click(input, "wear-reset");
                    check(!scene.Recording && scene.Session.car.mechanics.parts.All(p => p.condition.health == 1 && !p.condition.failed), "Wear reset repairs copied parts independently of recording.");
                    scene.SetRecording(true);
                    // Check actual live rollover plumbing without waiting ten minutes;
                    // the domain gate separately exercises the complete 72,000-tick limit.
                    input.Key(0x57, true); yield return new WaitForSecondsRealtime(.5f);
                    var previous = scene.Session; long total = previous.TotalTick; double rpm = previous.car.state.rpm;
                    scene.ContinueLiveRecording();
                    check(scene.Session.TotalTick == total && Math.Abs(scene.Session.car.state.rpm - rpm) < 1e-9 && scene.KeyboardOwned && !scene.Paused, "Live segment rollover preserves exact RPM, continuous time and keyboard ownership.");
                    yield return new WaitForSecondsRealtime(.15f); input.Key(0x57, false); scene.EndRun();
                    check(scene.Session.TotalTick > total && scene.Session.snapshot.hasStartingDynamics && TimedCsv(scene.Session) == PhysicalDrivingSession.Replay(scene.Session.snapshot).Csv(), "Continued live inputs append after checkpoint and replay independently.");
                    scene.Export(output); string continuation = Directory.GetFiles(output, "*.json").Where(p => p != sentinelPath && !p.EndsWith("audio-settings.json") && !p.EndsWith("unit-settings.json")).OrderByDescending(p => p).First();
                    check(scene.Import(continuation), "Continuation JSON imports its captured crank/drivetrain state.");
                    yield return new WaitForSecondsRealtime(.4f);
                    check(scene.Paused && scene.Session.snapshot.hasStartingDynamics, "Imported continuation replays to its captured end.");
                    yield return Click(input, "reset");
                    check(scene.Session.snapshot.startingTick == 0 && !scene.Session.snapshot.hasStartingDynamics && scene.Session.car.state.gear == 0, "Reset after continuation replay clears checkpoint and restarts time at neutral idle.");
                }
                check(File.ReadAllText(sentinelPath) == sentinel && errors == 0, "Destructive bench/scenario/reset/export have no player-save changes or runtime errors.");
                File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), report + "Runtime errors: " + errors + "\n"); yield break;
            }
            check(Math.Abs(scene.Session.car.state.rpm - scene.Session.car.spec.idleRpm) < scene.Session.car.spec.idleRpm * .05 && scene.Session.car.state.gear == 0 && scene.Audio.OutputRms > .00001, "Standalone physical bench starts at audible neutral idle without loading a race/save.");
            using (var input = new WindowsKeyboardProbe())
            {
                input.Key(0x57, true); yield return new WaitForSecondsRealtime(.4f);
                check(scene.KeyboardOwned && scene.Session.car.state.throttle == 1 && scene.Session.car.state.rpm > 1200 && scene.Session.car.state.speed == 0, "Held OS W revs with staging brakes held and takes control from sliders.");
                yield return Click(input,"graph-Energy");check(scene.GraphLabel=="Body kJ"&&scene.KeyboardOwned&&scene.Session.car.state.throttle==1,"Actual graph group click selects measured energy without changing held pedal ownership.");
                yield return Click(input,"graph-Wheel torque");check(scene.GraphLabel=="FL drive Nm"&&scene.KeyboardOwned,"Actual graph click exposes per-wheel torque while keyboard remains authoritative.");
                yield return Capture("physical-live-wheel-torque");
                scene.SelectVehicle(Array.FindIndex(cars, v => v.spec.id == "utility-pickup")); yield return new WaitForSecondsRealtime(.15f);
                check(scene.KeyboardOwned && scene.Session.car.spec.id == "utility-pickup" && scene.Session.car.state.throttle == 1, "Physical test hardware reset preserves keyboard mode and held W.");
                yield return Click(input, "seed");
                check(scene.KeyboardOwned && scene.Session.car.state.throttle == 1, "Editing the independent scenario seed preserves keyboard mode and held W.");
                scene.SelectVehicle(Array.FindIndex(cars, v => v.spec.id == "starter-hatch")); yield return new WaitForSecondsRealtime(.4f);
                check(scene.AppliedInput.clutch == 0 && scene.AppliedInput.brake == 0 && scene.Session.car.state.brake == 1, "Live slider feedback reflects requested pedals separately from neutral coupling and automatic brake hold.");
                input.Key(0x45, true); yield return new WaitForSecondsRealtime(.07f); input.Key(0x45, false); yield return new WaitForSecondsRealtime(.4f);
                check(scene.Session.car.state.gear == 1 && scene.Session.car.state.throttle == 1 && scene.Session.car.state.speed > 0, "E engages first without countdown or cancelling W.");
                yield return Click(input, "up"); yield return Capture("physical-held-w-shift"); check(scene.Session.car.state.gear == 2 && scene.Session.car.state.throttle == 1 && scene.Session.car.state.rpm > 0, "Actual mouse shift-up preserves held gas and engine rotation: gear " + scene.Session.car.state.gear + " / gas " + scene.Session.car.state.throttle + " / RPM " + scene.Session.car.state.rpm + " / " + scene.Session.car.state.feedback + " / driver " + scene.Session.car.driver.shifting);
                input.Key(0x20, true); input.Key(0x57, false); yield return new WaitForSecondsRealtime(.2f);
                check(scene.Session.car.state.throttle == 1, "Overlapping W/Space releases only the released gas key.");
                input.Key(0x20, false); input.Key(0x4E, true); yield return new WaitForSecondsRealtime(.07f); input.Key(0x4E, false); yield return new WaitForSecondsRealtime(.2f);
                check(scene.KeyboardOwned && scene.Session.car.state.gear == 0 && scene.Session.car.state.speed > 0 && scene.Session.car.state.throttle == 0 && scene.Session.car.state.rpm > 0, "Neutral retains road momentum and flywheel RPM; released keyboard gas stays released.");
                yield return Click(input, "sliders");
                var slider = scene.pedalRects["Throttle"]; int x = (int)(slider.x + slider.width * .4); int y = (int)slider.center.y;
                input.MouseButtonAt(x, y, true); yield return new WaitForSecondsRealtime(.07f); input.MouseButtonAt(x, y, false); yield return new WaitForSecondsRealtime(.35f);
                double gas = scene.Session.car.state.throttle;
                check(!scene.KeyboardOwned && gas > .3 && gas < .5, "Actual slider explicitly takes control and sets a latched partial throttle.");
                yield return new WaitForSecondsRealtime(.2f); check(scene.Session.car.state.throttle == gas, "Slider value remains latched while physics continues.");
                input.Key(0x57, true); yield return new WaitForSecondsRealtime(.1f); input.Key(0x57, false); yield return new WaitForSecondsRealtime(.2f);
                check(scene.KeyboardOwned && scene.Session.car.state.throttle == 0, "Keyboard takeover disables slider authority and does not restore its old value.");
                yield return Click(input, "pause"); long tick = scene.Session.Tick; yield return new WaitForSecondsRealtime(.2f);
                check(scene.Session.Tick == tick && scene.Audio.CurrentVolume == 0, "Pause freezes every recorded tick and silences DSP.");
                double speedBeforeMass=scene.Session.car.state.speed, rpmBeforeMass=scene.Session.car.state.rpm;
                Rect ballastRect = new Rect(); bool visibleMass=false;
                for(int offset=0;offset<3000;offset+=40) { scene.MassPanelForVerification(offset); yield return new WaitForEndOfFrame(); if(scene.massRects.TryGetValue("Test ballast (kg)",out ballastRect) && ballastRect.center.y>270 && ballastRect.center.y<450){visibleMass=true;break;} }
                check(visibleMass,"Bench exposes ballast slider in scrollable physical diagnostics.");
                int bx=(int)(ballastRect.x+ballastRect.width*.23), by=(int)ballastRect.center.y;
                input.MouseButtonAt(bx,by,true);yield return new WaitForSecondsRealtime(.07f);input.MouseButtonAt(bx,by,false);yield return new WaitForSecondsRealtime(.2f);
                check(scene.Session.car.mass.payloadKg>100 && scene.Session.car.state.speed==speedBeforeMass && scene.Session.car.state.rpm==rpmBeforeMass,"Actual mouse ballast edit updates physical mass without resetting paused movement/RPM.");
                check(scene.KeyboardOwned && scene.Paused && !scene.Recording,"Mass controls preserve keyboard pedal ownership, pause and recording choice.");
                yield return Capture("physical-live-mass-controls");
                double fuelBeforePause = scene.Session.car.state.fuelLitres; yield return new WaitForSecondsRealtime(.2f);
                check(scene.Session.car.state.fuelLitres == fuelBeforePause, "Paused physical bench burns no fuel.");
                var liveCar=scene.Session.car; liveCar.SetMassInputs(liveCar.tune.driverMassKg,liveCar.tune.payloadMassKg,liveCar.tune.payloadPositionM,liveCar.tune.payloadHeightM,10);
                Rect fillRect=new Rect();bool fillVisible=false;
                for(int offset=0;offset<3000;offset+=40){scene.MassPanelForVerification(offset);yield return new WaitForEndOfFrame();if(scene.buttonRects.TryGetValue("fuel-fill",out fillRect)&&fillRect.center.y>270&&fillRect.center.y<450){fillVisible=true;break;}}
                check(fillVisible,"Free refuel button is reachable in physical diagnostics.");input.Key(0x57,true);yield return Click(input,"fuel-fill");
                check(scene.Session.car.state.fuelLitres==scene.Session.car.spec.fuel.capacityLitres && scene.Session.car.state.speed==speedBeforeMass && scene.Session.car.state.rpm==rpmBeforeMass && scene.KeyboardOwned && scene.Paused, "Actual refuel click caps tank and preserves motion, RPM, keyboard ownership and pause.");
                yield return Capture("physical-fuel-controls");scene.SetPaused(false);yield return new WaitForSecondsRealtime(.15f);check(scene.Session.car.state.throttle==1 && scene.Session.car.state.fuelLitres<scene.Session.car.spec.fuel.capacityLitres,"Held OS W remains active after refuel and consumption resumes.");input.Key(0x57,false);scene.SetPaused(true);
                Rect oilRect=new Rect();bool oilVisible=false;
                for(int offset=0;offset<3000;offset+=40){scene.MassPanelForVerification(offset);yield return new WaitForEndOfFrame();if(scene.massRects.TryGetValue("Test oil quantity (L)",out oilRect)&&oilRect.center.y>270&&oilRect.center.y<450){oilVisible=true;break;}}
                check(oilVisible,"Physical oil quantity slider is reachable without changing pedal ownership.");
                input.MouseButtonAt((int)(oilRect.x+oilRect.width*.2),(int)oilRect.center.y,true);yield return new WaitForSecondsRealtime(.07f);input.MouseButtonAt((int)(oilRect.x+oilRect.width*.2),(int)oilRect.center.y,false);yield return new WaitForSecondsRealtime(.2f);
                double oilBeforeReset=scene.Session.car.state.fluids.oilLitres;
                check(oilBeforeReset<3&&scene.KeyboardOwned&&scene.Paused,"Actual oil slider changes isolated quantity while keeping keyboard control and pause.");
                yield return Click(input,"wear-reset");
                check(scene.Session.car.state.fluids.oilLitres==oilBeforeReset,"Wear reset leaves the deliberately lowered oil quantity unchanged.");
                bool oilFillVisible=false;Rect oilFillRect=new Rect();for(int offset=0;offset<3000;offset+=40){scene.MassPanelForVerification(offset);yield return new WaitForEndOfFrame();if(scene.buttonRects.TryGetValue("oil-fill",out oilFillRect)&&oilFillRect.center.y>270&&oilFillRect.center.y<450){oilFillVisible=true;break;}}
                check(oilFillVisible,"Test oil top-up button is reachable in physical diagnostics.");yield return Click(input,"oil-fill");
                check(scene.Session.car.state.fluids.oilLitres==scene.Session.car.spec.fluids.oilCapacityLitres&&scene.KeyboardOwned&&scene.Paused,"Actual test top-up caps to pan capacity and retains driving ownership.");yield return Capture("physical-fluid-controls");

                check(scene.SetStartingTire(2,.35f,110),"Paused bench accepts independent rear-left tire life/temperature");
                var assignedTires=scene.Session.car.mechanics.parts.Find(p=>p.slot=="Tires").condition.tires;
                check(assignedTires[2].health==.35f&&assignedTires[2].temperatureC==110&&assignedTires[3].health==1&&scene.KeyboardOwned,"Single tire assignment keeps other tires and keyboard pedal ownership");
                scene.SetPaused(true);Rect tireRect=new Rect();bool tireVisible=false;
                for(int offset=0;offset<2200;offset+=40){scene.TirePanelForVerification(offset);yield return new WaitForEndOfFrame();if(scene.tireRects.TryGetValue("life:2",out tireRect)&&tireRect.center.y>320&&tireRect.center.y<650){tireVisible=true;break;}}
                check(tireVisible,"Per-wheel tire life slider is reachable in the physical bench");
                input.MouseButtonAt((int)(tireRect.x+tireRect.width*.6),(int)tireRect.center.y,true);yield return new WaitForSecondsRealtime(.07f);input.MouseButtonAt((int)(tireRect.x+tireRect.width*.6),(int)tireRect.center.y,false);yield return new WaitForSecondsRealtime(.15f);
                check(scene.Session.car.mechanics.parts.Find(p=>p.slot=="Tires").condition.tires[2].health>.5f&&scene.KeyboardOwned,"Actual mouse tire edit updates only its condition and keeps keyboard pedal source");
                scene.SetPaused(true);yield return Capture("physical-tire-controls");yield return Click(input,"wear-reset");check(scene.Session.car.mechanics.parts.Find(p=>p.slot=="Tires").condition.tires.All(t=>t.health==1&&t.temperatureC==20),"Actual repair button resets all four isolated tire conditions");
                yield return Capture("physical-manual-controls");
                yield return Click(input, "reset"); check(scene.Session.car.state.gear == 0 && Math.Abs(scene.Session.car.state.rpm - scene.Session.car.spec.idleRpm) < scene.Session.car.spec.idleRpm * .05, "Actual Reset returns to neutral idle.");
                yield return Click(input, "scenario"); yield return AwaitEnd();
                string first = TimedCsv(scene.Session);
                check(scene.Session.samples.Count == 1921 && scene.Session.samples.Any(s => s.shifting && s.throttle == 1) && scene.Session.samples.Last().brake == 1, "Full live scenario captures staged revs, shifts, lift, neutral coast and braking at 120 Hz.");
                scene.ElementPanelForVerification("Ignition",0); // Show the graph for visual trace review.
                yield return Capture("physical-full-trace");
                yield return Click(input, "replay"); yield return AwaitEnd();
                check(TimedCsv(scene.Session) == first, "Live replay produces exactly the same complete chronological trace.");
                scene.Export(output); string json = Directory.GetFiles(output, "*.json").Where(p => p != sentinelPath && !p.EndsWith("audio-settings.json") && !p.EndsWith("unit-settings.json")).OrderByDescending(p => p).First();
                check(scene.Import(json), "Exported snapshot can be imported without a race cycle."); yield return AwaitEnd();
                check(Math.Abs(scene.Session.samples.Last().distance - scene.history[scene.history.Count - 2].samples.Last().distance) < 1e-8, "Imported replay retains output within JSON precision.");
                check(File.ReadAllText(sentinelPath) == sentinel, "Reset/scenario/replay/export leave player-save sentinel untouched.");
                yield return Capture("physical-replay-overlays");
                scene.SelectVehicle(Array.FindIndex(cars,v=>v.spec.id=="starter-hatch"));
                input.Key(0x57,true);yield return new WaitForSecondsRealtime(.1f);input.Key(0x57,false);scene.SetPaused(true);
                Rect elementRect=new Rect();bool elementVisible=false;
                for(int offset=0;offset<3500;offset+=40){scene.ElementPanelForVerification("Ignition",offset);yield return new WaitForEndOfFrame();if(scene.massRects.TryGetValue("Individual structural health",out elementRect)&&elementRect.center.y>520&&elementRect.center.y<640){elementVisible=true;break;}}
                check(elementVisible,"Individual ignition health slider is reachable in the component panel.");
                input.MouseButtonAt((int)(elementRect.x+elementRect.width*.6),(int)elementRect.center.y,true);yield return new WaitForSecondsRealtime(.07f);input.MouseButtonAt((int)(elementRect.x+elementRect.width*.6),(int)elementRect.center.y,false);yield return new WaitForSecondsRealtime(.15f);scene.SetPaused(true);
                var ignition=scene.Session.car.mechanics.parts.Find(p=>p.slot=="Ignition");
                check(ignition.condition.elements[0].health<.7f&&ignition.condition.elements[1].health>.99f&&scene.KeyboardOwned,"Actual individual slider changes only one element and retains keyboard ownership.");
                bool faultVisible=false;
                for(int offset=0;offset<3500;offset+=40){scene.ElementPanelForVerification("Ignition",offset);yield return new WaitForEndOfFrame();if(scene.massRects.TryGetValue("Individual fault choices",out elementRect)&&elementRect.center.y>520&&elementRect.center.y<640){faultVisible=true;break;}}
                check(faultVisible,"Applicable individual fault buttons are reachable in the component panel.");
                input.MouseButtonAt((int)(elementRect.x+elementRect.width*.5),(int)elementRect.center.y,true);yield return new WaitForSecondsRealtime(.07f);input.MouseButtonAt((int)(elementRect.x+elementRect.width*.5),(int)elementRect.center.y,false);yield return new WaitForSecondsRealtime(.15f);scene.SetPaused(true);
                check(scene.Session.car.state.cylinderContribution[0]==0&&scene.Session.car.state.cylinderContribution[1]>.99&&!scene.Session.car.state.engineFailed&&scene.KeyboardOwned,"Actual disabled-plug button removes one cylinder's combustion without disabling the engine or keyboard.");
                yield return Capture("physical-individual-fault-controls");
                bool replaceVisible=false;for(int offset=0;offset<3500;offset+=40){scene.ElementPanelForVerification("Ignition",offset);yield return new WaitForEndOfFrame();if(scene.buttonRects.TryGetValue("element-replace",out elementRect)&&elementRect.center.y>520&&elementRect.center.y<640){replaceVisible=true;break;}}
                check(replaceVisible,"Individual isolated replacement button is reachable.");yield return Click(input,"element-replace");scene.SetPaused(false);yield return new WaitForSecondsRealtime(.1f);scene.SetPaused(true);
                check(scene.Session.car.state.cylinderContribution.All(v=>v>.99)&&scene.KeyboardOwned,"Actual individual test replacement restores its cylinder while preserving pedal ownership.");
                check(File.ReadAllText(sentinelPath)==sentinel,"Individual fault and replacement controls leave player save untouched.");
                yield return Capture("physical-individual-repaired");
            }
            check(errors == 0, "Physical bench has no runtime/DSP/GUI errors."); File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), report + "Runtime errors: " + errors + "\n");
#else
            yield return null; throw new InvalidOperationException("Physical bench probe requires Windows development player.");
#endif
        }
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator Click(WindowsKeyboardProbe input, string key)
        {
            yield return new WaitForEndOfFrame(); Rect rect; if (!scene.buttonRects.TryGetValue(key, out rect) || rect.center.y >= Screen.height) throw new InvalidOperationException("Physical control not visible: " + key);
            input.MouseButtonAt((int)rect.center.x, (int)rect.center.y, true); yield return new WaitForSecondsRealtime(.06f); input.MouseButtonAt((int)rect.center.x, (int)rect.center.y, false); yield return new WaitForSecondsRealtime(.2f);
        }
#endif
        IEnumerator AwaitEnd() { float until = Time.realtimeSinceStartup + 30; while (!scene.Paused && Time.realtimeSinceStartup < until) yield return null; if (!scene.Paused || scene.Session.Tick != 1920) throw new InvalidOperationException("Full physical scenario did not end at its captured tick."); }
        static string TimedCsv(PhysicalDrivingSession session) { using(FramePacingProbe.Measure(8)) return session.Csv(); }
        IEnumerator Capture(string name) { yield return new WaitForEndOfFrame(); var image = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG()); Destroy(image); }
    }
}
