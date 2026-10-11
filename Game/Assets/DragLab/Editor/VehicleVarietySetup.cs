using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Editor {
 public static class VehicleVarietySetup {
 public static void Prepare(){
 var cars=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
 foreach(var v in cars){if(string.IsNullOrEmpty(v.spec.engineFamily)){v.spec.engineFamily=v.spec.id;v.spec.chassisFamily=v.spec.id;v.spec.modelFamily=v.spec.displayName.Split(' ')[0];v.spec.trim="Standard";EditorUtility.SetDirty(v);}}
 var hatch=cars.Single(v=>v.spec.id=="starter-hatch");var coupe=cars.Single(v=>v.spec.id=="sport-coupe");var truck=cars.Single(v=>v.spec.id=="utility-pickup");
 Add(hatch,"juniper-economy","Juniper 1.4 Economy",2400,0,s=>{s.engineFamily="juniper-14";s.displacementLitres=s.sound.displacementLitres=1.4f;s.engineBoreMm=75;s.massKg-=65;s.trim="Economy";s.finalDrive=4.3f;s.torque=Array.ConvertAll(s.torque,t=>new TorquePoint(t.rpm,t.torqueNm*.94f));});
 Add(hatch,"juniper-club","Juniper 1.6 Club",3400,0,s=>{s.massKg-=35;s.trim="Club";s.tireFriction*=1.04f;s.torque=Array.ConvertAll(s.torque,t=>new TorquePoint(t.rpm,t.torqueNm*1.04f));});
 Add(coupe,"kestrel-track","Kestrel 2.0T Track",6200,4,s=>{s.massKg-=90;s.trim="Track";s.factoryForgedInternals=true;s.tireFriction*=1.07f;s.transmission.type=TransmissionType.DCT;});
 Add(truck,"timber-diesel","Timber 2.8D Work",7200,2,s=>Diesel(s,false));
 Add(truck,"timber-diesel-tow","Timber 2.8D Tow",8900,4,s=>Diesel(s,true));
 AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("Vehicle variety assets prepared: "+Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Length);
 }
 static void Add(VehicleDefinition source,string id,string name,int price,int rep,Action<VehicleSpec> tune){
 string path="Assets/DragLab/Resources/Data/Vehicles/"+id+".asset";var existing=AssetDatabase.LoadAssetAtPath<VehicleDefinition>(path);if(existing!=null){if(existing.spec.engineBoreMm==0){existing.spec.engineBoreMm=MechanicalCatalog.Bore(source.spec);EditorUtility.SetDirty(existing);}return;}
 var v=ScriptableObject.CreateInstance<VehicleDefinition>();v.spec=source.spec.Copy();v.spec.id=id;v.spec.displayName=name;v.spec.engineBoreMm=MechanicalCatalog.Bore(source.spec);tune(v.spec);v.purchasePrice=price;v.reputationRequired=rep;v.defaultPaint=source.defaultPaint;AssetDatabase.CreateAsset(v,path);
 }
 static void Diesel(VehicleSpec s,bool tow){
 s.engineFamily="irontrail-28d";s.trim=tow?"Tow":"Work";s.sound.cylinders=4;s.sound.layout=EngineLayout.Inline;s.displacementLitres=s.sound.displacementLitres=2.8f;s.engineBoreMm=94;
 s.combustion=new CombustionSpec{fuelKind=FuelKind.Diesel,referenceCompression=17.5f,compressionRatio=17.5f,crown=PistonCrown.DieselBowl,referenceCrownCc=12,crownCc=12};
 s.engineBehavior.fueling=FuelSystem.Diesel;s.engineBehavior.cut=s.engineBehavior.launchCut=CutMethod.Fuel;s.engineBehavior.limiter=LimiterStyle.Soft;s.engineBehavior.twoStep=false;s.engineBehavior.antiLag=s.engineBehavior.launchPreload=false;s.engineBehavior.camOverlap=.02f;s.engineBehavior.mechanicalClatter=.12f;
 s.induction.factory=InductionType.Turbo;s.induction.turboBoostBar=tow?1.15f:.9f;s.induction.torqueCurveIncludesFactoryInduction=true;
 s.fuel.densityKgPerLitre=.835f;s.fuel.bsfcKgPerKwh=.235f;s.fuel.idleLitresPerHour=.65f;s.fuel.capacityLitres=65;
 s.idleRpm=750;s.redlineRpm=4500;s.massKg=tow?2130:2020;s.finalDrive=tow?4.1f:3.73f;s.transmission.type=TransmissionType.Automatic;s.transmission.converterStallRpm=1900;s.transmission.cvtTargetRpm=3500;s.factoryForgedInternals=true;s.valvesPerCylinder=4;
 s.torque=new[]{new TorquePoint(750,160),new TorquePoint(1500,tow?440:380),new TorquePoint(2200,tow?480:420),new TorquePoint(3000,tow?460:390),new TorquePoint(3800,330),new TorquePoint(4500,230)};
 }
 }
}
