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
        IEnumerator BusinessRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            bool resume=Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-business-resume")>=0;
            var log=new StringBuilder(resume?"3F actual second process / offline reservation / real-clock claim.\n":"3F actual Windows business expansion / job assignment / durable shutdown.\n");
            Action<bool,string> check=(ok,label)=>{Check(ok,label);log.AppendLine("PASS: "+label);File.WriteAllText(Path.Combine(output,resume?"resume-progress.txt":"verification-progress.txt"),log.ToString(),Encoding.UTF8);};
            using(var mouse=new WindowsKeyboardProbe()){
                if(!resume){
                    var car=game.Profile.SelectedCar;car.fuel.litres=7;game.Profile.parts.Find(i=>i.instanceId==car.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId).condition.health=.8f;ProfileStore.Save(game.Profile);
                    int cash=game.Profile.cash;game.OpenBusiness();yield return new WaitForSecondsRealtime(.2f);yield return ClickPart(mouse,"business-upgrade");
                    check(game.Profile.garageLevel==2&&game.Profile.cash==cash-game.BusinessPolicy.upgradeCosts[0],"Actual expansion button settles old rate and purchases level two.");
                    yield return ClickPart(mouse,"job-assign:photo-session");var job=game.Profile.business.jobs.Single();
                    check(game.Profile.SelectedCar.garageJobId==job.id&&job.carId==game.Profile.SelectedCar.instanceId,"Actual assignment reserves exact selected car/engine/parts.");
                    check(CareerProgression.EntryError(game.Profile,game.Profile.SelectedCar,null)!=null,"Assigned car is unavailable to race.");
                    File.WriteAllText(Path.Combine(output,"business-stage-profile.json"),JsonUtility.ToJson(game.Profile),Encoding.UTF8);yield return Capture("business-assigned");
                }else{
                    var stage=JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(Path.Combine(output,"business-stage-profile.json"),Encoding.UTF8));var job=game.Profile.business.jobs.Single();
                    check(job.id==stage.business.jobs.Single().id&&game.Profile.SelectedCar.garageJobId==job.id,"Actual restart retains captured deadline, reward and reservation.");
                    string physical=JsonUtility.ToJson(stage.SelectedCar.fuel)+JsonUtility.ToJson(stage.SelectedCar.fluids)+string.Join("",stage.parts.Select(i=>JsonUtility.ToJson(i.condition)));game.OpenBusiness();yield return new WaitForSecondsRealtime(.2f);
                    while(DateTime.UtcNow.Ticks<job.readyUtcTicks)yield return new WaitForSecondsRealtime(.1f);int cash=game.Profile.cash;yield return ClickPart(mouse,"job-claim:"+job.id);
                    check(game.Profile.cash==cash+job.reward&&game.Profile.business.jobs.Count==0&&string.IsNullOrEmpty(game.Profile.SelectedCar.garageJobId),"Actual real-clock claim pays captured reward and releases once.");
                    check(physical==JsonUtility.ToJson(game.Profile.SelectedCar.fuel)+JsonUtility.ToJson(game.Profile.SelectedCar.fluids)+string.Join("",game.Profile.parts.Select(i=>JsonUtility.ToJson(i.condition))),"Offline work retains exact fuel, liquids and worn components.");
                    yield return ClickPart(mouse,"job-assign:photo-session");string cancel=game.Profile.business.jobs.Single().id;cash=game.Profile.cash;yield return ClickPart(mouse,"job-cancel:"+cancel);check(game.Profile.cash==cash&&game.Profile.business.jobs.Count==0,"Actual cancel releases without payout.");
                    // Only this isolated fixture backdates its accrual epoch to represent offline time.
                    game.Profile.business.accruedThroughUtcTicks-=TimeSpan.TicksPerHour;ProfileStore.Save(game.Profile);int expected=GarageBusiness.Pending(game.Profile,game.BusinessPolicy,DateTime.UtcNow.Ticks);cash=game.Profile.cash;yield return ClickPart(mouse,"business-idle");
                    check(game.Profile.cash-cash>=expected&&game.Profile.cash-cash<=expected+1&&expected>=180,"Actual idle button claims old captured rate, retaining fractional income.");
                    var saved=ProfileStore.Load();check(saved.cash==game.Profile.cash&&saved.business.jobs.Count==0&&GarageBusiness.Validate(saved)==null,"Claim/cancel/income remain valid after disk reload.");yield return Capture("business-released");
                }
            }
            check(errors==0,"No runtime / GUI / audio errors.");File.WriteAllText(Path.Combine(output,resume?"resume-verification.txt":"stage-verification.txt"),log+"Runtime errors: "+errors+"\n",Encoding.UTF8);File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),log+"Runtime errors: "+errors+"\n",Encoding.UTF8);
#else
            yield return null;throw new InvalidOperationException("Requires Windows development player.");
#endif
        }
    }
}
