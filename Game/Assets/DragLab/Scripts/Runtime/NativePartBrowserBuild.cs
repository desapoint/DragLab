using System;
using System.Diagnostics;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal enum NativePartBrowserBuildState { Capturing, Preparing, Complete, Failed, Cancelled }

    // Empty-cart Parts preparation. All mutable model entries remain private until
    // the final same-turn typed read-set fence succeeds.
    internal sealed class NativePartBrowserBuild
    {
        readonly NativePartsBaseReadInputs initialInputs;
        readonly NativePartsBaseReadSet.CaptureSession capture;
        NativePartsBaseReadSet readSet;
        NativePartBrowser privateModel;
        NativePartBrowserBuildState state=NativePartBrowserBuildState.Capturing;
        internal NativePartBrowserBuildState State=>state;
        internal bool Complete=>state==NativePartBrowserBuildState.Complete;
        internal bool Failed=>state==NativePartBrowserBuildState.Failed||state==NativePartBrowserBuildState.Cancelled;
        internal string FailureReason{get;private set;}
        internal int ItemsPrepared{get;private set;}
        internal int PreparationItems{get;private set;}
        internal int CaptureItems=>capture.ItemsCaptured;
        internal int CaptureTotal=>capture.TotalItems;
        internal double CaptureMilliseconds=>capture.CaptureMilliseconds;
        internal double LargestCaptureStepMilliseconds=>capture.LargestCaptureStepMilliseconds;
        internal double ValidationMilliseconds{get;private set;}
        internal double LargestValidationMilliseconds{get;private set;}
        internal double PreparationMilliseconds{get;private set;}
        internal double LargestPreparationStepMilliseconds{get;private set;}
        internal double ModelSetupMilliseconds{get;private set;}
        internal double PublishValidationMilliseconds{get;private set;}
        internal NativePartBrowser Model=>Complete?privateModel:null;

        NativePartBrowserBuild(NativePartsBaseReadInputs inputs)
        {initialInputs=inputs;capture=NativePartsBaseReadSet.BeginCapture(inputs);}

        internal static NativePartBrowserBuild Begin(NativePartsBaseReadInputs inputs,NativePartPreviewCache cache=null)
        {
            if(inputs==null)throw new ArgumentNullException(nameof(inputs));
            NativePartsOwnerContext.AssertMainThread();
            if(inputs.Lines.Length!=0)throw new InvalidOperationException("Nonempty cart previews remain on the legacy path until typed PreviewReadOnly is integrated.");
            if(inputs.HasInvalidPriceFacts)throw new ArgumentException("Price facts must have unique nonempty source/asset keys.",nameof(inputs));
            return new NativePartBrowserBuild(inputs);
        }

        internal bool Advance(NativePartsBaseReadInputs freshInputs,int itemCap,double softMilliseconds)
        {
            NativePartsOwnerContext.AssertMainThread();
            if(state==NativePartBrowserBuildState.Complete||Failed)return Complete;
            if(freshInputs==null){Fail("Fresh typed input facts were not supplied.");return false;}
            if(freshInputs.HasInvalidPriceFacts){Fail("Fresh external price facts have duplicate or invalid source/asset keys.");return false;}
            try
            {
            if(state==NativePartBrowserBuildState.Capturing)
            {
                bool captured=capture.Advance(itemCap,softMilliseconds);
                if(capture.Failed){Fail(capture.FailureReason);return false;}
                if(!captured)return false;
                if(!capture.PriceFactsComplete){Fail(capture.PriceFactsFailure);return false;}
                long validateStart=Stopwatch.GetTimestamp();bool finished=capture.TryFinish(freshInputs,out readSet,out var reason);RecordValidation(validateStart,finished);
                if(!finished){Fail(reason);return false;}
                // This setup reads live data, so it follows a complete atomic fence.
                // Cache is intentionally omitted on this path until its typed-key
                // implementation is available; no legacy encoder is invoked.
                privateModel=new NativePartBrowser(initialInputs.Profile,initialInputs.Lines,initialInputs.Planned,initialInputs.Page,
                    stock=>Price("Used",stock?.Id),SalePrice,null,true,true,readSet.ReadyShopGroups,readSet.ReadyShopGroupById);
                ModelSetupMilliseconds=privateModel.ConstructorSetupMilliseconds;PreparationMilliseconds+=ModelSetupMilliseconds;LargestPreparationStepMilliseconds=Math.Max(LargestPreparationStepMilliseconds,ModelSetupMilliseconds);
                if(privateModel.PreparationUnavailable){Fail("Ready Shop product groups are not available; bounded catalog warmup is required.");return false;}
                state=NativePartBrowserBuildState.Preparing;
                long afterSetup=Stopwatch.GetTimestamp();bool stillCurrent=readSet.MatchesCurrent(freshInputs,out reason);RecordValidation(afterSetup,stillCurrent);
                if(!stillCurrent){Fail(reason);return false;}
            }
            if(state==NativePartBrowserBuildState.Preparing)
            {
                // One full current comparison directly precedes each synchronous
                // preparation chunk. The chunk does not yield or dispatch callbacks.
                long validateStart=Stopwatch.GetTimestamp();bool current=readSet.MatchesCurrent(freshInputs,out var reason);RecordValidation(validateStart,current);
                if(!current){Fail(reason);return false;}
                long prepareStart=Stopwatch.GetTimestamp();bool built=privateModel.AdvanceBuild(itemCap,softMilliseconds);double prep=ElapsedMs(prepareStart);
                PreparationMilliseconds+=prep;LargestPreparationStepMilliseconds=Math.Max(LargestPreparationStepMilliseconds,prep);ItemsPrepared=privateModel.Entries.Count;
                PreparationItems+=privateModel.LastAdvanceItems;
                if(privateModel.PreparationUnavailable){Fail("Ready Shop product groups are not available; bounded catalog warmup is required.");return false;}
                if(!built)return false;
                long publishStart=Stopwatch.GetTimestamp();bool publishCurrent=readSet.MatchesCurrent(freshInputs,out reason);PublishValidationMilliseconds=ElapsedMs(publishStart);RecordValidation(publishStart,publishCurrent);
                if(!publishCurrent){Fail(reason);return false;}
                state=NativePartBrowserBuildState.Complete;
            }
            return Complete;
            }
            catch(Exception error){Fail(error.Message);return false;}
        }

        int? Price(string source,string assetKey)
        {
            return initialInputs.TryPrice(source,assetKey,out var fact)?fact.Price:null;
        }
        int SalePrice(string id,bool unit)
        {
            var price=Price(unit?"OwnedUnit":"OwnedPart",id);
            if(!price.HasValue)throw new InvalidOperationException("The current sale valuation is unavailable.");
            return price.Value;
        }
        void RecordValidation(long start,bool ignored)=>RecordValidation(start);
        void RecordValidation(long start)
        {double value=ElapsedMs(start);ValidationMilliseconds+=value;LargestValidationMilliseconds=Math.Max(LargestValidationMilliseconds,value);}
        void Fail(string reason){state=NativePartBrowserBuildState.Failed;FailureReason=string.IsNullOrEmpty(reason)?"Parts preparation failed.":reason;privateModel?.AbandonBuild();privateModel=null;}
        internal void Cancel(string reason="Parts preparation was cancelled.")
        {if(state==NativePartBrowserBuildState.Complete||Failed)return;state=NativePartBrowserBuildState.Cancelled;FailureReason=reason;privateModel?.AbandonBuild();privateModel=null;}
        static double ElapsedMs(long start)=>(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
    }
}
