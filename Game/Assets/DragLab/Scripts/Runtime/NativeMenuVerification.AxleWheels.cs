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
        IEnumerator AxleTuningChecks(WindowsKeyboardProbe input)
        {
            var original=game.Profile;string baseline=JsonUtility.ToJson(original);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            float radius=original.catalog.Spec(original.SelectedCar.vehicleId).wheelRadiusM/(float)DisplayUnits.MetresPerInch;
            var field=typeof(PrototypeGame).GetField("profile",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            try{
                yield return RevealTune(input,"Wheels","wheelAxle");yield return Click(input,"tuneChoice:wheelAxle:1");
                Check(!game.DraftDirty&&shell.Root.Q<Button>("tuneChoice:wheelAxle:1").ClassListContains("selected"),"Front axle selection is a view setting, not a saved tune change");
                yield return RevealTune(input,"Wheels","width");shell.Root.Q<FloatField>("tuneNumber:width").value=.5f;
                shell.Root.Q<FloatField>("tuneNumber:radius").value=radius*.9f;
                Check(game.DraftValues.frontWheelWidth==.5f&&Math.Abs(game.DraftValues.frontWheelDiameter-.9f)<.001&&game.DraftValues.rearWheelWidth==0,"Front skinny-wheel edits leave rear geometry untouched");
                yield return RevealTune(input,"Wheels","pressure");var pressureRange=shell.Root.Q<NativeTuningRange>("tuneRange:pressure");double frontDisplay=Math.Round(DisplayUnits.Pressure(36/DisplayUnits.PsiPerBar,SharedUnitSettings.Current.pressure)/pressureRange.FineStep,MidpointRounding.AwayFromZero)*pressureRange.FineStep;double frontPsi=DisplayUnits.ToBar(frontDisplay,SharedUnitSettings.Current.pressure)*DisplayUnits.PsiPerBar;
                shell.Root.Q<FloatField>("tuneNumber:pressure").value=(float)frontDisplay;
                yield return RevealTune(input,"Wheels","wheelAxle");yield return Click(input,"tuneChoice:wheelAxle:2");Check(game.WheelTuneAxle==2,"Rear axle selector receives real mouse input");
                yield return RevealTune(input,"Wheels","width");shell.Root.Q<FloatField>("tuneNumber:width").value=1.4f;shell.Root.Q<FloatField>("tuneNumber:radius").value=radius*1.2f;
                yield return RevealTune(input,"Wheels","pressure");double rearDisplay=Math.Round(DisplayUnits.Pressure(23/DisplayUnits.PsiPerBar,SharedUnitSettings.Current.pressure)/pressureRange.FineStep,MidpointRounding.AwayFromZero)*pressureRange.FineStep;double rearPsi=DisplayUnits.ToBar(rearDisplay,SharedUnitSettings.Current.pressure)*DisplayUnits.PsiPerBar;shell.Root.Q<FloatField>("tuneNumber:pressure").value=(float)rearDisplay;
                yield return new WaitForSecondsRealtime(.3f);
                Check(Math.Abs(game.DraftValues.AxlePressure(true)-frontPsi)<.001&&Math.Abs(game.DraftValues.AxlePressure(false)-rearPsi)<.001&&game.DraftValues.frontWheelWidth==.5f&&game.DraftValues.rearWheelWidth==1.4f,"Rear edits retain front width and independent physical pressure through unit conversion");
                var panel=shell.Root.Q<NativeStatsPanel>("tuningStats");
                Check(panel.Proposed.Inspection.wheelRadiiIn[0]<panel.Proposed.Inspection.wheelRadiiIn[2]&&panel.Proposed.Inspection.wheelMassesKg[0]<panel.Proposed.Inspection.wheelMassesKg[2],"Shared preview exposes actual separate radii and wheel masses");
                var images=shell.Root.Q("tuningWheelPreview").Query<Image>().ToList();var front=images.First(i=>i.name=="carLayer:front-wheel");var rear=images.First(i=>i.name=="carLayer:rear-wheel");
                Check(front.style.height.value.value<rear.style.height.value.value,"Rendered front/rear wheel sprites have different sizes in the draft preview");
                yield return RevealTune(input,"Wheels","wheelAxle");yield return Capture("tuning-staggered-rear");
                yield return Click(input,"tuneChoice:wheelAxle:1");Check(shell.Root.Q<FloatField>("tuneNumber:width").value==.5f,"Returning to Front shows the retained front value rather than copying Rear");
                yield return RevealTune(input,"Wheels","width");var widthRange=shell.Root.Q<NativeTuningRange>("tuneRange:width");var track=widthRange.Slider.Q(className:"unity-base-slider__tracker").worldBound;
                input.MouseButtonAt((int)track.center.x,(int)track.center.y,true,2);yield return null;yield return new WaitForSecondsRealtime(.1f);input.MouseButtonAt((int)track.center.x,(int)track.center.y,false,2);yield return null;yield return new WaitForSecondsRealtime(.15f);
                Check(game.DraftValues.frontWheelWidth==1&&game.DraftValues.rearWheelWidth==1.4f,"Middle-click resets only the selected axle to its default");widthRange.Number.value=.5f;
                input.MouseButtonAt((int)track.center.x,(int)track.center.y,true,1);yield return null;yield return new WaitForSecondsRealtime(.1f);input.MoveMouseAt((int)track.center.x+35,(int)track.center.y);yield return new WaitForSecondsRealtime(.15f);input.MouseButtonAt((int)track.center.x+35,(int)track.center.y,false,1);yield return null;yield return new WaitForSecondsRealtime(.15f);
                Check(game.DraftValues.frontWheelWidth>.5f&&game.DraftValues.frontWheelWidth<.55f&&game.DraftValues.rearWheelWidth==1.4f,"Right-drag fine tuning changes only the selected axle");widthRange.Number.value=.5f;
                yield return Capture("tuning-staggered-front");
                foreach(string group in new[]{"Tires","FrontWheels","RearWheels"}){
                    panel=shell.Root.Q<NativeStatsPanel>("tuningStats");var button=panel.Q<Button>("statsGroup:"+group);Check(button!=null,"Live axle disclosure exists: "+group);var scroll=panel.GetFirstAncestorOfType<ScrollView>();scroll.ScrollTo(button);yield return new WaitForSecondsRealtime(.15f);panel=shell.Root.Q<NativeStatsPanel>("tuningStats");yield return Click(input,panel.Q<Button>("statsGroup:"+group));
                }
                panel=shell.Root.Q<NativeStatsPanel>("tuningStats");
                Check(panel.Q<Label>("statValue:axle:Front:tirePressure").text.Contains(" → ")&&panel.Q<Label>("statValue:axle:Rear:wheelMass").text.Contains(" → "),"Reusable axle disclosures show pressure/mass comparisons with current and proposed values");yield return Capture("tuning-axle-stats");
                Check(baseline==JsonUtility.ToJson(game.Profile)&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk),"Axle selection and all draft edits preserve actual parts, wear, resources and saved bytes");
                yield return Click(input,"tuningApply");var loaded=ProfileStore.Load();
                Check(loaded.SelectedCar.tune.frontWheelWidth==.5f&&loaded.SelectedCar.tune.rearWheelWidth==1.4f&&Math.Abs(loaded.SelectedCar.tune.AxlePressure(false)-rearPsi)<.001,"Explicit Apply durably saves independent front/rear settings");
                yield return Click(input,"settingsButton");yield return Click(input,"cancelSettings");yield return Key(input,0x77);yield return Key(input,0x77);
                Check(game.WheelTuneAxle==1&&game.DraftValues.frontWheelWidth==.5f,"Settings Cancel and F8 retain axle selection and the authoritative tune");
            }finally{field.SetValue(game,original);ProfileStore.Flush();SqliteStateStore.Invalidate();File.WriteAllBytes(ProfileStore.FilePath,disk);game.WheelTuneAxle=0;game.BeginTuneDraft();shell.Refresh();}
            shell.Root.Q<ScrollView>("tuningPreviewScroll").scrollOffset=Vector2.zero;
            yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator MixedTirePurchaseChecks(WindowsKeyboardProbe input,NativePartEntry offer)
        {
            bool wheel=offer.Definition.category==PartCategory.Wheels;string role=wheel?"wheel":"tire",slot=wheel?"Wheels":"Tires";
            string before=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            yield return Click(input,"partAdd");yield return PartDialogClick(input,"partPurchaseDestination:Install");yield return PartDialogClick(input,"partSelectRear");
            Check(shell.Root.Q<FloatField>("partQuantity").value==2&&shell.Root.Q<Button>("purchasePosition:"+role+":2").ClassListContains("selected")&&shell.Root.Q<Button>("purchasePosition:"+role+":3").ClassListContains("selected")&&!shell.Root.Q<Button>("purchasePosition:"+role+":0").ClassListContains("selected"),"Rear shortcut selects exactly two rear tires for compatible mixed-compound installation");
            yield return Capture(wheel?"purchase-mixed-rear-wheels":"purchase-mixed-rear-tires");yield return Click(input,"dialogAddtocart");
            Check(game.CartLines.Single().action==CartAction.NewUnits&&game.CartLines.Single().quantity==2&&game.CartLines.Single().install&&game.CartLines.Single().positions.SequenceEqual(new[]{role+":2",role+":3"}),"Native purchase stages the new compound at exact rear positions");
            var prepared=PartCart.Preview(game.Profile,game.Profile.SelectedCar,game.CartLines);var pack=game.Profile.parts.Find(p=>p.instanceId==game.Profile.SelectedCar.installed.slots.First(s=>s.slot==slot).partInstanceId);
            var defs=wheel?prepared.build.spec.cornerWheelDefinitions:prepared.build.spec.cornerTireDefinitions;
            Check(defs[0].id==(wheel?WheelFitments.DefinitionId(pack,0):TireFitments.DefinitionId(pack,0))&&defs[2].id==offer.Definition.id&&before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Cumulative mixed-tire preview keeps actual fronts and does not buy, heal, save or charge during staging");
            game.ClearNativeCart();yield return new WaitForSecondsRealtime(.2f);
        }
#endif
    }
}
