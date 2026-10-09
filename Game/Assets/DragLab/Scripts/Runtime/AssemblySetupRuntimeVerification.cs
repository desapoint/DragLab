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
        IEnumerator AssemblySetupRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log=new System.Text.StringBuilder("3B actual Windows saved-config / missing-parts purchase / stored engine transfer.\n");
            Action<bool,string> check=(ok,label)=>{Check(ok,label);log.AppendLine("PASS: "+label);File.WriteAllText(Path.Combine(output,"verification-progress.txt"),log.ToString());};
            game.Profile.cash=100000;game.Profile.reputation=99;ProfileStore.Save(game.Profile);
            string original=game.Profile.SelectedCar.instanceId,engine=game.Profile.SelectedCar.engineAssemblyId;
            string oem=game.Profile.SelectedCar.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId;
            string forged=game.Profile.catalog.Id("starter-hatch",PartCategory.Pistons,1);
            using(var mouse=new WindowsKeyboardProbe()) {
                game.ShowParts(PartCategory.Pistons);yield return new WaitForSecondsRealtime(.3f);yield return ClickPart(mouse,"fit:"+forged);
                string sold=game.Profile.SelectedCar.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId;
                game.setupName="Saved forged build";game.setupScope=SetupScope.WholeCar;game.OpenSetups();yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"setup-save");
                check(game.Profile.savedSetups.Count==1&&game.Profile.savedSetups[0].parts.Any(p=>p.definitionId==forged),"Actual Save stores applied hardware definitions and tuning.");string saved=game.Profile.savedSetups[0].id;
                game.ShowParts(PartCategory.Pistons);yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"install:"+oem);yield return ClickPart(mouse,"sell:"+sold);yield return ClickPart(mouse,"quote-sell-preview");yield return ClickPart(mouse,"quote-sell-confirm");
                check(game.Profile.parts.All(p=>p.instanceId!=sold)&&game.Profile.savedSetups.Count==1,"Actual sale removes hardware while retaining the saved configuration.");
                game.OpenSetups();yield return new WaitForSecondsRealtime(.2f);int cash=game.Profile.cash;yield return ClickPart(mouse,"setup-preview:"+saved);
                check(game.setupPlan!=null&&game.setupPlan.Missing==1&&game.Profile.cash==cash,"Actual Load previews one missing purchase without changing cash/build.");yield return Capture("setup-missing-preview");int cost=game.setupPlan.cost;
                yield return ClickPart(mouse,"setup-apply-buy");
                string replaced=game.Profile.SelectedCar.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId;
                check(replaced!=sold&&game.Profile.parts.Find(p=>p.instanceId==replaced).definitionId==forged&&game.Profile.cash==cash-cost,"Actual Buy missing + apply restores setup with new paid identity.");
                game.Profile.SelectedCar.fluids.oilLitres=3.5;game.Profile.SelectedCar.fluids.coolantLitres=6;game.Profile.parts.Find(p=>p.instanceId==replaced).condition.health=.7f;ProfileStore.Save(game.Profile);
                game.OpenAssemblies();yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"engine-retention");yield return Capture("assembly-retention-preview");yield return ClickPart(mouse,"engine-detach");
                check(string.IsNullOrEmpty(game.Profile.SelectedCar.engineAssemblyId)&&game.Profile.engines.Find(e=>e.instanceId==engine).fluids.oilLitres==3.5,"Actual detach retains engine/liquids and a non-running chassis.");
                yield return ClickPart(mouse,"engine-inspect:"+engine);yield return ClickPart(mouse,"engine-remove:"+replaced);
                check(game.Profile.engines.Find(e=>e.instanceId==engine).slots.All(s=>s.slot!="Pistons"),"Stored assembly supports removing an individual pack without generating replacements.");yield return Capture("assembly-incomplete");
                yield return ClickPart(mouse,"engine-install:"+replaced);check(game.Profile.parts.Find(p=>p.instanceId==replaced).condition.health==.7f,"Actual stored reassembly preserves worn hardware.");
                yield return ClickPart(mouse,"assemblies-collection");yield return ClickPart(mouse,"car-buy:starter-hatch");string target=game.Profile.SelectedCar.instanceId;check(target!=original,"Transfer target is a separately purchased owned chassis.");
                game.OpenAssemblies();yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"engine-detach");yield return ClickPart(mouse,"engine-attach:"+engine);
                check(game.Profile.SelectedCar.engineAssemblyId==engine&&game.Profile.SelectedCar.fluids.oilLitres==3.5&&game.Profile.parts.Find(p=>p.instanceId==replaced).ownerId==engine,"Actual attach transfers exact engine descendants and liquids to second chassis.");yield return Capture("assembly-transferred");
                game.setupName="Saved tire tune";game.setupScope=SetupScope.TuneOnly;game.Profile.SelectedCar.tune.tirePressurePsi=26;game.OpenSetups();yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"setup-save");string tune=game.Profile.savedSetups.Last().id;
                game.Profile.SelectedCar.tune.tirePressurePsi=40;ProfileStore.Save(game.Profile);yield return ClickPart(mouse,"setup-preview:"+tune);yield return ClickPart(mouse,"setup-apply-owned");
                check(game.Profile.SelectedCar.tune.tirePressurePsi==26,"Actual tuning-only load/apply changes adjustments without a parts purchase.");
                game.ReturnToGarage();game.settings.ApplyPreset(DrivingPreset.Automatic);game.verificationInput=()=>new DriverInput{throttle=game.Race.player.state.launched?1:0};game.StartPractice();yield return AwaitFinish();game.ReturnToGarage();
                check(game.Profile.cars.Find(c=>c.instanceId==target).completedRuns==1&&game.Profile.cars.Find(c=>c.instanceId==original).completedRuns==0,"Transferred used assembly races on its actual new owner with independent records.");
                var loaded=ProfileStore.Load();check(loaded.SelectedCar.instanceId==target&&loaded.savedSetups.Count==2&&loaded.engines.Count(e=>string.IsNullOrEmpty(e.parentVehicleId))==1&&MechanicsOwnership.Validate(loaded,loaded.catalog)==null,"Reload preserves configurations, stored engine and exclusive ownership.");
            }
            game.verificationInput=null;check(errors==0,"No runtime/GUI/DSP errors during new interfaces and race.");File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),log+"Runtime errors: "+errors+"\n");
#else
            yield return null;throw new InvalidOperationException("Requires Windows development player.");
#endif
        }
    }
}
