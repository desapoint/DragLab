using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace DragLab.Domain.Mechanics {
 [Serializable] public sealed class PartCatalogOverrideData { public int version=1; public List<PartDefinition> parts=new List<PartDefinition>(); }
 // Project authoring data. Captured fitted definitions remain owned by their replay.
 public static class PartCatalogOverrides {
  public const string ResourcePath="Data/part-overrides";
  public static readonly string[] IdentityFields={"id","category","fitment","boreMm","stage"};
  public static bool Editable(string field){return !IdentityFields.Contains(field);}
  public static PartCatalogOverrideData Load(){var asset=Resources.Load<TextAsset>(ResourcePath);return asset==null?new PartCatalogOverrideData():JsonUtility.FromJson<PartCatalogOverrideData>(asset.text);}
  public static string Validate(PartDefinition original,PartDefinition draft){
   if(original==null||draft==null)return "Unknown part.";
   foreach(var f in typeof(PartDefinition).GetFields()){
    object value=f.GetValue(draft);
    if(!Editable(f.Name)&&!Equals(value,f.GetValue(original)))return "Stable identity/fitment cannot change: "+f.Name;
    if(f.FieldType==typeof(float)){double n=(float)value;if(!MechanicsSchema.Finite(n)||Math.Abs(n)>1e12)return "Invalid number: "+f.Name;if(n<0&&f.Name!="camTorqueBias"&&f.Name!="positionFromFrontAxleM"&&f.Name!="crownVolumeCc")return "Negative number: "+f.Name;}
    if(f.FieldType==typeof(int)&&((int)value<0||(int)value>10000000))return "Invalid integer: "+f.Name;
    if(f.FieldType.IsEnum&&(f.Name=="tuning"?(((int)(PartTuningOptions)value&~511)!=0):!Enum.IsDefined(f.FieldType,value)))return "Invalid option: "+f.Name;
   }
   if(string.IsNullOrWhiteSpace(draft.name))return "Name required.";
   if(new[]{draft.power,draft.grip,draft.massScale,draft.shiftScale,draft.capacityScale,draft.boostScale}.Any(n=>n<=0||n>4))return "Hardware multipliers must be >0 and <=4.";
   if(draft.centerRatioMin<.5||draft.centerRatioMax>2||draft.centerRatioMin>draft.centerRatioMax)return "Invalid center speed ratio range.";
   if(draft.boostMin>draft.boostMax||draft.pulleyMin>draft.pulleyMax||draft.finalDriveMin>draft.finalDriveMax||draft.fluidMinimumLitres>draft.fluidCapacityLitres)return "Invalid minimum/maximum range.";
   if(draft.clutchPlateCount<1||draft.clutchPlateCount>64||draft.clutchOilCapacityLitres>10)return "Invalid clutch plate/bath capacity.";
   if(PartSemantics.Wears(draft.category)&&(draft.heatCapacityJPerC<=0||draft.wearEnergyJ<=0||draft.overloadSeconds<=0||draft.thermalLimitC<=0))return "Wearing parts require positive thermal mass, life and overload tolerance.";
   if(draft.category==PartCategory.Tires&&draft.tireLifeDistanceM<=0)return "Tire rolling lifetime must be positive.";
   if(draft.torqueBiasRatio<1||draft.torqueBiasRatio>10)return "Differential torque bias must be 1–10.";
   if(draft.turboSize<=0||draft.fixedBoost<=0||draft.fixedPulley<=0||draft.sequentialShiftScale<=0)return "Physical hardware scales must be positive.";
   return null;
  }
  public static void Apply(MechanicalCatalog catalog,PartCatalogOverrideData data,bool requireAll=false){
   if(data==null||data.version!=1||data.parts==null)throw new ArgumentException("Unsupported part-authoring data.");
   var seen=new HashSet<string>();var pending=new List<PartDefinition>();
   foreach(var draft in data.parts){if(draft==null||string.IsNullOrEmpty(draft.id)||!seen.Add(draft.id))throw new ArgumentException("Missing/duplicate override identity.");var original=catalog.Find(draft.id);if(original==null){if(requireAll)throw new ArgumentException("Unknown override: "+draft.id);continue;}string error=Validate(original,draft);if(error!=null)throw new ArgumentException(draft.id+": "+error);pending.Add(draft.Copy());}
   foreach(var draft in pending)catalog.parts[catalog.parts.FindIndex(p=>p.id==draft.id)]=draft;
  }
 }
}
