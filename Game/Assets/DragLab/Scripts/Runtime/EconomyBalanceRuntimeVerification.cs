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
            // This probe targets IMGUI control IDs; native pages have their own suite.
            InterfaceComparison.SetReplacement("Parts",false);InterfaceComparison.SetReplacement("Business",false);
            game.Profile.cash=0;game.Profile.SelectedCar.fuel.litres=0;ProfileStore.Save(game.Profile);game.OpenRecovery();yield return new WaitForSecondsRealtime(.2f);
            using(var mouse=new WindowsKeyboardProbe()){
                yield return Capture("recovery-options");yield return ClickPart(mouse,"recovery-start");var recovery=game.Profile.recoveryWork;
                check(recovery.reward==250&&recovery.readyUtcTicks-recovery.startedUtcTicks==TimeSpan.TicksPerMinute,"Balanced work button captures $250 / real 60-second deadline.");
                while(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks<recovery.readyUtcTicks)yield return new WaitForSecondsRealtime(.1f);yield return ClickPart(mouse,"recovery-claim");check(game.Profile.cash==250&&game.Profile.SelectedCar.fuel.litres==0,"Work pays once and leaves empty physical tank unchanged.");
                yield return ClickPart(mouse,"recovery-fuel");check(game.Profile.cash==248&&game.Profile.SelectedCar.fuel.litres==1,"Actual small-refill recovery button pays $2 for one litre.");
                game.settings.ApplyPreset(DrivingPreset.Automatic);game.verificationInput=()=>new DriverInput{throttle=game.Race.player.state.launched?1:0};
                var first=game.career.Single(e=>e.stableId=="first-pass");string upgrade=game.Profile.catalog.Id("starter-hatch",PartCategory.HeadIntake,1);int expectedCash=248,passes=0;
                while(game.Profile.cash<game.PaymentCost(game.Payment(PaymentKind.Part,target:upgrade))){
                    bool claimed=game.Profile.careerWins.Contains(first.stableId);game.OpenCareer();yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"career-first-pass");yield return AwaitFinish();game.ReturnToGarage();
                    expectedCash+=first.cashWin-first.entryFee+(claimed?0:first.firstWinBonus);if(++passes>6)throw new InvalidOperationException("Recovered starter cannot fund one modest upgrade within its race budget.");
                    check(game.Profile.cash==expectedCash&&game.Profile.careerWins.Contains(first.stableId)&&game.Profile.SelectedCar.fuel.litres<1,"Each real win persists the authored payout and physical costs while saving for an upgrade.");
                }
                int price=game.PaymentCost(game.Payment(PaymentKind.Part,target:upgrade));string old=game.Profile.SelectedCar.installed.slots.Find(s=>s.slot=="HeadIntake").partInstanceId;game.ShowParts(PartCategory.HeadIntake);yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"fit:"+upgrade);
                expectedCash-=price;check(price>=600&&game.Profile.cash==expectedCash&&game.Profile.SelectedCar.tune.engineLevel==1&&game.Profile.parts.Find(i=>i.instanceId==old).location==PartLocation.Inventory,"Saved race income pays the actual stock/express-inclusive post-race quote and retains the original part.");
                game.OpenBusiness();yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"job-assign:photo-session");var job=game.Profile.business.jobs.Single();var reload=ProfileStore.Load();check(reload.business.jobs.Single().id==job.id&&GarageBusiness.Validate(reload)==null,"Previously driven job snapshot survives actual disk reload.");
                while(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks<job.readyUtcTicks)yield return new WaitForSecondsRealtime(.1f);yield return ClickPart(mouse,"job-claim:"+job.id);check(game.Profile.cash==expectedCash+job.reward&&game.Profile.business.jobs.Count==0,"Previously driven car's job pays/release once without false condition changes.");yield return Capture("funded-and-released");
            }
            check(errors==0,"No runtime / GUI / audio errors.");File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),log+"Runtime errors: "+errors+"\n",Encoding.UTF8);
#else
            yield return null;throw new InvalidOperationException("Requires Windows development player.");
#endif
        }
    }
}
