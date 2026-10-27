using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed class NativePartsIdleSnapshot
    {
        readonly PlayerProfile profile;
        readonly MechanicalCatalog catalog;
        readonly OwnedCar selectedCar;
        readonly InventoryState inventory;
        readonly PartsStockState market;
        readonly CartReservationState reservation;
        readonly IReadOnlyList<PartCartLine> cartCollection;
        readonly PartCartLine[] lines;
        readonly List<PartInstance> parts;
        readonly List<LooseEngineElement> looseUnits;
        readonly List<DealerStock> dealerRows;
        readonly List<NewPartStock> marketRows;
        readonly List<PartCartLine> reservationLines;
        readonly List<ReservedPartSupply> reservationSupply;
        readonly NativePartsOwnerContext owner;
        readonly string page,selectedOwnedVehicleId,selectedVehicleId,selectedCarId,selectedModel,selectedEngine;
        readonly long revision,storageCommit;
        readonly bool planned,usingReservation;
        readonly int authoringRevision,cash,reputation,marketVersion,marketSeed,marketGeneration,marketCount,dealerCount,partsCount,looseCount,reservationLineCount,reservationSupplyCount,vehicleSeed,vehicleGeneration,vehiclePurchases,vehicleRuns,vehicleKnowledge;
        readonly int? unitVersion;
        readonly SpeedUnit? unitSpeed;
        readonly PressureUnit? unitPressure;
        readonly TorqueUnit? unitTorque;

        internal NativePartsIdleSnapshot(PrototypeGame game,string partsPage,bool partsPlanned)
        {
            profile=game.Profile;catalog=profile?.catalog;selectedCar=profile?.SelectedCar;inventory=profile?.inventory;
            market=inventory?.market;reservation=inventory?.cart;cartCollection=game.CartLines;
            parts=profile?.parts;looseUnits=profile?.looseEngineElements;dealerRows=inventory?.dealer;marketRows=market?.parts;reservationLines=reservation?.lines;reservationSupply=reservation?.supply;
            lines=(game.CartLines??Array.Empty<PartCartLine>()).Select(NativePartsBaseReadInputs.CopyLineExact).ToArray();
            owner=NativePartsOwnerContext.Capture();page=partsPage;planned=partsPlanned;
            revision=profile?.revision??-1;storageCommit=profile?.storageCommit??-1;authoringRevision=catalog?.AuthoringRevision??-1;cash=profile?.cash??-1;reputation=profile?.reputation??-1;
            selectedOwnedVehicleId=profile?.selectedOwnedVehicleId;selectedVehicleId=profile?.selectedVehicleId;
            selectedCarId=selectedCar?.instanceId;selectedModel=selectedCar?.vehicleId;selectedEngine=selectedCar?.engineAssemblyId;
            usingReservation=inventory?.usingCartReservation??false;
            marketVersion=market?.version??-1;marketSeed=market?.seed??-1;marketGeneration=market?.generation??-1;marketCount=market?.parts?.Count??-1;dealerCount=inventory?.dealer?.Count??-1;partsCount=profile?.parts?.Count??-1;looseCount=profile?.looseEngineElements?.Count??-1;reservationLineCount=reservation?.lines?.Count??-1;reservationSupplyCount=reservation?.supply?.Count??-1;
            vehicleSeed=profile?.vehicleStock?.seed??-1;vehicleGeneration=profile?.vehicleStock?.generation??-1;vehiclePurchases=profile?.vehicleStock?.purchases??-1;vehicleRuns=profile?.vehicleStock?.completedRuns??-1;vehicleKnowledge=profile?.vehicleStock?.knowledgeRevision??-1;
            var units=SharedUnitSettings.Current;unitVersion=units?.version;unitSpeed=units?.speed;unitPressure=units?.pressure;unitTorque=units?.torque;
        }

        internal bool Matches(PrototypeGame game,string currentPage,bool currentPlanned)
        {
            var p=game.Profile;var car=p?.SelectedCar;var inv=p?.inventory;var currentMarket=inv?.market;var currentLines=game.CartLines;
            var currentOwner=NativePartsOwnerContext.Capture();var units=SharedUnitSettings.Current;
            if(!ReferenceEquals(p,profile)||!ReferenceEquals(p?.catalog,catalog)||(p?.catalog?.AuthoringRevision??-1)!=authoringRevision||!ReferenceEquals(car,selectedCar)||
                !ReferenceEquals(inv,inventory)||!ReferenceEquals(currentMarket,market)||!ReferenceEquals(inv?.cart,reservation)||
                !ReferenceEquals(p?.parts,parts)||!ReferenceEquals(p?.looseEngineElements,looseUnits)||!ReferenceEquals(inv?.dealer,dealerRows)||!ReferenceEquals(currentMarket?.parts,marketRows)||
                !ReferenceEquals(inv?.cart?.lines,reservationLines)||!ReferenceEquals(inv?.cart?.supply,reservationSupply)||
                !ReferenceEquals(currentLines,cartCollection)||!owner.Same(currentOwner)||!owner.IsCurrent()||
                (p?.revision??-1)!=revision||(p?.storageCommit??-1)!=storageCommit||(p?.cash??-1)!=cash||(p?.reputation??-1)!=reputation||
                p?.selectedOwnedVehicleId!=selectedOwnedVehicleId||p?.selectedVehicleId!=selectedVehicleId||
                car?.instanceId!=selectedCarId||car?.vehicleId!=selectedModel||car?.engineAssemblyId!=selectedEngine||
                currentPage!=page||currentPlanned!=planned||(inv?.usingCartReservation??false)!=usingReservation||
                (currentMarket?.version??-1)!=marketVersion||(currentMarket?.seed??-1)!=marketSeed||(currentMarket?.generation??-1)!=marketGeneration||
                (currentMarket?.parts?.Count??-1)!=marketCount||(inv?.dealer?.Count??-1)!=dealerCount||(p?.parts?.Count??-1)!=partsCount||(p?.looseEngineElements?.Count??-1)!=looseCount||(inv?.cart?.lines?.Count??-1)!=reservationLineCount||(inv?.cart?.supply?.Count??-1)!=reservationSupplyCount||
                (p?.vehicleStock?.seed??-1)!=vehicleSeed||(p?.vehicleStock?.generation??-1)!=vehicleGeneration||
                (p?.vehicleStock?.purchases??-1)!=vehiclePurchases||(p?.vehicleStock?.completedRuns??-1)!=vehicleRuns||(p?.vehicleStock?.knowledgeRevision??-1)!=vehicleKnowledge||
                units?.version!=unitVersion||units?.speed!=unitSpeed||units?.pressure!=unitPressure||units?.torque!=unitTorque)
                return false;
            int count=currentLines?.Count??0;if(count!=lines.Length)return false;
            for(int i=0;i<count;i++)if(!SameLine(lines[i],currentLines[i]))return false;
            return true;
        }

        internal static bool SameLine(PartCartLine a,PartCartLine b)
        {
            if(a==null||b==null)return a==b;
            if(a.definitionId!=b.definitionId||a.quantity!=b.quantity||a.install!=b.install||a.preparedBlock!=b.preparedBlock||a.preparationOwner!=b.preparationOwner||a.action!=b.action||a.assetId!=b.assetId||a.role!=b.role||a.destinationId!=b.destinationId||a.destination!=b.destination)return false;
            if(a.positions==null||b.positions==null)return a.positions==b.positions;
            return a.positions.SequenceEqual(b.positions,StringComparer.Ordinal);
        }
    }

    internal sealed class NativePartWindowBuild
    {
        internal sealed class Card
        {
            internal NativePartEntry Entry;
            internal Button Button;
            internal Label[] Labels;
            internal RacingSurface Surface;
            internal Action Click;
        }
        internal readonly int DataGeneration,PresentationGeneration,WindowGeneration,First,Last;
        internal readonly float Before,After,ScrollY,ViewportHeight,ViewportWidth;
        internal readonly List<Card> Cards=new List<Card>();
        internal readonly VisualElement StagedGrid,BeforeElement,AfterElement;
        internal bool CardsComplete=>Cards.Count>=Last-First;
        internal bool Complete{get;private set;}
        internal double CreateMilliseconds{get;private set;}
        internal int LastStepItems{get;private set;}
        internal NativePartWindowBuild(int dataGeneration,int presentationGeneration,int windowGeneration,NativeCardWindow window,float scrollY,float viewportHeight,float viewportWidth)
        {
            DataGeneration=dataGeneration;PresentationGeneration=presentationGeneration;WindowGeneration=windowGeneration;First=window.First;Last=window.Last;Before=window.Before;After=window.After;ScrollY=scrollY;ViewportHeight=viewportHeight;ViewportWidth=viewportWidth;
            StagedGrid=new VisualElement{name="partsGrid"};StagedGrid.AddToClassList("owned-cars-grid");
            BeforeElement=new VisualElement{name="partsWindowBefore",pickingMode=PickingMode.Ignore};AfterElement=new VisualElement{name="partsWindowAfter",pickingMode=PickingMode.Ignore};
            foreach(var spacer in new[]{BeforeElement,AfterElement}){spacer.style.width=Length.Percent(100);spacer.style.flexShrink=0;}
            BeforeElement.style.height=Before;AfterElement.style.height=After;
            if(Last>First)StagedGrid.Add(BeforeElement);
            if(Last>First)StagedGrid.Add(AfterElement);
        }
        internal void Stage(Card card){Cards.Add(card);if(Last>First)StagedGrid.Insert(StagedGrid.childCount-1,card.Button);}
        internal void Add(Card card,double milliseconds){Stage(card);CreateMilliseconds+=milliseconds;LastStepItems++;}
        internal void MarkPublished()=>Complete=true;
    }
}
