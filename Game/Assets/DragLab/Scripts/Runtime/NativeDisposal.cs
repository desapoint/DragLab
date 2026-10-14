using System;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        string disposalCarId,disposalKey;
        VisualElement disposalPage,disposalContent;
        internal bool DisposalOpen=>disposalCarId!=null&&game.ScreenName=="Garage"&&Visible;
        void InitializeDisposal()
        {
            disposalPage=new VisualElement{name="disposalPage"};disposalPage.AddToClassList("car-details-page");disposalPage.AddToClassList("hidden");root.Q(className:"workspace").Add(disposalPage);
            var heading=new VisualElement();heading.AddToClassList("car-details-heading");disposalPage.Add(heading);
            var back=new Button(BackFromDisposal){name="disposalBack",tooltip="Back to car"};back.AddToClassList("action-button");back.AddToClassList("car-back-button");heading.Add(back);AddSurface(back,RacingSurface.Kind.SecondaryAction);back.Add(new FilledMenuIcon("Back"));
            var title=new Label("Sell / Salvage");title.AddToClassList("car-detail-title");heading.Add(title);
            var frame=new VisualElement();frame.AddToClassList("car-details-frame");disposalPage.Add(frame);AddSurface(frame,RacingSurface.Kind.Panel);
            var scroll=new ScrollView{name="disposalScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("car-details-scroll");frame.Add(scroll);
            disposalContent=new VisualElement{name="disposalContent"};disposalContent.AddToClassList("car-details-content");scroll.Add(disposalContent);
            var button=new Button(()=>OpenDisposal(ownedDetailId)){name="ownedSellSalvage",text="Sell / Salvage"};button.AddToClassList("action-button");ownedUse.parent.Insert(1,button);AddSurface(button,RacingSurface.Kind.SecondaryAction);
        }
        void OpenDisposal(string id){if(ModalOpen||!game.Profile.cars.Exists(c=>c.instanceId==id))return;disposalCarId=id;disposalKey=null;Refresh();}
        void BackFromDisposal(){disposalCarId=null;disposalKey=null;Refresh();}
        void RefreshDisposal(string route)
        {
            if(route!="Garage"&&route!="Settings")disposalCarId=null;
            var car=game.Profile.cars.Find(c=>c.instanceId==disposalCarId);if(car==null)disposalCarId=null;
            bool show=route=="Garage"&&disposalCarId!=null&&!dealerMode;disposalPage.EnableInClassList("hidden",!show);if(!show)return;
            ownedDetails.AddToClassList("hidden");ownedScroll.AddToClassList("hidden");ownedToolbar.AddToClassList("hidden");
            string key=disposalCarId+"/"+game.Profile.revision;if(disposalKey==key)return;disposalKey=key;disposalContent.Clear();
            var name=new Label(VehicleCollection.DisplayName(game.Profile.catalog,car)){name="disposalCarName",enableRichText=false};name.AddToClassList("car-detail-title");disposalContent.Add(name);
            disposalContent.Add(CarPreview(MechanicsOwnership.Resolve(game.Profile,car,game.Profile.catalog),car.paintHex));
            ExcludeTabTraversal();
        }
    }
}
