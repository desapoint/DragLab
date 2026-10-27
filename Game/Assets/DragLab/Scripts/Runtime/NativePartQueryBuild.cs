using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DragLab.Runtime
{
    // Incremental, exact filter/evaluation/sort pass for one presentation generation.
    // It never owns UI state or changes the base browser build lifetime.
    internal sealed class NativePartQueryBuild
    {
        enum Phase { Filter, Evaluate, FinalFilter, SortBuffer, Sort, Complete }
        readonly NativePartBrowser model;
        readonly NativePartQuery query;
        readonly NativePartSection section;
        readonly int cash;
        readonly List<NativePartEntry> candidates=new List<NativePartEntry>();
        List<NativePartEntry> sorted=new List<NativePartEntry>();
        List<NativePartEntry> merge;
        Phase phase;
        int index,width=1,mergeStart,left,middle,right,i,j,k,sortBufferIndex;
        bool merging;
        internal readonly int PresentationGeneration;
        internal bool Complete=>phase==Phase.Complete;
        internal IReadOnlyList<NativePartEntry> Result=>Complete?(IReadOnlyList<NativePartEntry>)sorted:null;
        internal List<NativePartEntry> TakeResult(){if(!Complete)return null;var result=sorted;sorted=null;return result;}
        internal int CandidateCount=>candidates.Count;
        internal int SectionCount{get;private set;}
        internal int ItemsVisited{get;private set;}
        internal double FilterMilliseconds{get;private set;}
        internal double EvaluationMilliseconds{get;private set;}
        internal double SortMilliseconds{get;private set;}
        internal int LastStepItems{get;private set;}

        internal NativePartQueryBuild(NativePartBrowser source,NativePartQuery current,NativePartSection currentSection,int currentCash,int generation)
        {
            model=source??throw new ArgumentNullException(nameof(source));
            query=(current??new NativePartQuery()).Copy();section=currentSection;cash=currentCash;PresentationGeneration=generation;
            phase=source.Entries.Count==0?Phase.Complete:Phase.Filter;
        }

        internal bool Advance(int itemCap,double softMilliseconds)
        {
            if(Complete)return true;if(itemCap<1)itemCap=1;
            long start=Stopwatch.GetTimestamp();int done=0;LastStepItems=0;
            while(!Complete&&done<itemCap&&(done==0||Elapsed(start)<softMilliseconds))
            {
                switch(phase)
                {
                    case Phase.Filter:
                    {
                        long itemStart=Stopwatch.GetTimestamp();
                        var entry=model.Entries[index++];
                        if(NativePartBrowser.Section(entry.Definition.category)==section){SectionCount++;if(query.Matches(entry,cash,false))candidates.Add(entry);}
                        FilterMilliseconds+=Elapsed(itemStart);ItemsVisited++;done++;LastStepItems++;
                        if(index>=model.Entries.Count){index=0;phase=query.RequiresChanges&&candidates.Count>0?Phase.Evaluate:Phase.FinalFilter;}
                        break;
                    }
                    case Phase.Evaluate:
                    {
                        long itemStart=Stopwatch.GetTimestamp();
                        var entry=candidates[index++];
                        if(!entry.Ready&&!model.Restore(entry))model.Evaluate(entry);
                        EvaluationMilliseconds+=Elapsed(itemStart);ItemsVisited++;done++;LastStepItems++;
                        if(index>=candidates.Count){index=0;phase=Phase.FinalFilter;}
                        break;
                    }
                    case Phase.FinalFilter:
                    {
                        if(index<candidates.Count)
                        {
                            long itemStart=Stopwatch.GetTimestamp();var entry=candidates[index++];
                            if(query.Matches(entry,cash))sorted.Add(entry);
                            FilterMilliseconds+=Elapsed(itemStart);ItemsVisited++;done++;LastStepItems++;
                        }
                        else
                        {
                            if(sorted.Count<2){phase=Phase.Complete;break;}
                            merge=new List<NativePartEntry>(sorted.Count);sortBufferIndex=0;
                            phase=Phase.SortBuffer;
                        }
                        break;
                    }
                    case Phase.SortBuffer:
                    {
                        merge.Add(null);sortBufferIndex++;done++;LastStepItems++;
                        if(sortBufferIndex>=sorted.Count){index=0;width=1;mergeStart=0;merging=false;phase=Phase.Sort;}
                        break;
                    }
                    case Phase.Sort:
                    {
                        long itemStart=Stopwatch.GetTimestamp();SortOne();SortMilliseconds+=Elapsed(itemStart);done++;LastStepItems++;
                        break;
                    }
                }
            }
            return Complete;
        }

        void SortOne()
        {
            if(!merging)
            {
                if(mergeStart>=sorted.Count)
                {
                    var swap=sorted;sorted=merge;merge=swap;width*=2;mergeStart=0;
                    if(width>=sorted.Count){phase=Phase.Complete;return;}
                }
                left=mergeStart;middle=Math.Min(left+width,sorted.Count);right=Math.Min(left+2*width,sorted.Count);
                i=left;j=middle;k=left;merging=true;
            }
            if(i<middle&&j<right)merge[k++]=query.Compare(sorted[i],sorted[j])<=0?sorted[i++]:sorted[j++];
            else if(i<middle)merge[k++]=sorted[i++];
            else if(j<right)merge[k++]=sorted[j++];
            if(i>=middle&&j>=right){mergeStart=right;merging=false;}
        }

        static double Elapsed(long start)=>(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
    }
}
