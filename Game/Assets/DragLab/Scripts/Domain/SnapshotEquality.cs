using System;
using System.Collections;
using System.Reflection;
using System.Collections.Concurrent;
using UnityEngine;
namespace DragLab.Domain.Mechanics
{
    // Unity's decimal/double JSON roundtrip can move history values by one ULP.
    // Compare the captured serialized state rather than a hash of its new decimal spelling.
    public static class SnapshotEquality
    {
        static readonly ConcurrentDictionary<Type,FieldInfo[]> fields=new ConcurrentDictionary<Type,FieldInfo[]>();
        static FieldInfo[] Fields(Type type)=>fields.GetOrAdd(type,t=>Array.FindAll(t.GetFields(BindingFlags.Public|BindingFlags.Instance),f=>!Attribute.IsDefined(f,typeof(NonSerializedAttribute))));
        public static bool Profile(string json,PlayerProfile current,MechanicalCatalog catalog)
        {
            string actual=JsonUtility.ToJson(current);if(actual==json)return true;
            var expected=JsonUtility.FromJson<PlayerProfile>(json);MechanicsOwnership.Ensure(expected,catalog);return JsonUtility.ToJson(expected)==actual||Same(expected,current);
        }
        public static bool Serialized<T>(string json,T current){return JsonUtility.ToJson(current)==json||Same(JsonUtility.FromJson<T>(json),current);}
        static bool Same(object a,object b,string field="")
        {
            if(a==null||b==null)return a==b;if(a.GetType()!=b.GetType())return false;var type=a.GetType();
            if(type==typeof(double)){
                double x=(double)a,y=(double)b;if(x==y)return true;
                if(field=="health"||!MechanicsSchema.Finite(x)||!MechanicsSchema.Finite(y)||Math.Sign(x)!=Math.Sign(y))return false;
                long xb=BitConverter.DoubleToInt64Bits(x),yb=BitConverter.DoubleToInt64Bits(y);return xb>yb?xb-yb<=4:yb-xb<=4;
            }
            if(type.IsPrimitive||type.IsEnum||type==typeof(string)||type==typeof(decimal))return a.Equals(b);
            if(a is IList al&&b is IList bl){if(al.Count!=bl.Count)return false;for(int i=0;i<al.Count;i++)if(!Same(al[i],bl[i],field))return false;return true;}
            foreach(var f in Fields(type))if(!Same(f.GetValue(a),f.GetValue(b),f.Name))return false;return true;
        }
    }
}
