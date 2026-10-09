using System;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime {
 public sealed partial class PrototypeGame {
  float refuelRequested=5;
  internal double RefuelOwned(double amount){double added=FuelDynamics.Refuel(Build.fuel,Selected.spec.fuel,amount);if(added>0){profile.revision++;ProfileStore.Save(profile);}return added;}
  void FuelGarage(){double free=Selected.spec.fuel.capacityLitres-Build.fuel.litres;GUILayout.Label("FUEL / "+Build.fuel.litres.ToString("F2")+" / "+Selected.spec.fuel.capacityLitres.ToString("F0")+" L / "+(Build.fuel.litres*Selected.spec.fuel.densityKgPerLitre).ToString("F1")+" kg / refuelling free in Slice 2");float quantity=(float)Math.Min(refuelRequested,Math.Max(0,free));GUILayout.Label("Add litres: "+quantity.ToString("F2")+" / free space "+free.ToString("F2")+" L");if(TuningSlider.Draw(ref quantity,0,(float)Math.Max(0,free),Math.Min(5,(float)free)))refuelRequested=quantity;GUI.enabled=quantity>0&&free>0;if(GUILayout.Button("Refuel free / add selected litres")){double chosen=quantity;pendingAction=()=>RefuelOwned(chosen);}if(GUILayout.Button("Fill tank free"))pendingAction=()=>RefuelOwned(Selected.spec.fuel.capacityLitres);GUI.enabled=true;}
 }
 public sealed partial class PhysicalDrivingBench {
  float refuelRequested=5;
  public double Refuel(double amount){Archive();double added=FuelDynamics.Refuel(Session.car,amount);config.startingFuel=Session.car.FuelSnapshot();Session=Session.ContinueRecording(Recording);notice="Added "+added.ToString("F2")+" L to isolated test tank. Momentum, condition and pedal source retained.";return added;}
  void FuelControls(){var car=Session.car;double free=Math.Max(0,car.spec.fuel.capacityLitres-car.state.fuelLitres);GUILayout.Label("FUEL / "+car.state.fuelLitres.ToString("F2")+" / "+car.spec.fuel.capacityLitres.ToString("F0")+" L / used "+car.state.fuelUsedLitres.ToString("F3")+" L / flow "+(Paused?0:car.state.fuelFlowLitresPerSecond*3600).ToString("F2")+" L/h");float quantity=(float)Math.Min(refuelRequested,free);if(MassSlider("Add fuel (litres)",ref quantity,0,(float)free,"F2",Math.Min(5,(float)free)))refuelRequested=quantity;GUI.enabled=quantity>0;if(Button("refuel","Refuel free / selected litres"))Refuel(quantity);GUI.enabled=true;GUI.enabled=free>0;if(Button("fuel-fill","Fill test tank"))Refuel(car.spec.fuel.capacityLitres);GUI.enabled=true;GUILayout.Label(car.state.fuelStarved?"OUT OF FUEL: refuel, then Enter to restart. Failure/wear is retained.":"Consumption runs only during physical simulation ticks. This tank is independent of player fuel.");}
 }
}
