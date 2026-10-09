using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator RaceThermalRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log=new StringBuilder("Windows thermal Settings / shared warm-up Update / save boundary\n");
            Action<bool,string> check=(ok,label)=>{Check(ok,label);log.AppendLine("PASS: "+label);};
            game.Profile.cash=100000;game.settings.optimalThermals=false;game.settings.tractionAssist=false;game.settings.throttleAssist=false;game.settings.shifting=ShiftMode.Sequential;game.settings.launch=LaunchMode.Assisted;
            game.OpenAudioSettings();yield return new WaitForSecondsRealtime(.2f);
            using(var mouse=new WindowsKeyboardProbe()) {
                yield return ThermalClick(mouse,()=>SimulationSettingsView.raceTemperaturesRect,1,3);
                check(game.settings.raceTemperatures==RaceTemperatureMode.Optimal&&!game.settings.optimalThermals,"Actual optimal race-start choice keeps heat management enabled");
                yield return ThermalClick(mouse,()=>SimulationSettingsView.heatManagementRect);
                check(game.settings.optimalThermals,"Actual Heat management toggle disables live heat");
                yield return ThermalClick(mouse,()=>SimulationSettingsView.raceTemperaturesRect,2,3);
                check(game.settings.raceTemperatures==RaceTemperatureMode.Optimal,"Disabled race-entry choices cannot change while heat management is OFF");
                yield return ThermalClick(mouse,()=>SimulationSettingsView.heatManagementRect);
                yield return ThermalClick(mouse,()=>SimulationSettingsView.raceTemperaturesRect,2,3);
                yield return ThermalClick(mouse,()=>SimulationSettingsView.warmupRect);
                check(game.settings.raceTemperatures==RaceTemperatureMode.Live&&game.settings.warmupBeforeRace&&!game.settings.optimalThermals,"Actual Live and warm-up controls select independent saved preferences");
                yield return new WaitForSecondsRealtime(.6f);var prefs=ProfileStore.Load().driving;
                check(prefs.raceTemperatures==RaceTemperatureMode.Live&&prefs.warmupBeforeRace&&!prefs.optimalThermals,"New temperature preferences persist through real disk reload");
                game.OpenCollection();yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"car-buy:sport-coupe");
                game.StartPractice();game.verificationInput=()=>new DriverInput();yield return new WaitForSecondsRealtime(.3f);
                check(game.Race.warmup&&game.Race.time==0&&game.Race.player.state.gear==0,"Actual player enters optional neutral warm-up with lights waiting");
                yield return ThermalClick(mouse,()=>game.warmupLineLockRect);
                check(game.Race.player.state.warmupLineLock,"Actual burnout line-lock toggle selects non-driven brake axle");
                double fuel=game.Race.player.state.fuelLitres;
                game.verificationInput=()=>new DriverInput{throttle=1,brake=1,shiftUp=game.Race.warmupNeutral};yield return new WaitForSecondsRealtime(3);
                var tire=game.Race.player.mechanics.Part(PartCategory.Tires);
                check(tire.condition.tires[2].slipEnergyJ>0&&tire.condition.tires[2].temperatureC>20&&game.Race.player.state.fuelLitres<fuel,"Actual Update warm-up produces road slip, tire heat and consumed fuel");
                game.PauseRace(true);double heat=tire.condition.tires[2].temperatureC;yield return new WaitForSecondsRealtime(.2f);
                check(tire.condition.tires[2].temperatureC==heat&&game.Race.time==0,"Warm-up pause freezes simulation without starting countdown");
                var saved=ProfileStore.Load();check(saved.SelectedCar.thermalUtcTicks>0&&saved.parts.Single(p=>p.instanceId==tire.instanceId).condition.tires[2].slipEnergyJ>0,"Warm-up checkpoint retains actual heat/work and per-car thermal clock");
                game.Race.player.state.warmupLineLock=false;game.verificationInput=()=>new DriverInput{brake=1,shiftDown=game.Race.player.state.gear>0};game.PauseRace(false);
                float until=Time.realtimeSinceStartup+20;while(!game.Race.WarmupStopped&&Time.realtimeSinceStartup<until)yield return null;
                check(game.Race.WarmupStopped,"Actual car can brake and stop after burnout");
                game.verificationInput=()=>new DriverInput();yield return ClickPart(mouse,"warmup-ready");
                check(!game.Race.warmup&&game.Race.time>0&&!game.Race.player.state.launched&&game.Race.player.state.gear==0,"Actual Ready button starts ordinary lights in neutral");
                yield return Capture("thermal-warmup-staging");game.ReturnToGarage();game.verificationInput=null;
                check(errors==0,"Thermal Settings/warm-up reports zero runtime/GUI/DSP errors");
            }
            File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),log+"Runtime errors: "+errors+"\n",new UTF8Encoding(false));
#else
            yield return null;throw new InvalidOperationException("Thermal UI probe requires a Windows development player.");
#endif
        }
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator ThermalClick(WindowsKeyboardProbe mouse,Func<Rect> get,int choice=0,int columns=1)
        {
            for(int i=0;i<35;i++) {
                yield return new WaitForEndOfFrame();var r=get();
                if(r.height>0&&r.center.y>=130&&r.center.y<Screen.height-90){int x=(int)(r.x+r.width*(choice+.5f)/columns),y=(int)r.center.y;mouse.MouseButtonAt(x,y,true);yield return new WaitForSecondsRealtime(.06f);mouse.MouseButtonAt(x,y,false);yield return new WaitForSecondsRealtime(.15f);yield break;}
                mouse.ScrollAt(Screen.width/2,Screen.height/2,r.center.y<130?2:-2);yield return new WaitForSecondsRealtime(.1f);
            }
            throw new InvalidOperationException("Thermal control could not be scrolled into view.");
        }
#endif
    }
}
