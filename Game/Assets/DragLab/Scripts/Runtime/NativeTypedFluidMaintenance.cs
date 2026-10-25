using System;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        string typedRefillViewOperationId;
        Button typedRefillConfirm;
        Label typedRefillConfirmCaption;
        Label typedRefillStatus;
        Label typedRefillCost;
        Label typedRefillError;
        NativeTuningRange typedRefillRange;
        TypedFluidRefillQuote typedRefillQuote;
        string typedRefillCarId;
        bool typedRefillCoolant;
        double typedRefillMaximum;
        bool typedRefillQuoteValid;
        Action typedRefillQuoteRefresh;
        long typedRefillObservedCompletion;
        bool typedRefillWasBlocked;
        string typedRefillTerminalStatus;
        bool typedRefillRecoveryOnly;

        bool TypedRefillDialogAttached => typedRefillStatus!=null && typedRefillConfirm!=null &&
            typedRefillStatus.panel!=null && typedRefillConfirm.panel!=null &&
            ReferenceEquals(typedRefillStatus.panel,root.panel) && ReferenceEquals(typedRefillConfirm.panel,root.panel) && ModalOpen;

        void OpenTypedManualRefill(string title,string carId,double maximum,bool coolant,string description)
        {
            bool retainedResolution=game.ManualRefillCanRetry||game.ManualRefillNeedsRecovery;
            if (ModalOpen || !OwnedDetailsOpen || carId != ownedDetailId || (maximum <= .000001&&!retainedResolution)) return;
            typedRefillCarId=carId;
            typedRefillCoolant=coolant;
            typedRefillMaximum=maximum;
            typedRefillRecoveryOnly=retainedResolution;
            typedRefillViewOperationId=null;
            typedRefillObservedCompletion=game.ManualRefillCompletion;
            typedRefillQuoteValid=false;
            typedRefillTerminalStatus=null;
            typedRefillConfirm=null;
            typedRefillStatus=null;
            typedRefillCost=null;
            typedRefillError=null;
            typedRefillRange=null;

            // NativeTuningRange shares the standard slider, precise number input, right-drag
            // fine adjustment and middle-click reset behavior used by tuning controls.
            float amount=(float)maximum;
            Action updateQuote=()=>RefreshTypedRefillQuote(amount);
            typedRefillQuoteRefresh=typedRefillRecoveryOnly?null:updateQuote;
            Action changed=typedRefillRecoveryOnly?new Action(()=>{}):updateQuote;
            typedRefillRange=new NativeTuningRange(
                "maintenanceRefill",
                ()=>amount,
                value=>amount=value,
                ()=>0,
                ()=> (float)typedRefillMaximum,
                ()=> (float)typedRefillMaximum,
                .01f,.001f,"L",changed,
                message=>{if(typedRefillError!=null)typedRefillError.text=message;});
            typedRefillRange.name="tuneRange:maintenanceRefill";

            OpenDialog(title,null,new[] {
                new DialogAction("Cancel",CloseDialog),
                new DialogAction("Confirm",()=>TypedRefillAction(amount),true)
            },populate:content=> {
                content.Add(new Label(description){enableRichText=false});
                content.Add(new Label("Amount"));
                typedRefillRange.Slider.name="maintenanceQuantitySlider";
                typedRefillRange.Number.name="maintenanceQuantity";
                var rangeContainer=new VisualElement{name="maintenanceRefillRange"};
                rangeContainer.Add(typedRefillRange);content.Add(rangeContainer);
                content.Add(new Label("Free capacity · "+maximum.ToString("F2")+" L") {name="maintenanceRefillCapacity"});
                typedRefillCost=new Label {name="maintenanceCost"};content.Add(typedRefillCost);
                typedRefillStatus=new Label {name="maintenanceRefillStatus"};typedRefillStatus.AddToClassList("maintenance-summary");content.Add(typedRefillStatus);
                typedRefillError=new Label {name="maintenanceError"};typedRefillError.AddToClassList("car-critical");content.Add(typedRefillError);
            });
            typedRefillConfirm=root.Q<Button>("dialogConfirm");
            typedRefillConfirmCaption=typedRefillConfirm?.Q<Label>();
            SetTypedRefillConfirmCaption("Confirm");
            typedRefillRange.GestureFinished+=updateQuote;
            UpdateTypedRefillDialog();
            if(!typedRefillRecoveryOnly)updateQuote();
            ExcludeTabTraversal();
        }

        void RefreshTypedRefillQuote(float amount)
        {
            if(typedRefillCost==null||typedRefillError==null)return;
            try {
                typedRefillTerminalStatus=null;
                if(float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0||amount>typedRefillMaximum+.00001)
                    throw new InvalidOperationException("Enter an amount above 0 and up to "+typedRefillMaximum.ToString("F2")+" L.");
                double oil=typedRefillCoolant?0:amount;
                double coolant=typedRefillCoolant?amount:0;
                if(!game.TryQuoteManualRefill(typedRefillCarId,oil,coolant,out typedRefillQuote,out string error))
                    throw new InvalidOperationException(error??"The refill quote is unavailable.");
                typedRefillQuoteValid=true;
                typedRefillCost.text="Total "+DisplayUnits.Money(typedRefillQuote.Cost)+" · Cash "+DisplayUnits.Money(typedRefillQuote.CashBefore);
                typedRefillError.text=typedRefillQuote.CashBefore>=typedRefillQuote.Cost?"":"Insufficient cash.";
                bool priorSuccess=SameTypedRefillOperation&&game.ManualRefillLastSucceeded;
                if(typedRefillConfirm!=null)typedRefillConfirm.SetEnabled(
                    typedRefillQuote.CashBefore>=typedRefillQuote.Cost && !game.ManualRefillPending &&
                    !game.ManualRefillCanRetry && !game.ManualRefillNeedsRecovery && !priorSuccess);
            } catch(Exception error) {
                typedRefillQuoteValid=false;
                typedRefillError.text=error.Message;
                typedRefillCost.text="";
                if(typedRefillConfirm!=null)typedRefillConfirm.SetEnabled(false);
            }
        }

        void ConfirmTypedManualRefill(float amount)
        {
            if(!typedRefillQuoteValid||game.ManualRefillPending)return;
            // Submit the immutable terms the player saw. The facade performs a fresh scoped
            // validation and rejects a stale quote instead of silently repricing it here.
            if(!game.BeginManualRefill(typedRefillQuote)) {
                string failure=game.ManualRefillStatus;
                if(game.ManualRefillNeedsRecovery||game.ManualRefillCanRetry) {
                    // Keep the exact terms the player just saw while recovery is required.
                    // They stay visibly stale and disabled until the verified reload/retry.
                    typedRefillTerminalStatus=string.IsNullOrEmpty(failure)
                        ?"The shown refill quote is stale. Resolve the save before continuing.":failure;
                    if(typedRefillError!=null)typedRefillError.text=typedRefillTerminalStatus;
                    if(typedRefillStatus!=null)typedRefillStatus.text=typedRefillTerminalStatus;
                } else {
                    RefreshTypedRefillQuote(amount);
                    typedRefillTerminalStatus=string.IsNullOrEmpty(failure)
                        ?"The refill was not accepted. Review the refreshed quote and confirm again.":failure;
                    if(typedRefillStatus!=null)typedRefillStatus.text=typedRefillTerminalStatus;
                }
                UpdateTypedRefillDialog();
                return;
            }
            typedRefillViewOperationId=game.ManualRefillOperationId;
            UpdateTypedRefillDialog();
        }

        void TypedRefillAction(float amount)
        {
            if(game.ManualRefillPending)return;
            if(game.ManualRefillCanRetry) {
                if(!game.RetryManualRefill()&&typedRefillError!=null)typedRefillError.text=game.ManualRefillStatus;
                UpdateTypedRefillDialog();return;
            }
            if(game.ManualRefillNeedsRecovery) {
                if(!game.RecoverManualRefill()&&typedRefillError!=null)typedRefillError.text=game.ManualRefillStatus;
                UpdateTypedRefillDialog();return;
            }
            ConfirmTypedManualRefill(amount);
        }

        void SetTypedRefillConfirmCaption(string text)
        {
            if(typedRefillConfirm==null)return;
            typedRefillConfirm.text="";
            if(typedRefillConfirmCaption!=null)typedRefillConfirmCaption.text=text;
        }

        // Called by the owner frame. This only changes dialog labels and action state; it never
        // rebuilds the view, so an active slider/number gesture remains captured and stationary.
        internal void UpdateTypedRefillDialog()
        {
            if(!TypedRefillDialogAttached) {
                typedRefillStatus=null;typedRefillConfirm=null;typedRefillConfirmCaption=null;typedRefillCost=null;typedRefillError=null;typedRefillRange=null;typedRefillQuoteRefresh=null;
                return;
            }
            bool sameOperation=SameTypedRefillOperation;
            bool retry=game.ManualRefillCanRetry;
            bool recover=game.ManualRefillNeedsRecovery;
            bool blocked=game.ManualRefillPending||retry||recover;
            if(typedRefillRange!=null)typedRefillRange.Allow(!typedRefillRecoveryOnly&&!blocked&&!(sameOperation&&game.ManualRefillLastSucceeded));
            if(blocked) {
                typedRefillWasBlocked=true;
                typedRefillStatus.text=game.ManualRefillStatus??"A refill operation is still being resolved.";
                typedRefillConfirm.SetEnabled(!game.ManualRefillPending&&(retry||recover));
                if(retry)SetTypedRefillConfirmCaption("Retry status");
                else if(recover)SetTypedRefillConfirmCaption("Recover");
                else SetTypedRefillConfirmCaption("Confirm");
                return;
            }

            // A dialog opened while another view's operation was retained can resume editing
            // only after that operation resolves. Refresh its shown terms once per completed
            // operation, outside confirmation and only when no range gesture owns the input.
            long completion=game.ManualRefillCompletion;
            bool completionChanged=completion!=typedRefillObservedCompletion;
            bool resolvedBlockedOperation=typedRefillWasBlocked;
            if(completionChanged||resolvedBlockedOperation) {
                if(sameOperation&&game.ManualRefillLastSucceeded) {
                    typedRefillObservedCompletion=completion;
                    typedRefillWasBlocked=false;
                }
                else if(typedRefillRecoveryOnly) {
                    typedRefillObservedCompletion=completion;
                    typedRefillWasBlocked=false;
                    typedRefillTerminalStatus=game.ManualRefillStatus;
                }
                else if(typedRefillRange==null||!typedRefillRange.Dragging) {
                    string terminal=game.ManualRefillStatus;
                    typedRefillObservedCompletion=completion;
                    typedRefillWasBlocked=false;
                    typedRefillQuoteRefresh?.Invoke();
                    typedRefillTerminalStatus=string.IsNullOrEmpty(terminal)&&sameOperation
                        ?"The refill was not saved. Review the refreshed quote before trying again.":terminal;
                }
            }
            if(sameOperation&&game.ManualRefillLastSucceeded) {
                typedRefillWasBlocked=false;
                typedRefillStatus.text=game.ManualRefillStatus??"Refill complete.";
                SetTypedRefillConfirmCaption("Complete");
                typedRefillConfirm.SetEnabled(false);
                return;
            }
            if(typedRefillRecoveryOnly) {
                typedRefillStatus.text=typedRefillTerminalStatus??"Save check complete. Close and reopen maintenance to review current capacity.";
                SetTypedRefillConfirmCaption("Complete");
                typedRefillConfirm.SetEnabled(false);
                return;
            }
            typedRefillStatus.text=typedRefillTerminalStatus??"";
            SetTypedRefillConfirmCaption("Confirm");
            typedRefillConfirm.SetEnabled(typedRefillQuoteValid&&typedRefillQuote!=null&&
                typedRefillQuote.Cost<=typedRefillQuote.CashBefore&&!game.ManualRefillPending&&
                !game.ManualRefillCanRetry&&!game.ManualRefillNeedsRecovery);
        }

        bool SameTypedRefillOperation => !string.IsNullOrEmpty(typedRefillViewOperationId)&&
            game.ManualRefillOperationId==typedRefillViewOperationId;
    }
}
