using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal sealed class NativePartPreviewCache
    {
        object profile,catalog;string context;
        readonly Dictionary<string,CarInspection> inspections=new Dictionary<string,CarInspection>();
        readonly Dictionary<string,PlayerProfile> plans=new Dictionary<string,PlayerProfile>();
        readonly Dictionary<string,NativePartEntry> comparisons=new Dictionary<string,NativePartEntry>();
        readonly LinkedList<string> recent=new LinkedList<string>();
        readonly Dictionary<string,LinkedListNode<string>> nodes=new Dictionary<string,LinkedListNode<string>>();
        internal const int ComparisonLimit=256;
        internal string Context=>context;
        internal int Hits,Misses;
        internal bool HasComparisons=>comparisons.Count>0;
        internal static string Fingerprint(string text){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));}
        internal void Bind(PlayerProfile p,OwnedCar car)
        {
            string key=p.revision+"/"+JsonUtility.ToJson(car)+"/"+JsonUtility.ToJson(p.catalog.Spec(car.vehicleId))+"/"+string.Join("/",car.installed.slots.Select(s=>{var part=p.parts.Find(i=>i.instanceId==s.partInstanceId);return part==null?"missing/"+s.partInstanceId:JsonUtility.ToJson(part)+JsonUtility.ToJson(p.catalog.Find(part.definitionId));}));
            if(ReferenceEquals(profile,p)&&ReferenceEquals(catalog,p.catalog)&&context==key)return;
            profile=p;catalog=p.catalog;context=key;inspections.Clear();plans.Clear();comparisons.Clear();recent.Clear();nodes.Clear();
        }
        internal CarInspection Inspection(string key,Func<CarInspection> calculate)
        {if(inspections.TryGetValue(key,out var value)){Hits++;return value;}Misses++;value=calculate();if(inspections.Count>=128)inspections.Clear();inspections[key]=value;return value;}
        internal PlayerProfile Plan(string key,Func<PlayerProfile> calculate)
        {if(plans.TryGetValue(key,out var value)){Hits++;return value;}Misses++;value=calculate();if(plans.Count>=4)plans.Clear();plans[key]=value;return value;}
        internal static string Key(string reference,NativePartEntry e)=>reference+"/"+JsonUtility.ToJson(e.Definition)+"/"+JsonUtility.ToJson(e.Part)+"/"+JsonUtility.ToJson(e.Unit)+"/"+e.Position;
        internal bool Restore(string key,NativePartEntry e)
        {
            if(!comparisons.TryGetValue(key,out var saved))return false;recent.Remove(nodes[key]);recent.AddLast(nodes[key]);Hits++;e.Ready=true;e.Proposed=saved.Proposed;e.Reason=saved.Reason;e.Compatibility=saved.Compatibility;e.Position=saved.Position;e.RequiresMatchingSet=saved.RequiresMatchingSet;e.Change=(double?[])saved.Change.Clone();return true;
        }
        internal void Store(string key,NativePartEntry e)
        {
            if(nodes.TryGetValue(key,out var node)){recent.Remove(node);recent.AddLast(node);}else{if(comparisons.Count>=ComparisonLimit){string oldest=recent.First.Value;recent.RemoveFirst();nodes.Remove(oldest);comparisons.Remove(oldest);}nodes[key]=recent.AddLast(key);}
            comparisons[key]=new NativePartEntry{Ready=true,Proposed=e.Proposed,Reason=e.Reason,Compatibility=e.Compatibility,Position=e.Position,RequiresMatchingSet=e.RequiresMatchingSet,Change=(double?[])e.Change.Clone()};
        }
    }
}
