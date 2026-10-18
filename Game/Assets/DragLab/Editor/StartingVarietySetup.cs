using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
namespace DragLab.Editor
{
    // Additive authored content. Existing assets/IDs and player-owned configurations stay intact.
    public static class StartingVarietySetup
    {
        public static void Prepare()
        {
            VehicleVarietySetup.Prepare();
            Add("starter-hatch","finch","Finch",CarBodyType.Hatchback,DriveType.FWD,1.3f,920,.91f,2600,0);
            Add("starter-hatch","wren","Wren",CarBodyType.Hatchback,DriveType.FWD,1.5f,980,.98f,3100,0);
            Add("starter-hatch","comet","Comet",CarBodyType.Sedan,DriveType.RWD,1.6f,1100,1.04f,3800,0);
            Add("starter-hatch","aster","Aster",CarBodyType.Wagon,DriveType.FWD,1.8f,1210,1.13f,3600,0);
            Add("starter-hatch","lark","Lark",CarBodyType.Coupe,DriveType.RWD,1.6f,1020,1.06f,4100,0);
            Add("starter-hatch","mistral","Mistral",CarBodyType.Sedan,DriveType.FWD,1.8f,1160,1.15f,3900,0);
            Add("starter-hatch","rook","Rook",CarBodyType.Hatchback,DriveType.FWD,1.4f,970,.96f,2800,0);
            Add("starter-hatch","swift","Swift",CarBodyType.Roadster,DriveType.RWD,1.6f,990,1.02f,4900,0);
            Add("starter-hatch","vale","Vale",CarBodyType.Sedan,DriveType.RWD,2f,1270,1.26f,5200,0);
            Add("starter-hatch","cinder","Cinder",CarBodyType.Coupe,DriveType.FWD,1.8f,1110,1.18f,4400,0);
            Add("starter-hatch","reed","Reed",CarBodyType.Wagon,DriveType.FWD,1.6f,1140,1.05f,3300,0);
            Add("starter-hatch","piper","Piper",CarBodyType.Hatchback,DriveType.FWD,1.5f,940,1.01f,3500,0);
            Add("starter-hatch","sable","Sable",CarBodyType.Sedan,DriveType.RWD,2f,1230,1.28f,8850,0);
            Add("sport-coupe","osprey","Osprey",CarBodyType.Coupe,DriveType.RWD,2.2f,1300,1.12f,9300,5);
            Add("rally-sedan","boreal","Boreal",CarBodyType.Wagon,DriveType.AWD,2.4f,1560,1.12f,12500,7);
            Add("touring-six","solstice","Solstice",CarBodyType.Roadster,DriveType.RWD,3.2f,1370,1.13f,16000,9);
            Add("super-v8","atlas","Atlas",CarBodyType.SUV,DriveType.AWD,5.2f,2150,1.13f,23000,12);
            Add("timber-diesel","fjord","Fjord",CarBodyType.Wagon,DriveType.FWD,2.8f,1520,.86f,8400,3);
            Trim("city-three","sprig-sport","Sport",.12f,80,650,0);
            Trim("rally-sedan","tempest-rs","RS",.13f,-50,2400,2);
            Trim("touring-six","meridian-sport","Sport",.12f,-65,2200,2);
            var sable=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Single(v=>v.spec.id=="sable");sable.purchasePrice=8850;EditorUtility.SetDirty(sable);
            var sableSport=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Single(v=>v.spec.id=="sable-sport");sableSport.purchasePrice=11682;EditorUtility.SetDirty(sableSport);
            EconomyBalanceVerification.ApplyRaceRewards();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("Starting roster: "+Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Length+" configurations");
        }
        static void Add(string source,string id,string family,CarBodyType body,DriveType drive,float litres,float mass,float torque,int price,int rep)
        {
            var original=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Single(v=>v.spec.id==source);
            string path="Assets/DragLab/Resources/Data/Vehicles/"+id+".asset";
            var v=AssetDatabase.LoadAssetAtPath<VehicleDefinition>(path);
            if(v==null){
                v=ScriptableObject.CreateInstance<VehicleDefinition>();v.spec=original.spec.Copy();var s=v.spec;
                s.id=id;s.displayName=family+" "+litres.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" Base";
                s.modelFamily=family;s.trim="Base";s.chassisFamily=id;s.engineFamily=id+"-"+litres.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);
                s.bodyType=body;s.drive=drive;s.displacementLitres=s.sound.displacementLitres=litres;s.engineBoreMm=MechanicalBore(original.spec,litres);
                s.massKg=mass;s.frontWeightFraction=drive==DriveType.RWD?.52f:drive==DriveType.AWD?.56f:.61f;
                s.wheelbaseM=body==CarBodyType.Roadster?2.35f:body==CarBodyType.Wagon?2.7f:body==CarBodyType.SUV?2.95f:2.55f;
                s.dragAreaM2=body==CarBodyType.SUV?.95f:body==CarBodyType.Wagon?.72f:body==CarBodyType.Roadster?.61f:.65f;
                s.torque=Array.ConvertAll(s.torque,t=>new TorquePoint(t.rpm,t.torqueNm*torque));
                v.purchasePrice=price;v.reputationRequired=rep;v.defaultPaint=Color.HSVToRGB((float)((price%997)/997.0),.46f,.72f);
                AssetDatabase.CreateAsset(v,path);
            }
            Trim(id,id+"-sport",rep==0?"Sport":"Performance",.12f,-35,(int)(price*.32f),rep==0?0:2);
        }
        static float MechanicalBore(VehicleSpec s,float litres)=>DragLab.Domain.Mechanics.MechanicalCatalog.Bore(s)*(float)Math.Pow(litres/s.DisplacementLitres,1.0/3);
        static void Trim(string source,string id,string label,float gain,float massDelta,int priceDelta,int repDelta)
        {
            string path="Assets/DragLab/Resources/Data/Vehicles/"+id+".asset";if(AssetDatabase.LoadAssetAtPath<VehicleDefinition>(path)!=null)return;
            var original=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Single(v=>v.spec.id==source);
            var v=ScriptableObject.CreateInstance<VehicleDefinition>();v.spec=original.spec.Copy();v.spec.id=id;v.spec.trim=label;
            v.spec.displayName=original.spec.displayName.Replace(" Base","")+" "+label;v.spec.massKg+=massDelta;v.spec.finalDrive*=1.04f;
            v.spec.torque=Array.ConvertAll(v.spec.torque,t=>new TorquePoint(t.rpm,t.torqueNm*(1+gain)));v.spec.tireFriction*=1.025f;
            v.purchasePrice=original.purchasePrice+priceDelta;v.reputationRequired=original.reputationRequired+repDelta;v.defaultPaint=original.defaultPaint;
            AssetDatabase.CreateAsset(v,path);
        }
    }
}
