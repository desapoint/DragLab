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
            var engine=p.engines.Find(e=>e.instanceId==id);return engine==null?id:"Engine · "+AssemblyOperations.DisplayName(p,engine)+" · #"+id.Substring(Math.Max(0,id.Length-6));
        }
        void OpenInventoryMove(NativePartEntry entry)
        {
            long revision=game.Profile.revision;string signature=JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()});
            var original=game.CartLines.FirstOrDefault(l=>l.assetId==entry.AssetId);InventoryContainer destination=original?.action==CartAction.Move?SharedInventory.Containers(game.Profile).FirstOrDefault(c=>c.kind==original.destination&&c.id==original.destinationId):null;
            var error=new Label{name="inventoryMoveError",enableRichText=false};error.AddToClassList("car-critical");VisualElement preview=null;Action update=null;
            var targets=SharedInventory.Containers(game.Profile).Where(c=>c.kind==InventoryKind.Storage||entry.Unit==null&&(c.kind==InventoryKind.Car||c.kind==InventoryKind.Engine&&game.Profile.engines.Any(e=>e.instanceId==c.id&&string.IsNullOrEmpty(e.parentVehicleId)))).ToArray();
            PartCartLine Line()=>new PartCartLine{action=CartAction.Move,assetId=entry.AssetId,definitionId=entry.Definition.id,destination=destination.kind,destinationId=destination.id};
            bool NeedsPreparation()=>entry.Definition.category==PartCategory.CylinderBlock&&destination?.kind==InventoryKind.Car&&(!MechanicsOwnership.Definitions(game.Profile,game.Profile.cars.Find(c=>c.instanceId==destination.id)).TryGetValue(PartCategory.CylinderBlock,out var fitted)||fitted!=entry.Definition.id);
            PlayerProfile Candidate(){var lines=game.CartLines.Where(l=>l.assetId!=entry.AssetId).Select(l=>l.Copy()).ToList();lines.Add(Line());PartCart.Total(game.Profile,game.Profile.SelectedCar.instanceId,lines);return PartCart.PreviewProfile(game.Profile,game.Profile.SelectedCar,lines);}
            OpenDialog("Move part",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Add move",()=>{
                if(game.Profile.revision!=revision||signature!=JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()})){error.text="Inventory or cart changed. Reopen Move.";return;}
                try{if(NeedsPreparation()){if(destination.id!=game.Profile.SelectedCar.instanceId)throw new InvalidOperationException("Select this car before preparing its block installation.");OpenBlockPreparation(entry,editing:Line());return;}Candidate();game.StageInventory(Line());CloseDialog();partsStateKey=null;cartPageKey=null;}catch(Exception e){error.text=e.Message;}
            },true)},populate:content=>{
                content.Add(new Label(entry.Definition.name){enableRichText=false});content.Add(new Label("From: "+InventoryLocation(entry.Location)){enableRichText=false});
                foreach(var target in targets){var captured=target;var b=new Button(()=>{destination=captured;update();}){name="inventoryMoveTarget:"+target.kind+":"+target.id,text=target.kind+" · "+(target.kind==InventoryKind.Storage?target.name:InventoryLocation(target.id).Substring(InventoryLocation(target.id).IndexOf('·')+2)),focusable=false,tabIndex=-1};b.AddToClassList("action-button");b.AddToClassList("inventory-target");content.Add(b);AddSurface(b,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(b.Q<RacingSurface>());}
                preview=new VisualElement{name="inventoryMovePreview"};content.Add(preview);content.Add(error);
                update=()=>{preview.Clear();error.text="";bool valid=false;
                    foreach(var b in content.Query<Button>().ToList().Where(b=>b.name.StartsWith("inventoryMoveTarget:")))b.EnableInClassList("selected",destination!=null&&b.name=="inventoryMoveTarget:"+destination.kind+":"+destination.id);
                    if(destination!=null)try{if(NeedsPreparation()){if(destination.id!=game.Profile.SelectedCar.instanceId)throw new InvalidOperationException("Select this car before preparing its block installation.");valid=true;preview.Add(new Label("Review required components before adding the move."));root.Q<Button>("dialogAddmove").SetEnabled(true);return;}var copy=Candidate();valid=true;preview.Add(new Label("No charge · Replaced parts stay in storage"));
                        var current=InspectOwned(game.Profile.SelectedCar)?.Stats;var proposed=string.IsNullOrEmpty(copy.SelectedCar.engineAssemblyId)?null:NativePartBrowser.Inspect(InspectionBuild.Capture(copy,copy.SelectedCar));
                        if(current!=null&&proposed!=null){preview.Add(new NativeDynoPanel(new[]{new NativeDynoSeries("Current","Current",current,NativeDynoStyle.Primary),new NativeDynoSeries("Planned","Planned",proposed,NativeDynoStyle.Alternative)},SharedUnitSettings.Current.torque,"inventoryMoveDyno"));preview.Add(new Label("Warnings · "+proposed.warnings.Count));}
                    }catch(Exception e){error.text=e.Message;}
                    root.Q<Button>("dialogAddmove").SetEnabled(valid);
                };
            });update();ExcludeTabTraversal();
        }
        void OpenTireInventorySwap()
        {
            var profile=game.Profile;
            var car=profile.SelectedCar;
            if(car==null)return;
            var installed=car.installed.slots.Find(s=>s.slot==PartCategory.Tires.ToString());
            var pack=installed==null?null:profile.parts.Find(p=>p.instanceId==installed.partInstanceId);
            var candidates=profile.looseEngineElements.Where(u=>u.tireUnit&&u.tire!=null&&u.element!=null)
                .OrderByDescending(u=>Math.Min(u.tire.health,Math.Max(0,1-u.tire.consumedLife)))
                .ThenBy(u=>u.element.id,StringComparer.Ordinal).ToArray();
            if(pack==null||candidates.Length==0){
                OpenDialog("Swap stored tires",pack==null?"Fit a complete tire set first.":"No individual tires are stored in inventory.",
                    new[]{new DialogAction("Close",CloseDialog)});
                return;
            }
            long revision=profile.revision;
            string carId=car.instanceId;
            var corners=new[]{"Front left","Front right","Rear left","Rear right"};
            var labels=new System.Collections.Generic.List<string>{"Keep fitted tire"};
            foreach(var tire in candidates){
                var d=profile.catalog.Find(tire.definitionId);
                var id=tire.element.id;
                var health=Math.Min(tire.tire.health,Math.Max(0,1-tire.tire.consumedLife));
                labels.Add((d==null?tire.definitionId:d.name)+" · "+health.ToString("P0")
                    +" · "+(tire.tire.distanceM/1000).ToString("F1")+" km"
                    +(tire.tire.failed?" · FAILED":"")+" · #"+id.Substring(Math.Max(0,id.Length-6)));
            }
            var positions=new DropdownField[4];
            var feedback=new Label{name="tireSwapFeedback",enableRichText=false};
            feedback.AddToClassList("car-critical");
            Action refresh=null;
            OpenDialog("Swap tires from inventory",null,new[]{
                new DialogAction("Cancel",CloseDialog),
                new DialogAction("Swap tires",()=>{
                    if(profile.revision!=revision){feedback.text="Inventory changed. Reopen the tire swap.";return;}
                    var assignments=new System.Collections.Generic.List<TireSwapAssignment>();
                    for(int i=0;i<4;i++)if(positions[i].index>0)
                        assignments.Add(new TireSwapAssignment(candidates[positions[i].index-1].element.id,i));
                    try{
                        if(game.CommitParts(revision,p=>InventoryUnits.SwapTires(p,carId,assignments))){
                            CloseDialog();partsStateKey=null;cartPageKey=null;Refresh();
                        }else feedback.text=game.NativeNotice;
                    }catch(Exception e){feedback.text=e.Message;}
                },true)
            },populate:content=>{
                content.Add(new Label("Choose the exact physical tires for each corner. All removed tires return to garage storage with their wear and identity intact."));
                var presets=new VisualElement();presets.style.flexDirection=FlexDirection.Row;content.Add(presets);
                foreach(string preset in new[]{"Front","Rear","All","Clear"}){
                    string choice=preset;
                    var button=new Button(()=>{
                        var chosen=new System.Collections.Generic.HashSet<int>();
                        for(int i=0;i<4;i++){
                            bool target=choice=="All"||choice=="Front"&&i<2||choice=="Rear"&&i>=2;
                            positions[i].index=0;
                            if(choice=="Clear"||!target)continue;
                            for(int n=0;n<candidates.Length;n++)
                                if(chosen.Add(n)){positions[i].index=n+1;break;}
                        }
                        refresh();
                    }){text=preset,name="tireSwapPreset:"+preset};
                    button.AddToClassList("action-button");presets.Add(button);
                }
                for(int i=0;i<4;i++){
                    int corner=i;
                    var row=new VisualElement();row.AddToClassList("setting-row");content.Add(row);
                    row.Add(new Label(corners[i]+" · fitted "+TireFitments.DefinitionId(pack,i)
                        +" · "+Math.Min(pack.condition.tires[i].health,Math.Max(0,1-pack.condition.tires[i].consumedLife)).ToString("P0")));
                    var dropdown=new DropdownField{ name="tireSwapCorner:"+i,choices=new System.Collections.Generic.List<string>(labels),index=0 };
                    dropdown.RegisterValueChangedCallback(_=>refresh());
                    positions[i]=dropdown;row.Add(dropdown);
                }
                content.Add(feedback);
                refresh=()=>{
                    var selected=positions.Where(x=>x.index>0).Select(x=>candidates[x.index-1].element.id).ToArray();
                    bool duplicate=selected.Distinct(StringComparer.Ordinal).Count()!=selected.Length;
                    bool failed=positions.Any(x=>x.index>0&&candidates[x.index-1].tire.failed);
                    feedback.text=duplicate?"A physical tire cannot occupy two corners.":failed?"Warning: one selected tire is failed and may have severely reduced grip.":"";
                    var submit=root.Q<Button>("dialogSwaptires");
                    if(submit!=null)submit.SetEnabled(selected.Length>0&&!duplicate);
                };
                refresh();
            });
            ExcludeTabTraversal();
        }

    }
}
