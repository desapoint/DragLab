#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal static class NativeCartPreviewCandidateProbe
    {
        internal sealed class Snapshot
        {
            internal readonly string Rows,SourceFacts,Entropy,AssetId,DefinitionId;
            internal readonly UnityEngine.Random.State Random;
            internal readonly long JsonCalls,Started;
            internal readonly bool Capture;
            internal readonly DateTime Clock;
            internal Snapshot(string rows,string sourceFacts,string entropy,string assetId,string definitionId,
                UnityEngine.Random.State random,long jsonCalls,long started,bool capture,DateTime clock)
            {Rows=rows;SourceFacts=sourceFacts;Entropy=entropy;AssetId=assetId;DefinitionId=definitionId;Random=random;JsonCalls=jsonCalls;Started=started;Capture=capture;Clock=clock;}
        }
        internal static Action<string> Completed;
        static int diagnosticFaultCount;
        static string lastDiagnosticFault;
        internal static int DiagnosticFaultCount=>Volatile.Read(ref diagnosticFaultCount);
        internal static string LastDiagnosticFault=>lastDiagnosticFault;

        internal static Snapshot Begin(PlayerProfile profile,IEnumerable<PartCartLine> rows,string assetId,string definitionId)
        {
            if(Completed==null)return null;
            try
            {
                string rowFacts=NativeMenuVerification.CartPreviewRows(rows);
                string sourceFacts=NativeMenuVerification.CartPreviewSourceFacts(profile,assetId,definitionId);
                string entropy=NativeMenuVerification.CartPreviewEntropySequences();
                return new Snapshot(rowFacts,sourceFacts,entropy,assetId,definitionId,UnityEngine.Random.state,
                    RuntimeDataEncodingProbe.CurrentThreadJsonCallCount,Stopwatch.GetTimestamp(),RuntimeEntropy.Capture,RuntimeEntropy.UtcNow);
            }
            catch(Exception error){RecordFault(error);return null;}
        }

        internal static void Complete(Snapshot snapshot,PlayerProfile profile,PlayerProfile candidate,Exception error)
        {
            if(snapshot==null||Completed==null)return;
            try
            {
                double milliseconds=(Stopwatch.GetTimestamp()-snapshot.Started)*1000.0/Stopwatch.Frequency;
                string candidateFacts=candidate==null?"<no-candidate>":NativeMenuVerification.CartPreviewSourceFacts(
                    candidate,snapshot.AssetId,snapshot.DefinitionId);
                string sourceAfter=NativeMenuVerification.CartPreviewSourceFacts(profile,snapshot.AssetId,snapshot.DefinitionId);
                string entropyAfter=NativeMenuVerification.CartPreviewTryEntropySequences(out var sequenceSnapshotAfter)?sequenceSnapshotAfter:"<unavailable>";
                long jsonDelta=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount-snapshot.JsonCalls;
                bool randomUnchanged=snapshot.Random.Equals(UnityEngine.Random.state);
                DateTime clockAfter=RuntimeEntropy.UtcNow;
                long requiredCaptureTicks=new DateTime(2026,10,16,12,0,0,DateTimeKind.Utc).Ticks;
                bool clockUnchanged=snapshot.Capture&&RuntimeEntropy.Capture&&snapshot.Clock.Ticks==requiredCaptureTicks&&
                    snapshot.Clock.Kind==DateTimeKind.Utc&&snapshot.Clock.Ticks==clockAfter.Ticks&&snapshot.Clock.Kind==clockAfter.Kind;
                bool sequenceUnchanged=snapshot.Entropy!="<unavailable>"&&entropyAfter!="<unavailable>"&&snapshot.Entropy==entropyAfter;
                bool sourceUnchanged=snapshot.SourceFacts==sourceAfter;
                if(!sourceUnchanged)RecordFinding("serialized profile source changed during candidate evaluation");
                if(jsonDelta!=0)RecordFinding("JSON counter delta="+jsonDelta.ToString(CultureInfo.InvariantCulture));
                if(!randomUnchanged)RecordFinding("UnityEngine.Random state changed during candidate evaluation");
                if(!clockUnchanged)RecordFinding("RuntimeEntropy capture clock was unavailable or changed");
                if(!sequenceUnchanged)RecordFinding("RuntimeEntropy sequence snapshot was unavailable or changed");
                string result="CART_PREVIEW_CANDIDATE_SCOPE|rows="+snapshot.Rows+"|input="+snapshot.SourceFacts+
                    "->"+sourceAfter+
                    "|candidateAsset="+candidateFacts+"|selected="+(candidate?.SelectedCar?.instanceId??"<null>")+
                    "|jsonDelta="+jsonDelta+"|sourceUnchanged="+sourceUnchanged+
                    "|unityRandomUnchanged="+randomUnchanged+"|capture="+snapshot.Capture+"->"+RuntimeEntropy.Capture+
                    "|clock="+NativeMenuVerification.CartPreviewClock(snapshot.Clock)+"->"+
                    NativeMenuVerification.CartPreviewClock(clockAfter)+"|clockUnchanged="+clockUnchanged+
                    "|entropy="+snapshot.Entropy+"->"+entropyAfter+"|sequenceUnchanged="+sequenceUnchanged+
                    "|elapsedMs="+milliseconds.ToString("F3",CultureInfo.InvariantCulture)+"|exception="+
                    (error==null?"<none>":error.GetType().FullName+" | "+error.Message);
                Interlocked.Increment(ref completedCount);
                Completed?.Invoke(result);
            }
            catch(Exception diagnosticError){RecordFault(diagnosticError);}
        }

        static int findingCount;
        static string lastFinding;
        static int completedCount;
        internal static int FindingCount=>Volatile.Read(ref findingCount);
        internal static string LastFinding=>lastFinding;
        internal static int CompletedCount=>Volatile.Read(ref completedCount);

        static void RecordFinding(string finding)
        {
            try
            {
                string safe=finding??"<null finding>";
                if(safe.Length>512)safe=safe.Substring(0,512);
                lastFinding=safe;
                Interlocked.Increment(ref findingCount);
            }
            catch{RecordFault(null);}
        }

        static void RecordFault(Exception error)
        {
            string safe="Diagnostic exception unavailable.";
            try
            {
                string type="<null>";
                string message="<null>";
                if(error!=null)
                {
                    try{type=error.GetType().FullName??"<unnamed exception>";}catch{type="<exception type unavailable>";}
                    try{message=error.Message??"<null>";}catch{message="<exception message accessor threw>";}
                }
                if(type.Length>256)type=type.Substring(0,256);
                if(message.Length>512)message=message.Substring(0,512);
                safe=type+" | "+message;
            }
            catch{}
            try{lastDiagnosticFault=safe;}catch{}
            Interlocked.Increment(ref diagnosticFaultCount);
        }
    }

    internal sealed partial class NativeMenuVerification
    {
        IEnumerator CartPreviewCharacterizationChecks(WindowsKeyboardProbe input)
        {
            var previousCandidateObserver=NativeCartPreviewCandidateProbe.Completed;
            NativeCartPreviewCandidateProbe.Completed=detail=>report.AppendLine(detail);
            int candidateProbeFaultsBefore=NativeCartPreviewCandidateProbe.DiagnosticFaultCount;
            int candidateProbeFindingsBefore=NativeCartPreviewCandidateProbe.FindingCount;
            int candidateProbeCompletionsBefore=NativeCartPreviewCandidateProbe.CompletedCount;
            bool cartCleanupAuthorized=false;
            try
            {
            var profile=game.Profile;
            bool suiteOk=true;
            string startRows=CartPreviewRows(game.CartLines);
            report.AppendLine("CART_PREVIEW_NATIVE|begin|capture="+RuntimeEntropy.Capture+"|utc="+
                RuntimeEntropy.UtcNow.ToString("O",CultureInfo.InvariantCulture)+"|screen="+game.ScreenName+
                "|selected="+(profile.SelectedCar?.instanceId??"<null>")+"|revision="+profile.revision+
                "|rows="+startRows+"|entropy="+CartPreviewEntropySequences());
            Check(RuntimeEntropy.Capture,"Cart preview native characterization uses deterministic isolated RuntimeEntropy capture mode");
            if(game.CartCount!=0)
            {
                report.AppendLine("OPEN|cart-preview-native|isolated native profile starts with a nonempty cart; refusing to clear preexisting rows.");
                Check(false,"Cart preview characterization starts with an empty isolated cart");
                yield break;
            }
            cartCleanupAuthorized=true;

            if(!shell.Visible)yield return Key(input,0x77);
            if(game.ScreenName!="Parts")yield return Click(input,"navBuild");
            yield return WaitParts();
            if(game.PartsPage!="Shop"){yield return Click(input,"partsSource:Shop");yield return WaitParts();}

            var car=profile.SelectedCar;
            if(car==null)
            {
                report.AppendLine("OPEN|cart-preview-native|no selected owned car for a real inventory Move fixture.");
                Check(false,"Cart preview characterization requires an actual selected car");
                yield break;
            }
            var selectedCarSpec=profile.catalog.Resolve(car.vehicleId,MechanicsOwnership.Definitions(profile,car),car.tune).spec;
            var definitions=profile.catalog.parts.Where(definition=>definition!=null&&definition.price>=0&&
                    definition.category!=PartCategory.CylinderBlock&&PartsMarket.FitsAny(profile,definition)&&
                    profile.reputation>=definition.reputationRequired)
                .Select(definition=>{
                    int count=PartsMarket.Count(profile,car,definition);
                    var stock=profile.inventory.market.parts.FirstOrDefault(row=>row!=null&&row.definitions!=null&&row.definitions.Contains(definition.id));
                    bool fitsSelectedCar=profile.catalog.Fits(selectedCarSpec,definition);
                    var quote=fitsSelectedCar&&count>0&&stock!=null?PartsMarket.Quote(profile,definition,count,unit:false,quantity:3):null;
                    return new{Definition=definition,Count=count,Stock=stock,Quote=quote,FitsSelectedCar=fitsSelectedCar};
                })
                .Where(candidate=>candidate.Count>0&&candidate.Stock!=null&&candidate.Definition.price>=0&&candidate.Quote!=null&&candidate.FitsSelectedCar)
                .OrderBy(candidate=>candidate.Quote.expressUnits==0?0:1).ThenBy(candidate=>candidate.Quote.cost)
                .ThenBy(candidate=>candidate.Definition.id,StringComparer.Ordinal)
                .ToArray();
            var fixtureDefinition=definitions.FirstOrDefault(candidate=>profile.cash>=candidate.Quote.cost);
            if(fixtureDefinition==null)
            {
                var unaffordable=definitions.FirstOrDefault();
                if(unaffordable==null)
                {
                    report.AppendLine("OPEN|cart-preview-native|no actual authored compatible market row has a valid quote for three isolated domain purchases.");
                    Check(false,"A real authored market item can be quoted for three Move characterization fixture purchases");
                }
                else
                {
                    report.AppendLine("OPEN|cart-preview-native|isolated profile cannot afford the exact three-purchase quote;cash="+profile.cash+
                        ";required="+unaffordable.Quote.cost+";local="+unaffordable.Quote.localUnits+";express="+unaffordable.Quote.expressUnits+
                        ";fee="+unaffordable.Quote.fee+";definition="+unaffordable.Definition.id);
                    Check(false,"Isolated profile funds the exact three-purchase Move fixture quote");
                }
                yield break;
            }

            string setupEntropyBefore=CartPreviewEntropySequences();
            int setupCashBefore=profile.cash;long setupRevisionBefore=profile.revision;
            var setupStockBefore=CartPreviewStockFacts(profile,fixtureDefinition.Definition.id);
            string[] fixturePartIds=new string[2];
            for(int i=0;i<fixturePartIds.Length;i++)
                fixturePartIds[i]=PartOperations.Buy(profile,profile.catalog,car.instanceId,fixtureDefinition.Definition.id,false);
            profile.revision=checked(profile.revision+1);
            report.AppendLine("CART_PREVIEW_NATIVE|fixture-setup|operation=PartOperations.Buy;definition="+
                fixtureDefinition.Definition.id+";count="+fixtureDefinition.Count+";ids="+string.Join(",",fixturePartIds)+
                ";selectedCar="+car.instanceId+";selectedVehicle="+car.vehicleId+";fitsSelectedBuild="+fixtureDefinition.FitsSelectedCar+
                ";threePurchaseQuote=local:"+fixtureDefinition.Quote.localUnits+",express:"+fixtureDefinition.Quote.expressUnits+
                ",fee:"+fixtureDefinition.Quote.fee+",cost:"+fixtureDefinition.Quote.cost+
                ";cash="+setupCashBefore+"->"+profile.cash+";revision="+setupRevisionBefore+"->"+profile.revision+
                ";stock="+setupStockBefore+"->"+CartPreviewStockFacts(profile,fixtureDefinition.Definition.id)+
                ";entropy="+setupEntropyBefore+"->"+CartPreviewEntropySequences());
            shell.Refresh();
            yield return WaitParts();
            if(game.PartsPage!="Inventory"){yield return Click(input,"partsSource:Inventory");yield return WaitParts();}

            var first=profile.parts.FirstOrDefault(part=>part!=null&&part.instanceId==fixturePartIds[0]);
            var second=profile.parts.FirstOrDefault(part=>part!=null&&part.instanceId==fixturePartIds[1]);
            if(first==null||second==null)
            {
                report.AppendLine("OPEN|cart-preview-native|domain-created real inventory parts did not remain available by identity.");
                Check(false,"Two actual bought spare parts are present for the UI Move callbacks");
                yield break;
            }

            yield return OpenCartPreviewOwnedPart(input,fixturePartIds[0],"native empty-cart cancel");
            string beforeCancel=CartPreviewSourceFacts(profile,fixturePartIds[0],fixtureDefinition.Definition.id);
            string rowsBeforeCancel=CartPreviewRows(game.CartLines);
            var randomBeforeCancel=UnityEngine.Random.state;
            long jsonBeforeCancel=RuntimeDataEncodingProbe.PartConsumerJsonCallCount;
            string entropyBeforeCancel=CartPreviewEntropySequences();
            int rejectedBeforeCancel=shell.PartActionGuardRejectedCount;
            yield return Click(input,"partMove");
            bool opened=shell.ModalOpen&&shell.Root.Q<Label>("modalTitle")?.text=="Move part";
            report.AppendLine("CART_PREVIEW_NATIVE|cancel-open|opened="+opened+"|origin=native-guarded-detail|"+
                CartPreviewObservation(profile,fixturePartIds[0],fixtureDefinition.Definition.id,beforeCancel,rowsBeforeCancel,CartPreviewRows(game.CartLines),
                    randomBeforeCancel,jsonBeforeCancel,entropyBeforeCancel));
            if(!opened)
            {
                suiteOk=false;
                report.AppendLine("FAIL|cart-preview-native|actual native partMove button did not open the Move modal.");
            }
            else
            {
                var storageTarget=shell.Root.Q<ScrollView>("modalContent")?.Query<Button>().ToList()
                    .FirstOrDefault(button=>button.name.StartsWith("inventoryMoveTarget:Storage:",StringComparison.Ordinal));
                if(storageTarget==null)
                {
                    suiteOk=false;
                    report.AppendLine("FAIL|cart-preview-native|actual Move dialog exposed no Storage destination.");
                    yield return Click(input,"dialogCancel");
                }
                else
                {
                    yield return PartDialogClick(input,storageTarget.name);
                    bool addEnabled=shell.Root.Q<Button>("dialogAddmove")?.enabledInHierarchy==true;
                    string preview=CartPreviewDialogFacts();
                    bool sourceUnchanged=beforeCancel==CartPreviewSourceFacts(profile,fixturePartIds[0],fixtureDefinition.Definition.id);
                    report.AppendLine("CART_PREVIEW_NATIVE|cancel-destination|target="+storageTarget.name+"|addEnabled="+addEnabled+
                        "|candidateRows="+CartPreviewMoveCandidateRows(game.CartLines,fixturePartIds[0],fixtureDefinition.Definition.id,
                            InventoryKind.Storage,SharedInventory.Garage)+"|sourceUnchanged="+sourceUnchanged+
                        "|preview="+preview+"|"+CartPreviewObservation(profile,fixturePartIds[0],fixtureDefinition.Definition.id,
                            beforeCancel,rowsBeforeCancel,CartPreviewRows(game.CartLines),randomBeforeCancel,jsonBeforeCancel,entropyBeforeCancel));
                    if(!addEnabled||!sourceUnchanged)
                    {
                        suiteOk=false;
                        if(!sourceUnchanged)report.AppendLine("FAIL|cart-preview-native|candidate destination preview changed captured source facts; legacy JSON mutation remains strict-red.");
                    }
                    yield return Click(input,"dialogCancel");
                    bool canceled=!shell.ModalOpen&&CartPreviewRows(game.CartLines)==rowsBeforeCancel;
                    report.AppendLine("CART_PREVIEW_NATIVE|cancel-result|canceled="+canceled+"|rows="+CartPreviewRows(game.CartLines)+
                        "|"+CartPreviewObservation(profile,fixturePartIds[0],fixtureDefinition.Definition.id,beforeCancel,rowsBeforeCancel,
                            CartPreviewRows(game.CartLines),
                            randomBeforeCancel,jsonBeforeCancel,entropyBeforeCancel));
                    if(!canceled)suiteOk=false;
                }
            }
            if(shell.ModalOpen)yield return Click(input,"dialogCancel");
            if(shell.PartActionGuardRejectedCount!=rejectedBeforeCancel)
                report.AppendLine("CART_PREVIEW_NATIVE|cancel-guard|rejectedDelta="+(shell.PartActionGuardRejectedCount-rejectedBeforeCancel)+
                    ";reason="+shell.PartActionLastRejectedReasonForChecks);
            yield return ResetCartPreviewBrowser(input,"cancel");
            // Invoke the exact currently bound Move callback after staling its origin;
            // this proves execution of its guard even if refresh disables the button.
            yield return OpenCartPreviewOwnedPart(input,fixturePartIds[0],"native stale origin");
            double oldFuel=car.fuel.litres;
            var staleMoveCallback=shell.CapturePartDetailActionForChecks("partMove");
            int staleOriginRejected=shell.PartActionGuardRejectedCount;
            car.fuel.litres=oldFuel+.625;
            staleMoveCallback?.Invoke();
            bool originRejected=staleMoveCallback!=null&&shell.PartActionGuardRejectedCount==staleOriginRejected+1&&!shell.ModalOpen;
            report.AppendLine("CART_PREVIEW_NATIVE|stale-origin|dispatch=captured-real-bound-callback|callbackPresent="+
                (staleMoveCallback!=null)+"|rejected="+originRejected+"|delta="+
                (shell.PartActionGuardRejectedCount-staleOriginRejected)+"|reason="+shell.PartActionLastRejectedReasonForChecks+
                "|asset="+fixturePartIds[0]+"|fuel="+oldFuel.ToString("R",CultureInfo.InvariantCulture)+"->"+
                car.fuel.litres.ToString("R",CultureInfo.InvariantCulture));
            car.fuel.litres=oldFuel;
            if(!originRejected)suiteOk=false;
            yield return ResetCartPreviewBrowser(input,"stale origin restored");

            // Revision is mutated only in this isolated test profile, then restored exactly.
            yield return OpenCartPreviewOwnedPart(input,fixturePartIds[0],"native stale revision");
            yield return Click(input,"partMove");
            var revisionTarget=shell.Root.Q<ScrollView>("modalContent")?.Query<Button>().ToList()
                .FirstOrDefault(button=>button.name.StartsWith("inventoryMoveTarget:Storage:",StringComparison.Ordinal));
            bool revisionRejected=false;
            if(revisionTarget!=null)
            {
                yield return PartDialogClick(input,revisionTarget.name);
                long oldRevision=profile.revision;
                profile.revision=checked(profile.revision+1);
                string revisionRows=CartPreviewRows(game.CartLines);
                int revisionGuardBefore=shell.PartActionGuardRejectedCount;
                yield return Click(input,"dialogAddmove");
                revisionRejected=shell.ModalOpen&&CartPreviewRows(game.CartLines)==revisionRows&&
                    shell.Root.Q<Label>("inventoryMoveError")!=null&&
                    !string.IsNullOrWhiteSpace(shell.Root.Q<Label>("inventoryMoveError").text);
                report.AppendLine("CART_PREVIEW_NATIVE|stale-revision|rejected="+revisionRejected+"|captured="+oldRevision+
                    "|current="+profile.revision+"|guardDelta="+(shell.PartActionGuardRejectedCount-revisionGuardBefore)+
                    "|reason="+shell.PartActionLastRejectedReasonForChecks+"|error="+
                    (shell.Root.Q<Label>("inventoryMoveError")?.text??"<none>")+"|rows="+revisionRows);
                profile.revision=oldRevision;
            }
            else report.AppendLine("OPEN|cart-preview-native|stale-revision Move dialog had no actual Storage target.");
            if(!revisionRejected)suiteOk=false;
            if(shell.ModalOpen)yield return Click(input,"dialogCancel");
            yield return ResetCartPreviewBrowser(input,"stale revision restored");

            // A third real item lets the stale-cart insertion remain independent of the later mixed-cart rows.
            int thirdPurchaseCash=profile.cash;long thirdPurchaseRevision=profile.revision;
            string thirdPurchaseStock=CartPreviewStockFacts(profile,fixtureDefinition.Definition.id);
            string staleCartPartId=PartOperations.Buy(profile,profile.catalog,car.instanceId,fixtureDefinition.Definition.id,false);
            profile.revision=checked(profile.revision+1);
            report.AppendLine("CART_PREVIEW_NATIVE|fixture-setup-third|operation=PartOperations.Buy;id="+staleCartPartId+
                ";cash="+thirdPurchaseCash+"->"+profile.cash+";revision="+thirdPurchaseRevision+"->"+profile.revision+
                ";stock="+thirdPurchaseStock+"->"+CartPreviewStockFacts(profile,fixtureDefinition.Definition.id)+
                ";entropy="+CartPreviewEntropySequences());
            shell.Refresh();yield return WaitParts();
            if(game.PartsPage!="Inventory"){yield return Click(input,"partsSource:Inventory");yield return WaitParts();}
            yield return OpenCartPreviewOwnedPart(input,fixturePartIds[0],"native stale cart");
            yield return Click(input,"partMove");
            var cartTarget=shell.Root.Q<ScrollView>("modalContent")?.Query<Button>().ToList()
                .FirstOrDefault(button=>button.name.StartsWith("inventoryMoveTarget:Storage:",StringComparison.Ordinal));
            bool staleCartRejected=false;
            if(cartTarget!=null)
            {
                yield return PartDialogClick(input,cartTarget.name);
                var injected=new PartCartLine{action=CartAction.Move,assetId=staleCartPartId,
                    definitionId=fixtureDefinition.Definition.id,destination=InventoryKind.Storage,destinationId=SharedInventory.Garage};
                game.StageInventory(injected);
                var addMoveButton=shell.Root.Q<Button>("dialogAddmove");
                bool addMoveEnabledBefore=addMoveButton!=null&&addMoveButton.enabledInHierarchy;
                var staleMoveError=shell.Root.Q<Label>("inventoryMoveError");
                string errorBefore=staleMoveError?.text??"<missing>";
                int cartRejected=shell.PartActionGuardRejectedCount;
                if(addMoveButton!=null)yield return Click(input,addMoveButton);
                string errorAfter=staleMoveError?.text??"<missing>";
                const string expectedCartStaleReason="This parts view has changed. Refresh it before continuing.";
                bool callbackRejected=addMoveEnabledBefore&&string.IsNullOrWhiteSpace(errorBefore)&&errorAfter==expectedCartStaleReason&&
                    shell.PartActionLastRejectedReasonForChecks==expectedCartStaleReason;
                staleCartRejected=callbackRejected&&shell.ModalOpen&&game.CartLines.Any(line=>line.assetId==staleCartPartId)&&
                    !game.CartLines.Any(line=>line.assetId==fixturePartIds[0]);
                report.AppendLine("CART_PREVIEW_NATIVE|stale-cart|rejected="+staleCartRejected+"|guardDelta="+
                    (shell.PartActionGuardRejectedCount-cartRejected)+"|callback=rendered-addmove|buttonEnabledBefore="+addMoveEnabledBefore+
                    "|error="+errorBefore+"->"+errorAfter+"|classification=dialog-origin-current-check|reason="+shell.PartActionLastRejectedReasonForChecks+
                    "|rows="+CartPreviewRows(game.CartLines)+"|reservation="+CartPreviewCanonical(profile.inventory.cart));
                game.RemoveNativeCartLine(game.CartLines.First(line=>line.assetId==staleCartPartId));
            }
            else report.AppendLine("OPEN|cart-preview-native|stale-cart Move dialog had no actual Storage target.");
            if(!staleCartRejected)suiteOk=false;
            if(shell.ModalOpen)yield return Click(input,"dialogCancel");
            game.ClearNativeCart();
            profile.revision=checked(profile.revision+1);
            yield return ResetCartPreviewBrowser(input,"stale cart removed");
            if(game.PartsPage!="Inventory"){yield return Click(input,"partsSource:Inventory");yield return WaitParts();}

            // Confirm one real Move using the current native guarded callback.
            yield return OpenCartPreviewOwnedPart(input,fixturePartIds[0],"native confirmed Move");
            string beforeAdd=CartPreviewSourceFacts(profile,fixturePartIds[0],fixtureDefinition.Definition.id);
            string rowsBeforeAdd=CartPreviewRows(game.CartLines);
            var randomBeforeAdd=UnityEngine.Random.state;
            long jsonBeforeAdd=RuntimeDataEncodingProbe.PartConsumerJsonCallCount;
            string entropyBeforeAdd=CartPreviewEntropySequences();
            yield return Click(input,"partMove");
            var addTarget=shell.Root.Q<ScrollView>("modalContent")?.Query<Button>().ToList()
                .FirstOrDefault(button=>button.name.StartsWith("inventoryMoveTarget:Storage:",StringComparison.Ordinal));
            bool addStaged=false,candidateSourceUnchanged=false;
            if(addTarget!=null)
            {
                yield return PartDialogClick(input,addTarget.name);
                string candidatePreview=CartPreviewDialogFacts();
                candidateSourceUnchanged=beforeAdd==CartPreviewSourceFacts(profile,fixturePartIds[0],fixtureDefinition.Definition.id);
                yield return Click(input,"dialogAddmove");
                addStaged=!shell.ModalOpen&&game.CartLines.Count==1&&game.CartLines[0].action==CartAction.Move&&
                    game.CartLines[0].assetId==fixturePartIds[0]&&game.CartLines[0].destination==InventoryKind.Storage&&
                    game.CartLines[0].destinationId==SharedInventory.Garage;
                report.AppendLine("CART_PREVIEW_NATIVE|confirmed-add|staged="+addStaged+"|candidateRows="+
                    CartPreviewMoveCandidateRows(game.CartLines,fixturePartIds[0],fixtureDefinition.Definition.id,
                        InventoryKind.Storage,SharedInventory.Garage)+"|candidateSourceUnchanged="+
                    candidateSourceUnchanged+"|candidate="+candidatePreview+
                    "|rows="+CartPreviewRows(game.CartLines)+"|reservation="+CartPreviewCanonical(profile.inventory.cart)+
                    "|"+CartPreviewObservation(profile,fixturePartIds[0],fixtureDefinition.Definition.id,beforeAdd,rowsBeforeAdd,
                        CartPreviewRows(game.CartLines),
                        randomBeforeAdd,jsonBeforeAdd,entropyBeforeAdd));
            }
            else report.AppendLine("OPEN|cart-preview-native|confirmed Move dialog had no actual Storage target.");
            if(!addStaged)suiteOk=false;
            if(!candidateSourceUnchanged)
            {
                suiteOk=false;
                report.AppendLine("FAIL|cart-preview-native|confirmed Move candidate preview changed captured source facts; legacy JSON mutation remains strict-red.");
            }

            // Existing nonempty cart routing uses the actual legacy callback closure. Stage an authored new set
            // through its normal Choose purchase dialog, then preview a second actual Move after it.
            if(addStaged)
            {
                yield return Click(input,"partBack");
                if(game.PartsPage!="Shop")yield return Click(input,"partsSource:Shop");
                yield return WaitBoundedPartsReady("mixed-cart Shop candidate list");
                var shopQuery=shell.PartQuery;
                int activeShopSection=-1;
                for(int i=0;i<NativePartBrowser.SectionNames.Length;i++)
                    if(shell.Root.Q<Button>("partsTab"+i)?.ClassListContains("selected")==true){activeShopSection=i;break;}
                var shopSectionEntries=shell.PartBrowser.Entries.Where(entry=>entry.Definition!=null&&
                    (int)NativePartBrowser.Section(entry.Definition.category)==activeShopSection).ToArray();
                var shopQueryMatches=shopSectionEntries.Where(entry=>shopQuery.Matches(entry,profile.cash)).ToArray();
                var shopOfferCandidates=shopQueryMatches.Where(entry=>entry.Source=="New"&&entry.Compatibility=="Compatible"&&
                    entry.StockUnits>0&&entry.Definition.category!=PartCategory.CylinderBlock&&entry.Price.HasValue&&entry.Price.Value<=profile.cash).ToArray();
                var shopOffer=shopOfferCandidates
                    .OrderBy(entry=>entry.Price??int.MaxValue).ThenBy(entry=>entry.Id,StringComparer.Ordinal).FirstOrDefault();
                if(shopOffer==null)
                {
                    report.AppendLine("OPEN|cart-preview-native|nonempty-cart Shop current query has no affordable compatible stocked New entry for mixed candidate;page="+
                        game.PartsPage+";section="+activeShopSection+":"+(activeShopSection>=0?NativePartBrowser.SectionNames[activeShopSection]:"<none>")+
                        ";query="+shopQuery.Search+"/"+shopQuery.Sort+"/compatibility="+shopQuery.Compatibility+"/availability="+shopQuery.Availability+
                        "/condition="+shopQuery.Condition+"/inStock="+shopQuery.InStockOnly+";sectionEntries="+shopSectionEntries.Length+
                        ";queryMatches="+shopQueryMatches.Length+";compatibleStockedAffordable="+shopOfferCandidates.Length+";cash="+profile.cash);
                    suiteOk=false;
                }
                else
                {
                    yield return OpenCartPreviewEntry(input,shopOffer,"mixed cart NewSet",preserveCurrentQuery:true);
                    yield return Click(input,"partAdd");
                    bool purchaseOpen=shell.ModalOpen&&shell.Root.Q<Label>("modalTitle")?.text=="Choose purchase";
                    if(purchaseOpen)
                    {
                        var storeChoice=shell.Root.Q<Button>("partPurchaseDestination:Store");
                        if(storeChoice!=null)yield return PartDialogClick(input,storeChoice.name);
                        yield return Click(input,"dialogAddtocart");
                    }
                    bool setStaged=game.CartLines.Any(line=>line.action==CartAction.NewSet&&line.definitionId==shopOffer.Definition.id);
                    report.AppendLine("CART_PREVIEW_NATIVE|mixed-new-set-stage|opened="+purchaseOpen+"|staged="+setStaged+
                        "|offer="+shopOffer.Definition.id+"|section="+activeShopSection+":"+NativePartBrowser.SectionNames[activeShopSection]+
                        "|queryMatch="+shopQuery.Matches(shopOffer,profile.cash)+"|compatibility="+shopOffer.Compatibility+
                        "|stockUnits="+shopOffer.StockUnits+"|setPrice="+(shopOffer.Price??-1)+"|cashBefore="+profile.cash+
                        "|rows="+CartPreviewRows(game.CartLines)+
                        "|cash="+profile.cash+"|stock="+CartPreviewStockFacts(profile,shopOffer.Definition.id)+
                        "|entropy="+CartPreviewEntropySequences());
                    if(!setStaged)suiteOk=false;
                    if(shell.ModalOpen)yield return Click(input,"dialogCancel");
                    if(setStaged)
                    {
                        yield return Click(input,"partBack");
                        if(game.PartsPage!="Inventory"){yield return Click(input,"partsSource:Inventory");yield return WaitParts();}
                        yield return OpenCartPreviewOwnedPart(input,fixturePartIds[1],"mixed cart Move");
                        string mixedSourceBefore=CartPreviewSourceFacts(profile,fixturePartIds[1],fixtureDefinition.Definition.id);
                        string mixedRowsBefore=CartPreviewRows(game.CartLines);
                        var mixedRandomBefore=UnityEngine.Random.state;
                        long mixedJsonBefore=RuntimeDataEncodingProbe.PartConsumerJsonCallCount;
                        string mixedEntropyBefore=CartPreviewEntropySequences();
                        yield return Click(input,"partMove");
                        var mixedTarget=shell.Root.Q<ScrollView>("modalContent")?.Query<Button>().ToList()
                            .FirstOrDefault(button=>button.name.StartsWith("inventoryMoveTarget:Storage:",StringComparison.Ordinal));
                        bool mixedStaged=false;
                        if(mixedTarget!=null)
                        {
                            yield return PartDialogClick(input,mixedTarget.name);
                            string mixedPreview=CartPreviewDialogFacts();
                            bool mixedSourceUnchanged=mixedSourceBefore==CartPreviewSourceFacts(profile,fixturePartIds[1],fixtureDefinition.Definition.id);
                            report.AppendLine("CART_PREVIEW_NATIVE|mixed-candidate|path=nonempty-cart-legacy-view;target="+
                                mixedTarget.name+"|sourceUnchanged="+mixedSourceUnchanged+"|candidatePreview="+mixedPreview+"|candidateSourceOrder="+
                                CartPreviewMoveCandidateRows(game.CartLines,fixturePartIds[1],fixtureDefinition.Definition.id,
                                    InventoryKind.Storage,SharedInventory.Garage)+
                                "|"+CartPreviewObservation(profile,fixturePartIds[1],fixtureDefinition.Definition.id,mixedSourceBefore,
                                    mixedRowsBefore,CartPreviewRows(game.CartLines),mixedRandomBefore,mixedJsonBefore,mixedEntropyBefore));
                            if(!mixedSourceUnchanged)
                            {
                                suiteOk=false;
                                report.AppendLine("FAIL|cart-preview-native|mixed Move candidate changed captured source facts; legacy JSON mutation remains strict-red.");
                            }
                            yield return Click(input,"dialogAddmove");
                            mixedStaged=!shell.ModalOpen&&game.CartLines.Any(line=>line.action==CartAction.Move&&line.assetId==fixturePartIds[1]);
                        }
                        report.AppendLine("CART_PREVIEW_NATIVE|mixed-add|staged="+mixedStaged+"|rows="+
                            CartPreviewRows(game.CartLines)+"|reservation="+CartPreviewCanonical(profile.inventory.cart)+
                            "|entropy="+mixedEntropyBefore+"->"+CartPreviewEntropySequences());
                        if(!mixedStaged)suiteOk=false;
                    }
                }
            }

            int candidateProbeFaultsAfter=NativeCartPreviewCandidateProbe.DiagnosticFaultCount;
            int candidateProbeFindingsAfter=NativeCartPreviewCandidateProbe.FindingCount;
            int candidateProbeCompletionsAfter=NativeCartPreviewCandidateProbe.CompletedCount;
            if(candidateProbeFaultsAfter!=candidateProbeFaultsBefore)
            {
                suiteOk=false;
                report.AppendLine("FAIL|cart-preview-native|candidate probe fault delta="+
                    (candidateProbeFaultsAfter-candidateProbeFaultsBefore)+"|last="+
                    NativeCartPreviewCandidateProbe.LastDiagnosticFault);
            }
            if(candidateProbeFindingsAfter!=candidateProbeFindingsBefore)
            {
                suiteOk=false;
                report.AppendLine("FAIL|cart-preview-native|read-only candidate contract finding delta="+
                    (candidateProbeFindingsAfter-candidateProbeFindingsBefore)+"|last="+
                    NativeCartPreviewCandidateProbe.LastFinding);
            }
            if(candidateProbeCompletionsAfter==candidateProbeCompletionsBefore)
            {
                suiteOk=false;
                report.AppendLine("FAIL|cart-preview-native|no candidate-scope invocation completed through the installed probe hook.");
            }
            report.AppendLine("CART_PREVIEW_NATIVE|end|passed="+suiteOk+"|candidateProbeFaults="+
                candidateProbeFaultsAfter+"|rows="+CartPreviewRows(game.CartLines)+
                "|cash="+profile.cash+"|revision="+profile.revision+"|entropy="+CartPreviewEntropySequences());
            game.ClearNativeCart();
            Check(suiteOk,"Native cart preview callbacks and typed state deltas were recorded without hiding source mutations");
            }
            finally
            {
                NativeCartPreviewCandidateProbe.Completed=previousCandidateObserver;
                try
                {
                    if(shell.ModalOpen)
                        typeof(NativeMenuShell).GetMethod("CloseDialog",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(shell,null);
                    if(cartCleanupAuthorized&&game.CartCount>0)game.ClearNativeCart();
                }
                catch(Exception cleanupError)
                {
                    report.AppendLine("FAIL|cart-preview-native|test-only cleanup error="+cleanupError.GetType().FullName+" | "+cleanupError.Message);
                }
            }
        }

        IEnumerator OpenCartPreviewOwnedPart(WindowsKeyboardProbe input,string id,string label)
        {
            if(game.PartsPage!="Inventory")yield return Click(input,"partsSource:Inventory");
            yield return WaitBoundedPartsReady("cart preview Inventory entry / "+label);
            var entry=shell.PartBrowser?.Entries.FirstOrDefault(candidate=>candidate.Source=="Owned"&&candidate.AssetId==id);
            if(entry==null)throw new InvalidOperationException("Actual owned inventory entry is missing / "+label+" / "+id+
                " / "+CartPreviewBrowserState(id,CartPreviewExpectedSection(id)));
            yield return OpenCartPreviewEntry(input,entry,label);
        }

        IEnumerator ResetCartPreviewBrowser(WindowsKeyboardProbe input,string label)
        {
            if(shell.ModalOpen)yield return Click(input,"dialogCancel");
            if(shell.PartBrowserDetailId!=null&&shell.Root.Q<Button>("partBack")!=null)
                yield return Click(input,"partBack");
            shell.RetryPartBrowserPreparation();
            shell.Refresh();
            yield return WaitBoundedPartsReady("cart preview "+label);
        }

        IEnumerator OpenCartPreviewEntry(WindowsKeyboardProbe input,NativePartEntry entry,string label,bool preserveCurrentQuery=false)
        {
            if(entry==null)throw new ArgumentNullException(nameof(entry));
            if(shell.PartBrowserDetailId!=null&&shell.PartBrowserDetailId!=entry.Id&&shell.Root.Q<Button>("partBack")!=null)
            {
                yield return Click(input,"partBack");
                yield return WaitBoundedPartsReady("cart preview "+label+" returned to list");
            }
            if(shell.Root.Q("partsDetail")==null||shell.PartBrowserDetailId!=entry.Id)
            {
                string page=entry.Source=="Owned"?"Inventory":"Shop";
                if(game.PartsPage!=page)yield return Click(input,"partsSource:"+page);
                yield return WaitBoundedPartsReady("cart preview "+label+" page="+page);
                int section=(int)NativePartBrowser.Section(entry.Definition.category);
                var sectionButton=shell.Root.Q<Button>("partsTab"+section);
                if(sectionButton==null)throw new InvalidOperationException("Actual category section button is missing / "+label+
                    " / section="+section+" / "+CartPreviewBrowserState(entry.Id,section));
                if(!sectionButton.ClassListContains("selected"))yield return Click(input,sectionButton);
                yield return WaitBoundedPartsReady("cart preview "+label+" category section");
                var query=shell.PartQuery;
                bool queryNeedsClear=query!=null&&(!string.IsNullOrWhiteSpace(query.Search)||query.RequiresChanges||query.Compatibility!="Compatible"||
                    query.Availability!="All"||query.Condition!="All"||query.Tuning!="All"||!query.InStockOnly||query.Container!="All"||
                    query.BrandFocus!="All"||query.Brands.Count>0||query.Categories.Count>0||query.Descending||query.Sort!="Price");
                if(queryNeedsClear&&!preserveCurrentQuery)
                {
                    yield return Click(input,"partsClear");
                    yield return WaitBoundedPartsReady("cart preview "+label+" cleared active query");
                }
                var currentEntry=shell.PartBrowser?.Entries.FirstOrDefault(candidate=>candidate.Id==entry.Id);
                if(currentEntry==null)throw new InvalidOperationException("Target entry is absent from the current page/section/filter model / "+label+
                    " / id="+entry.Id+" / "+CartPreviewBrowserState(entry.Id,section));
                shell.ScrollToPart(currentEntry.Id);
                yield return WaitBoundedPartsReady("cart preview "+label+" accepted target card window");
                var card=BoundedPartCard("partCard:"+currentEntry.Id);
                if(card==null||!BoundedPartCardHitTestable(card))
                    throw new InvalidOperationException("Actual target card is not hit-testable in the accepted virtual window / "+label+
                        " / id="+entry.Id+" / "+CartPreviewBrowserState(entry.Id,section));
                yield return Click(input,card);
            }
            string expectedAction=entry.Source=="Owned"?"partMove":"partAdd";
            if(game.CartCount==0&&!shell.PartBrowserDetailCurrent)
                yield return WaitBoundedDetailReady(label,entry.Id);
            else
            {
                float started=Time.realtimeSinceStartup;
                while((shell.PartBrowserPreparationPending||shell.PartBrowserDetailId!=entry.Id||!CartPreviewActionVisibleEnabled(expectedAction))&&
                    Time.realtimeSinceStartup-started<90f)yield return null;
                Check(!shell.PartBrowserPreparationPending&&shell.PartBrowserDetailId==entry.Id&&CartPreviewActionVisibleEnabled(expectedAction),
                    "Actual legacy/nonempty-cart detail exposes the source-appropriate action / "+label+" / "+entry.Id+
                    " action="+expectedAction+" / "+CartPreviewBrowserState(entry.Id,-1));
            }
            if(!CartPreviewActionVisibleEnabled(expectedAction))
                throw new InvalidOperationException("Actual detail did not expose its visible enabled "+expectedAction+" action / "+label+
                    " / "+entry.Id+" / "+CartPreviewBrowserState(entry.Id,-1));
        }

        bool CartPreviewActionVisibleEnabled(string actionName)
        {
            var detail=shell.Root?.Q("partsDetail");var action=shell.Root?.Q<Button>(actionName);
            if(detail==null||detail.ClassListContains("hidden")||action==null||!action.enabledInHierarchy||
                action.worldBound.width<=0||action.worldBound.height<=0)return false;
            var picked=shell.Root.panel?.Pick(action.worldBound.center);
            return picked!=null&&(ReferenceEquals(picked,action)||action.Contains(picked));
        }

        string CartPreviewBrowserState(string entryId,int expectedSection)
        {
            int selectedSection=-1;
            for(int i=0;i<NativePartBrowser.SectionNames.Length;i++)
                if(shell.Root.Q<Button>("partsTab"+i)?.ClassListContains("selected")==true){selectedSection=i;break;}
            var card=shell.Root.Q<Button>("partCard:"+entryId);
            var query=shell.PartQuery;
            var selectedCar=game.Profile.SelectedCar;
            var selectedBuild=selectedCar==null?null:MechanicsOwnership.Definitions(game.Profile,selectedCar);
            var modelEntry=shell.PartBrowser?.Entries.FirstOrDefault(candidate=>candidate.Id==entryId);
            return "page="+game.PartsPage+";expectedSection="+expectedSection+":"+
                (expectedSection>=0&&expectedSection<NativePartBrowser.SectionNames.Length?NativePartBrowser.SectionNames[expectedSection]:"<unknown>")+
                ";selectedSection="+selectedSection+":"+(selectedSection>=0?NativePartBrowser.SectionNames[selectedSection]:"<none>")+
                ";state="+shell.PartBrowserPreparationState+";pending="+shell.PartBrowserPreparationPending+
                ";actions="+shell.PartBrowserActionsEnabled+";dataGen="+shell.PartBrowserDataGeneration+
                ";presentationGen="+shell.PartBrowserPresentationGeneration+";windowGen="+shell.PartBrowserWindowGeneration+
                ";entries="+(shell.PartBrowser?.Entries.Count??-1)+";windowCards="+shell.PartBrowserWindowCardCount+
                ";selectedCar="+(selectedCar?.instanceId??"<none>")+";selectedVehicle="+(selectedCar?.vehicleId??"<none>")+
                ";selectedBuild="+(selectedBuild==null?"<none>":string.Join(",",selectedBuild.OrderBy(item=>item.Key).Select(item=>item.Key+"="+item.Value)))+
                ";query="+(query==null?"<null>":query.Search+"/"+query.Sort+"/descending="+query.Descending+
                    "/changes="+query.RequiresChanges+"/compatibility="+query.Compatibility+"/availability="+query.Availability+
                    "/condition="+query.Condition+"/tuning="+query.Tuning+"/inStock="+query.InStockOnly+
                    "/container="+query.Container+"/brands="+query.Brands.Count+"/categories="+query.Categories.Count)+
                ";modelEntry="+(modelEntry==null?"<absent>":"present;compatibility="+modelEntry.Compatibility+";reason="+modelEntry.Reason)+
                ";card="+(card==null?"<missing>":"present;enabled="+card.enabledInHierarchy+";hitTestable="+
                    (shell.Root.Q<ScrollView>("partsScroll")!=null&&BoundedPartCardHitTestable(card)));
        }

        int CartPreviewExpectedSection(string assetId)
        {
            var part=game.Profile.parts.FirstOrDefault(candidate=>candidate?.instanceId==assetId);
            if(part!=null)
            {
                var definition=game.Profile.catalog.Find(part.definitionId);
                if(definition!=null)return (int)NativePartBrowser.Section(definition.category);
            }
            var unit=game.Profile.looseEngineElements.FirstOrDefault(candidate=>candidate?.element?.id==assetId);
            var unitDefinition=unit==null?null:game.Profile.catalog.Find(unit.definitionId);
            return unitDefinition==null?-1:(int)NativePartBrowser.Section(unitDefinition.category);
        }

        static string CartPreviewObservation(PlayerProfile profile,string assetId,string definitionId,string sourceBefore,
            string rowsBefore,string rowsAfter,UnityEngine.Random.State randomBefore,long jsonBefore,string entropyBefore)
        {
            return "source="+sourceBefore+"->"+CartPreviewSourceFacts(profile,assetId,definitionId)+
                "|rows="+rowsBefore+"->"+rowsAfter+
                "|cash="+profile.cash+"|revision="+profile.revision+"|stock="+CartPreviewStockFacts(profile,definitionId)+
                "|jsonDelta="+(RuntimeDataEncodingProbe.PartConsumerJsonCallCount-jsonBefore)+
                "|lastOperation="+(RuntimeDataEncodingProbe.LastOperation??"<null>")+"|unityRandomUnchanged="+
                randomBefore.Equals(UnityEngine.Random.state)+"|entropy="+entropyBefore+"->"+CartPreviewEntropySequences();
        }

        string CartPreviewDialogFacts()
        {
            var content=shell.Root?.Q<ScrollView>("modalContent");
            if(content==null)return "<missing-modal-content>";
            return "labels="+string.Join("¦",content.Query<Label>().ToList().Select(label=>
                (label.name??"")+"="+(label.text??"")))+"|addEnabled="+
                (shell.Root.Q<Button>("dialogAddmove")?.enabledInHierarchy==true);
        }

        internal static string CartPreviewSourceFacts(PlayerProfile profile,string assetId,string definitionId)
        {
            var part=profile.parts.FirstOrDefault(candidate=>candidate!=null&&candidate.instanceId==assetId);
            var unit=profile.looseEngineElements.FirstOrDefault(candidate=>candidate?.element?.id==assetId);
            string physical=part!=null?CartPreviewCanonical(part):unit!=null?CartPreviewCanonical(unit):"<missing-asset>";
            var selected=profile.SelectedCar;
            return "serializedProfile="+CartPreviewCanonical(profile)+";catalogScope=excluded-nonserialized;catalogIdentity="+
                (profile.catalog==null?"<null>":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(profile.catalog).ToString(CultureInfo.InvariantCulture))+
                ";catalogAuthoringRevision="+(profile.catalog==null?"<null>":profile.catalog.AuthoringRevision.ToString(CultureInfo.InvariantCulture))+
                ";id="+assetId+";definition="+definitionId+";physical="+physical+
                ";selected="+(selected?.instanceId??"<null>")+";selectedEngine="+(selected?.engineAssemblyId??"<null>")+
                ";cash="+profile.cash+";revision="+profile.revision+";stock="+CartPreviewStockFacts(profile,definitionId)+
                ";dealerPayloads="+CartPreviewCanonical(profile.inventory.dealer)+
                ";reservation="+CartPreviewCanonical(profile.inventory.cart);
        }

        static string CartPreviewStockFacts(PlayerProfile profile,string definitionId)
        {
            var rows=profile.inventory.market?.parts?.Where(row=>row!=null&&row.definitions!=null&&row.definitions.Contains(definitionId))??
                Enumerable.Empty<NewPartStock>();
            string stock=string.Join("|",rows.Select(row=>definitionId+"["+string.Join(",",row.definitions)+"]="+
                row.units+"/"+row.initialUnits+"/delivery="+row.deliveryPercent+"/discovered="+row.discovered));
            var holds=profile.inventory.cart?.supply?.Where(hold=>hold!=null&&hold.definitionId==definitionId)??
                Enumerable.Empty<ReservedPartSupply>();
            return "parts="+stock+";holds="+string.Join("|",holds.Select(hold=>hold.definitionId+"="+hold.units+"@"+hold.generation));
        }

        internal static string CartPreviewRows(IEnumerable<PartCartLine> rows)
        {
            if(rows==null)return "<null>";
            return string.Join("||",rows.Select((line,index)=>line==null?index+":<null>":index+":"+line.action+
                ";asset="+(line.assetId??"<null>")+";def="+(line.definitionId??"<null>")+";qty="+line.quantity+
                ";role="+(line.role??"<null>")+";install="+line.install+";dest="+line.destination+":"+
                (line.destinationId??"<null>")+";prepared="+line.preparedBlock+";owner="+
                (line.preparationOwner??"<null>")+";positions="+(line.positions==null?"<null>":string.Join(",",line.positions.Select(value=>value??"<null>")))));
        }

        internal static string CartPreviewMoveCandidateRows(IEnumerable<PartCartLine> current,string assetId,string definitionId,
            InventoryKind destination,string destinationId)
        {
            var lines=(current??Enumerable.Empty<PartCartLine>()).Where(line=>line?.assetId!=assetId)
                .Select(line=>line?.Copy()).ToList();
            lines.Add(new PartCartLine{action=CartAction.Move,assetId=assetId,definitionId=definitionId,
                destination=destination,destinationId=destinationId});
            return CartPreviewRows(lines);
        }

        static string CartPreviewCanonical(object value)
        {
            var output=new StringBuilder();CartPreviewAppendCanonical(output,value,new List<object>(),"$");return output.ToString();
        }

        static void CartPreviewAppendCanonical(StringBuilder output,object value,List<object> ancestors,string path)
        {
            if(value==null){output.Append("null;");return;}
            Type type=value.GetType();
            if(type==typeof(string)){string text=(string)value;output.Append("String{").Append(text.Length).Append(':').Append(text).Append("};");return;}
            if(type.IsEnum){output.Append(type.FullName).Append('{').Append(Convert.ToInt64(value,CultureInfo.InvariantCulture)).Append("};");return;}
            if(type==typeof(DateTime)){var date=(DateTime)value;output.Append("DateTime{").Append(date.Ticks).Append(':').Append((int)date.Kind).Append("};");return;}
            if(type==typeof(TimeSpan)){output.Append("TimeSpan{").Append(((TimeSpan)value).Ticks).Append("};");return;}
            if(type==typeof(Guid)){output.Append("Guid{").Append(((Guid)value).ToString("N")).Append("};");return;}
            if(type==typeof(decimal)){output.Append("Decimal{").Append(string.Join(",",decimal.GetBits((decimal)value))).Append("};");return;}
            if(type==typeof(bool)||type==typeof(byte)||type==typeof(sbyte)||type==typeof(short)||type==typeof(ushort)||
                type==typeof(int)||type==typeof(uint)||type==typeof(long)||type==typeof(ulong)||type==typeof(char))
            {output.Append(type.FullName).Append('{').Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append("};");return;}
            if(type==typeof(float)){output.Append("Single{").Append(BitConverter.SingleToInt32Bits((float)value).ToString("X8",CultureInfo.InvariantCulture)).Append("};");return;}
            if(type==typeof(double)){output.Append("Double{").Append(BitConverter.DoubleToInt64Bits((double)value).ToString("X16",CultureInfo.InvariantCulture)).Append("};");return;}
            if(type.IsValueType&&type.IsPrimitive){output.Append(type.FullName).Append('{').Append(value).Append("};");return;}
            if(ancestors.Any(item=>ReferenceEquals(item,value))){output.Append(type.FullName).Append("{cycle};");return;}
            ancestors.Add(value);
            if(value is IEnumerable enumerable)
            {
                output.Append(type.FullName).Append('[');int index=0;
                foreach(object item in enumerable){output.Append(index++).Append('=');CartPreviewAppendCanonical(output,item,ancestors,path+"["+index+"]");}
                output.Append("]; ");ancestors.RemoveAt(ancestors.Count-1);return;
            }
            output.Append(type.FullName).Append('{');
            var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                .Where(field=>!field.IsStatic&&!field.IsNotSerialized&&(field.IsPublic||
                    field.IsDefined(typeof(SerializeField),true)||field.IsDefined(typeof(SerializeReference),true)))
                .OrderBy(field=>field.Name,StringComparer.Ordinal).ToArray();
            foreach(var field in fields)
            {
                output.Append(field.Name).Append('<').Append(field.FieldType.FullName).Append('>').Append('=');
                CartPreviewAppendCanonical(output,field.GetValue(value),ancestors,path+"."+field.Name);
            }
            output.Append("};");ancestors.RemoveAt(ancestors.Count-1);
        }

        internal static string CartPreviewEntropySequences()
        {
            return CartPreviewTryEntropySequences(out var result)?result:"<unavailable>";
        }

        internal static string CartPreviewClock(DateTime value)=>value.Ticks.ToString(CultureInfo.InvariantCulture)+":"+((int)value.Kind).ToString(CultureInfo.InvariantCulture);

        internal static bool CartPreviewTryEntropySequences(out string result)
        {
            var field=typeof(RuntimeEntropy).GetField("sequences",BindingFlags.Static|BindingFlags.NonPublic);
            var values=field?.GetValue(null) as Dictionary<string,int>;
            if(values==null){result=null;return false;}
            lock(values)result=string.Join(",",values.OrderBy(pair=>pair.Key,StringComparer.Ordinal)
                .Select(pair=>pair.Key+"="+pair.Value.ToString(CultureInfo.InvariantCulture)));
            return true;
        }
    }
}
#endif
