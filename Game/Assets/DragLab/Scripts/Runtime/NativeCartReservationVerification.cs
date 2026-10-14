using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
        IEnumerator CartReservationInterfaceChecks(WindowsKeyboardProbe input)
        {
            var p=game.Profile;var car=p.SelectedCar;var d=p.catalog.Find(p.catalog.Id(car.vehicleId,PartCategory.HeadIntake,1));
            var stock=p.inventory.market.parts.Find(s=>s.definitions.Contains(d.id));stock.units=stock.initialUnits=2;
            Check(game.StageNativePurchase(d.id,null,1,false),"Reservation interface stages an ordinary shop purchase");int expected=game.NativeCartTotal();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(PrototypeGame).GetMethod("PersistNativeCartReservation",flags).Invoke(game,null);p.revision++;
            Check(ProfileStore.TrySave(p),"Race/quit cart capture can be durably saved");var loaded=ProfileStore.Load();
            Check(loaded.inventory.cart.lines.Count==1&&CartReservations.Active(loaded)&&PartCart.Total(loaded,car.instanceId,loaded.inventory.cart.lines)==expected,"Player save reload preserves the actual cart, held stock and price");
            typeof(PrototypeGame).GetField("cartOwner",flags).SetValue(game,null);game.PrepareNativeCart();
            Check(game.CartLines.Count==1&&game.CartLines[0].definitionId==d.id,"Opening Build restores saved cart rows for the selected car");
            yield return Click(input,"partsCart");yield return new WaitForSecondsRealtime(.2f);
            Check(shell.Root.Q<Label>("cartReservationStatus").text.Contains("9 races"),"Cart displays its reserved-stock lifetime");
            for(int i=0;i<3;i++)VehicleListings.AfterRace(p,p.catalog);foreach(var item in p.inventory.market.parts)item.units=0;p.revision++;
            yield return new WaitForSecondsRealtime(.3f);
            Check(game.NativeCartTotal()==expected&&shell.Root.Q<Label>("cartTotal").text==DisplayUnits.Money(expected)&&shell.Root.Q<Label>("cartReservationStatus").text.Contains("6 races"),"A refreshed empty shelf does not lose or reprice the reserved cart");yield return Capture("cart-reservation");
            yield return Click(input,"cartCheckout");yield return Click(input,"dialogCancel");Check(CartReservations.Active(p)&&game.CartCount==1,"Cancelling checkout keeps the reservation");
            yield return Click(input,"cartClear");yield return Click(input,"dialogClear");Check(p.inventory.cart==null&&game.CartCount==0,"Confirmed Clear releases reserved stock without buying it");yield return Click(input,"cartBack");
        }
#endif
    }
}
