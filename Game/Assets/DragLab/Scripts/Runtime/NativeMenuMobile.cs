using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        void InitializeTouchMenus()
        {
            root.RegisterCallback<FocusInEvent>(evt=>{
                if(!NativeMobile.Touch)return;
                for(var item=evt.target as VisualElement;item!=null;item=item.parent){
                    if(item is FloatField number){number.keyboardType=TouchScreenKeyboardType.NumbersAndPunctuation;number.hideMobileInput=false;break;}
                    if(item is TextField text){text.hideMobileInput=false;break;}
                }
            },TrickleDown.TrickleDown);
        }
        internal void TouchBack()
        {
            if(game.ResetRequired)return;
            if(EditingText){(root.focusController.focusedElement as VisualElement)?.Blur();return;}
            if(ModalOpen){CloseDialog();return;}
            if(game.ScreenName=="Settings"){ReadApplied();game.QueueNativeNavigation(game.SettingsReturnRoute);return;}
            if(game.ScreenName=="Tuning"&&setupLibraryOpen){BackFromSetupLibrary();return;}
            if(game.ScreenName=="Parts"&&cartPageOpen){BackFromNativeCart();return;}
            if(game.ScreenName=="Parts"&&enginePageOpen){enginePageOpen=false;engineDetailId=null;partsStateKey=null;return;}
            if(game.ScreenName=="Parts"&&partsDetailId!=null){BackToParts();return;}
            if(DealerDetailsOpen){BackToDealer();return;}
            if(DisposalOpen){BackFromDisposal();return;}
            if(OwnedDetailsOpen){BackToOwnedCars();return;}
            if(game.ScreenName!="Garage")Navigate("Garage");
        }
    }
}
