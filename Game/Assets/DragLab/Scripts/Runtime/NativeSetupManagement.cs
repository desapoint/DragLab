using System;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        internal bool ManageNativeSetup(string id,long revision,string expected,string name,bool delete)
        {
            if(BlockForManualRefill())return false;
            try{
                if(screen!="Tuning"||pendingAction!=null||profile.revision!=revision)throw new InvalidOperationException("Saved setups changed. Reopen the dialog.");
                var saved=profile.savedSetups.Find(s=>s.id==id);if(saved==null||JsonUtility.ToJson(saved)!=expected)throw new InvalidOperationException("Saved setup changed or no longer exists.");
                if(!delete&&SavedSetups.NormalizeName(name)==saved.name)return true;
                var previousDraft=draft;
                if(!MechanicsTransactions.Try(profile,profile.catalog,revision,p=>{if(delete)SavedSetups.Delete(p,id);else SavedSetups.Rename(p,id,name);},ProfileStore.TrySaveValidated,out var committed,out var error)){notice=error;return false;}
                profile=committed;settings=profile.driving;saveDelay=0;lastBenchmarkKey=statsKey=cartPriceKey=null;
                if(previousDraft!=null&&previousDraft.revision==revision&&Build?.instanceId==previousDraft.vehicleInstanceId)draft=new TuneDraft(Build,profile.revision){values=previousDraft.values.Copy(),paint=previousDraft.paint};
                notice=delete?"Saved setup deleted.":"Setup name saved.";return true;
            }catch(Exception e){notice=e.Message;return false;}
        }
    }
    internal sealed partial class NativeMenuShell
    {
        Button setupDelete;
        void InitializeSetupManagement()
        {
            setupDelete=new Button(OpenSetupDelete){name="setupDelete",text="Delete",tooltip="Delete the selected saved setup"};setupDelete.AddToClassList("action-button");setupLoad.parent.Insert(setupLoad.parent.IndexOf(setupLoad),setupDelete);AddSurface(setupDelete,RacingSurface.Kind.SecondaryAction);
        }
        Button SetupRenameButton()
        {
            var button=new Button(OpenSetupRename){name="setupRename",tooltip="Rename saved setup"};button.AddToClassList("action-button");button.AddToClassList("car-rename-button");button.Add(new FilledMenuIcon("Pencil"));SetupSurface(button,RacingSurface.Kind.SecondaryAction);return button;
        }
        void OpenSetupRename()
        {
            if(ModalOpen)return;var saved=game.Profile.savedSetups.Find(s=>s.id==setupSelectedId);if(saved==null)return;
            string id=saved.id,expected=JsonUtility.ToJson(saved);long revision=game.Profile.revision;
            var input=new TextField{name="setupRenameName",value=saved.name,maxLength=SavedSetups.NameLimit};input.AddToClassList("setting-text");
            var error=new Label{name="setupManageError",enableRichText=false};error.AddToClassList("car-critical");
            OpenDialog("Rename setup",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Save",()=>{
                if(!game.ManageNativeSetup(id,revision,expected,input.value,false)){error.text=game.NativeNotice;return;}CloseDialog();setupLibraryKey=null;RefreshSetupLibrary("Tuning");
            },true)},populate:content=>{content.Add(input);content.Add(error);});
            void Validate(){try{var name=SavedSetups.NormalizeName(input.value);error.text="";root.Q<Button>("dialogSave").SetEnabled(name!=saved.name);}catch(Exception e){error.text=e.Message;root.Q<Button>("dialogSave").SetEnabled(false);}}
            input.RegisterValueChangedCallback(_=>Validate());Validate();input.Focus();ExcludeTabTraversal();
        }
        void OpenSetupDelete()
        {
            if(ModalOpen)return;var saved=game.Profile.savedSetups.Find(s=>s.id==setupSelectedId);if(saved==null)return;
            string id=saved.id,expected=JsonUtility.ToJson(saved);long revision=game.Profile.revision;
            var error=new Label{name="setupManageError",enableRichText=false};error.AddToClassList("car-critical");
            OpenDialog("Delete setup",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Delete",()=>{
                if(!game.ManageNativeSetup(id,revision,expected,null,true)){error.text=game.NativeNotice;return;}CloseDialog();setupSelectedId=null;setupLibraryKey=null;RefreshSetupLibrary("Tuning");
            },true)},populate:content=>{content.Add(new Label(saved.name){name="setupDeleteIdentity",enableRichText=false});content.Add(new Label("Only this saved setup is deleted. Cars and parts are kept."));content.Add(error);});
        }
    }
}
