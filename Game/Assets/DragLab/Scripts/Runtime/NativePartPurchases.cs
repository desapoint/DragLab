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
            internal string Role;
            internal int Quantity=1;
            internal List<string> Positions=new List<string>();
            internal NativePartBrowser PreviewModel;
            internal string PreviewKey;
            internal NativePartEntry Preview;
        }
        readonly Dictionary<string,PartPurchaseDraft> partPurchases=new Dictionary<string,PartPurchaseDraft>();
        readonly List<RacingSurface> partPurchaseSurfaces=new List<RacingSurface>();
        VisualElement partPurchaseControls;
        string partPurchaseControlsKey;
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
        void ClearPurchaseControls()
        {
            partPurchaseControls?.RemoveFromHierarchy();partPurchaseControls=null;partPurchaseControlsKey=null;
            foreach(var s in partPurchaseSurfaces)surfaces.Remove(s);partPurchaseSurfaces.Clear();
        }
        void PurchaseSurface(VisualElement element,RacingSurface.Kind kind){AddSurface(element,kind);partPurchaseSurfaces.Add(element.Q<RacingSurface>());}
        void PurchaseChoices(VisualElement parent,string id,string[] labels,Func<string> get,Action<string> set)
        {
            var group=new VisualElement();group.AddToClassList("choice-group");group.AddToClassList("part-purchase-choice");parent.Add(group);
            for(int i=0;i<labels.Length;i++){
                string value=labels[i];
                var b=new Button(()=>{set(value);OpenPartDetail(partsDetailId,false);}){name=id+":"+value,text=value,focusable=false,tabIndex=-1};
                b.AddToClassList("choice-button");b.AddToClassList(i==0?"choice-first":i==labels.Length-1?"choice-last":"choice-middle");group.Add(b);
                PurchaseSurface(b,i==0?RacingSurface.Kind.ChoiceFirst:i==labels.Length-1?RacingSurface.Kind.ChoiceLast:RacingSurface.Kind.ChoiceMiddle);
                b.userData=(Action)(()=>b.EnableInClassList("selected",get()==value));
            }
        }
        void BuildPurchaseControls(NativePartEntry offer)
        {
            if(offer.Source!="New"){ClearPurchaseControls();return;}
            var draft=PurchaseDraft(offer);var roles=InventoryUnits.Roles(game.Profile,game.Profile.SelectedCar,offer.Definition);
            int count=InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,offer.Definition);
            string key=game.Profile.SelectedCar.instanceId+"/"+offer.Id+"/"+count+"/"+string.Join("/",roles);
            if(partPurchaseControlsKey!=key){
                ClearPurchaseControls();partPurchaseControlsKey=key;partPurchaseControls=new VisualElement{name="partPurchase"};partPurchaseControls.AddToClassList("part-purchase");
                var row=new VisualElement();row.AddToClassList("part-purchase-row");partPurchaseControls.Add(row);
                if(roles.Length>0)PurchaseChoices(row,"partPurchaseMode",new[]{"Complete set","Individual"},()=>draft.Individual?"Individual":"Complete set",v=>{draft.Individual=v=="Individual";draft.Positions.Clear();});
                var quantity=new VisualElement();quantity.AddToClassList("part-purchase-quantity");row.Add(quantity);
                quantity.Add(new Label{name="partQuantityLabel"});
                Action<int> assign=value=>{draft.Quantity=Mathf.Clamp(value,1,99);if(draft.Positions.Count>draft.Quantity){draft.Positions=draft.Positions.Take(draft.Quantity).ToList();OpenPartDetail(partsDetailId,false);}RefreshPurchaseControls(offer);};
                var minus=new Button(()=>assign(draft.Quantity-1)){name="partQuantityMinus",text="−",focusable=false,tabIndex=-1};minus.AddToClassList("action-button");minus.AddToClassList("part-quantity-step");quantity.Add(minus);PurchaseSurface(minus,RacingSurface.Kind.SecondaryAction);
                var number=new FloatField{name="partQuantity",isDelayed=true,formatString="0"};number.AddToClassList("setting-number");quantity.Add(number);
                number.RegisterValueChangedCallback(e=>{float value=e.newValue;if(float.IsNaN(value)||float.IsInfinity(value)||value!=Mathf.Floor(value)){number.SetValueWithoutNotify(draft.Quantity);partPurchaseControls.Q<Label>("partPurchaseError").text="Enter a whole quantity.";return;}assign((int)Mathf.Clamp(value,1,99));});
                var plus=new Button(()=>assign(draft.Quantity+1)){name="partQuantityPlus",text="+",focusable=false,tabIndex=-1};plus.AddToClassList("action-button");plus.AddToClassList("part-quantity-step");quantity.Add(plus);PurchaseSurface(plus,RacingSurface.Kind.SecondaryAction);
                var roleRow=new VisualElement{name="partPurchaseRoles"};roleRow.AddToClassList("part-purchase-row");roleRow.AddToClassList("part-purchase-roles");partPurchaseControls.Add(roleRow);
                if(roles.Length>1){roleRow.Add(new Label("Type"));PurchaseChoices(roleRow,"partPurchaseRole",roles,()=>draft.Role,v=>{draft.Role=v;draft.Positions.Clear();});}
                var error=new Label{name="partPurchaseError",enableRichText=false};error.AddToClassList("car-critical");error.AddToClassList("part-purchase-error");partPurchaseControls.Add(error);
            }
            partsDetail.Insert(1,partPurchaseControls);RefreshPurchaseControls(offer);
        }
        void RefreshPurchaseControls(NativePartEntry offer)
        {
            if(partPurchaseControls==null)return;var draft=PurchaseDraft(offer);
            foreach(var b in partPurchaseControls.Query<Button>(className:"choice-button").ToList())(b.userData as Action)?.Invoke();
            partPurchaseControls.Q("partPurchaseRoles").EnableInClassList("hidden",!draft.Individual||InventoryUnits.Roles(game.Profile,game.Profile.SelectedCar,offer.Definition).Length<2);
            partPurchaseControls.Q<Label>("partQuantityLabel").text=draft.Individual?"Units":InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,offer.Definition)>1?"Sets":"Quantity";
            partPurchaseControls.Q<FloatField>("partQuantity").SetValueWithoutNotify(draft.Quantity);
            partPurchaseControls.Q<Button>("partQuantityMinus").SetEnabled(draft.Quantity>1);partPurchaseControls.Q<Button>("partQuantityPlus").SetEnabled(draft.Quantity<99);
            partPurchaseControls.Q<Label>("partPurchaseError").text="";RefreshPurchasePrice(offer);
        }
        void RefreshPurchasePrice(NativePartEntry offer)
        {
            var draft=PurchaseDraft(offer);int each=draft.Individual?InventoryPricing.NewUnit(offer.Definition,InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,offer.Definition)):InventoryPricing.NewSet(offer.Definition);
            var price=partsDetail.Q<Label>("partDetailPrice");if(price!=null)price.text=DisplayUnits.Money((long)each*draft.Quantity);
            var label=partsDetail.Q<Label>("partQueued");if(label!=null){int queued=game.CartLines.Where(l=>l.definitionId==offer.Definition.id&&l.action==(draft.Individual?CartAction.NewUnits:CartAction.NewSet)&&l.role==(draft.Individual?draft.Role:null)).Sum(l=>l.quantity);label.text=queued>0?"Queued × "+queued:"";}
        }
        void StagePurchase(NativePartEntry offer,bool install)
        {
            if(offer.Source!="New"){game.StageNativePart(offer,install,offer.Position);partsStateKey=null;return;}
            var draft=PurchaseDraft(offer);
            if(install&&draft.Individual){ChoosePurchasePositions(offer,draft);return;}
            if(game.StageNativePurchase(offer.Definition.id,draft.Individual?draft.Role:null,draft.Quantity,install))partsStateKey=null;
            else partPurchaseControls.Q<Label>("partPurchaseError").text=game.NativeNotice;
        }
        void ChoosePurchasePositions(NativePartEntry offer,PartPurchaseDraft draft)
        {
            OpenPurchasePositions(offer.Definition,draft.Role,draft.Quantity,draft.Positions,selected=>{
                if(!game.StageNativePurchase(offer.Definition.id,draft.Role,draft.Quantity,true,selected))return false;
                draft.Positions=selected.ToList();return true;
            },()=>partsStateKey=null);
        }
        void ChooseCartUnitPositions(PartCartLine line)
        {
            OpenPurchasePositions(game.Profile.catalog.Find(line.definitionId),line.role,line.quantity,line.positions,
                selected=>game.SetNativeCartUnitPositions(line,selected),()=>{partsStateKey=null;OpenNativeCart();});
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
                foreach(var position in positions){string target=position;var b=new Button(()=>{if(!selected.Remove(target))selected.Add(target);update();}){name="purchasePosition:"+target,text=InventoryUnits.PositionLabel(game.Profile,game.Profile.SelectedCar,definition.id,target),focusable=false,tabIndex=-1};b.AddToClassList("action-button");b.AddToClassList("part-position-choice");content.Add(b);AddSurface(b,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(b.Q<RacingSurface>());buttons.Add(Tuple.Create(b,target));}
                var error=new Label{name="purchasePositionsError"};error.AddToClassList("car-critical");content.Add(error);
            });update();
        }
    }
}
