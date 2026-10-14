using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Editor
{
    public static class DrivingCorrectionsVerification
    {
        public static void Run()
        {
            int checks=0;var report=new StringBuilder();void Check(bool ok,string label){checks++;if(!ok)throw new InvalidOperationException(label);report.AppendLine("PASS "+label);}
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            foreach(var v in vehicles){var build=catalog.Resolve(v.spec.id,catalog.Recipe(v.spec.id),v.CreateStockTune());
                CarSimulation Car(){var c=new CarSimulation(build.spec,build.tune,new DriverSettings{wearMode=WearMode.None,fuelUsage=FuelUsageMode.None,optimalThermals=true});c.Launch(0);c.state.speed=30;c.state.rpm=3500;c.state.finished=true;c.state.elapsed=12.3;c.state.trapSpeed=30;c.state.distance=402.336;c.state.brake=0;return c;}
                var car=Car();double first=car.AssistedShutdownBrake(DragMath.FixedStep);Check(first>0&&first<.02,v.spec.id+" first automatic brake step eases in");double maximum=0;for(int i=0;i<3000&&car.state.speed>0;i++){double pedal=car.AssistedShutdownBrake(DragMath.FixedStep);maximum=Math.Max(maximum,pedal);car.Shutdown(DragMath.FixedStep,pedal);}
                Check(car.state.speed<.1&&maximum<=.60001,v.spec.id+" gentle shutdown stops with bounded pressure");Check(car.state.elapsed==12.3&&car.state.trapSpeed==30&&car.state.finished,v.spec.id+" shutdown preserves recorded race result");
                car=Car();car.state.brake=.4;car.state.contact.slip[0]=-.6;Check(car.AssistedShutdownBrake(DragMath.FixedStep)<.4,v.spec.id+" wheel-lock slip releases automatic pressure");
                car=Car();car.Shutdown(DragMath.FixedStep,1);Check(car.state.brake==1,v.spec.id+" direct manual brake remains fully available");
            }
            report.AppendLine("Checks: "+checks+"; failures: 0");string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/driving-corrections"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"verification.txt"),report.ToString(),new UTF8Encoding(false));Debug.Log("Driving corrections passed: "+checks);
        }
    }
}
