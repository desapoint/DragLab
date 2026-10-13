using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace DragLab.Domain {
 public sealed class DrivingTraceChannel {
  public readonly string group,label; readonly Func<DrivingTraceSample,double> read;
  public DrivingTraceChannel(string group,string label,Func<DrivingTraceSample,double> read){this.group=group;this.label=label;this.read=read;}
  public double Value(DrivingTraceSample sample){return read(sample);}
 }
 // One measured channel registry for plots, CSV and acceptance tests. CSV units are explicit.
 public static class DrivingTraceChannels {
  public static readonly DrivingTraceChannel[] All=Create();
  public static readonly string[] Groups=All.Select(c=>c.group).Distinct().ToArray();
  static DrivingTraceChannel[] Create(){var list=new List<DrivingTraceChannel>();
   list.Add(new DrivingTraceChannel("Power","HP",s=>s.hp*1));
   list.Add(new DrivingTraceChannel("Power","RPM",s=>s.rpm*1));
   list.Add(new DrivingTraceChannel("Power","Speed km/h",s=>s.speedKmh*1));
   list.Add(new DrivingTraceChannel("Power","Shaft Nm",s=>s.shaftNm*1));
   list.Add(new DrivingTraceChannel("Contact","Max slip %",s=>s.slipEstimate*100));
   list.Add(new DrivingTraceChannel("Coupling","Clutch slip RPM",s=>s.signedClutchSlipRpm*1));
   list.Add(new DrivingTraceChannel("Condition","Engine damage %",s=>s.engineDamage*100));
   list.Add(new DrivingTraceChannel("Power","Boost bar",s=>s.boost*1));
   list.Add(new DrivingTraceChannel("Fluids","Oil L",s=>s.oilLitres*1));
   list.Add(new DrivingTraceChannel("Fluids","Coolant L",s=>s.coolantLitres*1));
   list.Add(new DrivingTraceChannel("Fluids","Oil °C",s=>s.oilTemperatureC*1));
   list.Add(new DrivingTraceChannel("Fluids","Oil bar",s=>s.oilPressureBar*1));
   list.Add(new DrivingTraceChannel("Fluids","Oil condition %",s=>s.oilCondition*100));
   list.Add(new DrivingTraceChannel("Condition","Valve float %",s=>s.valveFloat*100));
   list.Add(new DrivingTraceChannel("Condition","Cylinder output %",s=>s.cylinderFactor*100));
   list.Add(new DrivingTraceChannel("Condition","Local float %",s=>s.individualValveFloat*100));
   list.Add(new DrivingTraceChannel("Fluids","Clutch oil L",s=>s.clutchOilL*1));
   list.Add(new DrivingTraceChannel("Power","Engine Nm",s=>s.engineNm*1));
   list.Add(new DrivingTraceChannel("Power","Gear",s=>s.gear*1));
   list.Add(new DrivingTraceChannel("Power","Throttle %",s=>s.throttle*100));
   list.Add(new DrivingTraceChannel("Power","Clutch pedal %",s=>s.clutch*100));
   list.Add(new DrivingTraceChannel("Power","Brake pedal %",s=>s.brake*100));
   list.Add(new DrivingTraceChannel("Power","Turbo spool %",s=>s.turboSpool*100));
   list.Add(new DrivingTraceChannel("Contact","Front slip %",s=>s.frontSlipEstimate*100));
   list.Add(new DrivingTraceChannel("Contact","Rear slip %",s=>s.rearSlipEstimate*100));
   list.Add(new DrivingTraceChannel("Contact","Front load N",s=>s.frontLoadN*1));
   list.Add(new DrivingTraceChannel("Contact","Rear load N",s=>s.rearLoadN*1));
   list.Add(new DrivingTraceChannel("Contact","Acceleration m/s²",s=>s.acceleration*1));
   list.Add(new DrivingTraceChannel("Contact","Front bias %",s=>s.frontShare*100));
   list.Add(new DrivingTraceChannel("Energy","Body kJ",s=>s.bodyEnergyJ*0.001));
   list.Add(new DrivingTraceChannel("Energy","Wheels kJ",s=>s.wheelEnergyJ*0.001));
   list.Add(new DrivingTraceChannel("Energy","Flywheel kJ",s=>s.flywheelJ*0.001));
   list.Add(new DrivingTraceChannel("Energy","Drive work kJ",s=>s.driveWorkJ*0.001));
   list.Add(new DrivingTraceChannel("Energy","Contact loss kJ",s=>s.contactWorkJ*0.001));
   list.Add(new DrivingTraceChannel("Energy","Diff loss kJ",s=>s.differentialWorkJ*0.001));
   list.Add(new DrivingTraceChannel("Energy","Resistance kJ",s=>s.resistanceWorkJ*0.001));
   list.Add(new DrivingTraceChannel("Energy","Brake work kJ",s=>s.brakeWorkJ*0.001));
   list.Add(new DrivingTraceChannel("Energy","Clutch heat kW",s=>s.clutchHeatW*0.001));
   list.Add(new DrivingTraceChannel("Energy","Brake heat kW",s=>s.brakeHeatW*0.001));
   list.Add(new DrivingTraceChannel("Energy","Air drag N",s=>s.aeroResistanceN*1));
   list.Add(new DrivingTraceChannel("Energy","Rolling drag N",s=>s.rollingResistanceN*1));
   list.Add(new DrivingTraceChannel("Coupling","Converter slip RPM",s=>s.converterSlipRpm*1));
   list.Add(new DrivingTraceChannel("Coupling","CVT ratio",s=>s.cvtRatio*1));
   list.Add(new DrivingTraceChannel("Coupling","Input RPM",s=>s.gearboxInputRpm*1));
   list.Add(new DrivingTraceChannel("Coupling","Front shaft Nm",s=>s.frontShaftNm*1));
   list.Add(new DrivingTraceChannel("Coupling","Rear shaft Nm",s=>s.rearShaftNm*1));
   list.Add(new DrivingTraceChannel("Coupling","Front shaft kW",s=>s.frontShaftPowerW*0.001));
   list.Add(new DrivingTraceChannel("Coupling","Rear shaft kW",s=>s.rearShaftPowerW*0.001));
   list.Add(new DrivingTraceChannel("Coupling","AWD slip work kJ",s=>s.awdCouplingWorkJ*.001));
   list.Add(new DrivingTraceChannel("Coupling","AWD heat kW",s=>s.awdCouplingHeatW*.001));
   list.Add(new DrivingTraceChannel("Mass / fuel","Total kg",s=>s.translatingMassKg*1));
   list.Add(new DrivingTraceChannel("Mass / fuel","Dry kg",s=>s.dryMassKg*1));
   list.Add(new DrivingTraceChannel("Mass / fuel","Driver kg",s=>s.driverMassKg*1));
   list.Add(new DrivingTraceChannel("Mass / fuel","Payload kg",s=>s.payloadMassKg*1));
   list.Add(new DrivingTraceChannel("Mass / fuel","Fuel kg",s=>s.fuelMassKg*1));
   list.Add(new DrivingTraceChannel("Mass / fuel","Fuel L",s=>s.fuelLitres*1));
   list.Add(new DrivingTraceChannel("Mass / fuel","Used L",s=>s.fuelUsedLitres*1));
   list.Add(new DrivingTraceChannel("Mass / fuel","Fuel L/h",s=>s.fuelFlowLps*3600));
   list.Add(new DrivingTraceChannel("Mass / fuel","CG height m",s=>s.cgHeightM*1));
   list.Add(new DrivingTraceChannel("Mass / fuel","CG position m",s=>s.cgPositionM*1));
   list.Add(new DrivingTraceChannel("Condition","Clutch health %",s=>s.clutchHealth*100));
   list.Add(new DrivingTraceChannel("Condition","Shaft health %",s=>s.driveshaftHealth*100));
   list.Add(new DrivingTraceChannel("Condition","Clutch °C",s=>s.clutchTemperatureC*1));
   list.Add(new DrivingTraceChannel("Condition","Gearbox °C",s=>s.gearboxTemperatureC*1));
   list.Add(new DrivingTraceChannel("Condition","Engine thermal 0–1",s=>s.engineTemperature*1));
   list.Add(new DrivingTraceChannel("Condition","Cooling kW",s=>s.coolingHeatRejectedW*0.001));
   list.Add(new DrivingTraceChannel("Condition","Clutch oil life %",s=>s.clutchOilCondition*100));
   list.Add(new DrivingTraceChannel("Fluid losses","Oil burn L/h",s=>s.oilBurnLps*3600));
   list.Add(new DrivingTraceChannel("Fluid losses","Oil leak L/h",s=>s.oilLeakLps*3600));
   list.Add(new DrivingTraceChannel("Fluid losses","Coolant leak L/h",s=>s.coolantLeakLps*3600));
   list.Add(new DrivingTraceChannel("Fluid losses","Coolant vent L/h",s=>s.coolantVentLps*3600));
   list.Add(new DrivingTraceChannel("Fluid losses","Coolant bar",s=>s.coolantPressureBar*1));
   list.Add(new DrivingTraceChannel("Wheel RPM","FL RPM",s=>s.flWheelRpm*1));
   list.Add(new DrivingTraceChannel("Wheel slip","FL slip %",s=>s.frontLeftSlip*100));
   list.Add(new DrivingTraceChannel("Wheel torque","FL drive Nm",s=>s.wheelDriveNm[0]*1));
   list.Add(new DrivingTraceChannel("Wheel torque","FL brake Nm",s=>s.wheelBrakeNm[0]*1));
   list.Add(new DrivingTraceChannel("Wheel torque","FL contact Nm",s=>s.wheelContactNm[0]*1));
   list.Add(new DrivingTraceChannel("Wheel force","FL force N",s=>s.wheelForceN[0]*1));
   list.Add(new DrivingTraceChannel("Tires","FL health %",s=>s.tireHealth[0]*100));
   list.Add(new DrivingTraceChannel("Tires","FL °C",s=>s.tireTemperatureC[0]*1));
   list.Add(new DrivingTraceChannel("Tires","FL slip kJ",s=>s.tireSlipEnergyJ[0]*0.001));
   list.Add(new DrivingTraceChannel("Wheel RPM","FR RPM",s=>s.frWheelRpm*1));
   list.Add(new DrivingTraceChannel("Wheel slip","FR slip %",s=>s.frontRightSlip*100));
   list.Add(new DrivingTraceChannel("Wheel torque","FR drive Nm",s=>s.wheelDriveNm[1]*1));
   list.Add(new DrivingTraceChannel("Wheel torque","FR brake Nm",s=>s.wheelBrakeNm[1]*1));
   list.Add(new DrivingTraceChannel("Wheel torque","FR contact Nm",s=>s.wheelContactNm[1]*1));
   list.Add(new DrivingTraceChannel("Wheel force","FR force N",s=>s.wheelForceN[1]*1));
   list.Add(new DrivingTraceChannel("Tires","FR health %",s=>s.tireHealth[1]*100));
   list.Add(new DrivingTraceChannel("Tires","FR °C",s=>s.tireTemperatureC[1]*1));
   list.Add(new DrivingTraceChannel("Tires","FR slip kJ",s=>s.tireSlipEnergyJ[1]*0.001));
   list.Add(new DrivingTraceChannel("Wheel RPM","RL RPM",s=>s.rlWheelRpm*1));
   list.Add(new DrivingTraceChannel("Wheel slip","RL slip %",s=>s.rearLeftSlip*100));
   list.Add(new DrivingTraceChannel("Wheel torque","RL drive Nm",s=>s.wheelDriveNm[2]*1));
   list.Add(new DrivingTraceChannel("Wheel torque","RL brake Nm",s=>s.wheelBrakeNm[2]*1));
   list.Add(new DrivingTraceChannel("Wheel torque","RL contact Nm",s=>s.wheelContactNm[2]*1));
   list.Add(new DrivingTraceChannel("Wheel force","RL force N",s=>s.wheelForceN[2]*1));
   list.Add(new DrivingTraceChannel("Tires","RL health %",s=>s.tireHealth[2]*100));
   list.Add(new DrivingTraceChannel("Tires","RL °C",s=>s.tireTemperatureC[2]*1));
   list.Add(new DrivingTraceChannel("Tires","RL slip kJ",s=>s.tireSlipEnergyJ[2]*0.001));
   list.Add(new DrivingTraceChannel("Wheel RPM","RR RPM",s=>s.rrWheelRpm*1));
   list.Add(new DrivingTraceChannel("Wheel slip","RR slip %",s=>s.rearRightSlip*100));
   list.Add(new DrivingTraceChannel("Wheel torque","RR drive Nm",s=>s.wheelDriveNm[3]*1));
   list.Add(new DrivingTraceChannel("Wheel torque","RR brake Nm",s=>s.wheelBrakeNm[3]*1));
   list.Add(new DrivingTraceChannel("Wheel torque","RR contact Nm",s=>s.wheelContactNm[3]*1));
   list.Add(new DrivingTraceChannel("Wheel force","RR force N",s=>s.wheelForceN[3]*1));
   list.Add(new DrivingTraceChannel("Tires","RR health %",s=>s.tireHealth[3]*100));
   list.Add(new DrivingTraceChannel("Tires","RR °C",s=>s.tireTemperatureC[3]*1));
   list.Add(new DrivingTraceChannel("Tires","RR slip kJ",s=>s.tireSlipEnergyJ[3]*0.001));
   list.Add(new DrivingTraceChannel("Coupling","Converter heat kW",s=>s.converterHeatW*.001));
   list.Add(new DrivingTraceChannel("Coupling","Cooling transfer ×",s=>s.engineCoolingFactor));
   list.Add(new DrivingTraceChannel("Condition","Brake capacity %",s=>s.brakeCapacityFactor*100));
   foreach(Mechanics.PartCategory category in Enum.GetValues(typeof(Mechanics.PartCategory))){int index=(int)category;list.Add(new DrivingTraceChannel("Part health "+(index/12+1),"Part "+category+" health %",s=>s.partHealth[index]*100));list.Add(new DrivingTraceChannel("Part heat "+(index/12+1),"Part "+category+" °C",s=>s.partTemperatureC[index]));}
   for(int c=0;c<12;c++){int cylinder=c;list.Add(new DrivingTraceChannel("Cylinders","C"+(c+1)+" output %",s=>s.cylinderContribution.Length>cylinder?s.cylinderContribution[cylinder]*100:0));}
   return list.ToArray();
  }
  public static string AppendCsv(string existing,IList<DrivingTraceSample> samples){
   var rows=existing.TrimEnd('\n').Split('\n');var output=new StringBuilder(rows[0]);
   foreach(var c in All)output.Append(",trace_"+c.label.Replace(" ","_").Replace("/","_"));output.Append('\n');
   for(int i=0;i<samples.Count;i++){output.Append(rows[i+1]);foreach(var c in All)output.Append(","+c.Value(samples[i]).ToString("R",CultureInfo.InvariantCulture));output.Append('\n');}return output.ToString();
  }
 }
}
