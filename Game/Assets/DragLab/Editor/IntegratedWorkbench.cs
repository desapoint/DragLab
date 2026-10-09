using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using System.Collections.Generic;
namespace DragLab.Editor {
 public sealed partial class DragWorkbench {
  bool integratedOpen=true,partFieldsOpen,buildFieldsOpen; PartCategory authoredCategory;string authoredId,authoredVehicle,buildVehicle,buildSummary;PartDefinition authoredDraft;
  Dictionary<PartCategory,string> testBuild;
  void DrawIntegratedTools(){
   integratedOpen=EditorGUILayout.Foldout(integratedOpen,"Integrated build / part-authoring tools",true);if(!integratedOpen)return;
   EditorGUILayout.HelpBox("Project authoring uses stable part IDs. Draft edits require Apply; player tuning permissions stay part-owned. Captured physical runs retain their original fitted definitions. Legacy stage controls below are balance probes; the integrated matrix uses actual resolved parts.",MessageType.Info);
   var assets=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");var defaults=new MechanicalCatalog(assets.Select(v=>v.spec),false);var catalog=new MechanicalCatalog(assets.Select(v=>v.spec));
   authoredCategory=(PartCategory)EditorGUILayout.EnumPopup("Part family",authoredCategory);
   var choices=catalog.parts.Where(p=>p.category==authoredCategory&&catalog.Fits(vehicle.spec,p)).ToArray();
   if(choices.Length>0){int index=Math.Max(0,Array.FindIndex(choices,p=>p.id==authoredId));index=EditorGUILayout.Popup("Definition",index,choices.Select(p=>p.name).ToArray());
    if(authoredId!=choices[index].id||authoredVehicle!=vehicle.spec.id){authoredId=choices[index].id;authoredVehicle=vehicle.spec.id;authoredDraft=choices[index].Copy();}
    EditorGUILayout.SelectableLabel(authoredId, GUILayout.Height(18));EditorGUILayout.LabelField(PartTuning.Summary(authoredDraft),EditorStyles.wordWrappedLabel);
    foreach(var stat in PartSemantics.Stats(authoredDraft))EditorGUILayout.LabelField(stat.label,stat.value.ToString("G5")+" "+stat.unit);
    partFieldsOpen=EditorGUILayout.Foldout(partFieldsOpen,"Edit authored parameters (draft)",true);
    if(partFieldsOpen)foreach(var f in typeof(PartDefinition).GetFields().Where(f=>PartCatalogOverrides.Editable(f.Name))){string label=ObjectNames.NicifyVariableName(f.Name);object value=f.GetValue(authoredDraft);
     if(f.FieldType==typeof(float))value=EditorGUILayout.FloatField(label,(float)value);
     else if(f.FieldType==typeof(int))value=EditorGUILayout.IntField(label,(int)value);
     else if(f.FieldType==typeof(bool))value=EditorGUILayout.Toggle(label,(bool)value);
     else if(f.FieldType==typeof(string))value=EditorGUILayout.TextField(label,(string)value??"");
     else if(f.FieldType.IsEnum)value=f.Name=="tuning"?EditorGUILayout.EnumFlagsField(label,(Enum)value):EditorGUILayout.EnumPopup(label,(Enum)value);
     f.SetValue(authoredDraft,value);
    }
    string error=PartCatalogOverrides.Validate(defaults.Find(authoredId),authoredDraft);if(error!=null)EditorGUILayout.HelpBox(error,MessageType.Error);
    EditorGUILayout.BeginHorizontal();using(new EditorGUI.DisabledScope(error!=null))if(GUILayout.Button("Apply project part override"))SavePartOverride(defaults,false);
    if(GUILayout.Button("Discard draft"))authoredDraft=catalog.Find(authoredId).Copy();
    if(GUILayout.Button("Restore authored default"))SavePartOverride(defaults,true);EditorGUILayout.EndHorizontal();
   }
   if(buildVehicle!=vehicle.spec.id||testBuild==null){buildVehicle=vehicle.spec.id;testBuild=catalog.Recipe(buildVehicle);buildSummary=null;}
   buildFieldsOpen=EditorGUILayout.Foldout(buildFieldsOpen,"Actual mixed-part test assembly",true);
   if(buildFieldsOpen){
    EditorGUILayout.HelpBox("This developer assembly is isolated from player ownership. Applied project definitions and the tune controls below are resolved through the production build resolver. Fixed hardware still constrains tuning.",MessageType.Info);
    foreach(var slot in MechanicalCatalog.Slots(vehicle.spec)){
     var options=catalog.parts.Where(p=>p.category==slot&&catalog.Fits(vehicle.spec,p)).ToArray();string installed;testBuild.TryGetValue(slot,out installed);int selected=Math.Max(0,Array.FindIndex(options,p=>p.id==installed)+1);
     int next=EditorGUILayout.Popup(slot.ToString(),selected,new[]{"Removed"}.Concat(options.Select(p=>p.name)).ToArray());if(next!=selected){if(next==0)testBuild.Remove(slot);else testBuild[slot]=options[next-1].id;buildSummary=null;}
    }
    var resolved=catalog.Resolve(vehicle.spec.id,testBuild,tune);foreach(string error in resolved.errors)EditorGUILayout.HelpBox(error,MessageType.Error);foreach(string warning in resolved.warnings)EditorGUILayout.HelpBox(warning,MessageType.Warning);
    EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Reset test assembly to OEM")){testBuild=catalog.Recipe(vehicle.spec.id);buildSummary=null;}
    using(new EditorGUI.DisabledScope(!resolved.Drivable)){
     if(GUILayout.Button("Benchmark resolved assembly")){var run=Benchmark.QuarterMile(resolved.spec,resolved.tune);buildSummary=run.elapsed.ToString("F3")+" s / "+run.trapKmh.ToString("F1")+" km/h / "+run.points+" PP / "+run.performanceClass;}
     if(GUILayout.Button("Export repeatable physical scenario")){
      string path=EditorUtility.SaveFilePanel("Export mixed-part physical scenario","",vehicle.spec.id+"-assembly.json","json");if(path.Length>0){var run=new PhysicalDrivingSession(new RunSnapshot{vehicle=resolved.spec,tune=resolved.tune,driving=new DriverSettings{shifting=ShiftMode.Sequential,throttleAssist=false,wearMode=WearMode.Full}});foreach(var input in PhysicalScenarios.Timeline(PhysicalScenario.FullPullShiftCoastBrake,1234))run.Step(input);File.WriteAllText(path,JsonUtility.ToJson(run.snapshot,true));File.WriteAllText(Path.ChangeExtension(path,"csv"),run.Csv());message="Resolved assembly JSON and full CSV exported.";}
     }
    }EditorGUILayout.EndHorizontal();if(!string.IsNullOrEmpty(buildSummary))EditorGUILayout.LabelField(buildSummary);
   }
   if(GUILayout.Button("Run integrated telemetry / authoring / calibration checks")){IntegratedToolsVerification.RunStandalone();message="2L calibration and traces exported to reports/phases/2l/.";}
   if(GUILayout.Button("Run current all-part / gearbox / induction / wear / failure matrix")){SliceTwoAudit.Run();message="Current matrix exported to reports/slice-2-audit/.";}
   if(GUILayout.Button("Run owned-condition recovery checks")){MechanicalRecoveryVerification.RunStandalone();message="Recovery evidence exported to reports/phases/2k/.";}
  }
  void SavePartOverride(MechanicalCatalog defaults,bool restore){
   var data=PartCatalogOverrides.Load();data.parts.RemoveAll(p=>p.id==authoredId);if(!restore)data.parts.Add(authoredDraft.Copy());PartCatalogOverrides.Apply(defaults,data,true);
   string path=Path.Combine(Application.dataPath,"DragLab/Resources/Data/part-overrides.json"),temp=path+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(data,true));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
   AssetDatabase.ImportAsset("Assets/DragLab/Resources/Data/part-overrides.json",ImportAssetOptions.ForceUpdate);authoredDraft=defaults.Find(authoredId).Copy();message=restore?"Part restored to authored default.":"Validated project part override applied.";
  }
 }
}
