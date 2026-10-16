using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator BalanceRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log=new StringBuilder("3G actual Windows zero-cash fuel recovery / race / worn-state purchase / job.\n");Action<bool,string> check=(ok,label)=>{Check(ok,label);log.AppendLine("PASS: "+label);File.WriteAllText(Path.Combine(output,"verification-progress.txt"),log.ToString(),Encoding.UTF8);};
            game.Profile.cash=0;game.Profile.SelectedCar.fuel.litres=0;ProfileStore.Save(game.Profile);game.OpenRecovery();yield return new WaitForSecondsRealtime(.2f);
            using(var mouse=new WindowsKeyboardProbe()){
                yield return Capture("recovery-options");yield return ClickPart(mouse,"recovery-start");var recovery=game.Profile.recoveryWork;
                check(recovery.reward==250&&recovery.readyUtcTicks-recovery.startedUtcTicks==TimeSpan.TicksPerMinute,"Balanced work button captures $250 / real 60-second deadline.");
                while(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks<recovery.readyUtcTicks)yield return new WaitForSecondsRealtime(.1f);yield return ClickPart(mouse,"recovery-claim");check(game.Profile.cash==250&&game.Profile.SelectedCar.fuel.litres==0,"Work pays once and leaves empty physical tank unchanged.");
                yield return ClickPart(mouse,"recovery-fuel");check(game.Profile.cash==248&&game.Profile.SelectedCar.fuel.litres==1,"Actual small-refill recovery button pays $2 for one litre.");
                game.settings.ApplyPreset(DrivingPreset.Automatic);game.verificationInput=()=>new DriverInput{throttle=game.Race.player.state.launched?1:0};game.OpenCareer();yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"career-first-pass");yield return AwaitFinish();game.ReturnToGarage();check(game.Profile.cash==748&&game.Profile.careerWins.Contains("first-pass")&&game.Profile.SelectedCar.fuel.litres<1,"Recovered car wins actual race and persists consumed fuel/wear.");
                string old=game.Profile.SelectedCar.installed.slots.Find(s=>s.slot=="HeadIntake").partInstanceId;game.ShowParts(PartCategory.HeadIntake);yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"fit:"+game.Profile.catalog.Id("starter-hatch",PartCategory.HeadIntake,1));
                check(game.Profile.cash==148&&game.Profile.SelectedCar.tune.engineLevel==1&&game.Profile.parts.Find(i=>i.instanceId==old).location==PartLocation.Inventory,"Actual post-race quote installs $600 part despite history JSON rounding; original retained.");
                game.OpenBusiness();yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"job-assign:photo-session");var job=game.Profile.business.jobs.Single();var reload=ProfileStore.Load();check(reload.business.jobs.Single().id==job.id&&GarageBusiness.Validate(reload)==null,"Previously driven job snapshot survives actual disk reload.");
                while(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks<job.readyUtcTicks)yield return new WaitForSecondsRealtime(.1f);yield return ClickPart(mouse,"job-claim:"+job.id);check(game.Profile.cash==168&&game.Profile.business.jobs.Count==0,"Previously driven car's job pays/release once without false condition changes.");yield return Capture("funded-and-released");
            }
            check(errors==0,"No runtime / GUI / audio errors.");File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),log+"Runtime errors: "+errors+"\n",Encoding.UTF8);
#else
            yield return null;throw new InvalidOperationException("Requires Windows development player.");
#endif
        }
    }
}
