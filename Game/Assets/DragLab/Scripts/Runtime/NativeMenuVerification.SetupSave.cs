using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        void SetupSnapshotChecks()
        {
            foreach(var vehicle in game.vehicles){var p=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(p,game.Profile.catalog);p.cash=1000000;p.reputation=1000;VehicleCollection.Buy(p,p.catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});
                foreach(SetupScope scope in Enum.GetValues(typeof(SetupScope))){string before=JsonUtility.ToJson(p);var car=p.SelectedCar;string id=SavedSetups.Save(p,car.instanceId,"  Café build  ",scope);var saved=p.savedSetups.Last();Check(saved.id==id&&saved.name=="Café build"&&saved.scope==scope&&saved.parts.Count==car.installed.slots.Count(s=>SavedSetups.InScope(scope,AssemblyOperations.Parse(s.slot)))&&JsonUtility.ToJson(saved.tune)==JsonUtility.ToJson(car.tune),"Applied setup snapshot captures the actual scope: "+vehicle.spec.id+" / "+scope);SavedSetups.Delete(p,id);Check(before==JsonUtility.ToJson(p),"Blueprint save/delete changes no physical assets or resources: "+vehicle.spec.id+" / "+scope);}
            }
            foreach(string invalid in new[]{"", "   ","two\nlines",new string('a',65)}){bool rejected=false;try{SavedSetups.NormalizeName(invalid);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Setup names reject blank, multiline and overlong input");}
        }
        IEnumerator SetupSaveChecks(WindowsKeyboardProbe input)
        {
            SetupSnapshotChecks();game.BeginTuneDraft();shell.Refresh();yield return new WaitForSecondsRealtime(.15f);
            Check(shell.Root.Q<Button>("tuningSaveSetup").enabledSelf&&shell.Root.Q("tuningSaveSetup").Q<FilledMenuIcon>()!=null,"Save setup is an available shared filled-icon action for applied tuning");
            game.DraftValues.driverMassKg+=1;shell.Refresh();yield return new WaitForSecondsRealtime(.15f);string dirty=JsonUtility.ToJson(game.Profile);Check(!shell.Root.Q<Button>("tuningSaveSetup").enabledSelf&&!game.SaveNativeSetup(game.Profile.SelectedCar.instanceId,game.Profile.revision,"Draft",SetupScope.TuneOnly)&&game.DraftDirty&&dirty==JsonUtility.ToJson(game.Profile),"Unapplied tuning cannot be silently saved or applied by Save setup");yield return Click(input,"tuningRevert");
            string before=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);yield return Click(input,"tuningSaveSetup");shell.Root.Q<TextField>("setupSaveName").value="Cancelled build";yield return Click(input,"setupSaveScope:Engine + controls");yield return Click(input,"dialogCancel");Check(before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Save setup Cancel leaves blueprints, real assets and disk unchanged");
            yield return Click(input,"tuningSaveSetup");shell.Root.Q<TextField>("setupSaveName").value="   ";Check(!shell.Root.Q<Button>("dialogSave").enabledSelf,"Blank name disables confirmation");shell.Root.Q<TextField>("setupSaveName").value="Café engine";yield return Click(input,"setupSaveScope:Engine + controls");yield return Key(input,0x77);yield return Key(input,0x77);Check(shell.ModalOpen&&shell.Root.Q<TextField>("setupSaveName").value=="Café engine"&&shell.Root.Q<Button>("setupSaveScope:Engine + controls").ClassListContains("selected"),"F8 retains the setup name and scope draft");
            yield return Capture("setup-save-dialog");Screen.SetResolution(1024,768,false);yield return new WaitForSecondsRealtime(.4f);Check(shell.Root.Q("modalActions").worldBound.yMax<=shell.Root.worldBound.yMax&&shell.Root.Q("setupSaveName").worldBound.xMax<=shell.Root.Q("modalContent").worldBound.xMax,"Save setup input and fixed actions fit compact windows");yield return Capture("setup-save-compact");Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);
            var target=new RenderTexture(3840,2160,24);target.Create();shell.Panel.targetTexture=target;shell.Refresh();yield return new WaitForSecondsRealtime(.3f);Check(shell.Panel.scale==2&&shell.Root.Q("modalActions").worldBound.yMax<=shell.Root.worldBound.yMax,"Save setup dialog uses the shared bounded 4K scale");var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(3840,2160,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"setup-save-4k.png"),image.EncodeToPNG());RenderTexture.active=previous;shell.Panel.targetTexture=null;target.Release();Destroy(target);Destroy(image);yield return new WaitForSecondsRealtime(.25f);
            using(var locked=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)){yield return Click(input,"dialogSave");Check(shell.ModalOpen&&shell.Root.Q<Label>("setupSaveError").text.Length>0&&before==JsonUtility.ToJson(game.Profile),"Failed durable blueprint save retains the dialog, applied build and library");}
            long revision=game.Profile.revision;int count=game.Profile.savedSetups.Count;yield return Click(input,"dialogSave");Check(!shell.ModalOpen&&game.Profile.revision==revision+1&&game.Profile.savedSetups.Count==count+1&&game.Profile.savedSetups.Last().name=="Café engine"&&game.Profile.savedSetups.Last().scope==SetupScope.Engine,"Explicit Save publishes one named engine blueprint after persistence succeeds");
            Check(shell.Root.Q("tuningSaveSetup").Q<Label>().text=="Saved"&&shell.Root.Q<Button>("tuningSaveSetup").text=="","Save feedback updates the existing caption without duplicating button text");
            var stripped=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(game.Profile));stripped.savedSetups.RemoveAt(stripped.savedSetups.Count-1);stripped.revision=revision;Check(SnapshotEquality.Profile(before,stripped,game.Profile.catalog),"Confirmed blueprint save preserves money, physical identities, wear, faults and supplies");Check(ProfileStore.Load().savedSetups.Last().name=="Café engine","Named native setup survives a real profile reload");
            foreach(string scope in new[]{"Whole car","Tune only"}){yield return Click(input,"tuningSaveSetup");shell.Root.Q<TextField>("setupSaveName").value="Same name";yield return Click(input,"setupSaveScope:"+scope);yield return Click(input,"dialogSave");}
            Check(game.Profile.savedSetups.TakeLast(2).Select(s=>s.scope).SequenceEqual(new[]{SetupScope.WholeCar,SetupScope.TuneOnly})&&game.Profile.savedSetups.TakeLast(2).Select(s=>s.id).Distinct().Count()==2&&game.Profile.savedSetups.Last().parts.Count==0,"All save scopes work and repeated names retain distinct blueprint identities");
            yield return Click(input,"tuningSaveSetup");game.DraftValues.driverMassKg+=1;string stale=JsonUtility.ToJson(game.Profile);yield return Click(input,"dialogSave");Check(shell.ModalOpen&&stale==JsonUtility.ToJson(game.Profile)&&game.DraftDirty,"Changes after opening Save setup reject stale confirmation without discarding the draft");yield return Click(input,"dialogCancel");yield return Click(input,"tuningRevert");
        }
#endif
    }
}
