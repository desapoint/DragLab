using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        string InventoryLocation(string id)
        {
            var p=game.Profile;var storage=p.inventory.storage.Find(s=>s.id==id);if(storage!=null)return "Storage · "+storage.name;
            var car=p.cars.Find(c=>c.instanceId==id||c.engineAssemblyId==id);if(car!=null)return "Car · "+VehicleCollection.DisplayName(p.catalog,car);
            var engine=p.engines.Find(e=>e.instanceId==id);return engine==null?id:"Engine · "+p.catalog.Spec(engine.vehicleDefinitionId).displayName+" · #"+id.Substring(Math.Max(0,id.Length-6));
        }
        void OpenInventoryMove(NativePartEntry entry)
        {
            long revision=game.Profile.revision;string signature=JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()});
            var original=game.CartLines.FirstOrDefault(l=>l.assetId==entry.AssetId);InventoryContainer destination=original?.action==CartAction.Move?SharedInventory.Containers(game.Profile).FirstOrDefault(c=>c.kind==original.destination&&c.id==original.destinationId):null;
            var error=new Label{name="inventoryMoveError",enableRichText=false};error.AddToClassList("car-critical");VisualElement preview=null;Action update=null;
            var targets=SharedInventory.Containers(game.Profile).Where(c=>c.kind==InventoryKind.Storage||entry.Unit==null&&(c.kind==InventoryKind.Car||c.kind==InventoryKind.Engine&&game.Profile.engines.Any(e=>e.instanceId==c.id&&string.IsNullOrEmpty(e.parentVehicleId)))).ToArray();
            PartCartLine Line()=>new PartCartLine{action=CartAction.Move,assetId=entry.AssetId,definitionId=entry.Definition.id,destination=destination.kind,destinationId=destination.id};
            PlayerProfile Candidate(){var lines=game.CartLines.Where(l=>l.assetId!=entry.AssetId).Select(l=>l.Copy()).ToList();lines.Add(Line());PartCart.Total(game.Profile,game.Profile.SelectedCar.instanceId,lines);return PartCart.PreviewProfile(game.Profile,game.Profile.SelectedCar,lines);}
            OpenDialog("Move part",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Add move",()=>{
                if(game.Profile.revision!=revision||signature!=JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()})){error.text="Inventory or cart changed. Reopen Move.";return;}
                try{Candidate();game.StageInventory(Line());CloseDialog();partsStateKey=null;cartPageKey=null;}catch(Exception e){error.text=e.Message;}
            },true)},populate:content=>{
                content.Add(new Label(entry.Definition.name){enableRichText=false});content.Add(new Label("From: "+InventoryLocation(entry.Location)){enableRichText=false});
                foreach(var target in targets){var captured=target;var b=new Button(()=>{destination=captured;update();}){name="inventoryMoveTarget:"+target.kind+":"+target.id,text=target.kind+" · "+(target.kind==InventoryKind.Storage?target.name:InventoryLocation(target.id).Substring(InventoryLocation(target.id).IndexOf('·')+2)),focusable=false,tabIndex=-1};b.AddToClassList("action-button");b.AddToClassList("inventory-target");content.Add(b);AddSurface(b,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(b.Q<RacingSurface>());}
                preview=new VisualElement{name="inventoryMovePreview"};content.Add(preview);content.Add(error);
                update=()=>{preview.Clear();error.text="";bool valid=false;
                    foreach(var b in content.Query<Button>().ToList().Where(b=>b.name.StartsWith("inventoryMoveTarget:")))b.EnableInClassList("selected",destination!=null&&b.name=="inventoryMoveTarget:"+destination.kind+":"+destination.id);
                    if(destination!=null)try{var copy=Candidate();valid=true;preview.Add(new Label("No charge · Replaced parts stay in storage"));
                        var current=InspectOwned(game.Profile.SelectedCar)?.Stats;var proposed=string.IsNullOrEmpty(copy.SelectedCar.engineAssemblyId)?null:NativePartBrowser.Inspect(InspectionBuild.Capture(copy,copy.SelectedCar));
                        if(current!=null&&proposed!=null){preview.Add(new NativeDynoPanel(new[]{new NativeDynoSeries("Current","Current",current,NativeDynoStyle.Primary),new NativeDynoSeries("Planned","Planned",proposed,NativeDynoStyle.Alternative)},SharedUnitSettings.Current.torque,"inventoryMoveDyno"));preview.Add(new Label("Warnings · "+proposed.warnings.Count));}
                    }catch(Exception e){error.text=e.Message;}
                    root.Q<Button>("dialogAddmove").SetEnabled(valid);
                };
            });update();ExcludeTabTraversal();
        }
    }
}
