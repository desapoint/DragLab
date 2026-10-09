using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator CollectionRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log=new System.Text.StringBuilder("3A actual Windows dealership / selection / independent real race / isolated save.\n");Action<bool,string> check=(ok,label)=>{Check(ok,label);log.AppendLine("PASS: "+label);};
            game.Profile.cash=100000;game.Profile.reputation=99;ProfileStore.Save(game.Profile);string original=game.Profile.SelectedCar.instanceId;
            game.Profile.SelectedCar.fuel.litres=9;game.Profile.SelectedCar.tune.tirePressurePsi=23;game.Profile.SelectedCar.SavePreset(0);game.OpenCollection();yield return new WaitForSecondsRealtime(.3f);
            using(var mouse=new WindowsKeyboardProbe()){
                yield return ClickPart(mouse,"car-buy:starter-hatch");string second=game.Profile.SelectedCar.instanceId;
                check(second!=original&&game.Profile.cars.Count==2,"Actual dealership button buys and selects an independent second starter.");
                check(game.Profile.cars.Find(c=>c.instanceId==original).fuel.litres==9&&game.Profile.SelectedCar.presets.Count==0,"Buying retains original resources/presets and creates independent defaults.");
                yield return Capture("collection-two-starters");yield return ClickPart(mouse,"car-select:"+original);
                check(game.Profile.SelectedCar.instanceId==original&&game.Profile.SelectedCar.tune.tirePressurePsi==23,"Actual selection restores the exact first instance and tune.");
                yield return ClickPart(mouse,"car-select:"+second);check(game.Profile.SelectedCar.instanceId==second,"Second instance selected before race.");game.settings.ApplyPreset(DrivingPreset.Automatic);game.verificationInput=()=>new DriverInput{throttle=game.Race.player.state.launched?1:0};game.StartPractice();yield return AwaitFinish();check(game.Profile.SelectedCar.instanceId==second,"Second instance remains selected at race finish.");game.ReturnToGarage();
                check(game.Profile.cars.Find(c=>c.instanceId==second).completedRuns==1&&game.Profile.cars.Find(c=>c.instanceId==original).completedRuns==0,"Real race records and wear affect only the selected duplicate.");
                var loaded=ProfileStore.Load();check(loaded.SelectedCar.instanceId==second&&loaded.cars.Count==2,"Reload preserves duplicate identities and exact selected car.");
                game.OpenCollection();yield return new WaitForSecondsRealtime(.2f);
                foreach(string model in new[]{"city-three","rally-sedan","touring-six","super-v8"}){yield return ClickPart(mouse,"car-buy:"+model);check(game.Profile.SelectedCar.vehicleId==model&&MechanicsOwnership.Resolve(game.Profile,game.Profile.SelectedCar,game.Profile.catalog).Drivable,"Actual new-car purchase resolves playable hardware: "+model);yield return Capture("collection-"+model);}
            }
            game.verificationInput=null;check(game.vehicles.Length==8&&errors==0,"Expanded collection has no runtime/GUI/DSP errors.");File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),log+"Runtime errors: "+errors+"\n");
#else
            yield return null;throw new InvalidOperationException("Requires Windows development player.");
#endif
        }
    }
}
