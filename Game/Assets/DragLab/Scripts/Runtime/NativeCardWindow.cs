using UnityEngine;

namespace DragLab.Runtime
{
    internal readonly struct NativeCardWindow
    {
        internal readonly int First,Last;
        internal readonly float Before,After;
        NativeCardWindow(int first,int last,float before,float after){First=first;Last=last;Before=before;After=after;}
        internal static NativeCardWindow Calculate(int count,float offset,float height,float stride)
        {
            if(count<=0)return new NativeCardWindow(0,0,0,0);
            stride=Mathf.Max(1,stride);
            int rows=(count+1)/2,top=Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0,offset)/stride),0,rows-1);
            int first=Mathf.Max(0,top-1),last=Mathf.Min(rows,top+Mathf.CeilToInt(Mathf.Max(stride,height)/stride)+2);
            return new NativeCardWindow(first*2,Mathf.Min(count,last*2),first*stride,(rows-last)*stride);
        }
    }
}
