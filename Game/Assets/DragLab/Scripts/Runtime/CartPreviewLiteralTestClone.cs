#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;
using UnityEditor;

namespace DragLab.Runtime
{
    /// <summary>
    /// Exact test-input isolation only. Never call from gameplay, saves, or a preview planner.
    /// Unsupported graph shapes fail closed; there is no JSON fallback.
    /// </summary>
    internal static class CartPreviewLiteralTestClone
    {
        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object left,object right)=>ReferenceEquals(left,right);
            public int GetHashCode(object value)=>RuntimeHelpers.GetHashCode(value);
        }

        sealed class CopyContext
        {
            public readonly MechanicalCatalog SharedCatalog;
            public readonly Dictionary<object,object> Copies=new Dictionary<object,object>(new ReferenceComparer());
            public CopyContext(MechanicalCatalog catalog){SharedCatalog=catalog;}
        }

        internal static PlayerProfile Copy(PlayerProfile source)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            var context=new CopyContext(source.catalog);
            var copy=CopyValue(source,context,"profile") as PlayerProfile;
            if(copy==null||ReferenceEquals(copy,source))throw new InvalidOperationException("Literal profile isolation did not create a distinct PlayerProfile.");
            // PlayerProfile.catalog is NonSerialized and authored catalogue state is intentionally shared.
            copy.catalog=source.catalog;
            if(!ReferenceEquals(copy.catalog,source.catalog))throw new InvalidOperationException("Literal clone lost its explicitly shared authored catalogue reference.");
            return copy;
        }

        static object CopyValue(object value,CopyContext context,string path)
        {
            if(ReferenceEquals(value,null))return null;
            var type=value.GetType();
            if(IsImmutable(type))return value;
            if(value is MechanicalCatalog)
            {
                if(!ReferenceEquals(value,context.SharedCatalog))throw Unsupported(type,path,"only the source authored catalogue may be shared");
                return value;
            }
            if(value is UnityEngine.Object authoredObject)
            {
                if(!EditorUtility.IsPersistent(authoredObject))throw Unsupported(type,path,"non-asset Unity object references are not valid profile fixture data");
                return authoredObject;
            }
            if(context.Copies.TryGetValue(value,out var existing))return existing;

            if(type.IsArray)return CopyArray((Array)value,context,path);
            if(IsExactList(type))return CopyList((IList)value,type,context,path);
            if(IsUnsupportedCollection(type))throw Unsupported(type,path,"only arrays and exact List<T> collections are copied");

            if(type.IsPointer||type.IsInterface||type.IsAbstract||!type.IsSerializable)
                throw Unsupported(type,path,"expected an immutable scalar, array, List<T>, authored reference, or serializable DTO");

            object result;
            try{result=FormatterServices.GetUninitializedObject(type);}
            catch(Exception error){throw new NotSupportedException("Cannot allocate literal test clone node at "+path+" of type "+type.FullName+" without running a constructor.",error);}
            context.Copies.Add(value,result);
            foreach(var field in SerializedFields(type))
            {
                object child=field.GetValue(value);
                object copied=CopyValue(child,context,path+"."+field.Name);
                try{field.SetValue(result,copied);}
                catch(Exception error){throw new NotSupportedException("Cannot copy serialized field at "+path+"."+field.Name+" without running constructors.",error);}
            }
            return result;
        }

        static Array CopyArray(Array source,CopyContext context,string path)
        {
            var type=source.GetType();var elementType=type.GetElementType();
            if(elementType==null||elementType.IsPointer)throw Unsupported(type,path,"array has an unsupported element type");
            int rank=source.Rank;var lengths=new int[rank];var lowerBounds=new int[rank];
            for(int dimension=0;dimension<rank;dimension++){lengths[dimension]=source.GetLength(dimension);lowerBounds[dimension]=source.GetLowerBound(dimension);}
            Array copy;
            try{copy=rank==1&&lowerBounds[0]==0?Array.CreateInstance(elementType,lengths[0]):Array.CreateInstance(elementType,lengths,lowerBounds);}
            catch(Exception error){throw new NotSupportedException("Cannot allocate literal test clone array at "+path+" of type "+type.FullName+".",error);}
            context.Copies.Add(source,copy);
            CopyArrayDimension(source,copy,context,path,new int[rank],0);
            return copy;
        }

        static void CopyArrayDimension(Array source,Array copy,CopyContext context,string path,int[] indices,int dimension)
        {
            if(dimension==source.Rank)
            {
                object value=source.GetValue(indices);
                copy.SetValue(CopyValue(value,context,path+"["+string.Join(",",indices)+"]"),indices);
                return;
            }
            int end=source.GetUpperBound(dimension);
            for(int index=source.GetLowerBound(dimension);index<=end;index++)
            {indices[dimension]=index;CopyArrayDimension(source,copy,context,path,indices,dimension+1);}
        }

        static IList CopyList(IList source,Type listType,CopyContext context,string path)
        {
            IList copy;
            try{copy=(IList)Activator.CreateInstance(listType);}
            catch(Exception error){throw new NotSupportedException("Cannot allocate empty test List<T> at "+path+" of type "+listType.FullName+".",error);}
            context.Copies.Add(source,copy);
            for(int i=0;i<source.Count;i++)copy.Add(CopyValue(source[i],context,path+"["+i.ToString(System.Globalization.CultureInfo.InvariantCulture)+"]"));
            return copy;
        }

        static bool IsExactList(Type type)
            =>type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(List<>);

        static bool IsUnsupportedCollection(Type type)
        {
            if(typeof(IDictionary).IsAssignableFrom(type)||typeof(IList).IsAssignableFrom(type))return true;
            foreach(var contract in type.GetInterfaces())
            {
                if(!contract.IsGenericType)continue;
                var definition=contract.GetGenericTypeDefinition();
                if(definition==typeof(IDictionary<,>)||definition==typeof(IReadOnlyDictionary<,>)||
                   definition==typeof(ICollection<>)||definition==typeof(IReadOnlyCollection<>)||definition==typeof(ISet<>))return true;
            }
            return false;
        }

        static bool IsImmutable(Type type)
            =>type.IsPrimitive||type.IsEnum||type==typeof(string)||type==typeof(decimal)||type==typeof(DateTime)||
              type==typeof(DateTimeOffset)||type==typeof(TimeSpan)||type==typeof(Guid);

        static IEnumerable<FieldInfo> SerializedFields(Type type)
        {
            var hierarchy=new Stack<Type>();
            for(var current=type;current!=null&&current!=typeof(object);current=current.BaseType)hierarchy.Push(current);
            while(hierarchy.Count>0)
                foreach(var field in hierarchy.Pop().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                {
                    if(field.IsStatic||field.IsInitOnly||field.IsNotSerialized)continue;
                    bool serialized=field.IsPublic||Attribute.IsDefined(field,typeof(SerializeField))||Attribute.IsDefined(field,typeof(SerializeReference));
                    if(serialized)yield return field;
                }
        }

        static NotSupportedException Unsupported(Type type,string path,string reason)
            =>new NotSupportedException("Unsupported literal cart-test clone node at "+path+" ("+type.FullName+"): "+reason+".");
    }
}
#endif
