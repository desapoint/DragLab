using System;
using System.Collections.Generic;
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
        sealed class PartPurchaseDraft
        {
            internal bool Individual;
            internal bool Install;
            internal string Role;
            internal int Quantity=1;
            internal List<string> Positions=new List<string>();
            internal NativePartBrowser PreviewModel;
            internal string PreviewKey;
            internal NativePartEntry Preview;
        }
        readonly Dictionary<string,PartPurchaseDraft> partPurchases=new Dictionary<string,PartPurchaseDraft>();
        PartPurchaseDraft PurchaseDraft(NativePartEntry offer)
        {
            string key=game.Profile.SelectedCar.instanceId+"/"+offer.Id;
            if(!partPurchases.TryGetValue(key,out var draft)){draft=new PartPurchaseDraft();partPurchases[key]=draft;}
            var roles=InventoryUnits.Roles(game.Profile,game.Profile.SelectedCar,offer.Definition);
            if(!roles.Contains(draft.Role)){draft.Role=roles.FirstOrDefault();draft.Positions.Clear();}
            if(roles.Length==0)draft.Individual=false;
            else {var positions=InventoryUnits.Positions(game.Profile,game.Profile.SelectedCar,offer.Definition.id,draft.Role);draft.Positions=draft.Positions.Where(positions.Contains).ToList();}
            return draft;
        }
        NativePartEntry PurchasePreview(NativePartEntry offer)
        {
            if(offer.Source!="New")return offer;var draft=PurchaseDraft(offer);if(!draft.Individual)return offer;
            string key=draft.Role+"/"+string.Join("/",draft.Positions);
            if(draft.PreviewModel!=partsBrowser||draft.PreviewKey!=key){draft.Preview=partsBrowser.PreviewNewUnit(offer,draft.Role,draft.Positions);draft.PreviewKey=key;draft.PreviewModel=partsBrowser;}
            return draft.Preview;
        }
        void RefreshPurchasePrice(NativePartEntry offer)
        {
            var draft=PurchaseDraft(offer);
            var quote=PartsMarket.Quote(game.Profile,offer.Definition,InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,offer.Definition),draft.Individual,draft.Quantity);
            var price=partsDetail.Q<Label>("partDetailPrice");if(price!=null)price.text=DisplayUnits.Money(quote.cost);
            var label=partsDetail.Q<Label>("partQueued");if(label!=null){int queued=game.CartLines.Where(l=>l.definitionId==offer.Definition.id&&l.action==(draft.Individual?CartAction.NewUnits:CartAction.NewSet)&&l.role==(draft.Individual?draft.Role:null)).Sum(l=>l.quantity);label.text=queued>0?"Queued × "+queued:"";}
        }
        void StagePurchase(NativePartEntry offer,bool install)
        {
            if(offer.Source=="New"||offer.Source=="Used"){OpenPartPurchase(offer);return;}
            game.StageNativePart(offer,install,offer.Position);partsStateKey=null;
        }
        void OpenPartPurchase(NativePartEntry offer,PartCartLine editing=null)
        {
            var d=offer.Definition;var car=game.Profile.SelectedCar;bool fresh=offer.Source=="New";
            var draft=fresh?PurchaseDraft(offer):null;string expected=editing==null?null:JsonUtility.ToJson(editing),carId=car.instanceId;
            var roles=fresh?InventoryUnits.Roles(game.Profile,car,d):offer.Unit!=null?new[]{offer.Unit.element.role}:new string[0];
            int setSize=InventoryUnits.Count(game.Profile,car,d);
            bool individual=editing!=null?editing.action==CartAction.NewUnits||editing.action==CartAction.BuyUsed&&offer.Unit!=null:fresh?draft.Individual:offer.Unit!=null,install=editing?.install??(fresh&&draft.Install);
            string role=editing?.role??(fresh?draft.Role:offer.Unit?.element.role);int quantity=editing?.quantity??(fresh?draft.Quantity:1);
            var binding=car.installed.slots.Find(s=>s.slot==d.category.ToString());
            var fitted=binding==null?null:game.Profile.parts.Find(p=>p.instanceId==binding.partInstanceId);
            bool matches=fitted?.definitionId==d.id||(d.category==PartCategory.Tires||d.category==PartCategory.Wheels)&&fitted!=null&&game.Profile.catalog.Fits(game.Profile.catalog.Spec(car.vehicleId),d);
            string[] all=fitted==null?new string[0]:d.category==PartCategory.Tires||d.category==PartCategory.Wheels?Enumerable.Range(0,4).Select(i=>(d.category==PartCategory.Tires?"tire:":"wheel:")+i).ToArray():fitted.condition.elements.Select(e=>e.id).ToArray();
            var selected=new HashSet<string>(editing!=null?editing.positions:fresh?draft.Positions:Enumerable.Empty<string>());
            var rows=new List<Tuple<Button,string>>();var groups=new List<VisualElement>();
            VisualElement positionsArea=null,roleGroup=null;Label summary=null,error=null,quantityLabel=null,unitPrice=null,notice=null;FloatField number=null;Button minus=null,plus=null;Action update=null;
            OpenDialog(editing==null?"Purchase":"Edit purchase",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction(editing==null?"Add to cart":"Apply",()=>{
                var targets=all.Where(selected.Contains).ToList();
                if(install&&game.NativeBlockPreparationPending(d)){OpenBlockPreparation(offer,quantity,editing);return;}
                if(editing!=null){if(!game.EditNativeCartPurchase(editing,expected,carId,individual,role,quantity,install,targets)){error.text=game.NativeNotice;return;}}
                else if(fresh){if(!game.StageNativePurchase(d.id,individual?role:null,quantity,install,individual&&install?targets:null)){error.text=game.NativeNotice;return;}
                    draft.Individual=individual;draft.Install=install;draft.Role=role;draft.Quantity=quantity;draft.Positions=individual&&install?targets:new List<string>();
                }else{
                    game.StageNativePart(offer,install,individual&&install?targets.Single():null);
                    if(!game.CartLines.Any(l=>l.assetId==offer.AssetId)){error.text=game.NativeNotice;return;}
                }
                CloseDialog();partsStateKey=null;if(editing!=null){cartPageKey=null;RefreshNativeCart("Parts");}
            },true)},populate:content=>{
                content.Add(new Label(d.name){enableRichText=false});
                if(!fresh){var condition=new Label("Used · "+((offer.Unit?.element.health??offer.Part?.condition.health??0)*100).ToString("0.#")+"% condition"){name="partPurchaseCondition"};condition.AddToClassList("car-warning");content.Add(condition);}
                if(fresh&&roles.Length>0){QueryChoices(content,"partPurchaseMode","Buy",new[]{"Complete set","Individual"},()=>individual?"Individual":"Complete set",value=>{individual=value=="Individual";quantity=1;selected.Clear();if(individual&&!matches)install=false;update();},2);if(d.category==PartCategory.Tires||d.category==PartCategory.Wheels){content.Q<Button>("partPurchaseMode:Complete set").Q<Label>().text=d.category==PartCategory.Tires?"Tire set (4)":"Wheel set (4)";content.Q<Button>("partPurchaseMode:Individual").Q<Label>().text=d.category==PartCategory.Tires?"Single tire":"Single wheel";}}
                QueryChoices(content,"partPurchaseDestination","Destination",new[]{"Store","Install"},()=>install?"Install":"Store",value=>{install=value=="Install";update();},2);
                QueryChoices(content,"partPurchaseRole","Type",roles,()=>role,value=>{role=value;selected.Clear();update();},2);
                roleGroup=content.Children().Last();
                var q=new VisualElement{name="partPurchaseQuantity"};q.AddToClassList("part-purchase-quantity");content.Add(q);
                quantityLabel=new Label();q.Add(quantityLabel);
                void Assign(int value){quantity=Mathf.Clamp(value,1,99);if(selected.Count>quantity){var keep=all.Where(selected.Contains).Take(quantity).ToArray();selected.Clear();selected.UnionWith(keep);}error.text="";update();}
                minus=new Button(()=>Assign(quantity-1)){name="partQuantityMinus",text="−",focusable=false,tabIndex=-1};minus.AddToClassList("action-button");minus.AddToClassList("part-quantity-step");q.Add(minus);AddSurface(minus,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(minus.Q<RacingSurface>());
                number=new FloatField{name="partQuantity",isDelayed=true,formatString="0"};number.AddToClassList("setting-number");q.Add(number);
                number.RegisterValueChangedCallback(e=>{float value=e.newValue;if(float.IsNaN(value)||float.IsInfinity(value)||value!=Mathf.Floor(value)){number.SetValueWithoutNotify(quantity);error.text="Enter a whole quantity.";return;}Assign((int)Mathf.Clamp(value,1,99));});
                plus=new Button(()=>Assign(quantity+1)){name="partQuantityPlus",text="+",focusable=false,tabIndex=-1};plus.AddToClassList("action-button");plus.AddToClassList("part-quantity-step");q.Add(plus);AddSurface(plus,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(plus.Q<RacingSurface>());
                unitPrice=new Label{name="partPurchaseUnitPrice"};content.Add(unitPrice);
                notice=new Label{name="partPurchaseNotice"};notice.AddToClassList("car-warning");content.Add(notice);
                positionsArea=new VisualElement{name="partPurchasePositions"};content.Add(positionsArea);
                var shortcuts=new VisualElement{name="partPurchaseShortcuts"};shortcuts.AddToClassList("part-purchase-shortcuts");positionsArea.Add(shortcuts);
                foreach(string choice in d.category==PartCategory.Tires||d.category==PartCategory.Wheels?new[]{"All","Front","Rear","Failed","Worn","Clear"}:new[]{"All","Failed","Worn","Clear"}){
                    string action=choice;var button=new Button(()=>{
                        individual=true;selected.Clear();var available=InventoryUnits.Positions(game.Profile,car,d.id,role);
                        foreach(string target in available){var member=target.StartsWith("wheel:")?fitted?.condition.elements[int.Parse(target.Substring(6))]:fitted?.condition.elements.FirstOrDefault(e=>e.id==target);var tire=target.StartsWith("tire:")?fitted?.condition.tires[int.Parse(target.Substring(5))]:null;
                            if(action=="All"||action=="Front"&&(target.StartsWith("tire:")&&int.Parse(target.Substring(5))<2||target.StartsWith("wheel:")&&int.Parse(target.Substring(6))<2)||action=="Rear"&&(target.StartsWith("tire:")&&int.Parse(target.Substring(5))>=2||target.StartsWith("wheel:")&&int.Parse(target.Substring(6))>=2)||action=="Failed"&&(member?.Failed==true||tire?.Failed==true)||action=="Worn"&&(member?.health<=.5||tire!=null&&TireDynamics.RemainingHealth(tire)<=.5))selected.Add(target);
                        }
                        quantity=Math.Max(quantity,selected.Count);update();
                    }){name="partSelect"+choice,text=choice,focusable=false,tabIndex=-1};button.AddToClassList("action-button");button.AddToClassList("part-selection-shortcut");shortcuts.Add(button);AddSurface(button,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(button.Q<RacingSurface>());
                    if(choice=="Worn")button.tooltip="At least 50% wear used.";
                }
                foreach(var group in all.GroupBy(position=>position.StartsWith("wheel:")?int.Parse(position.Substring(6))<2?"Front axle":"Rear axle":position.StartsWith("tire:")?int.Parse(position.Substring(5))<2?"Front axle":"Rear axle":fitted.condition.elements.First(e=>e.id==position).cylinder<0?"Engine":"Cylinder "+(fitted.condition.elements.First(e=>e.id==position).cylinder+1))){
                    var block=new VisualElement();block.AddToClassList("part-replacement-group");positionsArea.Add(block);groups.Add(block);
                    if(d.category!=PartCategory.Pistons)block.Add(new Label(group.Key){name="replacementGroup:"+group.Key});
                    foreach(string position in group){string target=position;var row=ReplacementPositionRow(block,d,target,"purchasePosition:"+target,()=>{
                        if(!individual){if(!fresh||!matches)return;individual=true;quantity=1;selected.Clear();}
                        if(!selected.Remove(target)){if(!fresh)selected.Clear();selected.Add(target);quantity=Math.Max(quantity,selected.Count);}update();
                    });rows.Add(Tuple.Create(row,target));}
                }
                if(all.Length==0)positionsArea.Add(new Label("No fitted units"));
                summary=new Label{name="partPurchaseSummary",enableRichText=false};summary.AddToClassList("part-purchase-summary");content.Add(summary);
                error=new Label{name="partPurchaseError"};error.AddToClassList("car-critical");content.Add(error);
                update=()=>{
                    var available=role==null?new string[0]:InventoryUnits.Positions(game.Profile,car,d.id,role);selected.RemoveWhere(p=>!available.Contains(p));
                    roleGroup.EnableInClassList("hidden",!individual||roles.Length<2);
                    positionsArea.EnableInClassList("hidden",!install||all.Length==0);
                    shortcuts.EnableInClassList("hidden",!fresh||!matches);
                    foreach(var button in content.Query<Button>().ToList().Where(b=>b.name.StartsWith("partPurchaseMode:")))button.EnableInClassList("selected",button.name=="partPurchaseMode:"+(individual?"Individual":"Complete set"));
                    foreach(var button in content.Query<Button>().ToList().Where(b=>b.name.StartsWith("partPurchaseDestination:")))button.EnableInClassList("selected",button.name=="partPurchaseDestination:"+(install?"Install":"Store"));
                    content.Q<Button>("partPurchaseMode:Individual")?.SetEnabled(true);
                    bool fits=game.Profile.catalog.Fits(game.Profile.catalog.Spec(car.vehicleId),d);content.Q<Button>("partPurchaseDestination:Install").SetEnabled(fits);
                    foreach(var pair in rows){bool valid=available.Contains(pair.Item2);pair.Item1.EnableInClassList("hidden",individual&&!valid);pair.Item1.EnableInClassList("selected",!individual||selected.Contains(pair.Item2));pair.Item1.SetEnabled(matches&&(fresh||individual)&&(!individual||valid));}
                    foreach(var group in groups)group.EnableInClassList("hidden",group.Query<Button>().ToList().All(b=>b.ClassListContains("hidden")));
                    quantityLabel.text=individual?"Units":setSize>1?"Sets":"Quantity";number.SetValueWithoutNotify(quantity);number.SetEnabled(fresh);minus.SetEnabled(fresh&&quantity>1);plus.SetEnabled(fresh&&quantity<99);
                    long each=fresh?individual?InventoryPricing.NewUnit(d,setSize):InventoryPricing.NewSet(d):offer.Price??0;
                    unitPrice.text=(individual?"1 unit":setSize>1?"1 set · "+setSize+" units":"1 part")+" · "+DisplayUnits.Money(each);
                    var supply=fresh?PartsMarket.Quote(game.Profile,d,setSize,individual,quantity):null;
                    string problem=install&&!fits?"Part does not fit this car; choose Store.":install&&individual&&!matches?"Individual replacements require the fitted specification.":"";notice.text=problem.Length>0?problem:supply==null?"":supply.expressUnits>0?supply.Summary+" · 5–10% on missing stock":PartsMarket.Available(game.Profile,d)+" units in stock";notice.EnableInClassList("hidden",notice.text.Length==0);
                    int stored=install?individual?quantity-selected.Count:quantity-1:quantity;
                    string buy=quantity+(individual?quantity==1?" unit":" units":setSize>1?quantity==1?" set":" sets":quantity==1?" part":" parts");
                    string replacements=individual?string.Join(", ",all.Where(selected.Contains).Select(p=>d.category==PartCategory.Pistons?"piston "+(fitted.condition.elements.First(e=>e.id==p).cylinder+1):InventoryUnits.PositionLabel(game.Profile,car,d.id,p))):setSize>1?"complete set":"part";
                    summary.tooltip=individual?string.Join("\n",all.Where(selected.Contains).Select(p=>InventoryUnits.PositionLabel(game.Profile,car,d.id,p))):"";
                    if(individual&&selected.Count>4)replacements=selected.Count+" "+(role??"units")+" units";
                    summary.text="Buy "+buy+" · "+DisplayUnits.Money(supply?.cost??each*quantity)+(install?"\nReplace "+(individual&&selected.Count==0?"— select positions":replacements)+" · Keep removed parts":"")+"\nStore "+stored+(individual?stored==1?" unit":" units":setSize>1?stored==1?" set":" sets":stored==1?" part":" parts");
                    root.Q<Button>(editing==null?"dialogAddtocart":"dialogApply")?.SetEnabled(problem.Length==0&&(!install||!individual||selected.Count>0&&selected.Count<=quantity));
                };
            });
            summary.RemoveFromHierarchy();error.RemoveFromHierarchy();
            var review=new VisualElement{name="partPurchaseReview"};review.AddToClassList("part-purchase-review");review.Add(summary);review.Add(error);
            var actions=root.Q("modalActions");actions.parent.Insert(actions.parent.IndexOf(actions),review);update();
        }
        void ChooseCartUnitPositions(PartCartLine line)
        {
            OpenPurchasePositions(game.Profile.catalog.Find(line.definitionId),line.role,line.quantity,line.positions,
                selected=>game.SetNativeCartUnitPositions(line,selected),()=>{partsStateKey=null;OpenNativeCart();});
        }
        Button ReplacementPositionRow(VisualElement parent,PartDefinition offer,string position,string name,Action clicked)
        {
            var car=game.Profile.SelectedCar;var binding=car.installed.slots.Find(s=>s.slot==offer.category.ToString());
            var part=binding==null?null:game.Profile.parts.Find(p=>p.instanceId==binding.partInstanceId);var fitted=part==null?null:game.Profile.catalog.Find(part.definitionId);
            var element=position.StartsWith("wheel:")?part?.condition.elements[int.Parse(position.Substring(6))]:part?.condition.elements.FirstOrDefault(e=>e.id==position);
            if(position.StartsWith("wheel:")&&part!=null)fitted=game.Profile.catalog.Find(WheelFitments.DefinitionId(part,int.Parse(position.Substring(6))));
            TireCondition tire=position.StartsWith("tire:",StringComparison.Ordinal)&&part?.condition.tires!=null?part.condition.tires[int.Parse(position.Substring(5))]:null;
            if(tire!=null)fitted=game.Profile.catalog.Find(TireFitments.DefinitionId(part,int.Parse(position.Substring(5))));
            double? health=element?.health??(tire==null?(double?)null:TireDynamics.RemainingHealth(tire));bool failed=element?.Failed??tire?.Failed??false;
            string title=InventoryUnits.PositionLabel(game.Profile,car,offer.id,position);
            if(element!=null&&offer.category==PartCategory.Pistons)title="Piston "+(element.cylinder+1);
            string type=element?.role??"Tire";
            if(fitted!=null&&offer.category==PartCategory.Pistons)type=PartSemantics.Friendly(fitted.pistonCrown.ToString())+(fitted.name.IndexOf(fitted.quality,StringComparison.OrdinalIgnoreCase)>=0?"":" · "+fitted.quality)+" · "+fitted.boreMm.ToString("0.##")+" mm";
            else if(fitted!=null)type+=" · "+fitted.quality;
            var button=new Button(clicked){name=name,focusable=false,tabIndex=-1};button.AddToClassList("action-button");button.AddToClassList("part-position-choice");parent.Add(button);
            var heading=new VisualElement();heading.AddToClassList("position-heading");button.Add(heading);
            var label=new Label(title){name="positionTitle:"+position,enableRichText=false,pickingMode=PickingMode.Ignore};label.AddToClassList("position-title");heading.Add(label);
            var wear=new Label(health.HasValue?(failed?"Failed · ":"")+"Wear "+((1-health.Value)*100).ToString("0.#")+"%":"Condition unavailable"){name="positionWear:"+position,pickingMode=PickingMode.Ignore};wear.AddToClassList("position-wear");wear.AddToClassList(failed||health<=.25?"car-critical":!health.HasValue||health<=.5?"car-warning":"car-ready");heading.Add(wear);
            var spec=new Label((fitted?.name??"Missing specification")+" · "+type){name="positionSpec:"+position,enableRichText=false,pickingMode=PickingMode.Ignore};spec.AddToClassList("position-spec");button.Add(spec);
            string fault=element!=null&&element.fault!=ElementFault.None?PartSemantics.Friendly(element.fault.ToString())+" · "+(element.severity*100).ToString("0.#")+"%":tire?.Failed==true?"Failed tire":element?.Failed==true?"Worn out":"";
            if(fault.Length>0){var reason=new Label(fault){name="positionFault:"+position,enableRichText=false,pickingMode=PickingMode.Ignore};reason.AddToClassList("position-fault");button.Add(reason);}
            button.tooltip=(fitted?.name??"")+"\n"+title+"\n"+type+"\nCondition "+(health.HasValue?health.Value.ToString("P0"):"unavailable")+(fault.Length>0?"\n"+fault:"");
            AddSurface(button,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(button.Q<RacingSurface>());return button;
        }
        void OpenPurchasePositions(PartDefinition definition,string role,int quantity,IEnumerable<string> previous,Func<IEnumerable<string>,bool> apply,Action completed)
        {
            var positions=InventoryUnits.Positions(game.Profile,game.Profile.SelectedCar,definition.id,role);
            var selected=new HashSet<string>(previous.Where(positions.Contains));Action update=null;
            OpenDialog("Replacement positions",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Add",()=>{
                if(!apply(positions.Where(selected.Contains))){root.Q<Label>("purchasePositionsError").text=game.NativeNotice;return;}
                CloseDialog();completed();
            },true)},populate:content=>{
                var buttons=new List<Tuple<Button,string>>();
                content.Add(new Label("Match the fitted specification. Extras go to storage."){enableRichText=false});
                var summary=new Label{name="purchasePositionsSummary"};content.Add(summary);
                update=()=>{summary.text=selected.Count+" installed · "+Math.Max(0,quantity-selected.Count)+" in storage";foreach(var pair in buttons){pair.Item1.EnableInClassList("selected",selected.Contains(pair.Item2));pair.Item1.SetEnabled(selected.Contains(pair.Item2)||selected.Count<quantity);}root.Q<Button>("dialogAdd")?.SetEnabled(selected.Count>0&&selected.Count<=quantity);};
                if(definition.category==PartCategory.Tires||definition.category==PartCategory.Wheels){var presets=new VisualElement();presets.AddToClassList("part-purchase-shortcuts");content.Add(presets);foreach(string option in new[]{"All","Front","Rear","Clear"}){string preset=option;var b=new Button(()=>{selected.Clear();foreach(var target in positions.Where((v,i)=>preset=="All"||preset=="Front"&&i<2||preset=="Rear"&&i>=2).Take(quantity))selected.Add(target);update();}){name="positionPreset:"+preset,text=preset,focusable=false,tabIndex=-1};b.AddToClassList("action-button");presets.Add(b);AddSurface(b,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(b.Q<RacingSurface>());}}
                foreach(var position in positions){string target=position;var b=ReplacementPositionRow(content,definition,target,"purchasePosition:"+target,()=>{if(!selected.Remove(target))selected.Add(target);update();});buttons.Add(Tuple.Create(b,target));}
                var error=new Label{name="purchasePositionsError"};error.AddToClassList("car-critical");content.Add(error);
            });update();
        }
    }
}
