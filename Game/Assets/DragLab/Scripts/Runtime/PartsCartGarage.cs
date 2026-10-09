using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        string partsPage="Shop",cartOwner;
        readonly List<PartCartLine> cart=new List<PartCartLine>();
        bool previewCart,inventoryAllCategories,inventoryCompatibleOnly;
        internal string PartsPage=>partsPage;
        internal int CartCount=>cart.Sum(l=>l.quantity);
        internal void SelectPartsPage(string page)
        {
            partsPage=page;scroll=Vector2.zero;partRects.Clear();PreviewPart();previewCart=page=="Shop"&&cart.Count>0;statsKey=null;
        }
        void EnsureCartOwner()
        {
            if(cartOwner==Build.instanceId)return;
            cartOwner=Build.instanceId;cart.Clear();previewCart=false;statsKey=null;
        }
        internal void AddToCart(string id,bool install=false)
        {
            EnsureCartOwner();var d=profile.catalog.Find(id);
            if(d==null||!profile.catalog.Fits(Selected.spec,d)||profile.reputation<d.reputationRequired){notice="Offer incompatible or locked by reputation.";return;}
            var line=cart.Find(l=>l.definitionId==id);
            if(line==null){if(cart.Count>=100){notice="Cart holds at most 100 distinct offers.";return;}line=new PartCartLine{definitionId=id};cart.Add(line);}else line.quantity=Math.Min(99,line.quantity+1);
            if(install)SetCartInstallation(line,true);
            previewCart=true;previewDefinitionId=previewInstanceId=null;previewRemoval=null;statsKey=null;
        }
        void SetCartInstallation(PartCartLine line,bool value)
        {
            if(value){var slot=profile.catalog.Find(line.definitionId).category;foreach(var other in cart)if(other!=line&&profile.catalog.Find(other.definitionId).category==slot)other.install=false;}
            line.install=value;previewCart=true;statsKey=null;
        }
        internal bool CheckoutCartNow()
        {
            try {
                var quote=PartCart.Quote(profile,Build.instanceId,cart,DateTime.UtcNow.Ticks,Costs.quoteSeconds);
                if(!CommitParts(quote.revision,p=>PartCart.Checkout(p,quote,DateTime.UtcNow.Ticks)))return false;
                cart.Clear();previewCart=false;statsKey=null;notice="Cart purchased and saved together. Checked items installed; spares and removed originals retained.";return true;
            }catch(Exception e){notice=e.Message;return false;}
        }
        void CartGUI()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("CART / "+CartCount+" parts / temporary until checkout",stat);
            foreach(var line in cart) {
                var captured=line;var d=profile.catalog.Find(line.definitionId);
                GUILayout.Label(d.name+" × "+line.quantity+" / $"+checked(d.price*line.quantity));
                GUILayout.BeginHorizontal();
                bool install=GUILayout.Toggle(line.install,"Install one after buying");if(install!=line.install)SetCartInstallation(line,install);
                if(PartButton("cart-minus:"+d.id,"−")&&line.quantity>1){line.quantity--;previewCart=true;statsKey=null;}
                if(PartButton("cart-plus:"+d.id,"+")&&line.quantity<99){line.quantity++;previewCart=true;statsKey=null;}
                if(PartButton("cart-remove:"+d.id,"Remove"))pendingAction=()=>{cart.Remove(captured);previewCart=cart.Count>0;statsKey=null;};
                GUILayout.EndHorizontal();
            }
            if(cart.Count>0) {
                int total=0;string error=null;try{total=PartCart.Total(profile,Build.instanceId,cart);}catch(Exception e){error=e.Message;}
                GUILayout.Label(error??("Total $"+total+" / cash after $"+(profile.cash-total)));
                GUILayout.Label("Combined preview includes checked installations across all categories. Extra copies remain loose. Only one offer may install per slot; checking another replaces that selection.",partProse);
                GUILayout.BeginHorizontal();
                if(PartButton("cart-preview","Preview combined changes")){previewCart=true;previewDefinitionId=previewInstanceId=null;previewRemoval=null;statsKey=null;}
                bool enabled=GUI.enabled;GUI.enabled=enabled&&error==null&&profile.cash>=total;
                if(PartButton("cart-checkout","Buy cart / install checked"))pendingAction=()=>CheckoutCartNow();GUI.enabled=enabled;
                if(PartButton("cart-clear","Clear cart"))pendingAction=()=>{cart.Clear();previewCart=false;statsKey=null;};
                GUILayout.EndHorizontal();
            } else GUILayout.Label("Add offers below. Cart edits and combined previews never save or spend cash.",partProse);
            GUILayout.EndVertical();
        }
    }
}
