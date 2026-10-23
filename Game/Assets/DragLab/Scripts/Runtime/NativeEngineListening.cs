using System;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed class EngineListeningPlayback : IDisposable
    {
        internal readonly CarSimulation Car;
        internal readonly EngineAudio Audio;
        internal double Elapsed {get;private set;}
        internal int HighestGear {get;private set;}
        internal bool Playing {get;private set;}=true;
        readonly GameObject host;
        readonly double pullEnd,safeRpm;
        double accumulator;
        internal double Duration=>pullEnd+6;
        internal string Stage=>!Playing?(Elapsed>=Duration?"Sequence complete":"Stopped"):Elapsed<2?"Idle":Elapsed<6?"Revving":Elapsed<pullEnd?"Acceleration / shifting":Elapsed<pullEnd+1?"Lift-off / pressure release":"Return to idle";
        internal EngineListeningPlayback(InspectionBuild source,Transform parent)
        {
            Car=CarInspection.PrepareCar(source);
            Car.driver.shifting=ShiftMode.Automatic;Car.driver.launch=LaunchMode.Automatic;Car.driver.clutchAssist=true;Car.driver.throttleAssist=false;
            Car.state.rpm=Car.spec.idleRpm;
            BuildLimits.Capture(Car,out double physicalLimit,out _,out _,out _);safeRpm=Math.Max(Car.spec.idleRpm*1.1,Math.Min(Car.spec.redlineRpm,physicalLimit));
            Car.tune.shiftRpm=(float)Math.Min(Car.tune.shiftRpm,Math.Max(Car.spec.idleRpm*1.5,safeRpm*.78));
            pullEnd=6+DragMath.Clamp(6+Car.spec.gears.Length*TransmissionDynamics.ShiftSeconds(Car.transmission,Car.spec,Car.tune)*1.5,8,14);
            host=new GameObject("Car engine listening");host.transform.SetParent(parent,false);
            try{Audio=host.AddComponent<EngineAudio>();Audio.Initialize();Audio.Configure(Car.spec,Car.tune);}
            catch{UnityEngine.Object.Destroy(host);throw;}
        }
        internal void Tick(double seconds)
        {
            if(!Playing)return;
            accumulator+=Math.Min(seconds,.1);
            while(accumulator>=DragMath.FixedStep){
                double throttle=Elapsed<2?0:Elapsed<3.2?.55:Elapsed<4?0:Elapsed<5.2?.8:Elapsed<6?0:Elapsed<pullEnd?.95:0;
                if(Elapsed<6&&throttle>0){double target=Car.spec.idleRpm+(safeRpm-Car.spec.idleRpm)*(Elapsed<4?.55:.7);throttle*=DragMath.Clamp((target-Car.state.rpm)/450,0,1);}
                Car.IndependentStep(DragMath.FixedStep,new DriverInput{throttle=throttle},Elapsed<6||Elapsed>=pullEnd+1,false);
                HighestGear=Math.Max(HighestGear,Car.state.gear);Elapsed+=DragMath.FixedStep;accumulator-=DragMath.FixedStep;
                if(Elapsed>=Duration){Stop();return;}
            }
            Audio.Render(Car.state,!Car.state.engineFailed&&!Car.state.stalled,.8f,tiresActive:false);
        }
        internal void Stop(){Playing=false;Audio?.Silence();}
        public void Dispose(){Stop();if(host!=null)UnityEngine.Object.Destroy(host);}
    }
    internal sealed partial class NativeMenuShell
    {
        EngineListeningPlayback engineListening;
        Label listeningStatus,listeningValues;
        PlayerProfile listeningProfile;
        long listeningRevision;
        string listeningContext;
        internal EngineListeningPlayback Listening=>engineListening;
        Button AddEngineListening(VisualElement parent,string id,Func<InspectionBuild> capture,Action<VisualElement,RacingSurface.Kind> decorate,Func<bool> estimated=null)
        {
            var button=new Button(()=>OpenEngineListening(capture,estimated?.Invoke()??false)){name=id,text="Hear engine",focusable=false,tabIndex=-1};button.AddToClassList("action-button");button.AddToClassList("car-stats-button");button.AddToClassList("car-detail-action-last");button.Add(new FilledMenuIcon("Play"));parent.Add(button);decorate(button,RacingSurface.Kind.SecondaryAction);
            button.style.marginTop=0;button.style.marginLeft=10;return button;
        }
        internal void OpenEngineListening(Func<InspectionBuild> capture,bool estimated=false)
        {
            if(ModalOpen||game.ScreenName!="Garage")return;
            string title="Hear engine";
            OpenDialog(title,estimated?"Estimated sound until this car is inspected. Listening does not use your fuel or change your parts.":"Listen to this car at idle, revving, shifting and lifting off. Your fuel and parts stay unchanged.",new[]{new DialogAction("Replay",()=>StartEngineListening(capture)),new DialogAction("Stop",StopEngineListening),new DialogAction("Close",CloseDialog)},StopEngineListening,content=>{
                listeningStatus=new Label{name="engineListeningStage"};listeningStatus.AddToClassList("car-detail-title");content.Add(listeningStatus);
                listeningValues=new Label{name="engineListeningValues"};content.Add(listeningValues);
                content.Add(new Label("Uses your Engine audio level. Turbo pressure release is heard only when the fitted hardware produces it."){enableRichText=false});
            });
            listeningProfile=game.Profile;listeningRevision=game.Profile.revision;listeningContext=dealerMode?dealerDetailId:ownedDetailId;StartEngineListening(capture);
        }
        void StartEngineListening(Func<InspectionBuild> capture)
        {
            StopEngineListening();
            if(!ModalOpen||game.Profile!=listeningProfile||game.Profile.revision!=listeningRevision){listeningStatus.text="Car changed; close and reopen listening.";return;}
            try{engineListening=new EngineListeningPlayback(capture(),transform);listeningStatus.text=engineListening.Stage;}
            catch(Exception){listeningStatus.text="Cannot start this engine";listeningValues.text="Reopen the car details and check its engine, condition and fluids.";}
        }
        void StopEngineListening(){engineListening?.Dispose();engineListening=null;if(listeningStatus!=null)listeningStatus.text="Stopped";}
        void TickEngineListening()
        {
            if(engineListening==null)return;
            if(!Visible||!ModalOpen||game.ScreenName!="Garage"||game.Profile!=listeningProfile||game.Profile.revision!=listeningRevision||listeningContext!=(dealerMode?dealerDetailId:ownedDetailId)){StopEngineListening();return;}
            engineListening.Tick(Time.unscaledDeltaTime);listeningStatus.text=engineListening.Stage;
            var car=engineListening.Car;
            listeningValues.text=car.state.rpm.ToString("F0")+" RPM · "+(car.state.gear==0?"Neutral":car.transmission.type==TransmissionType.CVT?"Drive":"Gear "+car.state.gear)+" · "+engineListening.Elapsed.ToString("F0")+" / "+engineListening.Duration.ToString("F0")+" s";
            if(car.state.engineFailed||car.state.stalled)listeningStatus.text="Engine stopped · check condition and fluids";
        }
        void OnApplicationFocus(bool focused){if(!focused)StopEngineListening();}
        void OnApplicationPause(bool suspended){if(suspended)StopEngineListening();}
    }
#if UNITY_EDITOR
    public static class EngineListeningDiagnostics
    {
        public static void Boost()
        {
            var vehicles=Resources.LoadAll<DragLab.Data.VehicleDefinition>("Data/Vehicles");
            var catalog=new DragLab.Domain.Mechanics.MechanicalCatalog(Array.ConvertAll(vehicles,v=>v.spec));
            var vehicle=Array.Find(vehicles,v=>v.spec.id=="rally-sedan");var tune=vehicle.CreateStockTune();
            var build=catalog.Resolve(vehicle.spec.id,catalog.Recipe(vehicle.spec.id,tune),tune);
            var car=new CarSimulation(build.spec,build.tune,new DriverSettings{shifting=ShiftMode.Automatic,launch=LaunchMode.Automatic,clutchAssist=true,throttleAssist=false});car.state.rpm=car.spec.idleRpm;
            double end=6+DragMath.Clamp(6+car.spec.gears.Length*TransmissionDynamics.ShiftSeconds(car.transmission,car.spec,car.tune)*1.5,8,14),peak=0;int gear=0;
            for(double t=0;t<end+6;t+=DragMath.FixedStep){
                double gas=t<2?0:t<3.2?.55:t<4?0:t<5.2?.8:t<6?0:t<end?.95:0;
                car.IndependentStep(DragMath.FixedStep,new DriverInput{throttle=gas},t<6||t>=end+1,false);peak=Math.Max(peak,car.state.turboBoostBar);gear=Math.Max(gear,car.state.gear);
                if(Math.Abs(t-Math.Round(t))<.001)Debug.Log("LISTEN PHYSICS "+t+" / "+car.state.rpm+" RPM / gear "+car.state.gear+" / boost "+car.state.turboBoostBar+" / failed "+car.state.engineFailed+" / stalled "+car.state.stalled);
            }
            Debug.Log("LISTEN BOOST "+car.induction.type+" / peak "+peak+" / releases "+car.state.boostReleaseCount+" / max gear "+gear);
        }
    }
#endif

}
