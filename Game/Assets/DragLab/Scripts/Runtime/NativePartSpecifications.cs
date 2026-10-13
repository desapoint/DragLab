using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal static class NativePartSpecifications
    {
        internal static PartStat[] Summary(PartDefinition p)
        {
            string[] priority;
            switch(p.category){
                case PartCategory.Tires:priority=new[]{"grip","rollinglife","slidinglife","tireoptimal"};break;
                case PartCategory.Wheels:priority=new[]{"mass","massscale"};break;
                case PartCategory.Cooling:priority=new[]{"radiator","transfer","fluidcapacity","thermalmass"};break;
                case PartCategory.Clutch:priority=new[]{"torque","plates","life","heat"};break;
                case PartCategory.Brakes:priority=new[]{"capacity","heat","life","mass"};break;
                case PartCategory.Gearbox:priority=new[]{"shift","fixedratio","torque","heat"};break;
                case PartCategory.Flywheel:priority=new[]{"inertia","mass","rpm"};break;
                case PartCategory.Camshaft:priority=new[]{"camlift","duration","overlap","valvetrainrpm"};break;
                case PartCategory.ValveSprings:priority=new[]{"springlift","valvetrainrpm","servicelife","mass"};break;
                case PartCategory.Pistons:priority=new[]{"crownvolume","relief","rpm","torque"};break;
                case PartCategory.Induction:priority=new[]{"turbohardware","spoolscale","airflow","boost","pulley"};break;
                case PartCategory.HeadIntake:priority=new[]{"airflow","rpm","heat","mass"};break;
                case PartCategory.FuelSystem:case PartCategory.Ignition:priority=new[]{"powerlimit","idlecontrol","mixturevariation","mass"};break;
                case PartCategory.OilPan:priority=new[]{"fluidcapacity","fluidminimum","baffling","mass"};break;
                case PartCategory.OilCooler:priority=new[]{"oilcooling","servicelife","heat","mass"};break;
                case PartCategory.Body:case PartCategory.Interior:case PartCategory.Accessories:priority=new[]{"mass","massscale"};break;
                default:priority=new[]{"bias","preload","pumpflow","cappressure","filtercapacity","servicelife","injectorcylinders","torque","rpm","heat","mass"};break;
            }
            var stats=PartSemantics.Stats(p).ToArray();return stats.OrderBy(s=>{int i=Array.IndexOf(priority,s.key);return i<0?priority.Length:i;}).Take(4).Select(s=>Rename(s)).ToArray();
        }
        static PartStat Rename(PartStat stat)
        {
            switch(stat.key){case "grip":stat.label="Grip factor";break;case "rollinglife":stat.label="Reference lifespan";break;case "slidinglife":stat.label="Slip-life budget";break;case "tireoptimal":stat.label="Best grip temperature";break;case "mass":stat.label="Mass";break;case "massscale":stat.label="Mass factor";break;case "airflow":stat.label="Airflow factor";break;case "spoolscale":stat.label="Spool-time factor";break;case "turbohardware":stat.label="Turbo size";break;case "shift":stat.label="Shift-time factor";break;}return stat;
        }
    }
    internal sealed class NativePartSpecificationCache
    {
        readonly Dictionary<string,PartStat[]> values=new Dictionary<string,PartStat[]>();
        internal PartStat[] Get(PartDefinition d)
        {string key=JsonUtility.ToJson(d);if(values.TryGetValue(key,out var stats))return stats;if(values.Count>=512)values.Clear();return values[key]=NativePartSpecifications.Summary(d);}
    }
}
