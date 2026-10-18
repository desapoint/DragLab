using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        bool onboardingPrompted;
        void ShowOnboardingIfNeeded()
        {
            if(onboardingPrompted||game.ResetRequired||!game.Profile.firstPurchasePending||DrivingPreferences.OnboardingComplete)return;
            onboardingPrompted=true;
            var driver=(DrivingPreset)(int)game.settings.shifting;
            var simulation=game.settings.simulationDifficulty==SimulationDifficulty.Custom?SimulationDifficulty.Simulation:game.settings.simulationDifficulty;
            int market=game.settings.guaranteedFittingParts==10?0:game.settings.guaranteedFittingParts==0?2:1;
            void Presets()
            {
                OpenDialog("Welcome to DragLab",null,new[]{new DialogAction("Next",Market,true,id:"onboardingNext")},populate:content=>{
                    QueryChoices(content,"onboardingDriving","Driving preset",new[]{"Automatic","Shift myself","Full manual"},()=>new[]{"Automatic","Shift myself","Full manual"}[(int)driver],v=>driver=(DrivingPreset)System.Array.IndexOf(new[]{"Automatic","Shift myself","Full manual"},v),3);
                    QueryChoices(content,"onboardingSimulation","Simulation preset",new[]{"Arcade","Balanced","Simulation"},()=>simulation.ToString(),v=>simulation=(SimulationDifficulty)System.Enum.Parse(typeof(SimulationDifficulty),v),3);
                    content.Add(new Label("Shift myself: choose gears without a clutch pedal. Full manual: clutch and launch control."));
                    content.Add(new Label("Simulation: full wear, fluids and temperature effects."));
                });
            }
            void Market()
            {
                var error=new Label{name="onboardingError"};error.AddToClassList("car-critical");
                OpenDialog("Market difficulty",null,new[]{new DialogAction("Back",Presets,id:"onboardingBack"),new DialogAction("Apply & choose car",()=>{
                    var settings=StartingPreferences.Create(driver,simulation,market);
                    if(!game.ApplyNativeSettings(settings,game.Profile.difficulty,SharedUnitSettings.Current,SharedAudioSettings.MasterVolume,SharedAudioSettings.OpponentVolume,out var message,onboardingComplete:true)){error.text=message;return;}
                    CloseDialog();
                },true,id:"onboardingApply")},populate:content=>{
                    QueryChoices(content,"onboardingMarket",null,new[]{"Easy · 10","Normal · 5","Hard · 0"},()=>new[]{"Easy · 10","Normal · 5","Hard · 0"}[market],v=>market=System.Array.IndexOf(new[]{"Easy · 10","Normal · 5","Hard · 0"},v),3);
                    content.Add(new Label("Minimum fitting offers in each new/used pool across your garage. Stock refreshes every 3 completed races."));
                    content.Add(new Label("The shortlist is fixed for this save. You can change settings later."));content.Add(error);
                });
            }
            Presets();
        }
    }
}
