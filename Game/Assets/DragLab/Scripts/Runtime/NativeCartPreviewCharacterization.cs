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
            internal readonly string SelectedBefore,TargetOwnerBefore,PhysicalBefore;
            internal readonly InventoryKind Destination;
            internal readonly string DestinationId;
            internal readonly UnityEngine.Random.State Random;
            internal readonly long JsonCalls,Started;
            internal readonly bool Capture;
            internal readonly DateTime Clock;
            internal Snapshot(string rows,string sourceFacts,string entropy,string assetId,string definitionId,
                InventoryKind destination,string destinationId,string selectedBefore,string targetOwnerBefore,string physicalBefore,
                UnityEngine.Random.State random,long jsonCalls,long started,bool capture,DateTime clock)
            {Rows=rows;SourceFacts=sourceFacts;Entropy=entropy;AssetId=assetId;DefinitionId=definitionId;Destination=destination;
                DestinationId=destinationId;SelectedBefore=selectedBefore;TargetOwnerBefore=targetOwnerBefore;PhysicalBefore=physicalBefore;
                Random=random;JsonCalls=jsonCalls;Started=started;Capture=capture;Clock=clock;}
        }
        internal static Action<string> Completed;
        internal static Action<string> Observed;
        static int diagnosticFaultCount;
        static string lastDiagnosticFault;
        internal static int DiagnosticFaultCount=>Volatile.Read(ref diagnosticFaultCount);
        internal static string LastDiagnosticFault=>lastDiagnosticFault;

        internal static Snapshot Begin(PlayerProfile profile,IEnumerable<PartCartLine> rows,string assetId,string definitionId)
        {
            if(Completed==null&&Observed==null)return null;
            try
            {
                var rowList=(rows??Enumerable.Empty<PartCartLine>()).ToArray();
                string rowFacts=NativeMenuVerification.CartPreviewRows(rowList);
                var target=rowList.LastOrDefault(line=>line!=null&&line.action==CartAction.Move&&line.assetId==assetId);
                string sourceFacts=NativeMenuVerification.CartPreviewSourceFacts(profile,assetId,definitionId);
                string entropy=NativeMenuVerification.CartPreviewEntropySequences();
                InventoryKind destination=target?.destination??InventoryKind.Storage;
                string destinationId=target?.destinationId;
                return new Snapshot(rowFacts,sourceFacts,entropy,assetId,definitionId,destination,destinationId,
                    NativeMenuVerification.CartPreviewSelectedFacts(profile),NativeMenuVerification.CartPreviewOwnerFacts(profile,destination,destinationId,definitionId,assetId),
                    NativeMenuVerification.CartPreviewPhysicalFacts(profile,assetId),UnityEngine.Random.state,
                    RuntimeDataEncodingProbe.CurrentThreadJsonCallCount,Stopwatch.GetTimestamp(),RuntimeEntropy.Capture,RuntimeEntropy.UtcNow);
            }
            catch(Exception error){RecordFault(error);return null;}
        }

        internal static void Complete(Snapshot snapshot,PlayerProfile profile,PlayerProfile candidate,Exception error)
        {
            if(snapshot==null||(Completed==null&&Observed==null))return;
            try
            {
                double milliseconds=(Stopwatch.GetTimestamp()-snapshot.Started)*1000.0/Stopwatch.Frequency;
                string candidateFacts=Completed==null?"<not-emitted>":candidate==null?"<no-candidate>":NativeMenuVerification.CartPreviewSourceFacts(
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
                Interlocked.Increment(ref completedCount);
                if(Completed!=null)
                {
                    string result="CART_PREVIEW_CANDIDATE_SCOPE|rows="+snapshot.Rows+"|input="+snapshot.SourceFacts+"->"+sourceAfter+
                        "|candidateAsset="+candidateFacts+"|selected="+(candidate?.SelectedCar?.instanceId??"<null>")+"|jsonDelta="+jsonDelta+
                        "|sourceUnchanged="+sourceUnchanged+"|unityRandomUnchanged="+randomUnchanged+"|capture="+snapshot.Capture+"->"+
                        RuntimeEntropy.Capture+"|clock="+NativeMenuVerification.CartPreviewClock(snapshot.Clock)+"->"+
                        NativeMenuVerification.CartPreviewClock(clockAfter)+"|clockUnchanged="+clockUnchanged+"|entropy="+snapshot.Entropy+
                        "->"+entropyAfter+"|sequenceUnchanged="+sequenceUnchanged+"|elapsedMs="+
                        milliseconds.ToString("F3",CultureInfo.InvariantCulture)+"|exception="+
                        (error==null?"<none>":error.GetType().FullName+" | "+error.Message);
                    Completed.Invoke(result);
                }
                if(Observed!=null)
                {
                    string summary="CART_PREVIEW_NATIVE_CANDIDATE|asset="+snapshot.AssetId+"|definition="+snapshot.DefinitionId+
                        "|target="+snapshot.Destination+":"+(snapshot.DestinationId??"<null>")+"|rows="+snapshot.Rows+
                        "|selectedBefore="+snapshot.SelectedBefore+"|targetOwnerBefore="+snapshot.TargetOwnerBefore+
                        "|physicalBefore="+snapshot.PhysicalBefore+"|selectedAfter="+NativeMenuVerification.CartPreviewSelectedFacts(candidate)+
                        "|targetOwnerAfter="+NativeMenuVerification.CartPreviewOwnerFacts(candidate,snapshot.Destination,snapshot.DestinationId,
                            snapshot.DefinitionId,snapshot.AssetId)+"|physicalAfter="+NativeMenuVerification.CartPreviewPhysicalFacts(candidate,snapshot.AssetId)+
                        "|sourceUnchanged="+sourceUnchanged+"|jsonDelta="+jsonDelta+"|randomUnchanged="+randomUnchanged+
                        "|capture="+snapshot.Capture+"->"+RuntimeEntropy.Capture+"|clock="+
                        NativeMenuVerification.CartPreviewClock(snapshot.Clock)+"->"+NativeMenuVerification.CartPreviewClock(clockAfter)+
                        "|clockUnchanged="+clockUnchanged+"|sequenceUnchanged="+sequenceUnchanged+
                        "|error="+(error==null?"<none>":error.GetType().FullName+"|"+error.Message);
                    Observed.Invoke(summary);
                }
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

        IEnumerator CartPreviewDestinationChecks(WindowsKeyboardProbe input)
        {
            var profile=game.Profile;
            bool cleanupAuthorized=game.CartCount==0;
            string selectedId=profile.SelectedCar?.instanceId;
            InventoryContainer staleStorageToRestore=null;
            int staleStorageRestoreIndex=-1;
            var previousCandidateObserver=NativeCartPreviewCandidateProbe.Completed;
            var previousCandidateSummaryObserver=NativeCartPreviewCandidateProbe.Observed;
            int candidateFindingsBefore=NativeCartPreviewCandidateProbe.FindingCount;
            int candidateFaultsBefore=NativeCartPreviewCandidateProbe.DiagnosticFaultCount;
            int candidateScopesBefore=NativeCartPreviewCandidateProbe.CompletedCount;
            int candidateSummaries=0;
            const int fixtureCashBudget=1000000;
            const int fixtureReputationBudget=1000;
            int cashBeforeBudget=profile.cash;
            int reputationBeforeBudget=profile.reputation;
            void ExpectCandidateScopes(int before,int expected,string label)
            {
                int actual=candidateSummaries-before;
                report.AppendLine("CART_PREVIEW_DESTINATIONS|candidate-family|name="+label+"|observed="+actual+"|expected="+expected);
                Check(actual==expected,"Actual candidate snapshots ran at every expected point in "+label);
            }
            report.AppendLine("CART_PREVIEW_DESTINATIONS|begin|capture="+RuntimeEntropy.Capture+"|selected="+
                (selectedId??"<none>")+"|cart="+CartPreviewRows(game.CartLines)+"|cash="+profile.cash+
                "|fixtureCashBudget="+fixtureCashBudget+"|reputation="+profile.reputation+
                "|fixtureReputationBudget="+fixtureReputationBudget);
            try
            {
                Check(RuntimeEntropy.Capture,"Destination characterization runs in deterministic capture mode");
                Check(cleanupAuthorized,"Destination characterization starts with an empty isolated cart");
                if(!cleanupAuthorized)yield break;
                if(selectedId==null){Check(false,"Destination characterization has a selected owned car");yield break;}
                NativeCartPreviewCandidateProbe.Completed=null;
                NativeCartPreviewCandidateProbe.Observed=summary=>{candidateSummaries++;report.AppendLine(summary);};
                profile.cash=checked(profile.cash+fixtureCashBudget);
                profile.reputation=checked(profile.reputation+fixtureReputationBudget);
                if(!shell.Visible)yield return Key(input,0x77);
                if(game.ScreenName!="Parts")yield return Click(input,"navBuild");
                yield return WaitParts();
                if(game.PartsPage!="Shop"){yield return Click(input,"partsSource:Shop");yield return WaitParts();}

                var selected=profile.cars.First(car=>car.instanceId==selectedId);
                var selectedSpec=profile.catalog.Resolve(selected.vehicleId,MechanicsOwnership.Definitions(profile,selected),selected.tune).spec;
                var ordinary=profile.catalog.parts.Where(d=>d!=null&&d.category!=PartCategory.CylinderBlock&&MechanicalCatalog.EngineSlot(d.category)&&d.price>=0&&
                        profile.reputation>=d.reputationRequired&&PartsMarket.FitsAny(profile,d)&&profile.catalog.Fits(selectedSpec,d)&&PartsMarket.Stock(profile,d)!=null)
                    .Select(d=>{int count=PartsMarket.Count(profile,selected,d);return new{Definition=d,Count=count,
                        Quote=count>0?PartsMarket.Quote(profile,d,count,unit:false,quantity:3):null};})
                    .Where(x=>x.Count>0&&x.Quote!=null&&x.Quote.cost>0)
                    .OrderBy(x=>x.Quote.localUnits==x.Count?0:1).ThenBy(x=>x.Quote.cost).ThenBy(x=>x.Definition.id,StringComparer.Ordinal).ToArray();
                var spareOffer=ordinary.FirstOrDefault(x=>x.Quote.cost<=profile.cash/5);
                Check(spareOffer!=null,"An authored ordinary part has a real affordable quote for Move destination fixtures");
                if(spareOffer==null)yield break;

                string[] wholeIds=new string[3];
                int purchaseCash=profile.cash;
                for(int i=0;i<wholeIds.Length;i++)wholeIds[i]=PartOperations.Buy(profile,profile.catalog,selected.instanceId,spareOffer.Definition.id,false);
                Check(purchaseCash-profile.cash==spareOffer.Quote.cost,"Three real purchases settle at the exact aggregate three-purchase quote");
                profile.revision=checked(profile.revision+1);
                report.AppendLine("CART_PREVIEW_DESTINATIONS|fixture-parts|definition="+spareOffer.Definition.id+
                    "|count="+spareOffer.Count+"|quantity=3|quote="+spareOffer.Quote.cost+"|ids="+string.Join(",",wholeIds)+
                    "|cash="+purchaseCash+"->"+profile.cash+"|stock="+CartPreviewStockFacts(profile,spareOffer.Definition.id));
                shell.Refresh();yield return WaitParts();
                if(game.PartsPage!="Inventory"){yield return Click(input,"partsSource:Inventory");yield return WaitParts();}

                var storageRows=profile.inventory.storage.Where(x=>x!=null&&x.kind==InventoryKind.Storage).ToArray();
                // Real purchases are delivered to Garage in this fixture. Keep that occupied
                // destination as the primary target and use the distinct empty Storage for stale setup.
                var primaryStorage=storageRows.FirstOrDefault(x=>x.id==SharedInventory.Garage)??storageRows.FirstOrDefault(x=>x.id==SharedInventory.Storage)??storageRows.FirstOrDefault();
                var staleStorage=storageRows.FirstOrDefault(x=>x!=null&&x.id!=primaryStorage?.id&&
                    !profile.parts.Any(p=>p.location==PartLocation.Inventory&&SharedInventory.StorageOf(p.storageId)==x.id)&&
                    !profile.looseEngineElements.Any(u=>SharedInventory.StorageOf(u.storageId)==x.id)&&
                    !profile.engines.Any(e=>string.IsNullOrEmpty(e.parentVehicleId)&&SharedInventory.StorageOf(e.storageId)==x.id));
                Check(primaryStorage!=null&&staleStorage!=null,"Two real Storage containers exist, including an empty container for stale-destination setup");
                if(primaryStorage==null||staleStorage==null)yield break;

                yield return OpenCartPreviewOwnedPart(input,wholeIds[0],"destination initial unselected Move");
                yield return Click(input,"partMove");
                bool noInitialSelection=!shell.Root.Q<ScrollView>("modalContent").Query<Button>().ToList()
                    .Any(b=>b.name.StartsWith("inventoryMoveTarget:",StringComparison.Ordinal)&&b.ClassListContains("selected"));
                bool initiallyDisabled=shell.Root.Q<Button>("dialogAddmove")?.enabledInHierarchy!=true;
                Check(noInitialSelection&&initiallyDisabled,"A first Move has no selected destination and Add stays disabled");
                yield return Click(input,"dialogCancel");

                yield return OpenCartPreviewOwnedPart(input,wholeIds[0],"destination initial Storage row");
                yield return Click(input,"partMove");
                string staleTargetName="inventoryMoveTarget:Storage:"+staleStorage.id;
                Check(shell.Root.Q<Button>(staleTargetName)!=null,"The actual empty Storage target is rendered");
                if(shell.Root.Q<Button>(staleTargetName)==null){yield return Click(input,"dialogCancel");yield break;}
                int firstTargetCandidateBefore=candidateSummaries;
                yield return PartDialogClick(input,staleTargetName);
                bool firstAddReady=shell.Root.Q<Button>("dialogAddmove")?.enabledInHierarchy==true;
                yield return Click(input,"dialogAddmove");
                ExpectCandidateScopes(firstTargetCandidateBefore,2,"first destination selection and Add");
                bool firstStaged=firstAddReady&&game.CartLines.Count==1&&game.CartLines[0].assetId==wholeIds[0]&&
                    game.CartLines[0].destination==InventoryKind.Storage&&game.CartLines[0].destinationId==staleStorage.id;
                Check(firstStaged,"The first rendered Move stages the actual selected Storage destination");
                if(!firstStaged)yield break;

                yield return OpenCartPreviewOwnedPart(input,wholeIds[0],"destination valid prior row restored");
                int validReopenCandidateBefore=candidateSummaries;
                yield return Click(input,"partMove");
                bool restoredSelection=shell.Root.Q<Button>(staleTargetName)?.ClassListContains("selected")==true&&
                    shell.Root.Q<Button>("dialogAddmove")?.enabledInHierarchy==true;
                ExpectCandidateScopes(validReopenCandidateBefore,1,"valid prior destination reopen preview");
                Check(restoredSelection,"Reopening Move restores a still-valid prior destination");
                yield return Click(input,"dialogCancel");

                yield return OpenCartPreviewOwnedPart(input,wholeIds[1],"destination unrelated row");
                yield return Click(input,"partMove");
                int unrelatedCandidateBefore=candidateSummaries;
                yield return PartDialogClick(input,"inventoryMoveTarget:Storage:"+primaryStorage.id);
                yield return Click(input,"dialogAddmove");
                ExpectCandidateScopes(unrelatedCandidateBefore,2,"unrelated Move selection and Add");
                bool secondStaged=game.CartLines.Count==2&&game.CartLines[0].assetId==wholeIds[0]&&game.CartLines[1].assetId==wholeIds[1];
                Check(secondStaged,"An unrelated Move row is staged after the first row");
                if(!secondStaged)yield break;

                int staleStorageIndex=profile.inventory.storage.IndexOf(staleStorage);
                staleStorageToRestore=staleStorage;staleStorageRestoreIndex=staleStorageIndex;
                profile.inventory.storage.Remove(staleStorage);
                shell.Refresh();yield return WaitParts();
                yield return OpenCartPreviewOwnedPart(input,wholeIds[0],"destination stale prior target removed");
                yield return Click(input,"partMove");
                bool staleUnselected=!shell.Root.Q<ScrollView>("modalContent").Query<Button>().ToList()
                    .Any(b=>b.name.StartsWith("inventoryMoveTarget:",StringComparison.Ordinal)&&b.ClassListContains("selected"));
                bool staleAddDisabled=shell.Root.Q<Button>("dialogAddmove")?.enabledInHierarchy!=true;
                Check(staleUnselected&&staleAddDisabled,"A stale prior destination is not restored and Add remains disabled");
                int staleReplacementCandidateBefore=candidateSummaries;
                yield return PartDialogClick(input,"inventoryMoveTarget:Storage:"+primaryStorage.id);
                yield return Click(input,"dialogAddmove");
                ExpectCandidateScopes(staleReplacementCandidateBefore,2,"stale destination replacement selection and Add");
                string replacedRows=CartPreviewRows(game.CartLines);
                bool replaced=game.CartLines.Count==2&&game.CartLines[0].assetId==wholeIds[1]&&game.CartLines[1].assetId==wholeIds[0]&&
                    game.CartLines.Count(line=>line.assetId==wholeIds[0])==1;
                Check(replaced,"Replacing a stale same-asset Move removes its old row and appends one replacement after unrelated rows");
                report.AppendLine("CART_PREVIEW_DESTINATIONS|move-edit|staleContainer="+staleStorage.id+"|primary="+primaryStorage.id+
                    "|staleUnselected="+staleUnselected+"|addDisabled="+staleAddDisabled+"|rows="+replacedRows);
                profile.inventory.storage.Insert(Math.Max(0,staleStorageIndex),staleStorage);staleStorageToRestore=null;

                string unitId=null;
                var unitOffer=profile.catalog.parts.Where(d=>d!=null&&d.category!=PartCategory.CylinderBlock&&d.price>=0&&
                        d.category!=PartCategory.Tires&&d.category!=PartCategory.Wheels&&MechanicalCatalog.EngineSlot(d.category)&&
                        profile.reputation>=d.reputationRequired&&PartsMarket.FitsAny(profile,d)&&profile.catalog.Fits(selectedSpec,d)&&PartsMarket.Stock(profile,d)!=null)
                    .Select(d=>{int count=PartsMarket.Count(profile,selected,d);return new{Definition=d,Count=count,Roles=count>0?InventoryUnits.Roles(profile,selected,d):Array.Empty<string>()};})
                    .Where(x=>x.Count>0&&x.Roles.Length>0).Select(x=>new{ x.Definition,x.Count,Role=x.Roles.FirstOrDefault(r=>!string.IsNullOrWhiteSpace(r)),
                        Quote=PartsMarket.Quote(profile,x.Definition,x.Count,unit:true)})
                    .Where(x=>x.Role!=null&&x.Quote!=null&&x.Quote.cost>0&&x.Quote.cost<=profile.cash/3)
                    .OrderBy(x=>x.Quote.localUnits==x.Count?0:1).ThenBy(x=>x.Quote.cost).ThenBy(x=>x.Definition.id,StringComparer.Ordinal).FirstOrDefault();
                Check(unitOffer!=null,"An authored individual-unit offer has an actual affordable unit quote");
                if(unitOffer==null)yield break;
                int unitCash=profile.cash;
                var actualUnit=InventoryUnits.Buy(profile,selected.instanceId,unitOffer.Definition.id,unitOffer.Role);
                Check(unitCash-profile.cash==unitOffer.Quote.cost,"The real individual-unit purchase settles at its quoted cost");
                unitId=actualUnit.element.id;profile.revision=checked(profile.revision+1);
                shell.Refresh();yield return WaitParts();
                yield return OpenCartPreviewOwnedPart(input,unitId,"owned loose unit destinations");
                yield return Click(input,"partMove");
                var unitButtons=shell.Root.Q<ScrollView>("modalContent").Query<Button>().ToList()
                    .Where(b=>b.name.StartsWith("inventoryMoveTarget:",StringComparison.Ordinal)).ToArray();
                bool unitStorageOnly=unitButtons.Length>0&&unitButtons.All(b=>b.name.StartsWith("inventoryMoveTarget:Storage:",StringComparison.Ordinal));
                bool unitUnselected=!unitButtons.Any(b=>b.ClassListContains("selected"));
                bool unitAddDisabled=shell.Root.Q<Button>("dialogAddmove")?.enabledInHierarchy!=true;
                Check(unitStorageOnly&&unitUnselected&&unitAddDisabled,"Owned loose unit Move exposes only unselected Storage destinations");
                if(unitStorageOnly&&unitButtons.Length>0)
                {
                    int unitCandidateBefore=candidateSummaries;
                    yield return PartDialogClick(input,unitButtons[0].name);
                    yield return Click(input,"dialogAddmove");
                    ExpectCandidateScopes(unitCandidateBefore,2,"owned loose unit Storage selection and Add");
                    bool unitStaged=game.CartLines.Any(line=>line.action==CartAction.Move&&line.assetId==unitId&&line.destination==InventoryKind.Storage);
                    Check(unitStaged,"An owned loose unit is moved through the rendered Storage target and Add action");
                    report.AppendLine("CART_PREVIEW_DESTINATIONS|unit|id="+unitId+"|definition="+unitOffer.Definition.id+
                        "|role="+unitOffer.Role+"|quote="+unitOffer.Quote.cost+"|cash="+unitCash+"->"+profile.cash+
                        "|targets="+string.Join(",",unitButtons.Select(b=>b.name))+"|staged="+unitStaged);
                }
                else if(shell.ModalOpen)yield return Click(input,"dialogCancel");

                // Use an actual compatible owned car and an actual detached engine. Prefer an existing
                // same-model chassis; otherwise buy a current authored listing within the declared budget.
                var other=profile.cars.FirstOrDefault(c=>c.instanceId!=selected.instanceId&&c.vehicleId==selected.vehicleId);
                string boughtCar="<existing>";
                if(other==null)
                {
                    var listing=profile.vehicleStock?.listings?.FirstOrDefault(x=>x!=null&&x.model==selected.vehicleId&&x.stock!=null&&
                        profile.cash>=x.price&&profile.reputation>=x.reputation);
                    if(listing!=null){boughtCar=VehicleCollection.Buy(profile,profile.catalog,listing);other=profile.cars.First(c=>c.instanceId==boughtCar);}
                }
                Check(other!=null,"A real same-model owned car is available as a second destination");
                if(other==null)yield break;
                string boughtDonorCar="<not-bought>";
                var detached=profile.engines.FirstOrDefault(e=>string.IsNullOrEmpty(e.parentVehicleId)&&e.vehicleDefinitionId==other.vehicleId);
                if(detached==null)
                {
                    var engineOffer=profile.inventory.market.engines.FirstOrDefault(offer=>offer!=null&&offer.model==other.vehicleId&&
                        (!offer.used||offer.quantity>0)&&profile.cash>=PartsMarket.EnginePrice(offer)&&
                        offer.parts!=null&&offer.parts.All(part=>profile.reputation>=profile.catalog.Find(part.definitionId).reputationRequired));
                    if(engineOffer!=null)
                    {
                        int engineCash=profile.cash;int enginePrice=PartsMarket.EnginePrice(engineOffer);
                        string engineId=PartsMarket.BuyEngine(profile,engineOffer.id,enginePrice);
                        Check(engineCash-profile.cash==enginePrice,"The real detached-engine purchase settles at its quoted offer price");
                        detached=profile.engines.First(e=>e.instanceId==engineId);
                        report.AppendLine("CART_PREVIEW_DESTINATIONS|fixture-engine|offer="+engineOffer.id+"|model="+engineOffer.model+
                            "|price="+enginePrice+"|id="+engineId+"|cash="+engineCash+"->"+profile.cash);
                    }
                }
                if(detached==null)
                {
                    var donor=profile.cars.FirstOrDefault(c=>c.instanceId!=selected.instanceId&&c.instanceId!=other.instanceId&&
                        !string.IsNullOrEmpty(c.engineAssemblyId)&&c.vehicleId==other.vehicleId);
                    if(donor==null)
                    {
                        var donorListing=profile.vehicleStock?.listings?.FirstOrDefault(x=>x!=null&&x.model==other.vehicleId&&x.stock!=null&&
                            profile.cash>=x.price&&profile.reputation>=x.reputation);
                        if(donorListing!=null)
                        {
                            boughtDonorCar=VehicleCollection.Buy(profile,profile.catalog,donorListing);
                            donor=profile.cars.FirstOrDefault(c=>c.instanceId==boughtDonorCar);
                            report.AppendLine("CART_PREVIEW_DESTINATIONS|fixture-donor-car|listing="+donorListing.listingId+
                                "|model="+donorListing.model+"|price="+donorListing.price+"|id="+boughtDonorCar);
                        }
                    }
                    if(donor!=null)
                    {
                        string detachedId=AssemblyOperations.Detach(profile,donor.instanceId);
                        detached=profile.engines.First(e=>e.instanceId==detachedId);
                    }
                }
                Check(detached!=null,"A real detached same-model engine is available as a Move destination");
                if(detached==null)yield break;
                VehicleCollection.Select(profile,selected.instanceId);profile.revision=checked(profile.revision+1);
                shell.Refresh();yield return WaitParts();

                foreach(var target in new[]{
                    new{Kind=InventoryKind.Storage,Id=primaryStorage.id,Name="Storage"},
                    new{Kind=InventoryKind.Car,Id=other.instanceId,Name="other-car"},
                    new{Kind=InventoryKind.Engine,Id=detached.instanceId,Name="detached-engine"}})
                {
                    yield return OpenCartPreviewOwnedPart(input,wholeIds[2],"destination Current/recompute "+target.Name);
                    yield return Click(input,"partMove");
                    string targetName="inventoryMoveTarget:"+target.Kind+":"+target.Id;
                    var actualTarget=shell.Root.Q<Button>(targetName);
                    Check(actualTarget!=null,"Actual "+target.Name+" destination button is present");
                    if(actualTarget!=null)
                    {
                        int targetCandidateBefore=candidateSummaries;
                        yield return PartDialogClick(input,targetName);
                        ExpectCandidateScopes(targetCandidateBefore,1,"whole-part "+target.Name+" recompute");
                        string candidateRows=CartPreviewRows(game.CartLines.Where(line=>line.assetId!=wholeIds[2]).Concat(new[]{new PartCartLine{
                            action=CartAction.Move,assetId=wholeIds[2],definitionId=spareOffer.Definition.id,
                            destination=target.Kind,destinationId=target.Id}}));
                        bool selectedTarget=actualTarget.ClassListContains("selected");
                        bool addReady=shell.Root.Q<Button>("dialogAddmove")?.enabledInHierarchy==true;
                        Check(selectedTarget&&addReady,"Actual "+target.Name+" selection recomputes an actionable candidate");
                        string dialogFacts=CartPreviewDialogFacts();
                        if(dialogFacts.Length>480)dialogFacts=dialogFacts.Substring(0,480)+"…";
                        report.AppendLine("CART_PREVIEW_DESTINATIONS|recompute|target="+target.Name+"|id="+target.Id+
                            "|selectedCar="+profile.SelectedCar.instanceId+"|targetOwner="+target.Id+
                            "|buttonSelected="+selectedTarget+"|addEnabled="+addReady+"|expectedRows="+candidateRows+
                            "|dialog="+dialogFacts+"|currentRows="+CartPreviewRows(game.CartLines));
                    }
                    yield return Click(input,"dialogCancel");
                }

                MechanicsOwnership.Definitions(profile,selected).TryGetValue(PartCategory.CylinderBlock,out var selectedBlockDefinitionId);
                var selectedBlock=profile.parts.FirstOrDefault(p=>p.location==PartLocation.Assembly&&p.ownerId==selected.engineAssemblyId&&
                    p.definitionId==selectedBlockDefinitionId);
                var blockDefinition=selectedBlock==null?null:profile.catalog.Find(selectedBlock.definitionId);
                int sameBlockCount=blockDefinition==null?0:PartsMarket.Count(profile,selected,blockDefinition);
                var sameBlockQuote=blockDefinition==null||sameBlockCount<=0||PartsMarket.Stock(profile,blockDefinition)==null?null:
                    PartsMarket.Quote(profile,blockDefinition,sameBlockCount,unit:false);
                Check(selectedBlock!=null&&sameBlockQuote!=null&&sameBlockQuote.cost>0&&sameBlockQuote.cost<=profile.cash/4,
                    "Installed selected-car block has a real affordable duplicate offer for the same-definition control");
                if(selectedBlock==null||sameBlockQuote==null||sameBlockQuote.cost<=0||sameBlockQuote.cost>profile.cash/4)yield break;
                int sameBlockCash=profile.cash;
                string sameBlockId=PartOperations.Buy(profile,profile.catalog,selected.instanceId,blockDefinition.id,false);
                Check(sameBlockCash-profile.cash==sameBlockQuote.cost,"The duplicate same-definition block purchase settles at its quote");
                profile.revision=checked(profile.revision+1);shell.Refresh();yield return WaitParts();
                yield return OpenCartPreviewOwnedPart(input,sameBlockId,"same-definition cylinder-block Move control");
                yield return Click(input,"partMove");
                int sameBlockCandidateBefore=candidateSummaries;
                yield return PartDialogClick(input,"inventoryMoveTarget:Car:"+selected.instanceId);
                bool sameBlockEnabled=shell.Root.Q<Button>("dialogAddmove")?.enabledInHierarchy==true;
                yield return Click(input,"dialogAddmove");
                ExpectCandidateScopes(sameBlockCandidateBefore,2,"same-definition block selection and Add");
                bool sameBlockNormal=sameBlockEnabled&&!shell.ModalOpen&&game.CartLines.Any(line=>line.assetId==sameBlockId&&line.action==CartAction.Move);
                Check(sameBlockNormal,"A real same-definition block uses normal Move staging without opening preparation");
                if(game.CartCount>0)game.ClearNativeCart();
                profile.revision=checked(profile.revision+1);shell.Refresh();yield return WaitParts();

                var changedBlock=profile.catalog.parts.Where(d=>d!=null&&d.category==PartCategory.CylinderBlock&&d.id!=blockDefinition.id&&
                        d.price>=0&&profile.reputation>=d.reputationRequired&&PartsMarket.FitsAny(profile,d)&&profile.catalog.Fits(selectedSpec,d)&&PartsMarket.Stock(profile,d)!=null)
                    .Select(d=>{int count=PartsMarket.Count(profile,selected,d);return new{Definition=d,Quote=count>0?PartsMarket.Quote(profile,d,count,unit:false):null};})
                    .Where(x=>x.Quote!=null&&x.Quote.cost>0&&x.Quote.cost<=profile.cash/4)
                    .OrderBy(x=>x.Quote.cost).ThenBy(x=>x.Definition.id,StringComparer.Ordinal).FirstOrDefault();
                Check(changedBlock!=null,"A different authored cylinder block has a real affordable selected-car quote");
                if(changedBlock==null)yield break;
                int changedBlockCash=profile.cash;
                string changedBlockId=PartOperations.Buy(profile,profile.catalog,selected.instanceId,changedBlock.Definition.id,false);
                Check(changedBlockCash-profile.cash==changedBlock.Quote.cost,"The different block purchase settles at its quote");
                profile.revision=checked(profile.revision+1);shell.Refresh();yield return WaitParts();
                yield return OpenCartPreviewOwnedPart(input,changedBlockId,"changed block selected-car preparation");
                yield return Click(input,"partMove");
                yield return PartDialogClick(input,"inventoryMoveTarget:Car:"+selected.instanceId);
                bool prepAddEnabled=shell.Root.Q<Button>("dialogAddmove")?.enabledInHierarchy==true;
                yield return Click(input,"dialogAddmove");
                bool preparationOpened=shell.ModalOpen&&shell.Root.Q("blockPreparationMode:Reuse")!=null&&
                    shell.Root.Q<Button>("dialogAddplan")!=null&&game.CartCount==0;
                Check(prepAddEnabled&&preparationOpened,"A fresh real changed-block Move opens preparation without staging a cart row");
                var addPlan=shell.Root.Q<Button>("dialogAddplan");
                if(addPlan!=null&&!addPlan.enabledInHierarchy)
                {
                    yield return PartDialogClick(input,"blockPreparationMode:OEM");
                    addPlan=shell.Root.Q<Button>("dialogAddplan");
                }
                bool planReady=addPlan!=null&&addPlan.enabledInHierarchy;
                Check(planReady,"Actual Reuse/OEM preparation controls produce a confirmable plan for the purchased block");
                report.AppendLine("CART_PREVIEW_DESTINATIONS|block-selected|sameDefinition="+blockDefinition.id+"|sameId="+sameBlockId+
                    "|normalStage="+sameBlockNormal+"|changedDefinition="+changedBlock.Definition.id+"|changedId="+changedBlockId+
                    "|preparationOpened="+preparationOpened+"|prePlanCart="+CartPreviewRows(game.CartLines)+"|planEnabled="+planReady);
                if(planReady)yield return Click(input,"dialogAddplan");
                var preparedCore=game.CartLines.FirstOrDefault(line=>line!=null&&line.preparedBlock&&line.assetId==changedBlockId);
                string expectedPreparationOwner=BlockPreparation.Owner(new PartCartLine{action=CartAction.Move,assetId=changedBlockId});
                bool exactPreparedCore=planReady&&preparedCore!=null&&preparedCore.action==CartAction.Move&&
                    preparedCore.destination==InventoryKind.Car&&preparedCore.destinationId==selected.instanceId&&
                    preparedCore.preparationOwner==expectedPreparationOwner&&game.CartLines.Count(line=>line?.assetId==changedBlockId)==1;
                Check(exactPreparedCore,"Explicit rendered Add Plan stages the real block ID once for the selected-car destination");
                report.AppendLine("CART_PREVIEW_DESTINATIONS|block-plan-confirmed|expectedOwner="+expectedPreparationOwner+
                    "|core="+(preparedCore==null?"<missing>":CartPreviewRows(new[]{preparedCore}))+"|rows="+CartPreviewRows(game.CartLines));
                if(game.CartCount>0)game.ClearNativeCart();
                profile.revision=checked(profile.revision+1);shell.Refresh();yield return WaitParts();

                // Verify the actual preparation origin guard after a real inventory move changes
                // the selected physical block's storage location while its modal remains open.
                yield return OpenCartPreviewOwnedPart(input,changedBlockId,"changed block origin guard setup");
                yield return Click(input,"partMove");
                yield return PartDialogClick(input,"inventoryMoveTarget:Car:"+selected.instanceId);
                yield return Click(input,"dialogAddmove");
                bool guardPreparationOpen=shell.ModalOpen&&shell.Root.Q<Button>("blockPreparationMode:Manual")!=null&&game.CartCount==0;
                Check(guardPreparationOpen,"Changed-block preparation reopens with the real detail action origin");
                if(guardPreparationOpen)
                {
                    var physicalBlock=profile.parts.FirstOrDefault(part=>part.instanceId==changedBlockId);
                    string originalBlockStorage=physicalBlock?.storageId;
                    string guardStorage=profile.inventory.storage.FirstOrDefault(container=>container!=null&&
                        container.id!=SharedInventory.StorageOf(originalBlockStorage))?.id;
                    Check(physicalBlock!=null&&physicalBlock.location==PartLocation.Inventory&&guardStorage!=null,
                        "A different actual Storage container is available to invalidate the block detail origin");
                    if(physicalBlock!=null&&guardStorage!=null)
                    {
                        int originGuardBefore=shell.PartActionGuardRejectedCount;
                        const string expectedOriginFailure="Page assets, dealer, stock, reservation or owned IDs changed.";
                        string originErrorText="<missing>";
                        bool originRejected=false;
                        try
                        {
                            SharedInventory.Move(profile,changedBlockId,InventoryKind.Storage,guardStorage);
                            yield return PartDialogClick(input,"blockPreparationMode:Manual");
                            originErrorText=shell.Root.Q<Label>("blockPreparationError")?.text??"<missing>";
                        originRejected=originErrorText==expectedOriginFailure&&game.CartCount==0&&
                            shell.PartActionGuardRejectedCount-originGuardBefore==0&&
                                shell.PartActionLastRejectedReasonForChecks==expectedOriginFailure;
                        }
                        finally
                        {
                            if(physicalBlock.location==PartLocation.Inventory&&
                                SharedInventory.StorageOf(physicalBlock.storageId)!=SharedInventory.StorageOf(originalBlockStorage))
                                SharedInventory.Move(profile,changedBlockId,InventoryKind.Storage,SharedInventory.StorageOf(originalBlockStorage));
                        }
                        report.AppendLine("CART_PREVIEW_DESTINATIONS|block-origin-rejected|asset="+changedBlockId+
                            "|storage="+SharedInventory.StorageOf(originalBlockStorage)+"->"+guardStorage+
                            "|error="+originErrorText+"|cart="+CartPreviewRows(game.CartLines)+
                            "|guardCounterDelta="+(shell.PartActionGuardRejectedCount-originGuardBefore));
                        Check(originRejected,"Actual mode control rejects a stale detail origin after the real block storage move");
                    }
                    if(shell.ModalOpen)yield return Click(input,"dialogCancel");
                    if(physicalBlock!=null&&guardStorage!=null)SharedInventory.Move(profile,changedBlockId,InventoryKind.Storage,SharedInventory.StorageOf(originalBlockStorage));
                    shell.Refresh();yield return WaitParts();
                }
                else if(shell.ModalOpen)yield return Click(input,"dialogCancel");
                Check(game.CartCount==0,"Block plan confirmation and origin rejection leave no staged test rows after cleanup");

                // Select an actual nonselected same-model car and click the rendered destination.
                // The target selection displays the existing guard; its disabled Add cannot dispatch.
                yield return OpenCartPreviewOwnedPart(input,changedBlockId,"changed block nonselected-car guard");
                int nonselectedReopenCandidateBefore=candidateSummaries;
                yield return Click(input,"partMove");
                var otherTarget=shell.Root.Q<Button>("inventoryMoveTarget:Car:"+other.instanceId);
                Check(otherTarget!=null,"The actual nonselected car destination is rendered for changed block guard");
                if(otherTarget!=null)
                {
                    string rowsBeforeRejectedDestination=CartPreviewRows(game.CartLines);
                    yield return PartDialogClick(input,otherTarget.name);
                    ExpectCandidateScopes(nonselectedReopenCandidateBefore,0,"changed block nonselected guard without a prior cart row");
                    var error=shell.Root.Q<Label>("inventoryMoveError");
                    bool rejected=error!=null&&error.text=="Select this car before preparing its block installation."&&
                        shell.Root.Q<Button>("dialogAddmove")?.enabledInHierarchy!=true&&CartPreviewRows(game.CartLines)==rowsBeforeRejectedDestination;
                    Check(rejected,"Changed block to a nonselected car shows the existing selection-required rejection");
                    report.AppendLine("CART_PREVIEW_DESTINATIONS|block-other-car|selectedInspection="+selected.instanceId+
                        "|targetOwner="+other.instanceId+"|error="+(error?.text??"<missing>")+"|addEnabled="+
                        (shell.Root.Q<Button>("dialogAddmove")?.enabledInHierarchy==true));
                }
                if(shell.ModalOpen)yield return Click(input,"dialogCancel");
                int findingDelta=NativeCartPreviewCandidateProbe.FindingCount-candidateFindingsBefore;
                int faultDelta=NativeCartPreviewCandidateProbe.DiagnosticFaultCount-candidateFaultsBefore;
                int scopeDelta=NativeCartPreviewCandidateProbe.CompletedCount-candidateScopesBefore;
                report.AppendLine("CART_PREVIEW_DESTINATIONS|candidate-summary|observed="+candidateSummaries+
                    "|scopeDelta="+scopeDelta+"|strictFindingDelta="+findingDelta+"|probeFaultDelta="+faultDelta+
                    "|lastStrictFinding="+(findingDelta>0?NativeCartPreviewCandidateProbe.LastFinding:"<none-new>"));
                Check(candidateSummaries==14&&scopeDelta==14,"All fourteen per-family actual candidate scopes were inspected as bounded snapshots");
                Check(faultDelta==0,"Native candidate observation reported no diagnostic fault");
                Check(findingDelta==0,"Actual candidate source, JSON counter, RNG, capture clock and sequence remain strict-unchanged");
                report.AppendLine("CART_PREVIEW_DESTINATIONS|end|completed=true|cart="+CartPreviewRows(game.CartLines)+
                    "|cash="+profile.cash+"|cashBeforeBudget="+cashBeforeBudget+"|fixtureCashBudget="+fixtureCashBudget+
                    "|reputation="+profile.reputation+"|reputationBeforeBudget="+reputationBeforeBudget+
                    "|fixtureReputationBudget="+fixtureReputationBudget+
                    "|selected="+(profile.SelectedCar?.instanceId??"<none>")+"|other="+other.instanceId+"|boughtOther="+boughtCar+
                    "|boughtDonor="+boughtDonorCar+
                    "|detachedEngine="+detached.instanceId);
            }
            finally
            {
                try
                {
                    NativeCartPreviewCandidateProbe.Completed=previousCandidateObserver;
                    NativeCartPreviewCandidateProbe.Observed=previousCandidateSummaryObserver;
                    if(shell.ModalOpen)typeof(NativeMenuShell).GetMethod("CloseDialog",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(shell,null);
                    if(staleStorageToRestore!=null&&!profile.inventory.storage.Contains(staleStorageToRestore))
                        profile.inventory.storage.Insert(Math.Max(0,staleStorageRestoreIndex),staleStorageToRestore);
                    if(cleanupAuthorized&&game.CartCount>0)game.ClearNativeCart();
                }
                catch(Exception e){report.AppendLine("FAIL|cart-preview-destinations|cleanup="+e.GetType().FullName+"|"+e.Message);}
            }
        }

        IEnumerator CartPreviewPassiveEnvelopeChecks(WindowsKeyboardProbe input)
        {
            var profile=game.Profile;
            if(!FramePacingProbe.Active){Check(false,"Passive envelope requires the whole-frame FramePacingProbe");yield break;}
            if(game.CartCount!=0){Check(false,"Passive envelope starts with an empty isolated cart");yield break;}
            if(profile.SelectedCar==null){Check(false,"Passive envelope has a selected car");yield break;}
            if(!shell.Visible)yield return Key(input,0x77);
            if(game.ScreenName!="Parts")yield return Click(input,"navBuild");
            yield return WaitParts();
            if(game.PartsPage!="Shop"){yield return Click(input,"partsSource:Shop");yield return WaitParts();}
            var car=profile.SelectedCar;
            var spec=profile.catalog.Resolve(car.vehicleId,MechanicsOwnership.Definitions(profile,car),car.tune).spec;
            var candidate=profile.catalog.parts.Where(d=>d!=null&&d.category!=PartCategory.CylinderBlock&&MechanicalCatalog.EngineSlot(d.category)&&d.price>=0&&
                    profile.reputation>=d.reputationRequired&&PartsMarket.FitsAny(profile,d)&&profile.catalog.Fits(spec,d)&&PartsMarket.Stock(profile,d)!=null)
                .Select(d=>{int count=PartsMarket.Count(profile,car,d);return new{Definition=d,Count=count,
                    Quote=count>0?PartsMarket.Quote(profile,d,count,unit:false):null};})
                .Where(x=>x.Count>0&&x.Quote!=null&&x.Quote.cost>0&&x.Quote.cost<=profile.cash)
                .OrderBy(x=>x.Quote.localUnits==x.Count?0:1).ThenBy(x=>x.Quote.cost).ThenBy(x=>x.Definition.id,StringComparer.Ordinal).FirstOrDefault();
            if(candidate==null){Check(false,"An actual authored part can be purchased for the passive Move envelope");yield break;}
            const int fixtureCashBudget=250000;
            int cashBefore=profile.cash;profile.cash=checked(profile.cash+fixtureCashBudget);
            string id=PartOperations.Buy(profile,profile.catalog,car.instanceId,candidate.Definition.id,false);
            profile.revision=checked(profile.revision+1);shell.Refresh();yield return WaitParts();
            report.AppendLine("CART_PREVIEW_PASSIVE|fixture|definition="+candidate.Definition.id+"|id="+id+
                "|quote="+candidate.Quote.cost+"|cash="+cashBefore+"->"+profile.cash+"|budget="+fixtureCashBudget+
                "|candidateObserver=null|capture="+RuntimeEntropy.Capture+"|effectiveUtc="+
                RuntimeEntropy.UtcNow.ToString("O",CultureInfo.InvariantCulture));
            if(game.PartsPage!="Inventory"){yield return Click(input,"partsSource:Inventory");yield return WaitParts();}
            var passiveStorage=SharedInventory.Containers(profile).FirstOrDefault(c=>c.kind==InventoryKind.Storage);
            if(passiveStorage==null){Check(false,"Passive envelope has an actual Storage target");yield break;}
            string passiveTargetName="inventoryMoveTarget:Storage:"+passiveStorage.id;
            yield return OpenCartPreviewOwnedPart(input,id,"passive Move callback-to-actionable envelope");
            var previous=NativeCartPreviewCandidateProbe.Completed;
            var previousObserved=NativeCartPreviewCandidateProbe.Observed;
            string previousPhase=FramePacingProbe.OverridePhase;
            NativeCartPreviewCandidateProbe.Completed=null;
            NativeCartPreviewCandidateProbe.Observed=null;
            Check(NativeCartPreviewCandidateProbe.Completed==null&&NativeCartPreviewCandidateProbe.Observed==null,
                "Passive envelope begins with both candidate observers disabled before timing");
            long startTicks=0,actionTicks=0,envelopeEndTicks=0,cancelStartTicks=0,cancelEndTicks=0;
            int startFrame=0,actionFrame=0,afterYieldFrame=0,cancelFrame=0,afterCancelYieldFrame=0;
            DateTime effectiveBefore=RuntimeEntropy.UtcNow,wallBefore=DateTime.UtcNow;
            long entropyTicks=effectiveBefore.Ticks,wallTicks=wallBefore.Ticks;
            DateTimeKind wallKind=wallBefore.Kind;
            bool actionable=false,cancelled=false;
            bool observersStayedDisabled=false;
            bool measurementClosed=false;
            try
            {
                FramePacingProbe.OverridePhase="cart-preview-passive-envelope";
                startFrame=Time.frameCount;startTicks=Stopwatch.GetTimestamp();
                yield return Click(input,"partMove");
                var target=shell.Root.Q<Button>(passiveTargetName);
                if(target!=null)yield return PartDialogClick(input,passiveTargetName);
                var add=shell.Root.Q<Button>("dialogAddmove");
                var dyno=shell.Root.Q("inventoryMoveDyno");
                var preview=shell.Root.Q<VisualElement>("inventoryMovePreview");
                bool warningVisible=false;
                if(preview!=null)foreach(var child in preview.Children())
                    if(child is Label label&&label.text!=null&&label.text.StartsWith("Warnings · ",StringComparison.Ordinal))
                        warningVisible=label.worldBound.width>0&&label.worldBound.height>0&&!label.ClassListContains("hidden");
                bool dynoVisible=dyno!=null&&dyno.worldBound.width>0&&dyno.worldBound.height>0&&!dyno.ClassListContains("hidden");
                var targetPick=target==null?null:shell.Root.panel?.Pick(target.worldBound.center);
                bool targetVisible=target!=null&&target.worldBound.width>0&&target.worldBound.height>0&&target.enabledInHierarchy&&
                    targetPick!=null&&(ReferenceEquals(targetPick,target)||target.Contains(targetPick));
                var addPick=add==null?null:shell.Root.panel?.Pick(add.worldBound.center);
                bool addVisible=add!=null&&add.enabledInHierarchy&&add.worldBound.width>0&&add.worldBound.height>0&&
                    addPick!=null&&(ReferenceEquals(addPick,add)||add.Contains(addPick));
                actionable=shell.ModalOpen&&targetVisible&&addVisible&&dynoVisible&&warningVisible;
                actionFrame=Time.frameCount;actionTicks=Stopwatch.GetTimestamp();
                yield return null;
                afterYieldFrame=Time.frameCount;
                cancelStartTicks=Stopwatch.GetTimestamp();
                yield return Click(input,"dialogCancel");
                cancelled=!shell.ModalOpen;cancelFrame=Time.frameCount;
                cancelEndTicks=Stopwatch.GetTimestamp();
                yield return null;
                afterCancelYieldFrame=Time.frameCount;envelopeEndTicks=Stopwatch.GetTimestamp();
                observersStayedDisabled=NativeCartPreviewCandidateProbe.Completed==null&&NativeCartPreviewCandidateProbe.Observed==null;
                FramePacingProbe.OverridePhase=previousPhase;
                measurementClosed=true;
            }
            finally
            {
                if(!measurementClosed)FramePacingProbe.OverridePhase=previousPhase;
                NativeCartPreviewCandidateProbe.Completed=previous;
                NativeCartPreviewCandidateProbe.Observed=previousObserved;
            }
            if(shell.ModalOpen)typeof(NativeMenuShell).GetMethod("CloseDialog",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(shell,null);
            var effective=RuntimeEntropy.UtcNow;
            var wall=DateTime.UtcNow;
            double envelopeMs=(actionTicks-startTicks)*1000.0/Stopwatch.Frequency;
            double fullSpanMs=(envelopeEndTicks-startTicks)*1000.0/Stopwatch.Frequency;
            double cancelMs=(cancelEndTicks-cancelStartTicks)*1000.0/Stopwatch.Frequency;
            report.AppendLine("CART_PREVIEW_PASSIVE|result|frameProbeActive="+FramePacingProbe.Active+
                "|phase=cart-preview-passive-envelope|startFrame="+startFrame+"|actionFrame="+actionFrame+
                "|frameAfterAction="+afterYieldFrame+"|cancelFrame="+cancelFrame+"|frameAfterCancel="+afterCancelYieldFrame+
                "|actionable="+actionable+"|dynoAndWarningVisible="+actionable+
                "|cancelled="+cancelled+"|capture="+RuntimeEntropy.Capture+"|effectiveUtc="+entropyTicks+"->"+
                effective.Ticks+":"+(int)effective.Kind+"|wallUtc="+wallTicks+":"+(int)wallKind+"->"+
                wall.Ticks+":"+(int)wall.Kind+"|requestToActionableMs="+envelopeMs.ToString("F3",CultureInfo.InvariantCulture)+
                "|requestThroughCancelAndFrameMs="+fullSpanMs.ToString("F3",CultureInfo.InvariantCulture)+
                "|frames="+startFrame+"->"+actionFrame+"->"+afterYieldFrame+"|cancelMs="+
                cancelMs.ToString("F3",CultureInfo.InvariantCulture)+"|observersStayedDisabled="+observersStayedDisabled+
                "|candidateObserverRestored="+
                (NativeCartPreviewCandidateProbe.Completed==previous)+"|candidateSummaryObserverRestored="+
                (NativeCartPreviewCandidateProbe.Observed==previousObserved));
            Check(actionable,"Passive envelope reached a fully visible actionable dyno/warnings result with no candidate observer");
            Check(cancelled,"Passive envelope exercised the actual rendered Cancel action");
            Check(observersStayedDisabled,"Both candidate observers remained disabled through the final measured frame");
            Check(afterYieldFrame>actionFrame,"Passive envelope includes a continuation frame after the actionable result");
        }

        IEnumerator OpenCartPreviewOwnedPart(WindowsKeyboardProbe input,string id,string label)
        {
            if(shell.ModalOpen)yield return Click(input,"dialogCancel");
            if(shell.PartBrowserDetailId!=null)
            {
                var back=shell.Root.Q<Button>("partBack");
                if(back==null)throw new InvalidOperationException("Existing part detail has no rendered Back control before bounded list wait / "+
                    label+" / "+CartPreviewBrowserState(id,CartPreviewExpectedSection(id)));
                yield return Click(input,back);
            }
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

        internal static string CartPreviewSelectedFacts(PlayerProfile profile)
        {
            var selected=profile?.SelectedCar;
            return selected==null?"<none>":selected.instanceId+"/"+selected.vehicleId+"/engine="+
                (selected.engineAssemblyId??"<null>");
        }

        internal static string CartPreviewOwnerFacts(PlayerProfile profile,InventoryKind kind,string id,string definitionId,string assetId)
        {
            if(profile==null)return "<no-candidate>";
            var definition=profile.catalog?.Find(definitionId);
            string category=definition?.category.ToString()??"<unknown-category>";
            if(kind==InventoryKind.Car)
            {
                var car=profile.cars?.FirstOrDefault(item=>item?.instanceId==id);
                if(car==null)return "Car:"+(id??"<null>")+"/<missing>";
                var binding=car.installed?.slots?.FirstOrDefault(slot=>slot.slot==category);
                return "Car:"+car.instanceId+"/model="+car.vehicleId+"/engine="+(car.engineAssemblyId??"<null>")+"/slot="+
                    category+"/part="+(binding?.partInstanceId??"<empty>")+"/contains="+(binding?.partInstanceId==assetId);
            }
            if(kind==InventoryKind.Engine)
            {
                var engine=profile.engines?.FirstOrDefault(item=>item?.instanceId==id);
                if(engine==null)return "Engine:"+(id??"<null>")+"/<missing>";
                var binding=engine.slots?.FirstOrDefault(slot=>slot.slot==category);
                return "Engine:"+engine.instanceId+"/model="+engine.vehicleDefinitionId+"/parent="+
                    (engine.parentVehicleId??"<null>")+"/slot="+category+"/part="+(binding?.partInstanceId??"<empty>")+"/contains="+
                    (binding?.partInstanceId==assetId);
            }
            if(kind==InventoryKind.Storage)
            {
                var container=profile.inventory?.storage?.FirstOrDefault(item=>item?.id==id);
                int parts=profile.parts?.Count(item=>item!=null&&item.location==PartLocation.Inventory&&SharedInventory.StorageOf(item.storageId)==id)??0;
                int units=profile.looseEngineElements?.Count(item=>item!=null&&SharedInventory.StorageOf(item.storageId)==id)??0;
                int engines=profile.engines?.Count(item=>item!=null&&string.IsNullOrEmpty(item.parentVehicleId)&&SharedInventory.StorageOf(item.storageId)==id)??0;
                return "Storage:"+(id??"<null>")+"/exists="+(container!=null)+"/name="+(container?.name??"<null>")+
                    "/parts="+parts+"/units="+units+"/engines="+engines+"/contains="+
                    SharedInventory.Entries(profile,InventoryKind.Storage,id).Any(item=>item.id==assetId);
            }
            return kind+":"+(id??"<null>")+"/<unsupported>";
        }

        internal static string CartPreviewPhysicalFacts(PlayerProfile profile,string assetId)
        {
            if(profile==null)return "<no-candidate>";
            var part=profile.parts?.FirstOrDefault(item=>item!=null&&item.instanceId==assetId);
            if(part!=null)return "Part:"+part.instanceId+"/definition="+part.definitionId+"/location="+part.location+
                "/owner="+(part.ownerId??"<null>")+"/slot="+(part.slot??"<null>")+"/storage="+(part.storageId??"<null>");
            var unit=profile.looseEngineElements?.FirstOrDefault(item=>item?.element?.id==assetId);
            if(unit!=null)return "Unit:"+assetId+"/definition="+unit.definitionId+"/role="+(unit.element.role??"<null>")+"/storage="+
                (unit.storageId??"<null>");
            return "<missing>";
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
