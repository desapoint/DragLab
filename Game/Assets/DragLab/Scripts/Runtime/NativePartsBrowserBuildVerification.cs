using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator WaitBoundedPartsReady(string label,float timeoutSeconds=90f)
        {
            string previous=FramePacingProbe.OverridePhase;float started=Time.realtimeSinceStartup;int firstFrame=Time.frameCount,frames=0;
            double price0=shell.PartPriceFactoryMilliseconds,query0=shell.PartQueryMilliseconds,sort0=shell.PartSortMilliseconds;
            int priceCalls0=shell.PartPriceFactoryCallCount;
            double windowCreate0=shell.PartWindowCreateMilliseconds,windowAttach0=shell.PartWindowAttachMilliseconds,validation0=shell.PartCurrentValidationMilliseconds;
            try
            {
                FramePacingProbe.OverridePhase="bounded-parts/"+label.Replace(' ','-')+"/prepare";
                while(shell.PartBrowserPreparationPending&&Time.realtimeSinceStartup-started<timeoutSeconds)
                {frames++;yield return null;}
            }
            finally{FramePacingProbe.OverridePhase=previous;}
            float elapsed=Time.realtimeSinceStartup-started;
            Check(!shell.PartBrowserPreparationPending&&shell.PartBrowserPreparationState=="Ready"&&
                shell.PartBrowser!=null&&shell.PartBrowserActionsEnabled,
                "Latest Parts model/query/window reaches a complete actionable state / "+label+" state="+
                shell.PartBrowserPreparationState+" entries="+(shell.PartBrowser?.Entries.Count??-1)+
                " cards="+shell.PartBrowserWindowCardCount);
            int section=Enumerable.Range(0,NativePartBrowser.SectionNames.Length)
                .First(i=>shell.Root.Q<Button>("partsTab"+i).ClassListContains("selected"));
            int sectionTotal=shell.PartBrowser.Entries.Count(e=>NativePartBrowser.Section(e.Definition.category)==(NativePartSection)section);
            var countParts=shell.Root.Q<Label>("partsCount").text.Split('/');
            Check(countParts.Length==2&&int.TryParse(countParts[0].Trim(),out _)&&
                int.TryParse(countParts[1].Trim(),out var displayedTotal)&&displayedTotal==sectionTotal&&
                (shell.PartBrowserWindowCardCount>0||shell.Root.Q<Label>("partsEmpty").text=="No matching parts"),
                "Published list restores numeric section count and the final empty message / "+label);
            report.AppendLine("PARTS_PHASE,case="+label+",frameRange="+firstFrame+".."+Time.frameCount+",frames="+frames+",elapsedMs="+(elapsed*1000f).ToString("F2")+
                ",dataGen="+shell.PartBrowserDataGeneration+",presentationGen="+shell.PartBrowserPresentationGeneration+
                ",windowGen="+shell.PartBrowserWindowGeneration+",entries="+(shell.PartBrowser?.Entries.Count??-1)+
                ",cards="+shell.PartBrowserWindowCardCount+",evaluations="+(shell.PartBrowser?.EvaluationCount??-1)+
                ",priceFactoryMs="+(shell.PartPriceFactoryMilliseconds-price0).ToString("F3")+
                ",priceCalls="+(shell.PartPriceFactoryCallCount-priceCalls0)+
                ",priceMaximumRunMs="+shell.PartPriceFactoryMaximumMilliseconds.ToString("F3")+
                ",priceMaximumCallsPerFrameRun="+shell.PartPriceFactoryMaximumCallsPerFrame+
                ",queryMs="+(shell.PartQueryMilliseconds-query0).ToString("F3")+
                ",sortMs="+(shell.PartSortMilliseconds-sort0).ToString("F3")+
                ",windowCreateMs="+(shell.PartWindowCreateMilliseconds-windowCreate0).ToString("F3")+
                ",windowAttachMs="+(shell.PartWindowAttachMilliseconds-windowAttach0).ToString("F3")+
                ",currentValidationMs="+(shell.PartCurrentValidationMilliseconds-validation0).ToString("F3")+
                ",actions="+shell.PartBrowserActionsEnabled);
        }

        IEnumerator WaitForPartsHeld(string label,float timeoutSeconds=90f)
        {
            string previous=FramePacingProbe.OverridePhase;float started=Time.realtimeSinceStartup;int frames=0;
            try
            {
                FramePacingProbe.OverridePhase="bounded-parts/"+label.Replace(' ','-')+"/prepare";
                while(!shell.PartBrowserStepHeld&&Time.realtimeSinceStartup-started<timeoutSeconds)
                {frames++;yield return null;}
            }
            finally{FramePacingProbe.OverridePhase=previous;}
            Check(shell.PartBrowserStepHeld&&shell.PartBrowserPreparationPending&&!shell.PartBrowserActionsEnabled,
                "Native UI holds after a real bounded work step / "+label+" state="+shell.PartBrowserPreparationState+
                " frames="+frames);
            report.AppendLine("PARTS_HELD,case="+label+",frames="+frames+",state="+shell.PartBrowserPreparationState+
                ",dataGen="+shell.PartBrowserDataGeneration+",presentationGen="+shell.PartBrowserPresentationGeneration+
                ",windowGen="+shell.PartBrowserWindowGeneration+",visibleCards="+shell.PartBrowserWindowCardCount);
        }

        IEnumerator WaitForAttachedWindowHold(string label,float timeoutSeconds=90f)
        {
            string previous=FramePacingProbe.OverridePhase;float started=Time.realtimeSinceStartup;int frames=0;
            try
            {
                FramePacingProbe.OverridePhase="bounded-parts/"+label.Replace(' ','-')+"/prepare";
                while((!shell.PartBrowserWindowLayoutPending||!shell.PartBrowserStepHeld)&&
                    Time.realtimeSinceStartup-started<timeoutSeconds){frames++;yield return null;}
            }
            finally{FramePacingProbe.OverridePhase=previous;}
            Check(shell.PartBrowserWindowLayoutPending&&shell.PartBrowserStepHeld&&
                shell.PartBrowserPreparationState=="WindowLayout"&&!shell.PartBrowserActionsEnabled&&
                shell.PartBrowserWindowLayoutAttachedCardCount>0,
                "Real attached card window is held privately before publication / "+label+" state="+
                shell.PartBrowserPreparationState+" frames="+frames+" geometry="+shell.PartWindowLayoutGeometryForChecks);
            report.AppendLine("PARTS_WINDOW_HELD,case="+label+",frames="+frames+",state="+
                shell.PartBrowserPreparationState+",layout="+shell.PartBrowserWindowLayoutGeneration+
                ",cards="+shell.PartBrowserWindowLayoutAttachedCardCount+",context="+shell.PartWindowLayoutContextForChecks);
        }

        IEnumerator AttachedPartWindowLifecycleChecks(WindowsKeyboardProbe input)
        {
            var scroll=shell.Root.Q<ScrollView>("partsScroll");var selected=game.Profile.SelectedCar;
            var visible=shell.Root.Q("partsGrid")?.Query<Button>(className:"part-card").ToList()
                .FirstOrDefault(BoundedPartCardHitTestable);
            if(scroll==null||selected==null||visible==null){Check(false,"Attached-window lifecycle fixture has a real current list, selected car and hit-testable card");yield break;}
            string entryId=visible.name.Substring("partCard:".Length);int dataBefore=shell.PartBrowserDataGeneration;
            int registeredBefore=shell.PartRegisteredSurfaceCountForChecks,oldCardCount=shell.PartBrowserWindowCardCount;
            int registeredPersistentSurfaces=registeredBefore-oldCardCount;double validationBefore=shell.PartCurrentValidationMilliseconds;
            float originalListScroll=scroll.scrollOffset.y;
            shell.HoldPartBrowserAfterWindowAttachForChecks();
            float maxListScroll=Mathf.Max(0,scroll.contentContainer.layout.height-scroll.contentViewport.layout.height);
            scroll.scrollOffset=new Vector2(0,Mathf.Min(maxListScroll,
                originalListScroll+Math.Max(180f,scroll.contentViewport.layout.height*.55f)));
            yield return WaitForAttachedWindowHold("same-revision fuel freshness");
            Action privateCardAction=shell.CapturePartCardActionForChecks(entryId);
            Check(privateCardAction==null&&shell.PartRegisteredSurfaceCountForChecks==registeredPersistentSurfaces&&
                shell.PartBrowserWindowLayoutValidationAttemptsForChecks==0,
                "Attached held window has no published action or registered surfaces before its fresh fence");
            double fuelBefore=selected.fuel.litres;long revisionBefore=game.Profile.revision;
            try
            {
                selected.fuel.litres=fuelBefore+.625;shell.ResumePartBrowserPreparation();
                float observeStart=Time.realtimeSinceStartup;
                while((shell.PartBrowserDataGeneration<=dataBefore||!shell.PartBrowserPreparationPending||
                    shell.PartBrowserActionsEnabled)&&Time.realtimeSinceStartup-observeStart<30f)yield return null;
                bool observedB=shell.PartBrowserDataGeneration>dataBefore&&shell.PartBrowserPreparationPending&&
                    !shell.PartBrowserActionsEnabled&&game.Profile.revision==revisionBefore;
                Check(observedB,"Attached-window final fence rejects same-revision fuel B before any card is published / "+
                    "state="+shell.PartBrowserPreparationState+" geometry="+shell.PartWindowLayoutGeometryForChecks);
                Check(shell.PartCurrentValidationMilliseconds>validationBefore,
                    "Attached-window publication performs a fresh current-input fence after attachment");
                if(observedB)selected.fuel.litres=fuelBefore;
            }
            finally{selected.fuel.litres=fuelBefore;shell.ResumePartBrowserPreparation();}
            yield return WaitBoundedPartsReady("fresh A after attached-window fuel rejection");

            // A viewport and scroll change while the actual attached tree is held
            // must supersede that ticket and publish only the newly calculated range.
            int originalWidth=Screen.width,originalHeight=Screen.height;bool originalFullscreen=Screen.fullScreen;
            int firstWindow=shell.PartBrowserWindowGeneration;double oldScroll=scroll.scrollOffset.y;
            try
            {
                shell.HoldPartBrowserAfterWindowAttachForChecks();
                Screen.SetResolution(1024,768,false);
                float resizeStart=Time.realtimeSinceStartup;
                while((Screen.width!=1024||Screen.height!=768)&&Time.realtimeSinceStartup-resizeStart<8f)yield return null;
                Check(Screen.width==1024&&Screen.height==768,"Attached-window geometry probe reaches its requested live viewport");
                yield return WaitForAttachedWindowHold("scroll and resize supersession");
                int heldWindow=shell.PartBrowserWindowLayoutGeneration;
                scroll.scrollOffset=new Vector2(0,Mathf.Max(0,scroll.contentContainer.layout.height-scroll.contentViewport.layout.height));
                shell.ResumePartBrowserPreparation();
                float supersedeStart=Time.realtimeSinceStartup;
                while(shell.PartBrowserWindowGeneration<=heldWindow&&Time.realtimeSinceStartup-supersedeStart<30f)yield return null;
                Check(shell.PartBrowserWindowGeneration>heldWindow&&!shell.PartBrowserActionsEnabled,
                    "Scroll and resize supersede the held attached ticket before publication / "+shell.PartWindowLayoutContextForChecks);
                yield return WaitBoundedPartsReady("latest attached-window scroll and resize ticket");
                string acceptedContext=shell.PartWindowContextForChecks;
                Check(shell.PartBrowserWindowGeneration>firstWindow&&shell.PartBrowserWindowGeometryCurrentForChecks&&
                    Math.Abs(scroll.scrollOffset.y-oldScroll)>1f,
                    "Only the newest attached window publishes using the resized viewport and current scroll / "+acceptedContext);
                report.AppendLine("PARTS_WINDOW_SUPERSEDE,held="+heldWindow+",published="+shell.PartBrowserWindowGeneration+
                    ",context="+acceptedContext+",geometry="+shell.PartWindowLayoutGeometryForChecks);
            }
            finally
            {
                shell.ResumePartBrowserPreparation();Screen.SetResolution(originalWidth,originalHeight,originalFullscreen);
            }
            float restoreStart=Time.realtimeSinceStartup;
            while((Screen.width!=originalWidth||Screen.height!=originalHeight)&&Time.realtimeSinceStartup-restoreStart<8f)yield return null;
            yield return WaitBoundedPartsReady("restore attached-window viewport");

            // Opening the real Sort dialog while attachment is pending must cancel
            // the private tree; closing it starts a new current window ticket.
            registeredBefore=shell.PartRegisteredSurfaceCountForChecks;oldCardCount=shell.PartBrowserWindowCardCount;
            registeredPersistentSurfaces=registeredBefore-oldCardCount;
            shell.HoldPartBrowserAfterWindowAttachForChecks();scroll.scrollOffset=Vector2.zero;
            yield return WaitForAttachedWindowHold("modal cancellation");
            var countCancelledWindowSurfaces=shell.CaptureAttachedPartWindowSurfaceRegistrationForChecks();
            int pendingCardsBeforeModal=shell.PartBrowserWindowLayoutAttachedCardCount;
            Check(pendingCardsBeforeModal>0&&countCancelledWindowSurfaces()==0,
                "Exact pending card surfaces are captured and remain unpublished before opening the modal");
            yield return Click(input,"partsSort");
            bool modalCancelled=shell.ModalOpen&&!shell.PartBrowserActionsEnabled&&
                !shell.PartBrowserWindowLayoutPending&&shell.CapturePartCardActionForChecks(entryId)==null&&
                shell.PartBrowserWindowCardCount==0&&countCancelledWindowSurfaces()==0;
            Check(modalCancelled,"Real modal opening cancels unpublished attachment and leaves no registered stale surfaces / "+
                shell.PartBrowserPreparationState+" pendingCardSurfaces="+countCancelledWindowSurfaces()+
                " globalSurfaces="+shell.PartRegisteredSurfaceCountForChecks+" "+shell.PartWindowContextForChecks);
            if(shell.ModalOpen)yield return Click(input,"dialogCancel");
            shell.ResumePartBrowserPreparation();yield return WaitBoundedPartsReady("fresh window after modal cancellation");

            // Synthetic UI-layout fault injection: deliberately hide the actual
            // viewport. This proves the terminal bound for this injected fault,
            // not that a natural user interaction produces invalid geometry.
            var viewport=scroll.contentViewport;var priorDisplay=viewport.style.display;
            shell.HoldPartBrowserAfterWindowAttachForChecks();
            scroll.scrollOffset=new Vector2(0,Mathf.Max(0,scroll.contentContainer.layout.height-scroll.contentViewport.layout.height));
            yield return WaitForAttachedWindowHold("synthetic invalid geometry terminal bound");
            try
            {
                viewport.style.display=DisplayStyle.None;yield return new WaitForEndOfFrame();yield return null;
                Check(shell.PartWindowLayoutGeometryForChecks.IndexOf("valid=False",StringComparison.Ordinal)>=0,
                    "Synthetic viewport-hide fault exposes invalid actual geometry / "+shell.PartWindowLayoutGeometryForChecks);
                shell.ResumePartBrowserPreparation();float failStart=Time.realtimeSinceStartup;
                while(shell.PartBrowserPreparationState!="Failed"&&Time.realtimeSinceStartup-failStart<15f)yield return null;
                Check(shell.PartBrowserPreparationState=="Failed"&&!shell.PartBrowserActionsEnabled&&
                    shell.PartBrowserWindowLayoutValidationAttemptsForChecks==4&&
                    shell.PartBrowserPreparationFailureForChecks=="Parts card layout did not settle after 4 rendered checks.",
                    "Synthetic invalid geometry reaches its strict four-rendered-check terminal failure / attempts="+
                    shell.PartBrowserWindowLayoutValidationAttemptsForChecks+" failure="+shell.PartBrowserPreparationFailureForChecks);
                report.AppendLine("PARTS_WINDOW_GEOMETRY_FAILURE,evidence=synthetic-ui-layout-fault,attempts="+
                    shell.PartBrowserWindowLayoutValidationAttemptsForChecks+",failure="+
                    shell.PartBrowserPreparationFailureForChecks+",geometry="+shell.PartWindowLayoutGeometryForChecks);
            }
            finally{viewport.style.display=priorDisplay;shell.ResumePartBrowserPreparation();}
            shell.RetryPartBrowserPreparation();yield return WaitBoundedPartsReady("explicit recovery after geometry failure");
            report.AppendLine("PARTS_WINDOW_NATURAL_INVALID_GEOMETRY,status=OPEN,reason=no-natural-user-path-reproduction");
            yield return ShortSingleRowCurrentCallbackChecks(input);
        }

        IEnumerator ShortSingleRowCurrentCallbackChecks(WindowsKeyboardProbe input)
        {
            var entries=shell.PartBrowser?.Entries;NativePartEntry candidate=null;string search=null;
            var visibleButtons=shell.Root.Q("partsGrid")?.Query<Button>(className:"part-card").ToList()??new List<Button>();
            var visibleIds=new HashSet<string>(visibleButtons.Select(button=>button.name.Substring("partCard:".Length)),StringComparer.Ordinal);
            int originalSection=Enumerable.Range(0,NativePartBrowser.SectionNames.Length)
                .First(i=>shell.Root.Q<Button>("partsTab"+i).ClassListContains("selected"));
            string originalSearch=shell.PartQuery.Search;
            foreach(var entry in entries??new List<NativePartEntry>())
            {
                if(!visibleIds.Contains(entry.Id)||string.IsNullOrEmpty(entry.AssetId))continue;
                var probe=shell.PartQuery.Copy();probe.Search=entry.AssetId;
                int matches=entries.Count(other=>(int)NativePartBrowser.Section(other.Definition.category)==originalSection&&
                    probe.Matches(other,game.Profile.cash));
                if(matches==1&&probe.Matches(entry,game.Profile.cash)){candidate=entry;search=entry.AssetId;break;}
            }
            if(candidate==null||string.IsNullOrEmpty(search))
            {Check(false,"Short one-row query fixture has one exact real asset match under the current filters");yield break;}

            // A full asset ID can contain punctuation unsupported by the native
            // typing helper. The public field setter raises the same production
            // ValueChanged/ApplyPartQuery path; readiness is never set by the test.
            shell.Root.Q<TextField>("partsSearch").value=search;
            yield return WaitBoundedPartsReady("one-row query post-layout");
            string emptyText=shell.Root.Q<Label>("partsEmpty").text;
            bool emptyHidden=shell.Root.Q<Label>("partsEmpty").ClassListContains("hidden");
            var card=shell.Root.Q("partsGrid")?.Query<Button>(className:"part-card").ToList()
                .FirstOrDefault(button=>button.name=="partCard:"+candidate.Id);
            Action actual=shell.CapturePartCardActionForChecks(candidate.Id);
            Check(shell.PartBrowserWindowCardCount==1&&card!=null&&card.enabledInHierarchy&&actual!=null&&
                shell.PartBrowserActionsEnabled&&shell.PartBrowserWindowGeometryCurrentForChecks&&emptyHidden,
                "Short one-row query completes final layout, retains its empty-label state, and exposes one real strict card callback / "+
                "cards="+shell.PartBrowserWindowCardCount+" label="+emptyHidden+":"+emptyText+" candidate="+candidate.Id);
            if(actual!=null)
            {
                actual();yield return WaitBoundedDetailReady("one-row current strict callback",candidate.Id);
                Check(shell.PartBrowserDetailId==candidate.Id&&shell.Root.Q<Label>("partsEmpty").text==emptyText&&
                    shell.Root.Q<Label>("partsEmpty").ClassListContains("hidden")==emptyHidden,
                    "Current one-row card callback opens its real detail without changing the final empty-label state");
                yield return Click(input,"partBack");yield return WaitBoundedPartsReady("one-row callback list return");
            }
            // An empty query has no card callback to invoke. Its final label
            // must already be in flow before the accepted geometry is measured.
            const string emptySearch="__draglab_no_part_match__";
            var emptyProbe=shell.PartQuery.Copy();emptyProbe.Search=emptySearch;
            Check(!entries.Any(entry=>(int)NativePartBrowser.Section(entry.Definition.category)==originalSection&&
                emptyProbe.Matches(entry,game.Profile.cash)),"Empty-result query is verified against actual catalogue and current filters");
            shell.Root.Q<TextField>("partsSearch").value=emptySearch;
            float emptyStarted=Time.realtimeSinceStartup;
            while((shell.PartBrowserPreparationPending||!shell.PartBrowserActionsEnabled)&&
                Time.realtimeSinceStartup-emptyStarted<90f)yield return null;
            var emptyLabel=shell.Root.Q<Label>("partsEmpty");
            Check(!shell.PartBrowserPreparationPending&&shell.PartBrowserPreparationState=="Ready"&&
                shell.PartBrowserActionsEnabled&&shell.PartBrowserWindowCardCount==0&&
                shell.PartBrowserWindowGeometryCurrentForChecks&&!emptyLabel.ClassListContains("hidden")&&
                emptyLabel.text=="No matching parts",
                "Zero-result window accepts current geometry with its final visible empty label and no card callbacks");
            report.AppendLine("PARTS_EMPTY_QUERY,cards=0,geometryCurrent="+shell.PartBrowserWindowGeometryCurrentForChecks+
                ",label="+emptyLabel.text+",context="+shell.PartWindowContextForChecks);
            if(shell.PartQuery.Search!=originalSearch)
            {yield return TypePartsSearch(input,originalSearch);yield return WaitBoundedPartsReady("restore search after one-row query");}
            Check(shell.PartQuery.Search==originalSearch&&shell.PartBrowserWindowGeometryCurrentForChecks,
                "Short one-row query restores the previous list and its current accepted geometry");
            report.AppendLine("PARTS_SHORT_QUERY,assetSearch="+search+",entry="+candidate.Id+",rows="+shell.PartBrowserWindowCardCount+
                ",windowGeometryCurrent="+shell.PartBrowserWindowGeometryCurrentForChecks+",restored=true");
        }

        IEnumerator WaitBoundedDetailReady(string label,string entryId,float timeoutSeconds=90f)
        {
            string previous=FramePacingProbe.OverridePhase;float started=Time.realtimeSinceStartup;int firstFrame=Time.frameCount,frames=0;
            double detail0=shell.PartDetailBuildMilliseconds,openReady0=shell.PartDetailOpenToReadyMilliseconds,
                actions0=shell.PartDetailActionsMilliseconds,attach0=shell.PartDetailAttachMilliseconds,
                publish0=shell.PartDetailPublishMilliseconds,layoutWait0=shell.PartDetailLayoutWaitMilliseconds,
                validation0=shell.PartCurrentValidationMilliseconds;
            int layoutFrames0=shell.PartDetailLayoutWaitFrames;
            try
            {
                FramePacingProbe.OverridePhase="bounded-parts/"+label.Replace(' ','-')+"/prepare";
                while((shell.PartBrowserPreparationPending||!shell.PartBrowserDetailCurrent||
                    shell.PartBrowserDetailId!=entryId)&&Time.realtimeSinceStartup-started<timeoutSeconds)
                {frames++;yield return null;}
            }
            finally{FramePacingProbe.OverridePhase=previous;}
            float elapsed=Time.realtimeSinceStartup-started;
            var detail=shell.Root.Q("partsDetail");var add=shell.Root.Q<Button>("partAdd");
            var dyno=shell.Root.Q("partDetailDyno");var stats=shell.Root.Q("partStats");var specific=shell.Root.Q("partSpecificStats");
            bool hasCurrentAction=new[]{"partAdd","partPlanInstall","partPlanSale","partMove","partPosition"}
                .Select(id=>shell.Root.Q<Button>(id)).Any(button=>button!=null&&button.enabledInHierarchy);
            Check(!shell.PartBrowserPreparationPending&&shell.PartBrowserDetailCurrent&&
                shell.PartBrowserDetailId==entryId&&shell.PartBrowserDetailDataGeneration==shell.PartBrowserDataGeneration&&
                shell.PartBrowserDetailPresentationGeneration==shell.PartBrowserPresentationGeneration&&
                detail!=null&&!detail.ClassListContains("hidden")&&hasCurrentAction&&
                dyno!=null&&stats!=null&&specific!=null,
                "Open detail is current and actionable after bounded rebuild / "+label+" id="+shell.PartBrowserDetailId+
                " detailData="+shell.PartBrowserDetailDataGeneration+" data="+shell.PartBrowserDataGeneration+
                " detailPresentation="+shell.PartBrowserDetailPresentationGeneration+" presentation="+shell.PartBrowserPresentationGeneration+
                " pending="+shell.PartBrowserPreparationPending+" state="+shell.PartBrowserPreparationState+
                " add="+(add!=null&&add.enabledInHierarchy)+" currentAction="+hasCurrentAction+
                " dyno="+(dyno!=null)+" stats="+(stats!=null&&specific!=null));
            var detailBar=shell.Root.Q<ScrollView>("partsDetailScroll").verticalScroller;
            Check(detailBar.ClassListContains("settings-scrollbar")&&detailBar.slider.ClassListContains("settings-scrollbar-slider")&&
                Mathf.Abs(detailBar.resolvedStyle.width-8)<.1f&&detailBar.lowButton.resolvedStyle.display==DisplayStyle.None&&
                detailBar.highButton.resolvedStyle.display==DisplayStyle.None,
                "Late-created detail reuses the eight-point Settings scrollbar without stock arrows / "+label+
                " width="+detailBar.resolvedStyle.width);
            report.AppendLine("PARTS_DETAIL_PHASE,case="+label+",frameRange="+firstFrame+".."+Time.frameCount+
                ",frames="+frames+",elapsedMs="+(elapsed*1000f).ToString("F2")+
                ",dataGen="+shell.PartBrowserDataGeneration+",presentationGen="+shell.PartBrowserPresentationGeneration+
                ",detailDataGen="+shell.PartBrowserDetailDataGeneration+",detailPresentationGen="+shell.PartBrowserDetailPresentationGeneration+
                ",buildCpuMs="+(shell.PartDetailBuildMilliseconds-detail0).ToString("F3")+
                ",openToReadyMs="+(shell.PartDetailOpenToReadyMilliseconds-openReady0).ToString("F3")+
                ",actionsMs="+(shell.PartDetailActionsMilliseconds-actions0).ToString("F3")+
                ",attachMs="+(shell.PartDetailAttachMilliseconds-attach0).ToString("F3")+
                ",publishMs="+(shell.PartDetailPublishMilliseconds-publish0).ToString("F3")+
                ",layoutWaitMs="+(shell.PartDetailLayoutWaitMilliseconds-layoutWait0).ToString("F3")+
                ",layoutWaitFrames="+(shell.PartDetailLayoutWaitFrames-layoutFrames0)+
                ",validationMs="+(shell.PartCurrentValidationMilliseconds-validation0).ToString("F3")+
                ",dynoChildren="+(dyno?.childCount??-1)+",statsChildren="+(stats?.childCount??-1)+
                ",specificChildren="+(specific?.childCount??-1)+",actions="+hasCurrentAction);
        }

        IEnumerator WaitPartDetailHeld(string label,string entryId,int expectedPhase,float timeoutSeconds=90f)
        {
            string previous=FramePacingProbe.OverridePhase;float started=Time.realtimeSinceStartup;int frames=0;
            try
            {
                FramePacingProbe.OverridePhase="bounded-parts/"+label.Replace(' ','-')+"/prepare";
                while(!shell.PartBrowserStepHeld&&Time.realtimeSinceStartup-started<timeoutSeconds)
                {frames++;yield return null;}
            }
            finally{FramePacingProbe.OverridePhase=previous;}
            var pending=shell.Root.Q<VisualElement>("partsDetailPending");
            bool attachedHidden=expectedPhase==6&&pending!=null&&pending.parent!=null&&
                pending.style.visibility==Visibility.Hidden&&!pending.enabledInHierarchy;
            bool unpublished=expectedPhase<6&&pending==null;
            // Old CSS-hidden detail buttons can retain enabledSelf. They are not
            // controls published by this private ticket. Check its own root and
            // the current callback seam instead of querying old hidden nodes.
            bool noEnabledDetailActions=shell.CapturePartDetailActionForChecks()==null&&
                (expectedPhase<6||pending!=null&&pending.Query<Button>().ToList().All(button=>!button.enabledInHierarchy));
            Check(shell.PartBrowserStepHeld&&shell.PartBrowserPreparationPending&&
                shell.PartBrowserDetailId==entryId&&shell.PartBrowserDetailPhase==expectedPhase&&
                !shell.PartBrowserDetailCurrent&&noEnabledDetailActions&&(attachedHidden||unpublished),
                "Detail phase is held privately and cannot act or publish / "+label+" phase="+
                shell.PartBrowserDetailPhase+" expected="+expectedPhase+" pendingRoot="+(pending!=null)+
                " attachedHidden="+attachedHidden+" actionsDisabled="+noEnabledDetailActions+" frames="+frames);
            report.AppendLine("PARTS_DETAIL_HELD,case="+label+",phase="+shell.PartBrowserDetailPhase+
                ",frames="+frames+",generation="+shell.PartBrowserDetailGeneration+
                ",detail="+shell.PartBrowserDetailId+",current="+shell.PartBrowserDetailCurrent+
                ",actionsDisabled="+noEnabledDetailActions+
                ",pendingRoot="+(pending!=null)+",attachedHidden="+attachedHidden);
        }

        IEnumerator DetailPhaseLifecycleChecks(WindowsKeyboardProbe input,string[] entryIds)
        {
            if(entryIds==null||entryIds.Length<2){Check(false,"Two actual visible card callbacks are available for detail phase lifecycle coverage");yield break;}
            // Drive every real production phase under the one-step hold. A
            // detached tree stays unpublished through phase 5; phase 6 is
            // attached hidden and disabled until its layout-ready frame.
            string id=entryIds[0];var start=shell.CapturePartCardActionForChecks(id);
            int generation=shell.PartBrowserDetailGeneration;
            int registered=shell.PartRegisteredSurfaceCountForChecks;
            var publishedCallback=shell.CapturePartDetailActionForChecks("partAdd");
            Check(start!=null,"Current visible card has its actual guarded detail-opening callback");
            int rejectedBefore=shell.PartActionGuardRejectedCount;
            report.AppendLine("DETAIL_TICKET_REQUEST_BEFORE,id="+id+",detail="+shell.PartBrowserDetailId+",phase="+shell.PartBrowserDetailPhase+
                ",data="+shell.PartBrowserDataGeneration+",presentation="+shell.PartBrowserPresentationGeneration+",window="+shell.PartBrowserWindowGeneration+
                ",actions="+shell.PartBrowserActionsEnabled+",state="+shell.PartBrowserPreparationState+",cart="+game.CartCount+
                ",screen="+game.ScreenName+",page="+game.PartsPage+",visible="+shell.Visible+","+shell.PartWindowContextForChecks);
            shell.HoldPartBrowserAfterNextStep();start?.Invoke();
            report.AppendLine("DETAIL_TICKET_REQUEST_AFTER,id="+id+",detail="+shell.PartBrowserDetailId+",phase="+shell.PartBrowserDetailPhase+
                ",pending="+shell.PartBrowserPreparationPending+",current="+shell.PartBrowserDetailCurrent+",rejected="+(shell.PartActionGuardRejectedCount-rejectedBefore)+
                ",reason="+shell.PartActionLastRejectedReasonForChecks+",data="+shell.PartBrowserDataGeneration+
                ",presentation="+shell.PartBrowserPresentationGeneration+",window="+shell.PartBrowserWindowGeneration+","+shell.PartWindowContextForChecks);
            Check(shell.PartBrowserDetailId==id&&shell.PartBrowserDetailPhase==0&&
                shell.PartBrowserPreparationPending&&!shell.PartBrowserDetailCurrent,
                "Real captured card callback creates an unpublished phase-0 detail ticket in the same owner-thread stack");
            Check(shell.PartRegisteredSurfaceCountForChecks==registered&&shell.CapturePartDetailActionForChecks()==null&&
                ReferenceEquals(shell.CapturePartDetailActionForChecks("partAdd"),publishedCallback),
                "Phase 0 exposes no new registered surfaces or published callbacks");
            for(int phase=1;phase<=6;phase++)
            {
                if(phase>1){shell.ResumePartBrowserPreparation();shell.HoldPartBrowserAfterNextStep();yield return null;}
                yield return WaitPartDetailHeld("detail phase "+phase,id,phase);
                Check(shell.PartRegisteredSurfaceCountForChecks==registered&&shell.CapturePartDetailActionForChecks()==null&&
                    ReferenceEquals(shell.CapturePartDetailActionForChecks("partAdd"),publishedCallback),
                    "Private surfaces and callbacks remain unregistered through held detail phase "+phase);
            }
            shell.ResumePartBrowserPreparation();
            yield return WaitBoundedDetailReady("detail phase publish",id);
            Check(shell.PartBrowserDetailGeneration==generation+1&&shell.PartBrowserDetailPhase==-1,
                "Only the held detail ticket publishes and releases its phase state");
            yield return Click(input,"partBack");
            yield return WaitBoundedPartsReady("list after detail phase lifecycle");

            // Supersede a real ticket at every phase boundary, including the
            // hidden attachment. Only its replacement may publish.
            string older=entryIds[0],newest=entryIds[1];
            for(int cancelPhase=0;cancelPhase<=6;cancelPhase++)
            {
            Vector2 returnScroll=shell.Root.Q<ScrollView>("partsScroll").scrollOffset;
            registered=shell.PartRegisteredSurfaceCountForChecks;
            publishedCallback=shell.CapturePartDetailActionForChecks("partAdd");
            var oldAction=shell.CapturePartCardActionForChecks(older);
            Check(oldAction!=null,"Actual older-card callback is available at cancellation phase "+cancelPhase);
            shell.HoldPartBrowserAfterNextStep();oldAction?.Invoke();
            Check(shell.PartBrowserDetailId==older&&shell.PartBrowserDetailPhase==0&&
                shell.PartBrowserPreparationPending&&!shell.PartBrowserDetailCurrent,
                "Older detail begins at phase 0 before any frame can advance it");
            for(int phase=1;phase<=cancelPhase;phase++)
            {
                if(phase>1){shell.ResumePartBrowserPreparation();shell.HoldPartBrowserAfterNextStep();yield return null;}
                yield return WaitPartDetailHeld("older detail before replacement "+cancelPhase,older,phase);
            }
            int oldGeneration=shell.PartBrowserDetailGeneration;
            var newestAction=shell.CapturePartCardActionForChecks(newest);
            Check(newestAction!=null,"A second actual card callback remains available while detail work is pending");
            newestAction?.Invoke();
            int newestGeneration=shell.PartBrowserDetailGeneration;
            Check(newestGeneration>oldGeneration&&shell.PartBrowserDetailId==newest&&
                shell.PartBrowserDetailPhase==0&&!shell.PartBrowserDetailCurrent,
                "New detail ticket cancels the held older ticket and owns the pending identity");
            Check(shell.PartRegisteredSurfaceCountForChecks==registered&&shell.Root.Q("partsDetailPending")==null&&
                shell.CapturePartDetailActionForChecks()==null&&ReferenceEquals(shell.CapturePartDetailActionForChecks("partAdd"),publishedCallback),
                "Cancellation releases the old private attachment without publishing surfaces or callbacks at phase "+cancelPhase);
            shell.ResumePartBrowserPreparation();
            yield return WaitBoundedDetailReady("newest detail ticket wins",newest);
            Check(shell.PartBrowserDetailGeneration==newestGeneration&&shell.PartBrowserDetailId==newest,
                "Canceled older detail ticket cannot publish over the newest request");
            yield return Click(input,"partBack");
            yield return WaitBoundedPartsReady("list after newest detail ticket");
            Check(Vector2.Distance(shell.Root.Q<ScrollView>("partsScroll").scrollOffset,returnScroll)<.1f,
                "Successful phased detail and Back preserve the accepted list offset at boundary "+cancelPhase);
            }
        }

        IEnumerator NaturalWarningRetentionChecks(WindowsKeyboardProbe input)
        {
            string returnPage=game.PartsPage;
            if(game.PartsPage!="Shop")yield return Click(input,"partsSource:Shop");
            yield return WaitBoundedPartsReady("Shop before natural warning discovery");
            if(shell.PartQuery.Search!="")
            {yield return TypePartsSearch(input,"");yield return WaitBoundedPartsReady("clear search before natural warning discovery");}

            NativePartEntry candidate=null;int inspected=0;float discoveryScanMs=0;
            string discoveryPhase=FramePacingProbe.OverridePhase;
            var availableShop=shell.PartBrowser.Entries.Where(entry=>(entry.Source=="New"||entry.Source=="Used")&&
                entry.StockUnits>0&&entry.Definition!=null).ToList();
            var shopCandidates=new List<NativePartEntry>();
            foreach(var category in new[]{PartCategory.Induction,PartCategory.Pistons,PartCategory.Ignition,
                PartCategory.Gearbox,PartCategory.Clutch})
            {
                var categoryRows=availableShop.Where(entry=>entry.Definition.category==category)
                    .OrderBy(entry=>entry.Source=="Used"?0:1)
                    .ThenByDescending(entry=>entry.Definition.name.IndexOf("twin",StringComparison.OrdinalIgnoreCase)>=0||
                        entry.Definition.name.IndexOf("turbo",StringComparison.OrdinalIgnoreCase)>=0)
                    .ThenByDescending(entry=>entry.Price??entry.Definition.price).Take(category==PartCategory.Induction?10:5);
                shopCandidates.AddRange(categoryRows);
            }
            shopCandidates.AddRange(availableShop.Where(entry=>entry.Source=="Used")
                .OrderByDescending(entry=>entry.Price??0).Take(10));
            shopCandidates.AddRange(availableShop.OrderByDescending(entry=>entry.Price??entry.Definition.price)
                .Take(Math.Max(0,48-shopCandidates.Count)));
            shopCandidates=shopCandidates.Distinct().Take(48).ToList();
            try
            {
                FramePacingProbe.OverridePhase="test-fixture/parts-warning-discovery";
                float scanStarted=Time.realtimeSinceStartup;
                foreach(var entry in shopCandidates)
                {
                    inspected++;shell.PartBrowser.Evaluate(entry);
                    if(entry.Compatibility=="Compatible"&&entry.Proposed!=null&&entry.Proposed.warnings.Count>0&&
                        !string.IsNullOrWhiteSpace(entry.Reason))
                    {candidate=entry;break;}
                }
                discoveryScanMs+=(Time.realtimeSinceStartup-scanStarted)*1000f;
            }
            finally{FramePacingProbe.OverridePhase=discoveryPhase;}

            // If the live Shop does not naturally produce a warning, inspect
            // current fitted entries under the same real evaluator. This never
            // edits condition, wear, faults, or authored definitions.
            if(candidate==null)
            {
                if(game.PartsPage!="Installed")yield return Click(input,"partsSource:Installed");
                yield return WaitBoundedPartsReady("Installed before natural warning discovery");
                discoveryPhase=FramePacingProbe.OverridePhase;
                try
                {
                    FramePacingProbe.OverridePhase="test-fixture/parts-warning-discovery";
                    float scanStarted=Time.realtimeSinceStartup;
                    foreach(var entry in shell.PartBrowser.Entries.Where(entry=>entry.Source=="Installed"&&entry.Definition!=null)
                        .OrderBy(entry=>entry.Definition.category==PartCategory.Induction||
                    entry.Definition.category==PartCategory.Gearbox||entry.Definition.category==PartCategory.Clutch?0:1)
                        .Take(48))
                    {
                        inspected++;shell.PartBrowser.Evaluate(entry);
                        if(entry.Compatibility=="Compatible"&&entry.Proposed!=null&&entry.Proposed.warnings.Count>0&&
                            !string.IsNullOrWhiteSpace(entry.Reason))
                        {candidate=entry;break;}
                    }
                    discoveryScanMs+=(Time.realtimeSinceStartup-scanStarted)*1000f;
                }
                finally{FramePacingProbe.OverridePhase=discoveryPhase;}
            }
            report.AppendLine("PARTS_WARNING_DISCOVERY,scope=bounded-current-market-and-fitted-entries,inspected="+inspected+
                ",found="+(candidate!=null)+",source="+(candidate?.Source??"none")+
                ",category="+(candidate?.Definition?.category.ToString()??"none")+
                ",testFixtureScanMs="+discoveryScanMs.ToString("F2"));
            if(candidate==null)
            {
                report.AppendLine("PARTS_WARNING_RETENTION,STATUS=OPEN,reason=no-natural-warning-found-in-up-to-48-diverse-Shop-and-48-Installed-entries");
                if(game.PartsPage!=returnPage)yield return Click(input,"partsSource:"+returnPage);
                yield return WaitBoundedPartsReady("restore Parts page after warning discovery");
                yield break;
            }

            int section=(int)NativePartBrowser.Section(candidate.Definition.category);
            var sectionButton=shell.Root.Q<Button>("partsTab"+section);
            if(sectionButton!=null&&!sectionButton.ClassListContains("selected"))yield return Click(input,sectionButton);
            yield return WaitBoundedPartsReady("warning candidate section");
            var card=shell.ScrollToPart(candidate.Id);
            yield return WaitBoundedPartsReady("warning candidate visible window");
            card=BoundedPartCard("partCard:"+candidate.Id);
            Check(card!=null&&BoundedPartCardHitTestable(card),
                "Naturally warned candidate is an actual visible hit-testable card / "+candidate.Id);
            if(card==null||!BoundedPartCardHitTestable(card))
            {
                if(game.PartsPage!=returnPage)yield return Click(input,"partsSource:"+returnPage);
                yield return WaitBoundedPartsReady("restore Parts page after warning card miss");
                yield break;
            }
            yield return Click(input,card);
            yield return WaitBoundedDetailReady("natural warning detail",candidate.Id);
            var warningButton=shell.Root.Q<Button>("partWarnings");
            Check(warningButton!=null&&warningButton.enabledInHierarchy,
                "Real evaluated detail exposes its authored warning action / "+candidate.Id);
            if(warningButton==null||!warningButton.enabledInHierarchy)
            {
                yield return Click(input,"partBack");yield return WaitBoundedPartsReady("restore list after warning action miss");
                if(game.PartsPage!=returnPage)yield return Click(input,"partsSource:"+returnPage);
                yield return WaitBoundedPartsReady("restore Parts page after warning action miss");
                yield break;
            }
            string warningShape=PartWarningButtonShape();
            yield return Click(input,"partWarnings");
            string warningPayload=PartWarningDialogText();
            Check(shell.ModalOpen&&!string.IsNullOrWhiteSpace(warningPayload),
                "Real warning action opens its nonempty authored dialog payload");
            if(shell.ModalOpen)yield return Click(input,"dialogClose");
            report.AppendLine("PARTS_WARNING_RETENTION,STATUS=EXERCISED,candidate="+candidate.Id+
                ",source="+candidate.Source+",category="+candidate.Definition.category+
                ",level="+(warningShape.Contains("critical=True")?"critical":"warning"));
            var selected=game.Profile.SelectedCar;double fuel=selected.fuel.litres;
            yield return RejectEditedDetailAndRebuild(input,"natural-warning-stale-failure-retry",candidate.Id,
                ()=>selected.fuel.litres=fuel+.625,()=>selected.fuel.litres=fuel,
                warningShape,warningPayload,"partWarnings");
            if(shell.PartBrowserDetailCurrent&&shell.PartBrowserDetailId==candidate.Id)
            {yield return Click(input,"partBack");yield return WaitBoundedPartsReady("list after warning retention check");}
            if(game.PartsPage!=returnPage)yield return Click(input,"partsSource:"+returnPage);
            yield return WaitBoundedPartsReady("restore Parts page after warning retention check");
        }

        string BoundedDetailShape()
        {
            var dyno=shell.Root.Q("partDetailDyno");var stats=shell.Root.Q("partStats");var specific=shell.Root.Q("partSpecificStats");
            string Labels(VisualElement root)=>root==null?"missing":string.Join("|",root.Query<Label>().ToList()
                .Select(label=>(label.name??"")+"="+(label.text??"")));
            return (dyno?.childCount??-1)+"/"+(stats?.childCount??-1)+"/"+(specific?.childCount??-1)+"/"+
                (shell.Root.Q<Label>("partDetailTitle")?.text??"")+"/"+Labels(dyno)+"/"+Labels(stats)+"/"+Labels(specific);
        }

        int BoundedDetailOpenGeneration()
        {
            var field=typeof(NativeMenuShell).GetField("partDetailGeneration",BindingFlags.Instance|BindingFlags.NonPublic);
            return field==null?-1:(int)field.GetValue(shell);
        }

        bool BoundedCurrentBuildProgress(out string state,out int captured,out int total,out int prepared)
        {
            var field=typeof(NativeMenuShell).GetField("activePartBuild",BindingFlags.Instance|BindingFlags.NonPublic);
            var build=field?.GetValue(shell);if(build==null){state="none";captured=total=prepared=0;return false;}
            var type=build.GetType();
            object Read(string name)=>type.GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)?.GetValue(build);
            state=Read("State")?.ToString()??"none";captured=Read("CaptureItems") is int c?c:0;
            total=Read("CaptureTotal") is int t?t:0;prepared=Read("PreparationItems") is int p?p:0;
            return true;
        }

        string PartWarningButtonShape()
        {
            var button=shell.Root.Q<Button>("partWarnings");
            return button==null?null:(button.text??"")+"|critical="+button.ClassListContains("car-critical")+
                "|warning="+button.ClassListContains("car-warning");
        }

        string PartWarningDialogText()
        {
            return shell.Root.Q<ScrollView>("modalContent")?.Query<Label>().ToList()
                .Select(label=>label.text??"").FirstOrDefault(text=>!string.IsNullOrWhiteSpace(text));
        }

        IEnumerator RejectEditedDetailAndRebuild(WindowsKeyboardProbe input,string label,string entryId,Action makeB,Action restoreA,
            string expectedWarningShape=null,string expectedWarningDialog=null,string actionName=null)
        {
            string previous=FramePacingProbe.OverridePhase;int generationBefore=shell.PartBrowserDataGeneration;
            int cartBefore=game.CartCount,cashBefore=game.Profile.cash;long revisionBefore=game.Profile.revision;
            string shapeBefore=BoundedDetailShape();Vector2 listOffset=shell.Root.Q<ScrollView>("partsScroll").scrollOffset;
            try
            {
                FramePacingProbe.OverridePhase="bounded-parts/"+label+"/prepare";
                Check(shell.PartBrowserDetailCurrent&&shell.PartBrowserDetailId==entryId,
                    "Detail action fixture starts from the current accepted offer / "+label);
                makeB();int cashAtAction=game.Profile.cash;
                var staleAction=actionName==null?shell.CapturePartDetailActionForChecks():
                    shell.CapturePartDetailActionForChecks(actionName);
                Check(staleAction!=null,"Current visible detail action exposes its production callback for the stale-action fixture / "+label+
                    " action="+(actionName??"partAdd"));
                staleAction?.Invoke();
                int observedGeneration=shell.PartBrowserDataGeneration;
                Check(observedGeneration>generationBefore&&!shell.PartBrowserActionsEnabled&&!shell.PartBrowserDetailCurrent&&
                    !shell.ModalOpen&&game.CartCount==cartBefore&&game.Profile.cash==cashAtAction&&game.Profile.revision==revisionBefore,
                    "Same-revision changed detail facts reject the production purchase callback without staging / "+label+
                    " generation="+generationBefore+"->"+observedGeneration);
                float captureStart=Time.realtimeSinceStartup;string buildState;int captured,total,prepared;
                while(Time.realtimeSinceStartup-captureStart<90f&&shell.PartBrowserPreparationState!="Failed"&&
                    (!BoundedCurrentBuildProgress(out buildState,out captured,out total,out prepared)||
                        buildState!="Preparing"||captured!=total||total<=0||prepared<=0))
                    yield return null;
                bool completeB=BoundedCurrentBuildProgress(out buildState,out captured,out total,out prepared)&&
                    buildState=="Preparing"&&captured==total&&total>0&&prepared>0;
                Check(completeB,
                    "Replacement builder has atomically captured B and entered real preparation before restoration / "+label+
                    " state="+buildState+" capture="+captured+"/"+total+" prepared="+prepared);
                if(!completeB)yield break;
                shell.HoldPartBrowserAfterNextStep();
                yield return WaitForPartsHeld(label+" complete B capture and preparation step");
                bool heldB=BoundedCurrentBuildProgress(out buildState,out captured,out total,out prepared)&&
                    buildState=="Preparing"&&captured==total&&total>0&&prepared>0;
                Check(heldB,
                    "Held builder still represents the complete B snapshot after real preparation work / "+label+
                    " state="+buildState+" capture="+captured+"/"+total+" prepared="+prepared);
                if(!heldB)yield break;
                Check(shell.PartBrowserDetailId==entryId&&!shell.Root.Q("partsDetail").ClassListContains("hidden")&&
                    !shell.PartBrowserDetailCurrent&&shell.PartBrowserPreparationPending,
                    "A stale detail remains visible but non-actionable while the replacement builder holds / "+label);
                Check(shell.Root.Q<Button>("partBack").enabledInHierarchy&&
                    !shell.Root.Q<Button>("partAdd").enabledInHierarchy,
                    "Held stale detail retains Back navigation while replacement actions are disabled / "+label);

                if(expectedWarningShape!=null)
                {
                    Check(PartWarningButtonShape()==expectedWarningShape,
                        "Naturally authored warning caption and severity remain visible during held stale rebuild / "+label);
                    shell.FailPartBrowserForChecks("Isolated warning-retention failure probe");
                    var failureLabel=shell.Root.Q<Label>("partsEmpty");
                    Check(shell.PartBrowserPreparationState=="Failed"&&!shell.PartBrowserPreparationPending&&
                        PartWarningButtonShape()==expectedWarningShape&&
                        !shell.Root.Q("partsDetail").ClassListContains("hidden")&&failureLabel!=null&&
                        failureLabel.text.Contains("Isolated warning-retention failure probe"),
                        "Terminal failure notice and the same warning detail/severity remain visible together / "+label);
                    restoreA();shell.ResumePartBrowserPreparation();shell.RetryPartBrowserPreparation();
                    yield return WaitBoundedDetailReady(label+" explicit retry",entryId);
                    Check(PartWarningButtonShape()==expectedWarningShape,
                        "Explicit retry preserves the actual warning caption and severity / "+label);
                    var warningButton=shell.Root.Q<Button>("partWarnings");
                    if(warningButton!=null)yield return Click(input,"partWarnings");
                    string retryDialog=PartWarningDialogText();
                    Check(shell.ModalOpen&&retryDialog==expectedWarningDialog,
                        "Explicit retry preserves the exact authored warning dialog payload / "+label);
                    if(shell.ModalOpen)yield return Click(input,"dialogClose");
                    yield break;
                }

                int heldGeneration=shell.PartBrowserDataGeneration;
                restoreA();shell.ResumePartBrowserPreparation();
                float retryStart=Time.realtimeSinceStartup;
                while(shell.PartBrowserDataGeneration<=heldGeneration&&Time.realtimeSinceStartup-retryStart<30f)
                    yield return null;
                Check(shell.PartBrowserDataGeneration>heldGeneration,
                    "After the complete B snapshot and preparation step, restoring A retires that build and starts a fresh generation / "+label+
                    " generation="+heldGeneration+"->"+shell.PartBrowserDataGeneration);
                yield return WaitBoundedDetailReady(label+" detail retry",entryId);
                Check(BoundedDetailShape()==shapeBefore&&
                    Math.Abs(shell.Root.Q<ScrollView>("partsScroll").scrollOffset.y-listOffset.y)<.1f,
                    "Detail dyno/stat surfaces and the original list return offset survive the completed rebuild / "+label+
                    " shape="+BoundedDetailShape()+" before="+shapeBefore);
            }
            finally
            {
                restoreA();shell.ResumePartBrowserPreparation();FramePacingProbe.OverridePhase=previous;
            }
        }

        IEnumerator TypePartsSearch(WindowsKeyboardProbe input,string value)
        {
            string previous=FramePacingProbe.OverridePhase;
            try
            {
                FramePacingProbe.OverridePhase="bounded-parts/search-input/prepare";
                var field=shell.Root.Q<TextField>("partsSearch");
                yield return Click(input,field);
                input.Key(0x11,true);input.Key(0x41,true);yield return null;input.Key(0x41,false);input.Key(0x11,false);yield return null;
                if(string.IsNullOrEmpty(value)){input.Key(0x08,true);yield return null;input.Key(0x08,false);yield return null;}
                foreach(char c in value)
                {
                    ushort key=(ushort)char.ToUpperInvariant(c);
                    input.Key(key,true);yield return null;input.Key(key,false);yield return null;
                }
            }
            finally{FramePacingProbe.OverridePhase=previous;}
        }

        Button BoundedPartCard(string id)=>shell.Root.Q<Button>(id);

        bool BoundedPartCardHitTestable(Button card)
        {
            if(card==null||!card.enabledInHierarchy)return false;
            var rect=card.worldBound;var viewport=shell.Root.Q<ScrollView>("partsScroll").contentViewport.worldBound;
            var picked=shell.Root.panel?.Pick(rect.center);
            return viewport.Contains(rect.min+Vector2.one*.1f)&&viewport.Contains(rect.max-Vector2.one*.1f)&&
                picked!=null&&(ReferenceEquals(picked,card)||card.Contains(picked));
        }

        IEnumerator CapturePartsProfileDiagnostic(Action<string> accept)
        {
            string previous=FramePacingProbe.OverridePhase;
            try
            {
                FramePacingProbe.OverridePhase="test-fixture/parts-profile-audit";
                accept(JsonUtility.ToJson(game.Profile));
                // The early frame probe records the preceding frame's CPU work.
                // Keep this harness-only label through that sample, so the next
                // product interaction does not inherit diagnostic serialization.
                yield return null;
            }
            finally{FramePacingProbe.OverridePhase=previous;}
        }

        static string NativeEncoderMark()=>MarketCatalogLayout.CanonicalKeyRequestCount+"/"+
            MarketCatalogLayout.CanonicalJsonEncodeCount+"/"+NativePartBrowser.LegacyPlanJsonCount+"/"+
            NativePartBrowser.LegacyOwnedCarJsonCount+"/"+NativePartBrowser.LegacyCartPreviewCount+"/"+
            NativePartPreviewCache.LegacyCarSignatureJsonCount+"/"+NativePartPreviewCache.LegacyEntryKeyJsonCount;

        IEnumerator BoundedPartsPhase(string name,IEnumerator work)
        {
            string previous=FramePacingProbe.OverridePhase;
            try
            {
                FramePacingProbe.OverridePhase="bounded-parts/"+name+"/prepare";
                while(work.MoveNext())yield return work.Current;
            }
            finally{FramePacingProbe.OverridePhase=previous;}
        }

        IEnumerator EnterBuildAndWait(WindowsKeyboardProbe input)
        {
            if(game.ScreenName!="Parts")yield return Click(input,"navBuild");
            yield return WaitBoundedPartsReady("Build entry");
        }

        IEnumerator SelectPartsPageAndWait(WindowsKeyboardProbe input,string page)
        {
            if(game.PartsPage!=page)yield return Click(input,"partsSource:"+page);
            yield return WaitBoundedPartsReady("page="+page);
        }

        IEnumerator CaptureBoundedParts(string name)
        {
            string previous=FramePacingProbe.OverridePhase;
            try
            {
                FramePacingProbe.OverridePhase="bounded-parts/"+name+"/capture";
                yield return Capture("bounded-parts-"+name);
            }
            finally{FramePacingProbe.OverridePhase=previous;}
        }

        IEnumerator CaptureOwnedUnitScrollbarResolutionChecks()
        {
            int previousWidth=Screen.width,previousHeight=Screen.height;bool previousFullscreen=Screen.fullScreen;
            try
            {
                Screen.SetResolution(1920,1080,false);
                float start=Time.realtimeSinceStartup;
                while((Screen.width!=1920||Screen.height!=1080)&&Time.realtimeSinceStartup-start<8f)yield return null;
                yield return new WaitForSecondsRealtime(.4f);
                Check(Screen.width==1920&&Screen.height==1080,"Owned unit detail reaches the real 1920x1080 viewport");
                CheckOwnedDetailScrollbar("owned unit detail at 1920x1080");
                yield return CaptureBoundedParts("owned-unit-detail-1920x1080");
                report.AppendLine("PARTS_CAPTURE,resolution=1920x1080,case=owned-unit-detail,scrollbar=checked,filename=bounded-parts-owned-unit-detail-1920x1080.png");

                var target=new RenderTexture(3840,2160,24);target.Create();
                var previousTarget=shell.Panel.targetTexture;var previousActive=RenderTexture.active;Texture2D image=null;
                try
                {
                    shell.Panel.targetTexture=target;shell.Refresh();yield return new WaitForSecondsRealtime(.4f);
                    Check(shell.Panel.scale==2,"Owned unit detail uses shared crisp 4K panel scaling");
                    CheckOwnedDetailScrollbar("owned unit detail at 3840x2160");
                    string previous=FramePacingProbe.OverridePhase;
                    try
                    {
                        FramePacingProbe.OverridePhase="bounded-parts/owned-unit-detail-3840x2160/capture";
                        yield return new WaitForEndOfFrame();
                        RenderTexture.active=target;image=new Texture2D(3840,2160,TextureFormat.RGB24,false);
                        image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();
                        File.WriteAllBytes(Path.Combine(folder,"bounded-parts-owned-unit-detail-3840x2160.png"),image.EncodeToPNG());
                        yield return new WaitForSecondsRealtime(.2f);
                    }
                    finally{FramePacingProbe.OverridePhase=previous;}
                }
                finally
                {
                    RenderTexture.active=previousActive;shell.Panel.targetTexture=previousTarget;shell.Refresh();
                    target.Release();Destroy(target);if(image!=null)Destroy(image);
                }
                report.AppendLine("PARTS_CAPTURE,resolution=3840x2160,renderTarget=offscreen-panel,screen="+
                    Screen.width+"x"+Screen.height+",case=owned-unit-detail,scrollbar=checked,filename=bounded-parts-owned-unit-detail-3840x2160.png");
            }
            finally
            {
                Screen.SetResolution(previousWidth,previousHeight,previousFullscreen);
            }
            float restoreStart=Time.realtimeSinceStartup;
            while((Screen.width!=previousWidth||Screen.height!=previousHeight)&&Time.realtimeSinceStartup-restoreStart<8f)yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            Check(Screen.width==previousWidth&&Screen.height==previousHeight&&Screen.fullScreen==previousFullscreen,
                "Owned unit detail capture restores the original window mode and resolution");
        }

        void CheckOwnedDetailScrollbar(string label)
        {
            var view=shell.Root.Q<ScrollView>("partsDetailScroll");var bar=view?.verticalScroller;
            Check(view!=null&&bar!=null&&bar.ClassListContains("settings-scrollbar")&&
                bar.slider.ClassListContains("settings-scrollbar-slider")&&Math.Abs(bar.resolvedStyle.width-8f)<.1f&&
                bar.resolvedStyle.display!=DisplayStyle.None&&view.contentContainer.layout.height>view.contentViewport.layout.height+1f&&
                bar.lowButton.resolvedStyle.display==DisplayStyle.None&&bar.highButton.resolvedStyle.display==DisplayStyle.None,
                "Owned unit detail retains the themed 8px scrollbar without arrows / "+label+
                " width="+(bar?.resolvedStyle.width??-1)+" classes="+(bar==null?"none":string.Join(" ",bar.GetClasses())));
        }

        IEnumerator NonemptyInventoryPriceChecks(WindowsKeyboardProbe input)
        {
            var profile=game.Profile;var car=profile.SelectedCar;string returnPage=game.PartsPage;
            var fittedHead=profile.componentsPart(PartCategory.HeadIntake);
            var fittedPistons=profile.componentsPart(PartCategory.Pistons);
            var pistonDefinition=fittedPistons==null?null:profile.catalog.Find(fittedPistons.definitionId);
            var pistonMember=fittedPistons?.condition?.elements?.FirstOrDefault(member=>member is PistonElement);
            Check(car!=null&&fittedHead!=null&&fittedPistons!=null&&pistonDefinition!=null&&pistonMember!=null&&
                profile.catalog.Fits(profile.catalog.Spec(car.vehicleId),pistonDefinition),
                "Nonempty Inventory fixtures derive from actual fitted head/intake and compatible piston/member data");
            if(car==null||fittedHead==null||fittedPistons==null||pistonDefinition==null||pistonMember==null)yield break;

            int partCountBefore=profile.parts.Count,unitCountBefore=profile.looseEngineElements.Count;
            long revisionBefore=profile.revision;int cashBefore=profile.cash;
            var addedParts=new List<PartInstance>();var addedUnits=new List<LooseEngineElement>();bool valid=false;
            string inventoryOraclePhase=FramePacingProbe.OverridePhase;
            try
            {
                FramePacingProbe.OverridePhase="test-fixture/parts-inventory-oracle";
                for(int i=0;i<24;i++)
                {
                    var copy=fittedHead.Copy();copy.instanceId=MechanicsOwnership.NewId("part");
                    copy.location=PartLocation.Inventory;copy.ownerId="";copy.slot="";copy.storageId=SharedInventory.Garage;
                    foreach(var member in copy.condition.elements??Array.Empty<EngineElement>())
                        if(member!=null)member.id=MechanicsOwnership.NewId("element");
                    profile.parts.Add(copy);addedParts.Add(copy);
                }
                int unitPrice=InventoryPricing.NewUnit(pistonDefinition,InventoryUnits.Count(profile,car,pistonDefinition));
                for(int i=0;i<12;i++)
                {
                    var copy=pistonMember.Copy();copy.id=MechanicsOwnership.NewId("element");
                    if(string.IsNullOrEmpty(copy.definitionId))copy.definitionId=pistonDefinition.id;
                    addedUnits.Add(new LooseEngineElement{definitionId=pistonDefinition.id,storageId=SharedInventory.Garage,
                        unitPrice=unitPrice,element=copy});
                }
                profile.looseEngineElements.AddRange(addedUnits);
                string ownershipError=MechanicsOwnership.ValidateForQuote(profile,profile.catalog);
                valid=ownershipError==null;
                Check(valid,"Expanded native Inventory fixture validates actual ownership and unique identities / "+(ownershipError??"valid"));
                report.AppendLine("INVENTORY_VALIDATION,direct="+(MechanicsOwnership.Validate(profile,profile.catalog)??"valid")+
                    ",quote="+(ownershipError??"valid")+",parts="+profile.parts.Count+",units="+profile.looseEngineElements.Count+
                    ",vehicleListings="+(profile.vehicleStock?.listings?.Count??-1));
                if(valid)
                {
                    // This reference map is fixture-oracle setup, outside the
                    // measured page build. The actual UI preparation still creates
                    // its own fresh once-per-wrapper price facts.
                    var expectedSalePrices=game.NativeSalePrices();
                    // Keep fixture construction and oracle work on the preceding
                    // frame; the following measured interval starts with only the
                    // real native Inventory route and its bounded UI preparation.
                    yield return null;
                    FramePacingProbe.OverridePhase=inventoryOraclePhase;
                    yield return BoundedPartsPhase("nonempty-inventory",SelectPartsPageAndWait(input,"Inventory"));
                    var model=shell.PartBrowser;
                    var addedPartIds=new HashSet<string>(addedParts.Select(part=>part.instanceId),StringComparer.Ordinal);
                    var addedUnitIds=new HashSet<string>(addedUnits.Select(unit=>unit.element.id),StringComparer.Ordinal);
                    var fixtureEntries=model.Entries.Where(entry=>addedPartIds.Contains(entry.AssetId)||addedUnitIds.Contains(entry.AssetId)).ToArray();
                    Check(fixtureEntries.Length==36&&fixtureEntries.All(entry=>entry.Source=="Owned"&&entry.Price.HasValue),
                        "Native Inventory model publishes all 24 actual copied fitted-pack parts and 12 typed loose piston members with prices / "+fixtureEntries.Length);
                    Check(addedParts.All(part=>fixtureEntries.Any(entry=>entry.AssetId==part.instanceId&&
                            entry.Price==expectedSalePrices(part.instanceId,false)))&&
                        addedUnits.All(unit=>fixtureEntries.Any(entry=>entry.AssetId==unit.element.id&&
                            entry.Price==expectedSalePrices(unit.element.id,true))),
                        "Inventory detail rows use exact current part and typed-member sale prices from the live valuation policy");
                    Check(shell.PartBrowserWindowCardCount>0&&shell.PartBrowserActionsEnabled,
                        "Nonempty Inventory publishes a real actionable card window after bounded preparation");
                    yield return BoundedPartsPhase("owned-actions",OwnedInventoryActionChecks(input,profile,car,addedParts[0],addedUnits[0]));
                    yield return WaitBoundedPartsReady("restore nonempty Inventory after stale action batch");
                    yield return CaptureBoundedParts("nonempty-inventory-actionable");
                    Check(profile.revision==revisionBefore&&profile.cash==cashBefore,
                        "Inventory list rendering does not purchase or revise the isolated profile");
                }
            }
            finally
            {
                FramePacingProbe.OverridePhase=inventoryOraclePhase;
                foreach(var part in addedParts)profile.parts.Remove(part);
                foreach(var unit in addedUnits)profile.looseEngineElements.Remove(unit);
                Check(profile.parts.Count==partCountBefore&&profile.looseEngineElements.Count==unitCountBefore,
                    "Inventory fixture cleanup removes only the object references appended by this check");
            }

            yield return BoundedPartsPhase("restore-parts-after-inventory-fixture",SelectPartsPageAndWait(input,returnPage));
        }

        IEnumerator OpenOwnedFixtureDetail(WindowsKeyboardProbe input,PartInstance part,string label)
        {
            var entry=shell.PartBrowser.Entries.FirstOrDefault(candidate=>candidate.AssetId==part.instanceId&&candidate.Source=="Owned");
            Check(entry!=null,"Live Inventory contains the exact temporary part fixture / "+label);
            if(entry==null)yield break;
            shell.ScrollToPart(entry.Id);
            yield return WaitBoundedPartsReady(label+" visible card");
            var card=BoundedPartCard("partCard:"+entry.Id);
            Check(card!=null&&BoundedPartCardHitTestable(card),"Temporary inventory part is a real hit-testable card / "+label);
            if(card==null||!BoundedPartCardHitTestable(card))yield break;
            yield return Click(input,card);
            yield return WaitBoundedDetailReady(label+" detail",entry.Id);
        }

        IEnumerator OpenOwnedFixtureDetail(WindowsKeyboardProbe input,LooseEngineElement unit,string label)
        {
            var entry=shell.PartBrowser.Entries.FirstOrDefault(candidate=>candidate.AssetId==unit.element.id&&candidate.Source=="Owned");
            Check(entry!=null,"Live Inventory contains the exact temporary unit fixture / "+label);
            if(entry==null)yield break;
            shell.ScrollToPart(entry.Id);
            yield return WaitBoundedPartsReady(label+" visible card");
            var card=BoundedPartCard("partCard:"+entry.Id);
            Check(card!=null&&BoundedPartCardHitTestable(card),"Temporary inventory unit is a real hit-testable card / "+label);
            if(card==null||!BoundedPartCardHitTestable(card))yield break;
            yield return Click(input,card);
            yield return WaitBoundedDetailReady(label+" detail",entry.Id);
        }

        IEnumerator OwnedInventoryActionChecks(WindowsKeyboardProbe input,PlayerProfile profile,OwnedCar car,
            PartInstance moveSalePart,LooseEngineElement pistonUnit)
        {
            int baselineCart=game.CartCount,baselineCash=profile.cash;long baselineRevision=profile.revision;
            double fuel=car.fuel.litres;
            string slots=string.Join("|",car.installed.slots.Select(slot=>slot.slot+":"+slot.partInstanceId));
            string partId=moveSalePart.instanceId,unitId=pistonUnit.element.id;

            // Position Apply: choose an actual rendered position while the origin is
            // current, then stale its physical read-set before the real Apply click.
            yield return OpenOwnedFixtureDetail(input,pistonUnit,"unit-position stale Apply");
            yield return CaptureBoundedParts("owned-unit-detail-actionable");
            Check(shell.Root.Q<Button>("partPosition")!=null&&shell.Root.Q<Button>("partPosition").enabledInHierarchy,
                "Current owned unit detail exposes its real position action");
            yield return Click(input,"partPosition");
            Check(shell.ModalOpen&&shell.Root.Q<Label>("modalTitle").text=="Replacement position",
                "Owned unit position selector opens through its actual detail action");
            var positionChoice=shell.Root.Q<ScrollView>("modalContent").Query<Button>().ToList()
                .FirstOrDefault(button=>button.name.StartsWith("partUnitPosition:",StringComparison.Ordinal));
            Check(positionChoice!=null,"Position dialog exposes an actual compatible rendered choice");
            if(positionChoice!=null)yield return PartDialogClick(input,positionChoice.name);
            int positionGeneration=shell.PartBrowserDataGeneration;
            car.fuel.litres=fuel+.625;
            yield return Click(input,"dialogApply");
            Check(shell.ModalOpen&&shell.PartBrowserDataGeneration>positionGeneration&&
                profile.looseEngineElements.Any(unit=>unit.element.id==unitId)&&
                string.Join("|",car.installed.slots.Select(slot=>slot.slot+":"+slot.partInstanceId))==slots&&
                game.CartCount==baselineCart&&profile.cash==baselineCash&&profile.revision==baselineRevision,
                "Stale unit-position Apply callback ran and rejected before changing the unit, installed slots, cart, cash or revision");
            car.fuel.litres=fuel;
            if(shell.ModalOpen)yield return Click(input,"dialogCancel");
            yield return WaitBoundedDetailReady("unit-position A recovery",pistonUnit.element.id);

            // Direct install: the real confirmation remains available after its
            // origin becomes stale, and must reject before InstallNativeUnit.
            yield return Click(input,"partPlanInstall");
            Check(shell.ModalOpen&&shell.Root.Q<Label>("modalTitle").text=="Install unit?",
                "Owned unit detail opens its real direct-install confirmation");
            int installGeneration=shell.PartBrowserDataGeneration;fuel=car.fuel.litres;
            car.fuel.litres=fuel+.625;
            yield return Click(input,"dialogInstall");
            Check(shell.ModalOpen&&shell.PartBrowserDataGeneration>installGeneration&&
                profile.looseEngineElements.Any(unit=>unit.element.id==unitId)&&
                string.Join("|",car.installed.slots.Select(slot=>slot.slot+":"+slot.partInstanceId))==slots&&
                game.CartCount==baselineCart&&profile.cash==baselineCash&&profile.revision==baselineRevision,
                "Stale direct-install confirmation callback ran and rejected before durable installation");
            car.fuel.litres=fuel;
            if(shell.ModalOpen)yield return Click(input,"dialogCancel");
            yield return WaitBoundedDetailReady("direct-install A recovery",pistonUnit.element.id);
            yield return CaptureOwnedUnitScrollbarResolutionChecks();
            yield return Click(input,"partBack");
            yield return WaitBoundedPartsReady("Inventory after unit detail actions");

            // Move: destination selection is a real modal draft. Its confirmation
            // callback must reject before staging a cart line when the origin ages.
            yield return OpenOwnedFixtureDetail(input,moveSalePart,"move confirmation");
            yield return Click(input,"partMove");
            Check(shell.ModalOpen&&shell.Root.Q<Label>("modalTitle").text=="Move part",
                "Owned part detail opens the actual move dialog");
            var target=shell.Root.Q<ScrollView>("modalContent").Query<Button>().ToList()
                .FirstOrDefault(button=>button.name.StartsWith("inventoryMoveTarget:",StringComparison.Ordinal));
            Check(target!=null,"Move dialog exposes a real destination");
            if(target!=null)yield return PartDialogClick(input,target.name);
            int moveGeneration=shell.PartBrowserDataGeneration;fuel=car.fuel.litres;
            car.fuel.litres=fuel+.625;
            yield return Click(input,"dialogAddmove");
            Check(shell.ModalOpen&&shell.Root.Q<Label>("inventoryMoveError")!=null&&
                !string.IsNullOrWhiteSpace(shell.Root.Q<Label>("inventoryMoveError").text)&&
                shell.PartBrowserDataGeneration>moveGeneration&&game.CartCount==baselineCart&&
                profile.parts.Any(part=>part.instanceId==partId&&part.location==PartLocation.Inventory)&&
                profile.cash==baselineCash&&profile.revision==baselineRevision,
                "Stale move confirmation callback ran and rejected before staging or changing the owned part");
            car.fuel.litres=fuel;
            if(shell.ModalOpen)yield return Click(input,"dialogCancel");
            yield return WaitBoundedDetailReady("move A recovery",moveSalePart.instanceId);

            // Sale has no dialog. Invoke the exact currently bound Guarded callback
            // after same-revision invalidation; an ordinary disabled-button no-op
            // cannot satisfy the rejection-count delta.
            var sale=shell.CapturePartDetailActionForChecks("partPlanSale");
            Check(sale!=null,"Current owned-part detail exposes its actual guarded sale callback");
            int rejectedBefore=shell.PartActionGuardRejectedCount;fuel=car.fuel.litres;
            car.fuel.litres=fuel+.625;
            sale?.Invoke();
            Check(shell.PartActionGuardRejectedCount==rejectedBefore+1&&game.CartCount==baselineCart&&
                profile.parts.Any(part=>part.instanceId==partId&&part.location==PartLocation.Inventory)&&
                profile.cash==baselineCash&&profile.revision==baselineRevision,
                "Captured stale sale callback executed its guard and rejected before cart staging");
            car.fuel.litres=fuel;
            yield return WaitBoundedDetailReady("sale A recovery",moveSalePart.instanceId);
            yield return Click(input,"partBack");
            yield return WaitBoundedPartsReady("Inventory after part actions");

            // Tire swap uses one exact copy of an installed tire only when the
            // isolated profile has no real loose tire. Its wear/fault state is
            // retained byte-for-byte except the unique fixture identity.
            LooseEngineElement temporaryTire=null;
            try
            {
                var existingTire=profile.looseEngineElements.FirstOrDefault(unit=>unit.tireUnit&&unit.tire!=null);
                if(existingTire==null)
                {
                    var installedTires=profile.componentsPart(PartCategory.Tires);
                    Check(installedTires?.condition?.tires!=null&&installedTires.condition.tires.Length==4,
                        "Selected car supplies four actual tire conditions for a reversible loose-tire fixture");
                    if(installedTires?.condition?.tires==null||installedTires.condition.tires.Length!=4)yield break;
                    var tire=installedTires.condition.tires[0].Copy();tire.id=MechanicsOwnership.NewId("tire");
                    temporaryTire=new LooseEngineElement{definitionId=installedTires.definitionId,storageId=SharedInventory.Garage,element=tire,tireUnit=true};
                    profile.looseEngineElements.Add(temporaryTire);
                    existingTire=temporaryTire;
                }
                // This deliberate test-fixture edit changes the exact Inventory
                // read set without a revision bump. Accept a fresh model before
                // testing the current-origin dialog, as normal ownership edits do.
                if(temporaryTire!=null)
                {
                    int previousGeneration=shell.PartBrowserDataGeneration;
                    shell.RetryPartBrowserPreparation();
                    int retryFrames=0;
                    while(shell.PartBrowserDataGeneration<=previousGeneration&&retryFrames++<240)yield return null;
                    Check(shell.PartBrowserDataGeneration>previousGeneration,"Retained tire fixture starts a genuinely newer Inventory build");
                    yield return WaitBoundedPartsReady("Inventory with retained tire fixture");
                    Check(shell.PartBrowserDataGeneration>previousGeneration&&shell.PartBrowser.Entries.Any(entry=>entry.AssetId==temporaryTire.element.id&&entry.Unit?.tireUnit==true),
                        "The actual retained tire is accepted by a newer current Inventory model before opening its dialog");
                }
                var installedBefore=profile.componentsPart(PartCategory.Tires).condition.tires.Select(tire=>tire.id).ToArray();
                var looseTireId=existingTire.element.id;
                yield return Click(input,"partsTireSwap");
                Check(shell.ModalOpen&&shell.Root.Q<Label>("modalTitle").text=="Swap stored tires",
                    "Parts tire-swap control opens its actual confirmation dialog");
                var group=shell.Root.Q<ScrollView>("modalContent").Query<Button>().ToList()
                    .FirstOrDefault(button=>button.name.StartsWith("tireSwapStock:",StringComparison.Ordinal));
                Check(group!=null,"Tire dialog contains a real retained-stock group");
                if(group!=null)yield return PartDialogClick(input,group.name);
                var corner=shell.Root.Q<ScrollView>("modalContent").Query<Button>().ToList()
                    .FirstOrDefault(button=>button.name.StartsWith("tireSwapCorner:",StringComparison.Ordinal));
                Check(corner!=null,"Tire dialog exposes a real corner choice");
                if(corner!=null)yield return PartDialogClick(input,corner.name);
                int tireGeneration=shell.PartBrowserDataGeneration;fuel=car.fuel.litres;
                car.fuel.litres=fuel+.625;
                yield return Click(input,"dialogSwaptires");
                Check(shell.ModalOpen&&shell.PartBrowserDataGeneration>tireGeneration&&
                    profile.componentsPart(PartCategory.Tires).condition.tires.Select(tire=>tire.id).SequenceEqual(installedBefore)&&
                    profile.looseEngineElements.Any(unit=>unit.tireUnit&&unit.element.id==looseTireId)&&
                    game.CartCount==baselineCart&&profile.cash==baselineCash&&profile.revision==baselineRevision,
                    "Stale tire-swap confirmation ran and rejected before CommitParts or tire identity changes");
                car.fuel.litres=fuel;
                if(shell.ModalOpen)yield return Click(input,"dialogCancel");
                yield return WaitBoundedPartsReady("tire-swap A recovery");
            }
            finally
            {
                if(temporaryTire!=null)profile.looseEngineElements.Remove(temporaryTire);
            }
            if(temporaryTire!=null)
            {
                int cleanupGeneration=shell.PartBrowserDataGeneration,cleanupFrames=0;
                shell.RetryPartBrowserPreparation();
                while(shell.PartBrowserDataGeneration<=cleanupGeneration&&cleanupFrames++<240)yield return null;
                Check(shell.PartBrowserDataGeneration>cleanupGeneration,"Removing the temporary tire starts a fresh Inventory build");
            }
            yield return WaitBoundedPartsReady("Inventory after tire-swap fixture cleanup");
            Check(game.CartCount==baselineCart&&profile.cash==baselineCash&&profile.revision==baselineRevision,
                "All stale owned-action fixtures leave cart, cash and profile revision untouched");
        }

        IEnumerator BoundedPartsBrowserChecks(WindowsKeyboardProbe input)
        {
            if(!shell.Visible)yield return Key(input,0x77);
            yield return BoundedPartsPhase("initial",EnterBuildAndWait(input));
            Check(game.ScreenName=="Parts"&&shell.Root.Q("partsPage")!=null&&
                shell.PartBrowserWindowCardCount>0,"Native Build route shows an accepted visible Parts card window");
            yield return CaptureBoundedParts("initial-actionable");
            var initialModel=shell.PartBrowser;
            Check(initialModel.EvaluationCount<initialModel.Entries.Count,
                "Intrinsic initial browse avoids evaluating every candidate performance comparison");

            // Supplement real mouse coverage with callbacks captured before an
            // invalidation and dispatched in the same owner-thread interval.
            // There is deliberately no intervening LateUpdate to hide a stale
            // action behind normal refresh/cancellation.
            var firstCard=shell.Root.Q("partsGrid").Query<Button>(className:"part-card").ToList().First();
            string firstId=firstCard.name.Substring("partCard:".Length);
            var queuedScrollAction=shell.CapturePartCardActionForChecks(firstId);
            Check(queuedScrollAction!=null,"Captured card guard uses an actual accepted visible entry");
            var staleScroll=shell.Root.Q<ScrollView>("partsScroll");var savedOffset=staleScroll.scrollOffset;
            int rejectedBefore=shell.PartActionGuardRejectedCount;
            staleScroll.scrollOffset=new Vector2(savedOffset.x,savedOffset.y+31f);
            Check(Math.Abs(staleScroll.scrollOffset.y-savedOffset.y)>1f,
                "Same-frame queued-card fixture changes the actual scroll offset");
            queuedScrollAction();
            Check(shell.PartActionGuardRejectedCount==rejectedBefore+1&&!shell.PartBrowserActionsEnabled,
                "A queued card callback rejects changed scroll before the next LateUpdate");
            staleScroll.scrollOffset=savedOffset;
            yield return WaitBoundedPartsReady("same-frame scroll action recovery");

            firstCard=shell.Root.Q("partsGrid").Query<Button>(className:"part-card").ToList().First();
            var queuedRouteAction=shell.CapturePartCardActionForChecks(firstCard.name.Substring("partCard:".Length));
            rejectedBefore=shell.PartActionGuardRejectedCount;bool dispatchedAfterRoute=false;
            game.QueueNativeNavigation("Garage",arrived:()=>{queuedRouteAction();dispatchedAfterRoute=true;});
            float routeStart=Time.realtimeSinceStartup;
            while(!dispatchedAfterRoute&&Time.realtimeSinceStartup-routeStart<8f)yield return null;
            Check(dispatchedAfterRoute&&game.ScreenName=="Garage"&&shell.PartActionGuardRejectedCount==rejectedBefore+1,
                "Queued card callback rejects a changed route inside arrival before normal Parts refresh");
            yield return BoundedPartsPhase("queued-route-return",EnterBuildAndWait(input));

            shell.FailPartBrowserForChecks("Isolated native terminal-failure probe");
            int failuresBefore=shell.PartBuildFailureCount,failedData=shell.PartBrowserDataGeneration;
            int failedBuilds=shell.PartBrowserBuildCount;double failedPriceMs=shell.PartPriceFactoryMilliseconds;
            for(int frame=0;frame<24;frame++)yield return null;
            Check(shell.PartBrowserPreparationState=="Failed"&&!shell.PartBrowserPreparationPending&&
                !shell.PartBrowserActionsEnabled&&shell.PartBuildFailureCount==failuresBefore&&
                shell.PartBrowserDataGeneration==failedData&&shell.PartBrowserBuildCount==failedBuilds&&
                shell.PartPriceFactoryMilliseconds==failedPriceMs,
                "Terminal failure remains visible without automatic rebuild or fresh-price work each frame");
            shell.RetryPartBrowserPreparation();
            yield return WaitBoundedPartsReady("explicit terminal-failure retry");

            // Page transitions use the real native source buttons and publish only
            // after the complete bounded model/query/window path settles.
            foreach(string page in new[]{"Shop","Inventory","Installed"})
            {
                yield return BoundedPartsPhase("page-"+page.ToLowerInvariant(),SelectPartsPageAndWait(input,page));
                Check(shell.PartBrowser.Entries.Count>0&&shell.PartBrowserWindowCardCount>0||
                    page=="Inventory"&&shell.PartBrowser.Entries.Count==0&&shell.PartBrowserWindowCardCount==0,
                    "Page publishes its actual entries and only a nonempty window when results exist / "+page);
                if(page=="Installed")Check(shell.PartBrowser.Entries.All(entry=>entry.Source=="Installed"),
                    "Installed route displays only fitted component entries");
                if(page=="Shop")Check(shell.PartBrowser.Entries.Any(entry=>entry.Source=="New"),
                    "Shop route retains live catalog offers");
                if(page=="Inventory")Check(shell.PartBrowser.Entries.All(entry=>entry.Source=="Owned"),
                    "Inventory route contains only owned retained entries (an empty inventory is valid)");
                yield return CaptureBoundedParts(page.ToLowerInvariant()+"-actionable");
            }
            yield return NonemptyInventoryPriceChecks(input);

            // Hold a real Shop query step, change search with native keyboard
            // input, then replace that query while the old query ticket is held.
            if(game.PartsPage!="Shop")yield return Click(input,"partsSource:Shop");
            yield return WaitBoundedPartsReady("Shop before query hold");
            var acceptedModel=shell.PartBrowser;int dataBefore=shell.PartBrowserDataGeneration;
            int presentationBefore=shell.PartBrowserPresentationGeneration;
            shell.HoldPartBrowserAfterNextStep();
            yield return TypePartsSearch(input,"piston");
            yield return WaitForPartsHeld("query first real step");
            Check(shell.PartBrowserDataGeneration==dataBefore&&shell.PartBrowserPresentationGeneration>presentationBefore,
                "Search invalidates presentation work only while retaining the accepted data model");
            int heldPresentation=shell.PartBrowserPresentationGeneration;
            yield return TypePartsSearch(input,"tire");
            yield return Click(input,"partsSort");
            yield return PartDialogClick(input,"partSortBy:Name");
            yield return Click(input,"dialogApply");
            Check(shell.PartBrowserPresentationGeneration>heldPresentation&&shell.PartBrowserDataGeneration==dataBefore&&
                ReferenceEquals(shell.PartBrowser,acceptedModel),
                "A newer query and sort replace held presentation tickets without restarting base model preparation");
            shell.ResumePartBrowserPreparation();
            yield return WaitBoundedPartsReady("latest held query");
            Check(shell.PartQuery.Search=="tire"&&shell.PartQuery.Sort=="Name"&&ReferenceEquals(shell.PartBrowser,acceptedModel),
                "Latest search and sort are applied to the same accepted model after held work resumes");
            yield return CaptureBoundedParts("latest-query-actionable");
            yield return TypePartsSearch(input,"");
            yield return WaitBoundedPartsReady("cleared search");

            var detailPhaseIds=shell.Root.Q("partsGrid").Query<Button>(className:"part-card").ToList()
                .Where(BoundedPartCardHitTestable).Select(button=>button.name.Substring("partCard:".Length))
                .Distinct(StringComparer.Ordinal).Take(2).ToArray();
            yield return BoundedPartsPhase("detail-lifecycle",DetailPhaseLifecycleChecks(input,detailPhaseIds));

            yield return Click(input,"partsSort");
            yield return PartDialogClick(input,"partSortBy:HP change");
            yield return Click(input,"dialogApply");
            yield return WaitBoundedPartsReady("HP-change sort before empty query");
            yield return TypePartsSearch(input,"zzzznotavailable");
            yield return WaitBoundedPartsReady("empty query under change-dependent sort");
            Check(shell.PartQuery.Sort=="HP change"&&shell.PartQuery.Search=="zzzznotavailable"&&
                shell.PartBrowserWindowCardCount==0&&shell.PartBrowserActionsEnabled,
                "Zero-candidate text query under a change-dependent sort completes with an empty actionable window");
            yield return TypePartsSearch(input,"");
            yield return Click(input,"partsSort");
            yield return PartDialogClick(input,"partSortBy:Name");
            yield return Click(input,"dialogApply");
            yield return WaitBoundedPartsReady("restore static sort after empty query");

            // Build and then supersede an unpublished visible-card ticket while
            // resizing. The base data generation remains accepted and current.
            var scroll=shell.Root.Q<ScrollView>("partsScroll");int windowBefore=shell.PartBrowserWindowGeneration;
            dataBefore=shell.PartBrowserDataGeneration;
            shell.HoldPartBrowserAfterNextStep();
            scroll.scrollOffset=new Vector2(0,Math.Max(0,scroll.contentContainer.layout.height-scroll.contentViewport.layout.height));
            float scrollBeforeResize=scroll.scrollOffset.y;
            yield return WaitForPartsHeld("virtual card window step");
            int windowHeld=shell.PartBrowserWindowGeneration;
            int previousWidth=Screen.width,previousHeight=Screen.height;bool previousFullscreen=Screen.fullScreen;
            try
            {
                Screen.SetResolution(1024,768,false);
                float resizeStart=Time.realtimeSinceStartup;
                while((Screen.width!=1024||Screen.height!=768)&&Time.realtimeSinceStartup-resizeStart<8f)yield return null;
                Check(Screen.width==1024&&Screen.height==768,
                    "Native window accepts the bounded live-layout resize used by this checkpoint");
                for(int frame=0;frame<4;frame++)yield return null;
                Check(shell.PartBrowserDataGeneration==dataBefore&&shell.PartBrowserWindowGeneration>=windowHeld&&
                    !shell.PartBrowserActionsEnabled,
                    "Viewport resize cancels or supersedes the held card ticket without replacing accepted base data");
                shell.ResumePartBrowserPreparation();
                yield return WaitBoundedPartsReady("latest resized virtual window");
                Check(shell.PartBrowserWindowGeneration>windowHeld&&shell.PartBrowserWindowGeneration>windowBefore&&shell.PartBrowserWindowCardCount>0&&
                    shell.PartBrowserActionsEnabled&&Math.Abs(scroll.scrollOffset.y-scrollBeforeResize)<246f,
                    "Only the latest complete resized card window publishes, keeps the current scroll position, and enables actions");
            }
            finally
            {
                shell.ResumePartBrowserPreparation();
                Screen.SetResolution(previousWidth,previousHeight,previousFullscreen);
            }
            float restoreResolutionStart=Time.realtimeSinceStartup;
            while((Screen.width!=previousWidth||Screen.height!=previousHeight)&&
                Time.realtimeSinceStartup-restoreResolutionStart<8f)yield return null;
            for(int layoutFrame=0;layoutFrame<4;layoutFrame++)yield return null;
            Check(Screen.width==previousWidth&&Screen.height==previousHeight,
                "Native window restores its original resolution before the following freshness fixture");
            shell.ResumePartBrowserPreparation();
            yield return WaitBoundedPartsReady("latest virtual window after resolution restore");

            // Reach Shop through a real page transition, then request a fresh
            // build on that route. A hold armed before the mouse click can be
            // consumed by the preceding page's pending layout/card work.
            // Arm the hold only after navigation has settled; the retry creates
            // an ordinary builder and never manufactures a captured result.
            if(game.PartsPage!="Installed")yield return Click(input,"partsSource:Installed");
            yield return WaitBoundedPartsReady("Installed before Shop physical freshness");
            if(game.PartsPage!="Shop")yield return Click(input,"partsSource:Shop");
            yield return WaitBoundedPartsReady("Shop before explicit physical-freshness retry");
            shell.RetryPartBrowserPreparation();
            shell.HoldPartBrowserAfterNextStep();
            yield return WaitForPartsHeld("Shop A builder starts with a real captured step");
            shell.ResumePartBrowserPreparation();
            float captureStart=Time.realtimeSinceStartup;string buildState="none";int captured=0,total=0,prepared=0;
            while(Time.realtimeSinceStartup-captureStart<90f&&
                (!BoundedCurrentBuildProgress(out buildState,out captured,out total,out prepared)||
                    buildState!="Preparing"||captured!=total||total<=0||prepared<=0))yield return null;
            bool capturedA=BoundedCurrentBuildProgress(out buildState,out captured,out total,out prepared)&&
                buildState=="Preparing"&&captured==total&&total>0&&prepared>0;
            Check(capturedA,"Shop replacement builder reaches complete A capture and real preparation before the hold / "+
                "state="+buildState+" capture="+captured+"/"+total+" prepared="+prepared);
            if(!capturedA)yield break;
            int heldGeneration=shell.PartBrowserDataGeneration;
            shell.HoldPartBrowserAfterNextStep();
            yield return WaitForPartsHeld("Shop physical freshness after complete A capture");
            bool heldA=BoundedCurrentBuildProgress(out buildState,out captured,out total,out prepared)&&
                buildState=="Preparing"&&captured==total&&total>0&&prepared>0;
            Check(heldA,"Held Shop work still has a complete captured A read set / state="+buildState+
                " capture="+captured+"/"+total+" prepared="+prepared);
            if(!heldA)yield break;
            var selected=game.Profile.SelectedCar;
            double fuelBefore=selected.fuel.litres;
            long revisionBeforeFuelEdit=game.Profile.revision;
            try
            {
                selected.fuel.litres=fuelBefore+.625;
                shell.ResumePartBrowserPreparation();
                float observeBStart=Time.realtimeSinceStartup;
                while(shell.PartBrowserDataGeneration<=heldGeneration&&Time.realtimeSinceStartup-observeBStart<30f)
                    yield return null;
                bool observedB=shell.PartBrowserDataGeneration>heldGeneration&&
                    !shell.PartBrowserActionsEnabled&&shell.PartBrowserPreparationPending&&
                    game.Profile.revision==revisionBeforeFuelEdit;
                Check(observedB,"The complete A read set observes changed physical state B at the next owner-thread fence before restoration / "+
                    "generation="+heldGeneration+"->"+shell.PartBrowserDataGeneration);
                if(!observedB)yield break;
                selected.fuel.litres=fuelBefore;
            }
            finally{selected.fuel.litres=fuelBefore;shell.ResumePartBrowserPreparation();}
            yield return WaitBoundedPartsReady("fresh A retry after observed B");
            Check(shell.PartQuery.Search==""&&shell.PartBrowserActionsEnabled,
                "Fresh retry publishes the current query and re-enables only current cards");

            // Authored catalog fields are equally capable of invalidating a
            // same-revision Shop snapshot. Prove B becomes a complete ready
            // model, then restore A and reject a callback captured from B.
            if(game.PartsPage!="Shop")yield return Click(input,"partsSource:Shop");
            yield return WaitBoundedPartsReady("Shop before authored-price A to B");
            var visiblePriceIds=shell.Root.Q("partsGrid").Query<Button>(className:"part-card").ToList()
                .Select(button=>button.name.Substring("partCard:".Length)).ToHashSet(StringComparer.Ordinal);
            var priceEntry=shell.PartBrowser.Entries.FirstOrDefault(entry=>visiblePriceIds.Contains(entry.Id)&&
                entry.Source=="New"&&entry.Compatibility=="Compatible"&&entry.StockUnits>0);
            if(priceEntry==null)priceEntry=shell.PartBrowser.Entries.FirstOrDefault(entry=>visiblePriceIds.Contains(entry.Id)&&
                entry.Source=="New"&&entry.Compatibility=="Compatible");
            Check(priceEntry!=null,"Current Shop model exposes a compatible authored definition for same-revision price freshness");
            if(priceEntry==null)yield break;
            var priceDefinition=priceEntry.Definition;int authoredPriceA=priceDefinition.price;
            int cashBeforePrice=game.Profile.cash;long revisionBeforePrice=game.Profile.revision;
            int priceGenerationA=shell.PartBrowserDataGeneration;string priceId=priceEntry.Id;
            int authoredPriceB=authoredPriceA<=int.MaxValue-100?authoredPriceA+100:authoredPriceA-100;
            string authoredPriceProfileSnapshot=null;
            yield return CapturePartsProfileDiagnostic(value=>authoredPriceProfileSnapshot=value);
            try
            {
                priceDefinition.price=authoredPriceB;
                var stalePriceCard=shell.CapturePartCardActionForChecks(priceId);
                Check(stalePriceCard!=null,"Accepted visible authored-price card provides its actual stale-action callback");
                stalePriceCard?.Invoke();
                Check(shell.PartBrowserDataGeneration>priceGenerationA&&!shell.PartBrowserActionsEnabled&&
                    game.Profile.cash==cashBeforePrice&&game.Profile.revision==revisionBeforePrice,
                    "Same-revision authored price B rejects the actual prior card callback without staging or payment");
                yield return WaitBoundedPartsReady("Shop complete authored-price B snapshot");
                var readyPriceB=shell.PartBrowser.Entries.FirstOrDefault(entry=>entry.Id==priceId&&entry.Source=="New");
                Check(readyPriceB!=null&&readyPriceB.Definition.price==priceDefinition.price&&
                    readyPriceB.Price!=priceEntry.Price,
                    "A complete new Shop model republishes the same offer from authored price B");
                var cardB=BoundedPartCard("partCard:"+priceId);
                Action staleB=shell.PartBrowserWindowCardCount>0&&cardB!=null?shell.CapturePartCardActionForChecks(priceId):null;
                Check(staleB!=null,"Published price B card exposes its actual callback before restoring A");
                int priceGenerationB=shell.PartBrowserDataGeneration;
                priceDefinition.price=authoredPriceA;
                staleB?.Invoke();
                Check(shell.PartBrowserDataGeneration>priceGenerationB&&!shell.PartBrowserActionsEnabled&&
                    game.Profile.cash==cashBeforePrice&&game.Profile.revision==revisionBeforePrice,
                    "Restoring authored price A invalidates the B card callback before it can stage an action");
            }
            finally{priceDefinition.price=authoredPriceA;}
            yield return WaitBoundedPartsReady("Shop complete authored-price A restoration");
            var readyPriceA=shell.PartBrowser.Entries.FirstOrDefault(entry=>entry.Id==priceId&&entry.Source=="New");
            Check(readyPriceA!=null&&readyPriceA.Definition.price==authoredPriceA&&
                game.Profile.cash==cashBeforePrice&&game.Profile.revision==revisionBeforePrice,
                "Restored authored price A reaches a complete current Shop model without changing profile resources");
            string restoredPriceProfileSnapshot=null;
            yield return CapturePartsProfileDiagnostic(value=>restoredPriceProfileSnapshot=value);
            Check(restoredPriceProfileSnapshot==authoredPriceProfileSnapshot,
                "Authored-price A→B→A invalidation preserves profile IDs, retained condition/wear, fluids, stock, seeds and reservations");
            yield return BoundedPartsPhase("warning-retention",NaturalWarningRetentionChecks(input));

            // Cancel genuine held work by leaving the route; returning builds a
            // fresh model and window rather than exposing its unpublished ticket.
            shell.HoldPartBrowserAfterNextStep();
            yield return TypePartsSearch(input,"piston");
            yield return WaitForPartsHeld("route cancellation query hold");
            yield return BoundedPartsPhase("route-away",Click(input,"navGarage"));
            Check(game.ScreenName=="Garage"&&!shell.PartBrowserActionsEnabled,
                "Navigation away cancels Parts actions while held work is private");
            yield return BoundedPartsPhase("route-return",EnterBuildAndWait(input));

            // Stock changes at the same profile revision invalidate an open native
            // purchase confirmation; it must not stage a cart line from stale data.
            if(game.PartsPage!="Shop")yield return Click(input,"partsSource:Shop");
            yield return WaitBoundedPartsReady("Shop before stale confirmation");
            // A virtual window contains overscan. Scroll to the real top and let
            // its current window publish before choosing an actually visible,
            // hit-testable card. Search helper VK events support plain words,
            // so do not inject an arbitrary product name containing '/' or '-'.
            shell.Root.Q<ScrollView>("partsScroll").scrollOffset=Vector2.zero;
            yield return null;
            yield return WaitBoundedPartsReady("top stocked offer window");
            Check(shell.PartQuery.Search=="","Returning to Build restores the default search before selecting an offer");
            var visibleCards=shell.Root.Q("partsGrid").Query<Button>(className:"part-card").ToList()
                .Where(BoundedPartCardHitTestable).ToList();
            var offer=visibleCards.Select(button=>shell.PartBrowser.Entries.FirstOrDefault(entry=>entry.Id==button.name.Substring("partCard:".Length)))
                .FirstOrDefault(entry=>entry!=null&&entry.Source=="New"&&entry.StockUnits>0&&entry.Compatibility=="Compatible");
            Check(offer!=null,"Current native Shop window has a stocked compatible offer for stale-confirmation coverage");
            var card=BoundedPartCard("partCard:"+offer.Id);
            Check(card!=null,"Current hit-testable offer still has its accepted card");
            var cardRect=card.worldBound;var viewportRect=shell.Root.Q<ScrollView>("partsScroll").contentViewport.worldBound;
            var picked=shell.Root.panel?.Pick(cardRect.center);
            bool hitCard=picked!=null&&(ReferenceEquals(picked,card)||card.Contains(picked));
            int rejectedBeforeOpen=shell.PartActionGuardRejectedCount,openDataBefore=shell.PartBrowserDataGeneration;
            double detailMs0=shell.PartDetailBuildMilliseconds,openReadyMs0=shell.PartDetailOpenToReadyMilliseconds,
                evaluationMs0=shell.PartDetailEvaluationMilliseconds,factsMs0=shell.PartDetailFactsMilliseconds,
                actionsMs0=shell.PartDetailActionsMilliseconds,statsMs0=shell.PartDetailStatsMilliseconds,
                attachMs0=shell.PartDetailAttachMilliseconds,publishMs0=shell.PartDetailPublishMilliseconds,
                layoutWaitMs0=shell.PartDetailLayoutWaitMilliseconds,healthyMs0=shell.PartDetailHealthyMilliseconds,
                priceMs0=shell.PartDetailPriceMilliseconds;
            int layoutWaitFrames0=shell.PartDetailLayoutWaitFrames;
            report.AppendLine("CARD_OPEN_BEFORE,id="+offer.Id+",rect="+cardRect+",viewport="+viewportRect+
                ",centerVisible="+viewportRect.Contains(cardRect.center)+",picked="+(picked?.name??"null")+
                ",enabled="+card.enabledInHierarchy+",data="+openDataBefore+",window="+shell.PartBrowserWindowGeneration);
            Check(card.enabledInHierarchy&&viewportRect.Contains(cardRect.center)&&
                viewportRect.Contains(cardRect.min+Vector2.one*.1f)&&viewportRect.Contains(cardRect.max-Vector2.one*.1f)&&hitCard,
                "Stocked offer's actual card is enabled, fully inside its viewport and receives the center hit test");
            yield return BoundedPartsPhase("offer-detail-open",Click(input,card));
            yield return WaitBoundedDetailReady("real card open to layout-ready",offer.Id);
            report.AppendLine("CARD_OPEN_AFTER,detail="+shell.PartBrowserDetailId+",data="+shell.PartBrowserDataGeneration+
                ",rejected="+(shell.PartActionGuardRejectedCount-rejectedBeforeOpen)+",state="+shell.PartBrowserPreparationState+
                ",pending="+shell.PartBrowserPreparationPending+",notice="+game.NativeNotice+
                ",buildCpuMs="+(shell.PartDetailBuildMilliseconds-detailMs0).ToString("F3")+
                ",openToReadyMs="+(shell.PartDetailOpenToReadyMilliseconds-openReadyMs0).ToString("F3")+
                ",evaluationMs="+(shell.PartDetailEvaluationMilliseconds-evaluationMs0).ToString("F3")+
                ",factsMs="+(shell.PartDetailFactsMilliseconds-factsMs0).ToString("F3")+
                ",actionsMs="+(shell.PartDetailActionsMilliseconds-actionsMs0).ToString("F3")+
                ",statsMs="+(shell.PartDetailStatsMilliseconds-statsMs0).ToString("F3")+
                ",attachMs="+(shell.PartDetailAttachMilliseconds-attachMs0).ToString("F3")+
                ",publishMs="+(shell.PartDetailPublishMilliseconds-publishMs0).ToString("F3")+
                ",layoutWaitMs="+(shell.PartDetailLayoutWaitMilliseconds-layoutWaitMs0).ToString("F3")+
                ",layoutWaitFrames="+(shell.PartDetailLayoutWaitFrames-layoutWaitFrames0)+
                ",healthyWithinStatsMs="+(shell.PartDetailHealthyMilliseconds-healthyMs0).ToString("F3")+
                ",purchasePriceMs="+(shell.PartDetailPriceMilliseconds-priceMs0).ToString("F3"));
            Check(shell.Root.Q("partsDetail")!=null&&!shell.Root.Q("partsDetail").ClassListContains("hidden"),
                "Native offer card opens its detail from the accepted window");
            var partsReturnOffset=shell.Root.Q<ScrollView>("partsScroll").scrollOffset;
            var sameIdStaleCallback=shell.CapturePartDetailActionForChecks();
            int sameIdCartBefore=game.CartCount,sameIdCashBefore=game.Profile.cash;long sameIdRevisionBefore=game.Profile.revision;
            int sameIdOpenBefore=BoundedDetailOpenGeneration();
            Check(sameIdStaleCallback!=null,"Current detail provides its exact production purchase callback before closing");
            yield return BoundedPartsPhase("offer-detail-back",Click(input,"partBack"));
            yield return WaitBoundedPartsReady("close detail before same-ID reopen");
            yield return BoundedPartsPhase("same-offer-detail-reopen",Click(input,BoundedPartCard("partCard:"+offer.Id)));
            yield return WaitBoundedDetailReady("reopen same offer ID",offer.Id);
            int sameIdReopenedData=shell.PartBrowserDataGeneration;
            int sameIdOpenAfter=BoundedDetailOpenGeneration();
            Check(sameIdOpenBefore>=0&&sameIdOpenAfter>sameIdOpenBefore,
                "Opening the same detail ID again advances the monotonic detail-origin generation / "+sameIdOpenBefore+"->"+sameIdOpenAfter);
            sameIdStaleCallback?.Invoke();
            Check(shell.PartBrowserDataGeneration>sameIdReopenedData&&!shell.PartBrowserDetailCurrent&&
                !shell.ModalOpen&&game.CartCount==sameIdCartBefore&&game.Profile.cash==sameIdCashBefore&&
                game.Profile.revision==sameIdRevisionBefore,
                "A callback captured before closing rejects after reopening the same ID and cannot open a modal or stage purchase");
            yield return WaitBoundedDetailReady("same-ID stale callback recovery",offer.Id);
            offer=shell.PartBrowser.Entries.First(entry=>entry.Id==offer.Id&&entry.Source=="New");
            string offerDetailShape=BoundedDetailShape();int detailGenerationBeforeStaleModal=shell.PartBrowserDataGeneration;
            yield return Click(input,"partAdd");
            Check(shell.ModalOpen&&shell.Root.Q("dialogAddtocart")!=null,"Native purchase confirmation is open");
            yield return PartDialogClick(input,"partPurchaseDestination:Store");
            var offerStock=PartsMarket.Stock(game.Profile,offer.Definition);
            Check(offerStock!=null,"Stale purchase fixture resolves the current authoritative stock row");
            int stockBefore=offerStock.units,priceBefore=offer.Definition.price;
            int cashBefore=game.Profile.cash,cartBefore=game.CartCount;long revisionBefore=game.Profile.revision;
            try
            {
                offerStock.units=stockBefore+1;
                offer.Definition.price=priceBefore+1;
                long consumerJsonBefore=PartConsumerEncoderProbe.PartConsumerJsonCallCount;
                long consumerKeyBefore=PartConsumerEncoderProbe.PartConsumerLegacyKeyCallCount;
                string rawEncoderBefore=NativeEncoderMark();
                yield return BoundedPartsPhase("stale-confirmation",Click(input,"dialogAddtocart"));
                long consumerJsonAfter=PartConsumerEncoderProbe.PartConsumerJsonCallCount;
                long consumerKeyAfter=PartConsumerEncoderProbe.PartConsumerLegacyKeyCallCount;
                string rawEncoderAfter=NativeEncoderMark();
                Check(game.CartCount==cartBefore&&game.Profile.cash==cashBefore&&game.Profile.revision==revisionBefore,
                    "Same-revision stock and authored-price changes prevent stale purchase confirmation from staging or charging");
                Check(shell.ModalOpen&&shell.Root.Q<Button>("partPurchaseDestination:Store").ClassListContains("selected")&&
                    shell.Root.Q<FloatField>("partQuantity").value==1&&
                    !string.IsNullOrWhiteSpace(shell.Root.Q<Label>("partPurchaseNotice")?.text),
                    "Stale confirmation rejection keeps the user's modal destination/quantity draft and visible notice");
                Check(consumerJsonAfter==consumerJsonBefore&&consumerKeyAfter==consumerKeyBefore,
                    "Stale confirmation interval performs no converted-consumer JSON or legacy-key calls / json="+
                    consumerJsonBefore+"->"+consumerJsonAfter+" key="+consumerKeyBefore+"->"+consumerKeyAfter);
                report.AppendLine("PARTS_ENCODER_INTERVAL,case=stale-confirmation,consumerJson="+
                    (consumerJsonAfter-consumerJsonBefore)+",consumerLegacyKey="+(consumerKeyAfter-consumerKeyBefore)+
                    ",rawEncoderBefore="+rawEncoderBefore+",rawEncoderAfter="+rawEncoderAfter);
            }
            finally{offerStock.units=stockBefore;offer.Definition.price=priceBefore;}
            if(shell.ModalOpen)
            {
                for(int frame=0;frame<4;frame++)yield return null;
                Check(shell.ModalOpen&&!shell.PartBrowserDetailCurrent&&
                    shell.Root.Q<Button>("partPurchaseDestination:Store").ClassListContains("selected")&&
                    shell.Root.Q<FloatField>("partQuantity").value==1,
                    "Stale confirmation keeps its draft mounted and blocks stale detail actions until the modal closes");
                Check(shell.PartBrowserDataGeneration>detailGenerationBeforeStaleModal,
                    "Stale modal action invalidates the old detail data generation");
                yield return Click(input,"dialogCancel");
            }
            yield return WaitBoundedDetailReady("detail recovery after closing stale modal",offer.Id);
            Check(BoundedDetailShape()==offerDetailShape,
                "Modal dismissal rebuilds the same open detail with its dyno and stats panels retained");

            // A stale detail action must fail synchronously against current facts.
            // Hold the genuine replacement builder after it consumed B, restore A
            // only then, and require the stale ticket to be replaced by a fresh
            // build that republishes this same detail.
            int cashOriginal=game.Profile.cash;
            yield return RejectEditedDetailAndRebuild(input,"detail-cash-staleness",offer.Id,
                ()=>game.Profile.cash=cashOriginal+1,()=>game.Profile.cash=cashOriginal);
            stockBefore=offerStock.units;
            yield return RejectEditedDetailAndRebuild(input,"detail-stock-staleness",offer.Id,
                ()=>offerStock.units=stockBefore+1,()=>offerStock.units=stockBefore);
            var detailSelectedCar=game.Profile.SelectedCar;double detailFuel=detailSelectedCar.fuel.litres;
            yield return RejectEditedDetailAndRebuild(input,"detail-physical-staleness",offer.Id,
                ()=>detailSelectedCar.fuel.litres=detailFuel+.625,()=>detailSelectedCar.fuel.litres=detailFuel);
            Check(game.Profile.revision==revisionBefore&&game.CartCount==cartBefore&&game.Profile.cash==cashOriginal&&
                offerStock.units==stockBefore&&Math.Abs(detailSelectedCar.fuel.litres-detailFuel)<.000001,
                "Detail freshness probes restore only their in-memory fixture fields and do not stage or heal profile state");
            yield return Click(input,"partBack");
            Check(Math.Abs(shell.Root.Q<ScrollView>("partsScroll").scrollOffset.y-partsReturnOffset.y)<.1f,
                "Returning from refreshed detail restores the original card-list scroll position");
            yield return WaitBoundedPartsReady("purchase and detail fences recover after restoring fixtures");

            yield return AttachedPartWindowLifecycleChecks(input);

            // F8 hides the native view while a real step is held. Reopening returns
            // through the normal route and must wait for a fresh actionable window.
            shell.HoldPartBrowserAfterNextStep();
            yield return TypePartsSearch(input,"tire");
            yield return WaitForPartsHeld("F8 cancellation hold");
            // Leave the real search field before exercising the guarded shortcut.
            // The native parts controls are mouse-only, so a click on the count
            // label is the real pointer transition; explicitly blur only if the
            // platform focus controller retains the TextField after that click.
            var partsSearch=shell.Root.Q<TextField>("partsSearch");
            yield return Click(input,shell.Root.Q<Label>("partsCount"));
            yield return null;
            var focusedSearch=FocusedSearchElement(partsSearch);
            bool explicitBlur=focusedSearch!=null;
            if(explicitBlur){focusedSearch.Blur();yield return null;}
            Check(!SearchFieldHasKeyboardFocus(partsSearch),
                "Real pointer departure or the field's Blur seam clears search focus before F8");
            report.AppendLine("PARTS_F8_FOCUS,realClick=partsCount,explicitBlur="+explicitBlur);
            yield return BoundedPartsPhase("f8-hide",Key(input,0x77));
            Check(!shell.Visible&&!shell.PartBrowserActionsEnabled,
                "F8 hides native Parts and its held card actions are not usable");
            yield return BoundedPartsPhase("f8-restore",Key(input,0x77));
            Check(shell.Visible,"F8 restores the native interface");
            yield return WaitBoundedPartsReady("F8 route recovery");
            yield return CaptureBoundedParts("final-actionable");
        }

        VisualElement FocusedSearchElement(TextField field)
        {
            var focused=shell.Root?.focusController?.focusedElement as VisualElement;
            return field!=null&&focused!=null&&(ReferenceEquals(focused,field)||field.Contains(focused))?focused:null;
        }
        bool SearchFieldHasKeyboardFocus(TextField field)=>FocusedSearchElement(field)!=null;
#endif
    }
}
