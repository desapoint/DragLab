using System;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        internal bool CommitEngineAction(long revision,Action<PlayerProfile> operation){if(screen!="Parts"||pendingAction!=null){notice="Workshop is no longer available.";return false;}return CommitParts(revision,operation);}
        internal bool RenameNativeEngine(string id,long revision,string name)
        {
            if(profile.revision!=revision){notice="Engine changed. Reopen Rename.";return false;}
            try{var normalized=VehicleCollection.NormalizeName(name);var e=profile.engines.Find(engine=>engine.instanceId==id);if(e==null)throw new InvalidOperationException("Engine no longer exists.");if((e.customName??"")==normalized)return true;return CommitEngineAction(revision,p=>AssemblyOperations.Rename(p,id,normalized));}catch(Exception error){notice=error.Message;return false;}
        }
        internal bool CommitEngineSwap(long revision,string engineId,string carId,PartCartQuote quote)
        {
            if(quote==null?cart.Count!=0:quote.linesJson!=UnityEngine.JsonUtility.ToJson(new PartCart.Lines{entries=cart.Select(l=>l.Copy()).ToList()})){notice="Cart changed. Reopen swap preview.";return false;}
            if(!CommitEngineAction(revision,p=>{if(quote!=null)PartCart.Checkout(p,quote,DragLab.Domain.RuntimeEntropy.UtcNow.Ticks,PricingPolicy);EngineWorkshop.Swap(p,engineId,carId);} ))return false;
            if(quote!=null)ClearNativeCart();return true;
        }
    }
}
