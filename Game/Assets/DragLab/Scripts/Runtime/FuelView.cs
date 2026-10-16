using System;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime {
 public sealed partial class PrototypeGame {
  float refuelRequested=5;
  internal double RefuelOwned(double amount){try{var quote=PaidEconomy.Quote(profile,profile.catalog,Costs,PricingPolicy,Payment(PaymentKind.Fuel,amount),DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);return PayNow(quote)?quote.Fuel:0;}catch(Exception e){notice=e.Message;return 0;}}
  string refuelText="5";
  void FuelGarage(){
   double free=Math.Max(0,Selected.spec.fuel.capacityLitres-Build.fuel.litres);
   GUILayout.Label("FUEL / "+Build.fuel.litres.ToString("F2")+" / "+Selected.spec.fuel.capacityLitres.ToString("F0")+" L / "+(Build.fuel.litres*Selected.spec.fuel.densityKgPerLitre).ToString("F1")+" kg / " + DisplayUnits.Money(Costs.fuelPerLitre, 2)+" per litre");
   GUILayout.Label("Free tank space "+free.ToString("F2")+" L. Quantity is capped to space; only accepted litres are charged.");
   float quantity=(float)Math.Min(refuelRequested,free);
   string entered=GUILayout.TextField(refuelText,16);if(Event.current.type==EventType.Repaint)partRects["fuel-quantity"]=RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());if(entered!=refuelText){refuelText=entered;double parsed;if(double.TryParse(entered,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.CurrentCulture,out parsed)&&MechanicsSchema.Finite(parsed)){refuelRequested=(float)Math.Max(0,Math.Min(parsed,free));quantity=refuelRequested;}}
   if(TuningSlider.Draw(ref quantity,0,(float)free,Math.Min(5,(float)free))){refuelRequested=quantity;refuelText=quantity.ToString("F2");}
   GUILayout.Label("Selected "+quantity.ToString("F2")+" L / fuel mass +"+(quantity*Selected.spec.fuel.densityKgPerLitre).ToString("F2")+" kg");
   PaidButton("fuel-refuel","Buy selected fuel",Payment(PaymentKind.Fuel,quantity),quantity>0);
   PaidButton("fuel-fill","Fill remaining tank",Payment(PaymentKind.Fuel,free),free>0);
  }
 }
 public sealed partial class PhysicalDrivingBench {
  float refuelRequested=5;
  public double Refuel(double amount){Archive();double added=FuelDynamics.Refuel(Session.car,amount);config.startingFuel=Session.car.FuelSnapshot();Session=Session.ContinueRecording(Recording);notice="Added "+added.ToString("F2")+" L to isolated test tank. Momentum, condition and pedal source retained.";return added;}
  void FuelControls(){var car=Session.car;double free=Math.Max(0,car.spec.fuel.capacityLitres-car.state.fuelLitres);GUILayout.Label("FUEL / "+car.state.fuelLitres.ToString("F2")+" / "+car.spec.fuel.capacityLitres.ToString("F0")+" L / used "+car.state.fuelUsedLitres.ToString("F3")+" L / flow "+(Paused?0:car.state.fuelFlowLitresPerSecond*3600).ToString("F2")+" L/h");float quantity=(float)Math.Min(refuelRequested,free);if(MassSlider("Add fuel (litres)",ref quantity,0,(float)free,"F2",Math.Min(5,(float)free)))refuelRequested=quantity;GUI.enabled=quantity>0;if(Button("refuel","Refuel free / selected litres"))Refuel(quantity);GUI.enabled=true;GUI.enabled=free>0;if(Button("fuel-fill","Fill test tank"))Refuel(car.spec.fuel.capacityLitres);GUI.enabled=true;GUILayout.Label(car.state.fuelStarved?"OUT OF FUEL: refuel, then Enter to restart. Failure/wear is retained.":"Consumption runs only during physical simulation ticks. This tank is independent of player fuel.");}
 }
}
