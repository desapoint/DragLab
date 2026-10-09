using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using DriveType=DragLab.Domain.DriveType;

namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator TireFeedbackRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var report=new StringBuilder("Native tire contact sound and bounded smoke; same shared warm-up simulation.\n");
            int checks=0;Action<bool,string> check=(ok,label)=>{Check(ok,label);checks++;report.AppendLine("PASS: "+label);};
            using(var input=new WindowsKeyboardProbe()) {
                foreach(string id in new[]{"starter-hatch","sport-coupe","rally-sedan"}) {
                    FramePacingProbe.Phase="effects-"+id;
                    game.ReturnToGarage();var definition=Array.Find(game.vehicles,v=>v.spec.id==id);
                    if(game.Profile.cars.Find(c=>c.vehicleId==id)==null)game.Profile.cars.Add(new OwnedCar{vehicleId=id,tune=definition.CreateStockTune()});
                    MechanicsOwnership.Ensure(game.Profile,game.Profile.catalog);game.PreviewVehicle(id);
                    game.settings.warmupBeforeRace=true;game.settings.throttleAssist=false;game.settings.tractionAssist=false;game.settings.shifting=ShiftMode.Sequential;game.settings.wearMode=WearMode.None;
                    game.StartPractice();var car=game.Race.player;
                    car.state.warmupLineLock=true;
                    // Low-grip driven tires let the non-driven line-lock hold the body still.
                    // With ordinary grip a burnout can drag locked tires too; those should also emit.
                    if(car.spec.drive!=DriveType.RWD)car.tune.frontLeftGrip=car.tune.frontRightGrip=.15f;
                    if(car.spec.drive!=DriveType.FWD)car.tune.rearLeftGrip=car.tune.rearRightGrip=.15f;
                    game.verificationInput=()=>new DriverInput{throttle=1,brake=car.spec.drive==DriveType.AWD?0:1,shiftUp=game.Race.warmupNeutral};
                    yield return new WaitForSecondsRealtime(2);
                    check(game.PlayerView.SlipParticleCount>0&&game.PlayerView.SlipParticleCount<=96,"Bounded actual contact smoke / "+id);
                    if(car.spec.drive==DriveType.FWD)check(game.PlayerView.FrontSmokeEmitted>0&&game.PlayerView.RearSmokeEmitted==0,"FWD smoke comes only from slipping front tires");
                    if(car.spec.drive==DriveType.RWD)check(game.PlayerView.RearSmokeEmitted>0&&game.PlayerView.FrontSmokeEmitted==0,"RWD smoke comes only from slipping rear tires");
                    if(car.spec.drive==DriveType.AWD)check(game.PlayerView.FrontSmokeEmitted>0&&game.PlayerView.RearSmokeEmitted>0,"AWD low-grip fixture emits from both actual slipping axles");
                    check(game.Audio.TireOutputRms>.0001,"Actual tire DSP is audible / "+id+" / RMS "+game.Audio.TireOutputRms);
                    report.AppendLine("PASS: "+id+" / particles "+game.PlayerView.SlipParticleCount+" / front "+game.PlayerView.FrontSmokeEmitted+" / rear "+game.PlayerView.RearSmokeEmitted+" / tire RMS "+game.Audio.TireOutputRms);
                    yield return Capture("tire-smoke-"+id);
                    game.PauseRace(true);int particles=game.PlayerView.SlipParticleCount;yield return new WaitForSecondsRealtime(.25f);
                    check(game.PlayerView.SlipParticleCount==particles&&game.Audio.TireOutputRms==0,"Pause freezes particles and silences tire source / "+id);
                    yield return new WaitForEndOfFrame();var smokeImage=ScreenCapture.CaptureScreenshotAsTexture();var smokePixels=smokeImage.GetPixels32();
                    File.WriteAllBytes(Path.Combine(output,"paused-smoke-"+id+".png"),smokeImage.EncodeToPNG());
                    game.PlayerView.ClearSlipEffects();yield return new WaitForEndOfFrame();var clearImage=ScreenCapture.CaptureScreenshotAsTexture();var clearPixels=clearImage.GetPixels32();
                    int changed=0;for(int pixel=0;pixel<smokePixels.Length;pixel++)if(Math.Abs(smokePixels[pixel].r-clearPixels[pixel].r)+Math.Abs(smokePixels[pixel].g-clearPixels[pixel].g)+Math.Abs(smokePixels[pixel].b-clearPixels[pixel].b)>12)changed++;
                    check(changed>100,"Particles change rendered pixels, not only emitter counters / "+id+" / pixels "+changed);
                    report.AppendLine("PASS: Rendered smoke pixel difference / "+id+" / "+changed);Destroy(smokeImage);Destroy(clearImage);
                    game.PauseRace(false);game.verificationInput=()=>new DriverInput{brake=1,shiftDown=car.state.gear>0};
                    yield return new WaitForSecondsRealtime(3);
                    check(game.PlayerView.SlipParticleCount==0,"Smoke expires after grip recovery / "+id);
                    game.verificationInput=null;game.ReturnToGarage();
                    check(game.PlayerView.SlipParticleCount==0,"Garage/reset clears stale smoke / "+id);
                }
            }
            check(errors==0,"Tire effects have zero runtime/GUI/DSP errors");
            File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),report+"Checks: "+checks+" / Runtime errors: "+errors+"\n",new UTF8Encoding(false));
#else
            yield return null;throw new InvalidOperationException("Tire effects probe requires a Windows player.");
#endif
        }
        IEnumerator PacingRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            using(var input=new WindowsKeyboardProbe()) {
            var report = new StringBuilder("Native race pacing probe; isolated fixture; unchanged saving and simulation.\n");
            foreach (int count in new[] {1,8}) {
                game.ReturnToGarage();
                if (count == 8) foreach (var definition in game.vehicles)
                    if (game.Profile.cars.Find(c=>c.vehicleId==definition.spec.id)==null)
                        game.Profile.cars.Add(new OwnedCar{vehicleId=definition.spec.id,tune=definition.CreateStockTune()});
                MechanicsOwnership.Ensure(game.Profile,game.Profile.catalog);
                game.settings.ApplyPreset(DrivingPreset.Automatic);
                game.settings.warmupBeforeRace=false;
                FramePacingProbe.Phase="garage-"+count;
                yield return new WaitForSecondsRealtime(1);
                game.StartPractice();
                game.verificationInput=()=>new DriverInput{throttle=1,launch=game.Race.phase==RacePhase.Racing};
                FramePacingProbe.Phase="race-"+count;
                yield return new WaitForSecondsRealtime(12);
                Check(game.Race.player.state.speed>0&&!game.Race.player.state.engineFailed,"Native held-gas run survives / owned cars "+count);
                report.AppendLine("PASS: Native simulation / owned cars "+count+" / tick time "+game.Race.time+" / speed "+game.Race.player.state.speed);
                FramePacingProbe.Phase="transition";
            }
            game.verificationInput=null;game.ReturnToGarage();
            Check(errors==0,"Pacing probe has zero runtime errors");
            File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),report+"Runtime errors: "+errors+"\n",new UTF8Encoding(false));
            }
#else
            yield return null;throw new InvalidOperationException("Pacing probe requires a Windows player.");
#endif
        }
    }
}
