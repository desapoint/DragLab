using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    { internal void OpenCareer(){DiscardPreviews();screen="Career";scroll=Vector2.zero;partRects.Clear();} }
    public sealed partial class RuntimeVerification
    {
        IEnumerator ExpandedCareerRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log=new StringBuilder("3E actual Windows career buttons / shared simulation / settlement.\n");
            Action<bool,string> check=(ok,label)=>{Check(ok,label);log.AppendLine("PASS: "+label);};
            check(game.career.Length==49&&game.career.Count(e=>e.stableId.StartsWith("side-",StringComparison.Ordinal))==28,"All 49 authored events, including 28 additional races, load in the player.");game.settings.ApplyPreset(DrivingPreset.Automatic);
            game.verificationInput=()=>new DriverInput{throttle=game.Race.player.state.launched?1:0};
            using(var mouse=new WindowsKeyboardProbe())for(int n=0;n<2;n++){
                var first=game.career.Single(e=>e.stableId=="first-pass");bool claimed=game.Profile.careerWins.Contains(first.stableId);
                int cash=game.Profile.cash,rep=game.Profile.reputation,runs=game.Profile.SelectedCar.completedRuns;
                game.OpenCareer();yield return new WaitForSecondsRealtime(.2f);yield return Capture("career-"+n);yield return ClickPart(mouse,"career-first-pass");yield return AwaitFinish();
                check(game.Profile.cash==cash-first.entryFee+first.cashWin+(claimed?0:first.firstWinBonus)&&game.Profile.reputation==rep+(claimed?0:first.reputationWin)&&game.Profile.SelectedCar.completedRuns==runs+1,"Real first/repeat result pays once and preserves first-win REP.");
                game.ReturnToGarage();var saved=ProfileStore.Load();check(saved.cash==game.Profile.cash&&saved.careerRuns.Last().complete&&saved.careerRuns.Last().outcome=="Win"&&saved.SelectedCar.fuel.litres<50,"Reload retains captured outcome and actual consumed fuel.");
            }
            check(errors==0,"No runtime / GUI / audio errors.");File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),log+"Runtime errors: "+errors+"\n",Encoding.UTF8);
#else
            yield return null;throw new InvalidOperationException("Requires Windows development player.");
#endif
        }
    }
}
