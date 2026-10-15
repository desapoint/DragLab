using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Infrastructure;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal sealed class NativeSetupLoadQuote
    {
        internal string CarId,SetupId,Context,Name,CarName;
        internal long Revision;internal SetupScope Scope;
        internal NativeSetupPreview Preview;
        internal int Cost=>Preview.Plan.cost;
        internal bool BuyMissing=>Preview.Plan.Missing>0;
        internal static string Fingerprint(PlayerProfile p)=>NativePartPreviewCache.Fingerprint(JsonUtility.ToJson(p)+JsonUtility.ToJson(p.catalog.Spec(p.SelectedCar.vehicleId))+string.Join("/",p.catalog.parts.Select(JsonUtility.ToJson)));
        internal static NativeSetupLoadQuote Capture(PlayerProfile p,string setupId,NativePartPreviewCache cache=null)
        {
            var car=p.SelectedCar??throw new InvalidOperationException("Select a car in Garage.");
            AssemblyOperations.MutableCar(p,car.instanceId);
            var setup=p.savedSetups.Find(s=>s.id==setupId)??throw new InvalidOperationException("Saved setup no longer exists.");
            var preview=NativeSetupPreview.Capture(p,car,setup,cache);
            if(preview.Unavailable!=null)throw new InvalidOperationException(preview.Unavailable);
            if(preview.Plan.errors.Count>0)throw new InvalidOperationException(string.Join("\n",preview.Plan.errors.Distinct()));
            if(preview.Proposed==null)throw new InvalidOperationException("This setup cannot be loaded on the selected car.");
            return new NativeSetupLoadQuote{CarId=car.instanceId,SetupId=setupId,Revision=p.revision,Scope=setup.scope,Name=setup.name,CarName=VehicleCollection.DisplayName(p.catalog,car)+" · #"+car.instanceId.Substring(Math.Max(0,car.instanceId.Length-6)),Preview=preview,Context=Fingerprint(p)};
        }
        internal void Validate(PlayerProfile p)
        {
            if(p.SelectedCar?.instanceId!=CarId||p.revision!=Revision||Context!=Fingerprint(p))throw new InvalidOperationException("Setup, parts or prices changed. Reopen the load review.");
        }
    }
    public sealed partial class PrototypeGame
    {
        internal bool LoadNativeSetup(NativeSetupLoadQuote quote)
        {
            try{
                if(screen!="Tuning"||pendingAction!=null||DraftDirty)throw new InvalidOperationException("Apply or revert tuning changes before loading a setup.");
                if(CartCount>0)throw new InvalidOperationException("Checkout or clear the cart before loading a setup.");
                quote.Validate(profile);
                if(!CommitParts(quote.Revision,p=>SavedSetups.Apply(p,quote.CarId,quote.SetupId,quote.Revision,quote.BuyMissing,quote.Cost)))return false;
                BeginTuneDraft();notice="Setup loaded.";return true;
            }catch(Exception e){notice=e.Message;return false;}
        }
    }
    internal sealed partial class NativeMenuShell
    {
        Button setupLoad;Label setupLoadStatus;
        void InitializeSetupLoad()
        {
            var actions=new VisualElement{name="setupLoadActions"};actions.AddToClassList("car-details-actions");setupPage.Add(actions);
            setupLoadStatus=new Label{name="setupLoadStatus",enableRichText=false};setupLoadStatus.AddToClassList("settings-status");setupLoadStatus.style.whiteSpace=WhiteSpace.Normal;setupLoadStatus.style.flexShrink=1;actions.Add(setupLoadStatus);
            setupLoad=new Button(()=>OpenSetupLoadReview()){name="setupLoad",text="Load",focusable=false,tabIndex=-1};setupLoad.AddToClassList("action-button");setupLoad.AddToClassList("primary-action");actions.Add(setupLoad);AddSurface(setupLoad,RacingSurface.Kind.Action);setupLoad.Insert(1,new FilledMenuIcon("Business"));
        }
        void RefreshSetupLoadAction()
        {
            setupDelete.SetEnabled(game.Profile.savedSetups.Any(s=>s.id==setupSelectedId));
            string reason=game.DraftDirty?"Apply or revert tuning changes first.":game.CartCount>0?"Checkout or clear the cart first.":setupPreview?.Unavailable;
            if(reason==null&&setupPreview?.Plan.errors.Count>0)reason=setupPreview.Plan.errors[0];
            bool selected=setupPreview?.Proposed!=null;setupLoad.SetEnabled(selected&&reason==null);setupLoadStatus.text=reason??"";setupLoad.tooltip=reason??"Review and confirm this saved setup.";
            setupLoad.Q<Label>().text=setupPreview?.Plan.Missing>0?"Buy & load · "+DisplayUnits.Money(setupPreview.Plan.cost):"Load";
        }
        void OpenSetupLoadReview(NativeSetupLoadQuote captured=null)
        {
            NativeSetupLoadQuote quote;
            try{
                if(game.DraftDirty||game.CartCount>0)throw new InvalidOperationException(game.DraftDirty?"Apply or revert tuning changes first.":"Checkout or clear the cart first.");
                quote=captured??NativeSetupLoadQuote.Capture(game.Profile,setupSelectedId,partPreviewCache);
            }catch(Exception e){setupLoadStatus.text=e.Message;return;}
            var error=new Label{name="setupLoadError",enableRichText=false};error.AddToClassList("car-critical");
            var plan=quote.Preview.Plan;
            OpenDialog("Load setup",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction(quote.BuyMissing?"Buy & load":"Load",()=>{
                if(setupSelectedId!=quote.SetupId){error.text="Setup selection changed. Reopen the load review.";return;}
                if(!game.LoadNativeSetup(quote)){error.text=game.NativeNotice;return;}
                CloseDialog();setupLibraryKey=null;Refresh();
            },true)},populate:content=>{
                content.Add(new Label(quote.Name+" · "+NativeSetupQuery.ScopeName(quote.Scope)){enableRichText=false,name="setupLoadIdentity"});
                content.Add(new Label(quote.CarName){enableRichText=false});
                content.Add(new Label(plan.lines.Count(l=>l.installed)+" fitted · "+plan.lines.Count(l=>!l.installed&&!l.purchase)+" reuse · "+plan.Missing+" buy"));
                content.Add(new Label("Total "+DisplayUnits.Money(quote.Cost)+" · Balance "+DisplayUnits.Money(game.Profile.cash-quote.Cost)){name="setupLoadTotal"});
                EngineComparison(content,quote.Preview.Current,quote.Preview.Proposed,"setupLoad",(element,kind)=>{AddSurface(element,kind);dialogSurfaces.Add(element.Q<RacingSurface>());},false,info:(definition,panel)=>OpenDialog(definition.Label,definition.InfoExplanation(panel.Current,panel.Proposed),new[]{new DialogAction("Close",CloseDialog)},dismiss:()=>root.schedule.Execute(()=>{if(game.ScreenName=="Tuning"&&setupLibraryOpen&&setupSelectedId==quote.SetupId)OpenSetupLoadReview(quote);})),proposedLabel:"Saved setup");
                foreach(var line in plan.lines.Where(l=>!l.installed)){var d=game.Profile.catalog.Find(line.definitionId);var item=game.Profile.parts.Find(i=>i.instanceId==line.itemId);content.Add(new Label(d.name+" · "+(line.purchase?"Buy · "+DisplayUnits.Money(line.cost):"Reuse · "+PrototypeGame.MaintenanceConditionHealth(item).ToString("P0"))){enableRichText=false});}
                foreach(var line in plan.tires)content.Add(new Label(InventoryUnits.PositionLabel(game.Profile,game.Profile.SelectedCar,line.definitionId,(line.wheel?"wheel:":"tire:")+line.corner)+" · "+game.Profile.catalog.Find(line.definitionId).name+" · "+(line.purchase?"Buy · "+DisplayUnits.Money(line.cost):"Reuse")){enableRichText=false});
                content.Add(new Label("Removed parts stay in inventory. Existing wear and liquids are retained."));
                foreach(var warning in quote.Preview.Proposed.warnings){var label=new Label(warning.title+" · "+SharedUnitSettings.TorqueText(warning.explanation)+"\n"+SharedUnitSettings.TorqueText(warning.suggestion)){enableRichText=false};label.AddToClassList(warning.critical?"car-critical":"car-warning");content.Add(label);}
                content.Add(error);
            });root.Q("modalActions").Query<Button>().ToList().Last().name="setupLoadConfirm";ExcludeTabTraversal();
        }
    }
}
