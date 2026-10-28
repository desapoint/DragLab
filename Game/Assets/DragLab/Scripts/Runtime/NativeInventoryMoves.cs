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
        void OpenInventoryMove(NativePartEntry entry,PartActionOrigin origin=null)
        {
            if(origin!=null&&!TryValidatePartAction(origin,out _,out var initialReason)){RejectPartAction(initialReason);return;}
            long revision=game.Profile.revision;PartCart.RecordRuntimeJsonCall();string signature=JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()});
            var original=game.CartLines.FirstOrDefault(l=>l.assetId==entry.AssetId);InventoryContainer destination=original?.action==CartAction.Move?SharedInventory.Containers(game.Profile).FirstOrDefault(c=>c.kind==original.destination&&c.id==original.destinationId):null;
            var error=new Label{name="inventoryMoveError",enableRichText=false};error.AddToClassList("car-critical");VisualElement preview=null;Action update=null;
            var targets=SharedInventory.Containers(game.Profile).Where(c=>c.kind==InventoryKind.Storage||entry.Unit==null&&(c.kind==InventoryKind.Car||c.kind==InventoryKind.Engine&&game.Profile.engines.Any(e=>e.instanceId==c.id&&string.IsNullOrEmpty(e.parentVehicleId)))).ToArray();
            PartCartLine Line()=>new PartCartLine{action=CartAction.Move,assetId=entry.AssetId,definitionId=entry.Definition.id,destination=destination.kind,destinationId=destination.id};
            bool Current(){if(origin==null)return true;if(TryValidatePartAction(origin,out _,out var reason))return true;error.text=reason;RejectPartAction(reason);return false;}
            string CurrentCartSignature(){PartCart.RecordRuntimeJsonCall();return JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()});}
            bool NeedsPreparation()=>entry.Definition.category==PartCategory.CylinderBlock&&destination?.kind==InventoryKind.Car&&(!MechanicsOwnership.Definitions(game.Profile,game.Profile.cars.Find(c=>c.instanceId==destination.id)).TryGetValue(PartCategory.CylinderBlock,out var fitted)||fitted!=entry.Definition.id);
            PlayerProfile Candidate()
            {
                var lines=game.CartLines.Where(l=>l.assetId!=entry.AssetId).Select(l=>l.Copy()).ToList();lines.Add(Line());
                PlayerProfile result=null;
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
                var observation=NativeCartPreviewCandidateProbe.Begin(game.Profile,lines,entry.AssetId,entry.Definition.id);
                var encodingScope=observation==null?default(RuntimeDataEncodingProbe.Scope):RuntimeDataEncodingProbe.Measure("parts/cart-candidate-characterization");
                Exception failure=null;
                try{
#endif
                    PartCart.Total(game.Profile,game.Profile.SelectedCar.instanceId,lines);
                    result=PartCart.PreviewProfile(game.Profile,game.Profile.SelectedCar,lines);
                    return result;
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
                }catch(Exception error){failure=error;throw;}
                finally{encodingScope.Dispose();NativeCartPreviewCandidateProbe.Complete(observation,game.Profile,result,failure);}
#endif
            }
            OpenDialog("Move part","Choose a destination. This move takes effect when you confirm checkout.",new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Add move",()=>{
                if(!Current())return;
                if(game.Profile.revision!=revision||signature!=CurrentCartSignature()){error.text="Inventory or cart changed. Reopen Move.";return;}
                try{if(NeedsPreparation()){if(destination.id!=game.Profile.SelectedCar.instanceId)throw new InvalidOperationException("Select this car before preparing its block installation.");OpenBlockPreparation(entry,editing:Line(),origin:origin);return;}Candidate();game.StageInventory(Line());CloseDialog();partsStateKey=null;cartPageKey=null;}catch(Exception e){error.text=e.Message;}
            },true)},populate:content=>{
                content.Add(new Label(entry.Definition.name){enableRichText=false});content.Add(new Label("From: "+InventoryLocation(entry.Location)){enableRichText=false});
                foreach(var target in targets){var captured=target;var b=new Button(()=>{if(!Current())return;destination=captured;update();}){name="inventoryMoveTarget:"+target.kind+":"+target.id,text=target.kind+" · "+(target.kind==InventoryKind.Storage?target.name:InventoryLocation(target.id).Substring(InventoryLocation(target.id).IndexOf('·')+2)),focusable=false,tabIndex=-1};b.AddToClassList("action-button");b.AddToClassList("inventory-target");content.Add(b);AddSurface(b,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(b.Q<RacingSurface>());}
                preview=new VisualElement{name="inventoryMovePreview"};content.Add(preview);content.Add(error);
                update=()=>{if(!Current())return;preview.Clear();error.text="";bool valid=false;
                    foreach(var b in content.Query<Button>().ToList().Where(b=>b.name.StartsWith("inventoryMoveTarget:")))b.EnableInClassList("selected",destination!=null&&b.name=="inventoryMoveTarget:"+destination.kind+":"+destination.id);
                    if(destination!=null)try{if(NeedsPreparation()){if(destination.id!=game.Profile.SelectedCar.instanceId)throw new InvalidOperationException("Select this car before preparing its block installation.");valid=true;preview.Add(new Label("Review required components before adding the move."));root.Q<Button>("dialogAddmove").SetEnabled(true);return;}var copy=Candidate();valid=true;preview.Add(new Label("No charge · Replaced parts stay in storage"));
                        var current=InspectOwned(game.Profile.SelectedCar)?.Stats;var proposed=string.IsNullOrEmpty(copy.SelectedCar.engineAssemblyId)?null:NativePartBrowser.Inspect(InspectionBuild.Capture(copy,copy.SelectedCar));
                        if(current!=null&&proposed!=null){preview.Add(new NativeDynoPanel(new[]{new NativeDynoSeries("Current","Current",current,NativeDynoStyle.Primary),new NativeDynoSeries("Planned","Planned",proposed,NativeDynoStyle.Alternative)},SharedUnitSettings.Current.torque,"inventoryMoveDyno"));preview.Add(new Label("Warnings · "+proposed.warnings.Count));}
                    }catch(Exception e){error.text=e.Message;}
                    root.Q<Button>("dialogAddmove").SetEnabled(valid);
                };
            });update();ExcludeTabTraversal();
        }
        struct TireSwapGroupKey : IEquatable<TireSwapGroupKey>
        {
            public string definition,reason;
            public float health,severity;
            public double life;
            public ElementFault fault;
            public bool failed;
            public bool Equals(TireSwapGroupKey other)=>definition==other.definition&&reason==other.reason&&
                BitConverter.SingleToInt32Bits(health)==BitConverter.SingleToInt32Bits(other.health)&&
                BitConverter.SingleToInt32Bits(severity)==BitConverter.SingleToInt32Bits(other.severity)&&
                BitConverter.DoubleToInt64Bits(life)==BitConverter.DoubleToInt64Bits(other.life)&&fault==other.fault&&failed==other.failed;
            public override bool Equals(object obj)=>obj is TireSwapGroupKey other&&Equals(other);
            public override int GetHashCode(){unchecked{int hash=17;hash=hash*31+(definition==null?0:StringComparer.Ordinal.GetHashCode(definition));hash=hash*31+(reason==null?0:StringComparer.Ordinal.GetHashCode(reason));hash=hash*31+BitConverter.SingleToInt32Bits(health);hash=hash*31+BitConverter.SingleToInt32Bits(severity);hash=hash*31+BitConverter.DoubleToInt64Bits(life).GetHashCode();hash=hash*31+(int)fault;hash=hash*31+(failed?1:0);return hash;}}
        }
        void OpenTireInventorySwap(PartActionOrigin origin=null)
        {
            VisualElement stock=null,targets=null;Label chosen=null,feedback=null;Action refresh=null;
            if(origin!=null&&!TryValidatePartAction(origin,out _,out var initialReason)){RejectPartAction(initialReason);return;}
            bool Current(){if(origin==null)return true;if(TryValidatePartAction(origin,out _,out var reason))return true;if(feedback!=null)feedback.text=reason;RejectPartAction(reason);return false;}
            var profile=game.Profile;
            var car=profile.SelectedCar;
            if(car==null)return;
            var installed=car.installed.slots.Find(s=>s.slot==PartCategory.Tires.ToString());
            var pack=installed==null?null:profile.parts.Find(p=>p.instanceId==installed.partInstanceId);
            var candidates=profile.looseEngineElements.Where(u=>u.tireUnit&&u.tire!=null&&u.element!=null
                &&profile.catalog.Find(u.definitionId)!=null
                &&profile.catalog.Fits(profile.catalog.Spec(car.vehicleId),profile.catalog.Find(u.definitionId)))
                .OrderByDescending(u=>Math.Min(u.tire.health,Math.Max(0,1-u.tire.consumedLife)))
                .ThenBy(u=>u.element.id,StringComparer.Ordinal).ToArray();
            if(pack==null||candidates.Length==0){
                OpenDialog("Swap stored tires",pack==null?"Fit a complete tire set first.":"No compatible individual tires are stored in inventory.",
                    new[]{new DialogAction("Close",CloseDialog)});
                return;
            }
            long revision=profile.revision;
            string carId=car.instanceId;
            // Only group equivalent specifications and wear; physical identities and history remain separate.
            TireSwapGroupKey GroupKey(LooseEngineElement unit)=>new TireSwapGroupKey{
                definition=unit.definitionId,health=unit.tire.health,life=unit.tire.consumedLife,
                fault=unit.tire.fault,severity=unit.tire.severity,failed=unit.tire.failed,reason=unit.tire.failureReason};
            var groups=candidates.Select((unit,index)=>new{unit,index}).GroupBy(x=>GroupKey(x.unit)).Select(g=>g.Select(x=>x.index).ToArray()).ToArray();
            var selected=Enumerable.Repeat(-1,4).ToArray();int groupIndex=-1;
            var positionRows=new Button[4];var planned=new Label[4];
            OpenDialog("Swap stored tires",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Swap tires",()=>{
                if(!Current())return;
                if(profile.revision!=revision){feedback.text="Inventory changed. Reopen the tire swap.";return;}
                var assignments=selected.Select((unit,corner)=>new{unit,corner}).Where(x=>x.unit>=0)
                    .Select(x=>new TireSwapAssignment(candidates[x.unit].element.id,x.corner)).ToArray();
                try{
                    if(game.CommitParts(revision,p=>InventoryUnits.SwapTires(p,carId,assignments))){CloseDialog();partsStateKey=null;cartPageKey=null;Refresh();}
                    else feedback.text=game.NativeNotice;
                }catch(Exception e){feedback.text=e.Message;}
            },true)},populate:content=>{
                stock=new VisualElement{name="tireSwapStock"};content.Add(stock);
                for(int g=0;g<groups.Length;g++){
                    int index=g;var units=groups[g];var tire=candidates[units[0]];var definition=profile.catalog.Find(tire.definitionId);
                    var button=new Button(()=>{if(!Current())return;groupIndex=index;refresh();root.Q<ScrollView>("modalContent").scrollOffset=Vector2.zero;}){name="tireSwapStock:"+g,focusable=false,tabIndex=-1};
                    button.AddToClassList("action-button");button.AddToClassList("part-position-choice");stock.Add(button);
                    var heading=new VisualElement();heading.AddToClassList("position-heading");button.Add(heading);
                    var title=new Label(definition.name){enableRichText=false,pickingMode=PickingMode.Ignore};title.AddToClassList("position-title");heading.Add(title);
                    var count=new Label("× "+units.Length){pickingMode=PickingMode.Ignore};count.AddToClassList("position-wear");heading.Add(count);
                    var condition=new Label((tire.tire.Failed?"Failed · ":"")+"Wear "+((1-TireDynamics.RemainingHealth(tire.tire))*100).ToString("0.#")+"%"){pickingMode=PickingMode.Ignore};
                    condition.AddToClassList("position-spec");condition.AddToClassList(tire.tire.Failed?"car-critical":"car-ready");button.Add(condition);
                    button.tooltip=string.Join("\n",units.Select(n=>InventoryLocation(SharedInventory.StorageOf(candidates[n].storageId))+" · "+(candidates[n].tire.distanceM/1000).ToString("0.#")+" km · #"+candidates[n].element.id));
                    AddSurface(button,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(button.Q<RacingSurface>());
                }
                targets=new VisualElement{name="tireSwapTargets"};content.Add(targets);
                var back=new Button(()=>{if(!Current())return;groupIndex=-1;refresh();root.Q<ScrollView>("modalContent").scrollOffset=Vector2.zero;}){name="tireSwapBack",focusable=false,tabIndex=-1};
                back.AddToClassList("action-button");back.AddToClassList("car-stats-button");back.Add(new FilledMenuIcon("Back"));back.Add(new Label("Tires"){pickingMode=PickingMode.Ignore});targets.Add(back);AddSurface(back,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(back.Q<RacingSurface>());
                chosen=new Label{name="tireSwapChosen",enableRichText=false};targets.Add(chosen);
                var presets=new VisualElement();presets.AddToClassList("part-purchase-shortcuts");targets.Add(presets);
                foreach(string option in new[]{"All","Front","Rear","Clear"}){
                    string preset=option;var button=new Button(()=>{
                        if(!Current())return;
                        for(int n=0;n<selected.Length;n++)selected[n]=-1;int next=0;
                        for(int i=0;i<4;i++)if(preset=="All"||preset=="Front"&&i<2||preset=="Rear"&&i>=2)selected[i]=groups[groupIndex][next++];
                        refresh();
                    }){text=preset,name="tireSwapPreset:"+preset,focusable=false,tabIndex=-1};
                    button.AddToClassList("action-button");button.AddToClassList("part-selection-shortcut");presets.Add(button);AddSurface(button,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(button.Q<RacingSurface>());
                }
                for(int i=0;i<4;i++){
                    int corner=i;
                    positionRows[i]=ReplacementPositionRow(targets,profile.catalog.Find(pack.definitionId),"tire:"+i,"tireSwapCorner:"+i,()=>{
                        if(!Current())return;
                        if(selected[corner]>=0&&groups[groupIndex].Contains(selected[corner]))selected[corner]=-1;
                        else {int next=groups[groupIndex].Where(n=>!selected.Contains(n)).DefaultIfEmpty(-1).First();if(next>=0)selected[corner]=next;}
                        refresh();
                    });
                    planned[i]=new Label{name="tireSwapPlanned:"+i,enableRichText=false,pickingMode=PickingMode.Ignore};planned[i].AddToClassList("position-spec");positionRows[i].Add(planned[i]);
                }
                feedback=new Label{name="tireSwapFeedback"};content.Add(feedback);
                refresh=()=>{if(!Current())return;
                    stock.EnableInClassList("hidden",groupIndex>=0);targets.EnableInClassList("hidden",groupIndex<0);
                    int count=selected.Count(n=>n>=0);bool failed=selected.Any(n=>n>=0&&candidates[n].tire.Failed);
                    feedback.text=count+" / 4 replacements · Removed tires stay in storage"+(failed?"\nWarning: failed tire selected.":"");feedback.EnableInClassList("car-warning",failed);
                    root.Q<Button>("dialogSwaptires").SetEnabled(count>0);
                    if(groupIndex<0)return;
                    var available=groups[groupIndex];var tire=candidates[available[0]];
                    chosen.text=profile.catalog.Find(tire.definitionId).name+" · Wear "+((1-TireDynamics.RemainingHealth(tire.tire))*100).ToString("0.#")+"% · "+available.Length+" stored";
                    foreach(string preset in new[]{"All","Front","Rear"}){var button=presets.Q<Button>("tireSwapPreset:"+preset);int needed=preset=="All"?4:2;button.SetEnabled(available.Length>=needed);button.tooltip=available.Length<needed?"Requires "+needed+" matching stored tires":"";}
                    for(int i=0;i<4;i++){
                        int n=selected[i];bool matching=n>=0&&available.Contains(n);
                        positionRows[i].EnableInClassList("selected",n>=0);positionRows[i].SetEnabled(matching||available.Any(unit=>!selected.Contains(unit)));
                        planned[i].text=n<0?"Keep fitted tire":"Replace with "+profile.catalog.Find(candidates[n].definitionId).name+" · Wear "+((1-TireDynamics.RemainingHealth(candidates[n].tire))*100).ToString("0.#")+"%";
                        planned[i].tooltip=n<0?"":"#"+candidates[n].element.id;
                    }
                };
            });
            refresh();
            ExcludeTabTraversal();
        }

    }
}
