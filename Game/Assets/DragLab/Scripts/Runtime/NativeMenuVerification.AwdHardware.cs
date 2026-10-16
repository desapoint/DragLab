using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
        IEnumerator AwdHardwareInterfaceChecks(WindowsKeyboardProbe input)
        {
            if(!shell.Visible)yield return Key(input,0x77);var original=game.Profile;byte[] disk=File.ReadAllBytes(ProfileStore.FilePath),backup=File.Exists(ProfileStore.FilePath+".bak")?File.ReadAllBytes(ProfileStore.FilePath+".bak"):null;var profileField=typeof(PrototypeGame).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic);var catalog=original.catalog;
            try{
                foreach(var vehicle in game.vehicles.Where(v=>v.spec.drive==DragLab.Domain.DriveType.AWD)){
                    var p=PlayerProfile.New(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);p.SelectedCar.vehicleId=p.selectedVehicleId=vehicle.spec.id;MechanicsOwnership.Ensure(p,catalog);p.cash=1000000;p.reputation=1000;profileField.SetValue(game,p);game.settings=p.driving;game.ReturnToGarage();game.ClearNativeCart();game.DiscardPreviews();yield return Click(input,"navTuning");yield return Click(input,"tuningTabDrivetrain");
                    Check(shell.Root.Q<Button>("tuneChoice:awdMode:0")==null&&shell.Root.Q("tuneReadout:awdMode")!=null,"OEM center type is an installed specification, not an unrestricted mode picker: "+vehicle.spec.id);
                    Check(shell.Root.Q<Slider>("tuneSlider:shaftInertia")==null&&shell.Root.Q<Toggle>("tuneSwitch:primaryFront")==null&&shell.Root.Q<Toggle>("tuneSwitch:isolateAxle")==null,"Primary axle, free-shaft properties and isolation cannot be changed in physical tuning");
                    PartOperations.Buy(p,catalog,p.SelectedCar.instanceId,catalog.Id(vehicle.spec.id,PartCategory.Controls,1),true);p.revision++;game.BeginTuneDraft();shell.Refresh();yield return null;Check(shell.Root.Q<NativeStatsPanel>("tuningStats").Proposed.Awd.mode==vehicle.spec.awd.mode,"Engine ECU does not change the OEM center system");
                    foreach(string key in new[]{"OpenCenter","OnDemandFixed","OnDemandController","ActiveFixed","ActiveController","AdjustableLSD","SwitchableLock"}){
                        var d=catalog.Find(vehicle.spec.id+"/Differential/"+key);PartOperations.Buy(p,catalog,p.SelectedCar.instanceId,d.id,true);p.revision++;game.BeginTuneDraft();shell.Refresh();yield return new WaitForSecondsRealtime(.15f);var proposed=shell.Root.Q<NativeStatsPanel>("tuningStats").Proposed;
                        Check(proposed.Awd.mode==d.awdHardware.calibration.mode&&shell.Root.Q("tuningPreviewError")==null,"Actual installed hardware drives the tuning preview: "+key+" / "+vehicle.spec.id);
                        Check((shell.Root.Q<Slider>("tuneSlider:secondaryShare")!=null)==(key=="OnDemandController")&&(shell.Root.Q<Slider>("tuneSlider:minimumFront")!=null)==(key=="ActiveController")&&(shell.Root.Q<Slider>("tuneSlider:centerBias")!=null)==(key=="AdjustableLSD"),"Only declared hardware/controller capabilities create editable sliders: "+key);
                        string before=JsonUtility.ToJson(p);
                        if(key=="OnDemandController"){shell.Root.Q<FloatField>("tuneNumber:secondaryShare").value=100;Check(game.DraftValues.awd.maximumSecondary<=.60001f&&Math.Abs(shell.Root.Q<Slider>("tuneSlider:secondaryShare").highValue-60)<.001,"Coupling share is clamped to installed 60% capacity");}
                        if(key=="ActiveController"){shell.Root.Q<FloatField>("tuneNumber:minimumFront").value=85;shell.Root.Q<FloatField>("tuneNumber:maximumFront").value=5;Check(game.DraftValues.awd.minimumFront<=game.DraftValues.awd.maximumFront&&game.DraftValues.awd.maximumFront<=.85001f,"Conflicting active limits remain ordered within hardware bounds");}
                        if(key=="AdjustableLSD"){shell.Root.Q<FloatField>("tuneNumber:centerBias").value=9;Check(game.DraftValues.awd.torqueBiasRatio==5,"Mechanical LSD adjustment respects its installed maximum bias");}
                        if(key=="SwitchableLock"){yield return RevealTune(input,"Drivetrain","awdMode");yield return Click(input,"tuneChoice:awdMode:1");Check(game.DraftValues.awd.mode==AwdMode.LockedCenter&&shell.Root.Q<Button>("tuneChoice:awdMode:2")==null,"Lockable assembly offers only its two supported modes");}
                        Check(before==JsonUtility.ToJson(p),"AWD edits remain drafts without healing, installing, charging or saving: "+key);
                        if(vehicle.spec.id=="rally-sedan"&&(key=="OnDemandController"||key=="ActiveController"||key=="AdjustableLSD"||key=="SwitchableLock"))yield return Capture("awd-hardware-"+key.ToLowerInvariant());
                    }
                    PartOperations.Buy(p,catalog,p.SelectedCar.instanceId,vehicle.spec.id+"/TransferCase/RatioLocked",true);p.revision++;game.BeginTuneDraft();shell.Refresh();yield return new WaitForSecondsRealtime(.15f);shell.Root.Q<FloatField>("tuneNumber:centerRatio").value=1.5f;game.DraftValues.frontWheelDiameter=.8f;game.DraftValues.rearWheelDiameter=1.2f;shell.Refresh();yield return new WaitForSecondsRealtime(.15f);
                    var ratio=shell.Root.Q<NativeStatsPanel>("tuningStats").Proposed;Check(Math.Abs(ratio.Awd.LockedRatio-1.5)<.00001&&ratio.Awd.mode==AwdMode.LockedCenter&&!ratio.Inspection.warnings.Any(w=>w.title=="AWD tire rolling sizes differ"),"Ratio-capable transfer hardware previews matched unequal tire sizes");
                    string draft=JsonUtility.ToJson(game.DraftValues);yield return Key(input,0x77);yield return Key(input,0x77);Check(draft==JsonUtility.ToJson(game.DraftValues),"F8 preserves the exact hardware-bounded AWD draft");
                    Check(game.ApplyTuneDraftNow()&&Math.Abs(game.Profile.SelectedCar.tune.awd.LockedRatio-1.5)<.00001,"Explicit Apply durably stores supported AWD calibration");Check(Math.Abs(ProfileStore.Load().SelectedCar.tune.awd.LockedRatio-1.5)<.00001,"Saved AWD ratio survives compressed-profile reload");
                    if(vehicle.spec.id=="rally-sedan")yield return Capture("awd-hardware-ratio-applied");
                }
            }finally{profileField.SetValue(game,original);game.settings=original.driving;game.ClearNativeCart();game.DiscardPreviews();ProfileStore.Flush();SqliteStateStore.Invalidate();File.WriteAllBytes(ProfileStore.FilePath,disk);if(backup!=null)File.WriteAllBytes(ProfileStore.FilePath+".bak",backup);game.QueueNativeNavigation("Garage");}
        }
#endif
    }
}
