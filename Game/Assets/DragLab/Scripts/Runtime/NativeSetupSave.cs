using System;
using System.Linq;
using UnityEngine.UIElements;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        internal bool SaveNativeSetup(string carId,long revision,string name,SetupScope scope)
        {
            if(screen!="Tuning"||pendingAction!=null||Build?.instanceId!=carId||profile.revision!=revision||DraftDirty){notice="Tuning changed. Reopen Save setup.";return false;}
            if(!CommitParts(revision,p=>SavedSetups.Save(p,carId,name,scope)))return false;
            notice="Setup saved.";return true;
        }
    }
    internal sealed partial class NativeMenuShell
    {
        void OpenSaveSetup()
        {
            var car=game.Profile.SelectedCar;if(car==null||game.DraftDirty)return;
            string id=car.instanceId;long revision=game.Profile.revision;SetupScope scope=SetupScope.WholeCar;
            string suggested=VehicleCollection.DisplayName(game.Profile.catalog,car)+" setup";
            var name=new TextField{name="setupSaveName",value=suggested.Substring(0,Math.Min(suggested.Length,SavedSetups.NameLimit)),maxLength=SavedSetups.NameLimit};name.AddToClassList("setting-text");
            var error=new Label{name="setupSaveError",enableRichText=false};error.AddToClassList("car-critical");
            var summary=new Label{name="setupSaveSummary",enableRichText=false};Action update=null;
            var scopes=new[]{"Engine + controls","Whole car","Tune only"};
            OpenDialog("Save setup",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Save",()=>{
                if(!game.SaveNativeSetup(id,revision,name.value,scope)){error.text=game.NativeNotice;return;}
                CloseDialog();tuningSaveSetup.Q<Label>().text="Saved";root.schedule.Execute(()=>tuningSaveSetup.Q<Label>().text="Save setup").StartingIn(1800);RefreshTuning("Tuning");
            },true)},populate:content=>{
                content.Add(name);QueryChoices(content,"setupSaveScope","Scope",scopes,()=>scopes[(int)scope],value=>{scope=(SetupScope)Array.IndexOf(scopes,value);update();},3);content.Add(summary);content.Add(error);
                update=()=>{
                    bool valid=true;try{SavedSetups.NormalizeName(name.value);error.text="";}catch(Exception e){valid=false;error.text=e.Message;}
                    int count=car.installed.slots.Count(s=>SavedSetups.InScope(scope,AssemblyOperations.Parse(s.slot)));
                    summary.text=scope==SetupScope.TuneOnly?"Applied tuning · no hardware":count+" fitted components + applied "+(scope==SetupScope.Engine?"engine tuning":"tuning");
                    root.Q<Button>("dialogSave").SetEnabled(valid);
                };name.RegisterValueChangedCallback(_=>update());
            });update();ExcludeTabTraversal();
        }
    }
}
