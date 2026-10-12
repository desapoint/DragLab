using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        readonly List<NativeTuningRange> tuningRanges=new List<NativeTuningRange>();
        readonly List<Action> tuningInputUpdates=new List<Action>();
        readonly List<RacingSurface> tuningInputSurfaces=new List<RacingSurface>();
        readonly List<SwitchSurface> tuningInputSwitches=new List<SwitchSurface>();
        string tuningInputsKey;
        PlayerProfile tuningInputsProfile;
        VisualElement tuningWheelPreview;
        float tuningLastPreview;
        Tune T=>game.DraftValues;
        void TuningEdited(){tuningError.text="";}
        void TuningInputSurface(VisualElement element,RacingSurface.Kind kind){AddSurface(element,kind);tuningInputSurfaces.Add(element.Q<RacingSurface>());}
        void ClearTuningInputs()
        {
            foreach(var surface in tuningInputSurfaces)surfaces.Remove(surface);tuningInputSurfaces.Clear();foreach(var surface in tuningInputSwitches)switchSurfaces.Remove(surface);tuningInputSwitches.Clear();
            tuningRanges.Clear();tuningInputUpdates.Clear();tuningWheelPreview=null;
            foreach(string tab in new[]{"Engine","Induction","Drivetrain","Wheels"})tuningControls.Q<ScrollView>("tuningArea"+tab).Clear();
        }
        VisualElement TuningRow(string tab,string id,string label,string help)
        {
            var row=new VisualElement{name="tuneRow:"+id};row.AddToClassList("tuning-input");tuningControls.Q<ScrollView>("tuningArea"+tab).Add(row);
            var caption=new VisualElement();caption.AddToClassList("tuning-input-caption");row.Add(caption);caption.Add(new Label(label));
            var info=new Button(()=>OpenDialog(label,help,new[]{new DialogAction("Close",CloseDialog)})){name="tuneInfo:"+id,text="i",focusable=false,tabIndex=-1};info.AddToClassList("info-button");caption.Add(info);return row;
        }
        void TuneRange(string tab,string id,string label,Func<float> get,Action<float> set,float min,float max,float reset,float step,float fine,string unit,string help,bool allowed=true)
            =>TuneRange(tab,id,label,get,set,()=>min,()=>max,()=>reset,step,fine,unit,help,allowed);
        void TuneRange(string tab,string id,string label,Func<float> get,Action<float> set,Func<float> min,Func<float> max,Func<float> reset,float step,float fine,string unit,string help,bool allowed=true)
        {
            var row=TuningRow(tab,id,label,help);var range=new NativeTuningRange(id,get,set,min,max,reset,step,fine,unit,TuningEdited,error=>tuningError.text=error);row.Add(range);range.Allow(allowed);tuningRanges.Add(range);
            tuningInputUpdates.Add(range.Refresh);
            if(!allowed){range.Slider.tooltip=help;range.Number.tooltip=help;}
        }
        void TuneChoice(string tab,string id,string label,string[] labels,Func<int> get,Action<int> set,string help,bool allowed=true)
        {
            var row=TuningRow(tab,id,label,help);var group=new VisualElement();group.AddToClassList("choice-group");group.AddToClassList("tuning-choice");row.Add(group);var buttons=new List<Button>();
            for(int i=0;i<labels.Length;i++){int value=i;var button=new Button(()=>{set(value);TuningEdited();}){name="tuneChoice:"+id+":"+i,text=labels[i],focusable=false,tabIndex=-1};button.AddToClassList("choice-button");button.AddToClassList(i==0?"choice-first":i==labels.Length-1?"choice-last":"choice-middle");group.Add(button);TuningInputSurface(button,i==0?RacingSurface.Kind.ChoiceFirst:i==labels.Length-1?RacingSurface.Kind.ChoiceLast:RacingSurface.Kind.ChoiceMiddle);buttons.Add(button);}
            group.SetEnabled(allowed);group.tooltip=help;tuningInputUpdates.Add(()=>{int index=get();for(int i=0;i<buttons.Count;i++)buttons[i].EnableInClassList("selected",i==index);});
        }
        void TuneSwitch(string tab,string id,string label,Func<bool> get,Action<bool> set,string help)
        {
            var row=TuningRow(tab,id,label,help);var toggle=new Toggle{name="tuneSwitch:"+id};toggle.AddToClassList("setting-toggle");row.Add(toggle);var surface=new SwitchSurface(toggle);toggle.Q(className:"unity-toggle__input").Add(surface);switchSurfaces.Add(surface);tuningInputSwitches.Add(surface);
            toggle.RegisterValueChangedCallback(e=>{set(e.newValue);TuningEdited();});tuningInputUpdates.Add(()=>{toggle.SetValueWithoutNotify(get());surface.SetValue(get());});
        }
        void TuneReadout(string tab,string id,string text)
        {var label=new Label(text){name="tuneReadout:"+id,enableRichText=false};label.AddToClassList("tuning-readout");tuningControls.Q<ScrollView>("tuningArea"+tab).Add(label);}
        void RefreshTuningInputs(bool available)
        {
            var profile=game.Profile;var car=profile.SelectedCar;string mode=available&&T.awdConfigured&&T.awd!=null?T.awd.mode.ToString():"Factory";
            string key=profile.revision+"/"+car?.instanceId+"/"+car?.engineAssemblyId+"/"+mode+"/"+JsonUtility.ToJson(SharedUnitSettings.Current);
            if(tuningInputsProfile!=profile||tuningInputsKey!=key){
                tuningInputsProfile=profile;tuningInputsKey=key;ClearTuningInputs();
                if(available)BuildTuningInputs(profile.catalog.Resolve(car.vehicleId,MechanicsOwnership.Definitions(profile,car),T),profile.catalog.Spec(car.vehicleId));
                ExcludeTabTraversal();
            }
            foreach(var update in tuningInputUpdates)update();
        }
        void RefreshTuningWheelPreview(BuildResolution build)
        {if(tuningWheelPreview==null)return;tuningWheelPreview.Clear();tuningWheelPreview.Add(CarPreview(build,game.Profile.SelectedCar.paintHex));}
        void BuildTuningInputs(BuildResolution build,VehicleSpec stock)
        {
            var spec=build.spec;var e=build.tune;var defaults=game.vehicles.First(v=>v.spec.id==stock.id).CreateStockTune();defaults.ClampTo(spec);var units=SharedUnitSettings.Current;
            TuneRange("Engine","launch","Launch RPM",()=>T.launchRpm,v=>T.launchRpm=v,spec.idleRpm,spec.redlineRpm-200,defaults.launchRpm,100,10,"RPM","Target for launch assists; it does not turn a foot-controlled launch into an electronic two-step.");
            TuneRange("Engine","camTiming","Cam timing",()=>T.camTimingDegrees,v=>T.camTimingDegrees=v,-6,6,0,1,.1f,"°","Advance (+) favors lower-RPM filling; retard (−) shifts filling upward. Match cam lift, spring strength and piston reliefs: timing reduces piston-to-valve clearance. Requires an adjustable timing drive.",spec.valvetrain.timingAdjustable);
            TuneSwitch("Engine","coldStart","Cold start",()=>T.coldStart,v=>T.coldStart=v,"Start cold and warm up through the same engine model; carbureted engines may stumble or hesitate.");
            TuneRange("Engine","limiterRpm","Rev limiter",()=>T.revLimitRpm>0?T.revLimitRpm:spec.redlineRpm,v=>T.revLimitRpm=v,spec.idleRpm+1500,spec.redlineRpm*1.2f,spec.redlineRpm,100,10,"RPM","Requires a programmable limiter. The applied threshold is capped by installed safe RPM; disabling the limiter permits engine abuse.",e.partLimiterAdjustable);
            TuneChoice("Engine","limiterStyle","Limiter behavior",new[]{"Factory","Bounce","Soft","Off"},()=>e.partLimiterAdjustable?(int)T.limiter:(int)spec.engineBehavior.limiter,v=>T.limiter=(LimiterStyle)v,"Factory uses the module default. Off removes RPM-cut protection. Requires a programmable limiter.",e.partLimiterAdjustable);
            CutChoice("limiterCut","Limiter cut",()=>T.limiterCut,v=>T.limiterCut=v,e.partLimiterAdjustable,spec);
            TuneChoice("Engine","twoStep","Two-step",new[]{"Factory","Off","On"},()=> (int)T.twoStep,v=>T.twoStep=(EquipmentSetting)v,"Electronic launch cut requires programmable two-step hardware. Launch RPM is its requested threshold; a basic module has a fixed threshold.",e.partTwoStepAdjustable);
            TuneChoice("Engine","twoStepStyle","Two-step behavior",new[]{"Factory","Bounce","Soft"},()=> (int)T.twoStepStyle,v=>T.twoStepStyle=(LimiterStyle)v,"Hard cuts bounce; soft cuts progressively reduce output. Requires programmable two-step hardware.",e.partTwoStepAdjustable);
            CutChoice("twoStepCut","Two-step cut",()=>T.twoStepCut,v=>T.twoStepCut=v,e.partTwoStepAdjustable,spec);
            TuneRange("Engine","shiftLight","Shift light",()=>T.shiftLightRpm>0?T.shiftLightRpm:(float)e.ShiftLightRpm(spec),v=>T.shiftLightRpm=v,spec.idleRpm+1000,spec.redlineRpm,spec.redlineRpm-200,100,10,"RPM",e.partShiftLight?"Adjustable threshold requires a programmable shift-light module.":"No shift light fitted. Install a shift-light module.",e.partShiftLightAdjustable);
            var induction=new InductionProfile(spec,e);bool boosted=induction.turboCount+induction.superchargerCount>0,turbo=induction.turboCount>0;
            TuneReadout("Induction","hardware",boosted?induction.type+" · "+induction.turboCount+" turbo / "+induction.superchargerCount+" supercharger":"Naturally aspirated");
            Func<float> boostBase=()=> (float)((turbo?induction.data.turboBoostBar:induction.data.superchargerBoostBar*T.SuperchargerPulley*T.SuperchargerPulley)*(e.partsResolved?e.partBoost:1));
            Func<float,float> pressure=bar=>(float)DisplayUnits.Pressure(bar,units.pressure);float ps=units.pressure==PressureUnit.Bar?.01f:.5f;
            if(boosted)TuneRange("Induction","boost","Boost target",()=>pressure(boostBase()*(float)(e.partBoostAdjustable?T.BoostSetting:e.partFixedBoost)),v=>T.boostSetting=(float)DisplayUnits.ToBar(v,units.pressure)/Mathf.Max(.001f,boostBase()),()=>pressure(boostBase()*e.partBoostMin),()=>pressure(boostBase()*e.partBoostMax),()=>pressure(boostBase()),ps,ps/10,DisplayUnits.PressureLabel(units.pressure),"Requested compressor target within the kit's calibration range. Turbo target is shown for twincharged setups; actual manifold boost also depends on flow, pulley/load and pressure limits. Requires adjustable boost hardware.",e.partBoostAdjustable);
            if(induction.superchargerCount>0)TuneRange("Induction","pulley","Pulley drive ratio",()=> (float)T.SuperchargerPulley,v=>T.superchargerPulley=v,e.partPulleyMin,e.partPulleyMax,1,.05f,.005f,"×","Higher drive speed increases boost and parasitic load; changing physical compressor size requires a part swap. Requires an interchangeable-pulley kit.",e.partPulleyAdjustable);
            if(turbo){TuneChoice("Induction","valve","Blow-off valve",new[]{"Kit","Recirc","Hiss","Chirp","Flutter"},()=> (int)(e.partValveAdjustable?T.blowOffValve:e.partFixedValve),v=>T.blowOffValve=(BlowOffType)v,"Pressure release behavior and sound come from the installed adjustable kit. Physical turbo size and unit count require replacing hardware.",e.partValveAdjustable);
                bool controls=e.controlLevel>0&&spec.engineBehavior.fueling!=FuelSystem.Diesel;
                TuneChoice("Induction","preload","Launch preload",new[]{"Factory","Off","On"},()=> (int)T.launchPreload,v=>T.launchPreload=(EquipmentSetting)v,"Builds turbo energy during launch cut. Requires a programmable controller and supported combustion hardware; diesel has no spark-based preload.",controls);
                TuneChoice("Induction","antiLag","Anti-lag",new[]{"Factory","Off","On"},()=> (int)T.antiLag,v=>T.antiLag=(EquipmentSetting)v,"Maintains exhaust energy during lifts/shifts, with fuel/heat/wear costs. Requires supported combustion and programmable controls.",controls);}
            TuneReadout("Drivetrain","gearbox",TransmissionDynamics.DisplayName(TransmissionDynamics.Configuration(spec,e)));
            TuneRange("Drivetrain","finalDrive","Final drive",()=>e.partFinalDriveAdjustable?T.finalDrive:e.finalDrive,v=>T.finalDrive=v,e.partFinalDriveMin,e.partFinalDriveMax,Mathf.Clamp(spec.finalDrive,e.partFinalDriveMin,e.partFinalDriveMax),.05f,.005f,":1","Higher ratios increase wheel torque and RPM at a given speed, reducing speed per gear. Requires a gearbox with interchangeable gearsets.",e.partFinalDriveAdjustable);
            TuneRange("Drivetrain","shiftRpm","Automatic shift RPM",()=>T.shiftRpm,v=>T.shiftRpm=v,spec.idleRpm+1500,spec.redlineRpm-100,defaults.shiftRpm,100,10,"RPM","Target for automatic shifting; it does not override transmission hardware or downshift protection.");
            BuildAwdTuning(spec,e);
            tuningWheelPreview=new VisualElement{name="tuningWheelPreview"};tuningWheelPreview.AddToClassList("tuning-wheel-preview");tuningControls.Q<ScrollView>("tuningAreaWheels").Add(tuningWheelPreview);RefreshTuningWheelPreview(build);
            float radius=(float)(stock.wheelRadiusM/DisplayUnits.MetresPerInch);
            TuneRange("Wheels","radius","Tire outer radius",()=>radius*(float)T.WheelDiameter,v=>T.wheelDiameter=v/radius,radius*.8f,radius*1.6f,radius,.1f,.01f,"in","External radius including rubber, not rim diameter. Larger tires change gearing, rotating inertia and load; extra mass can bog the engine.");
            TuneRange("Wheels","width","Tire width",()=> (float)T.WheelWidth,v=>T.wheelWidth=v,.8f,1.6f,1,.05f,.005f,"×","Relative to the car's reference width. More width can add grip but increases wheel/tire mass; it is not a guaranteed improvement.");
            TuneRange("Wheels","pressure","Tire pressure",()=>pressure(T.tirePressurePsi/(float)DisplayUnits.PsiPerBar),v=>T.tirePressurePsi=(float)(DisplayUnits.ToBar(v,units.pressure)*DisplayUnits.PsiPerBar),pressure((float)(18/DisplayUnits.PsiPerBar)),pressure((float)(42/DisplayUnits.PsiPerBar)),pressure((float)(defaults.tirePressurePsi/DisplayUnits.PsiPerBar)),units.pressure==PressureUnit.Bar?.05f:1,units.pressure==PressureUnit.Bar?.005f:.1f,DisplayUnits.PressureLabel(units.pressure),"Pressure affects contact, grip and wear. Very low/high settings can hurt traction. Values convert to the same physical pressure in Settings units.");
            TuneRange("Wheels","driverMass","Driver mass",()=>T.driverMassKg,v=>T.driverMassKg=v,0,200,75,1,.1f,"kg","Adds physical mass, changing acceleration, axle loads and grip.");
            TuneRange("Wheels","payload","Ballast",()=>T.payloadMassKg,v=>T.payloadMassKg=v,0,1000,0,5,.5f,"kg","Added payload has real mass and load-transfer effects. Its position/height determine balance.");
            TuneRange("Wheels","payloadPosition","Ballast position",()=>T.payloadPositionM,v=>T.payloadPositionM=v,-.5f,spec.wheelbaseM+.5f,1.5f,.05f,.005f,"m","Distance from the front axle; changes axle load and traction balance.");
            TuneRange("Wheels","payloadHeight","Ballast height",()=>T.payloadHeightM,v=>T.payloadHeightM=v,0,2,.45f,.05f,.005f,"m","Height increases longitudinal weight transfer under acceleration/braking.");
        }
        void CutChoice(string id,string label,Func<CutMethod> get,Action<CutMethod> set,bool programmable,VehicleSpec spec)
        {bool injection=spec.engineBehavior.fueling==FuelSystem.Injection;TuneChoice("Engine",id,label,new[]{"Factory","Fuel","Ignition"},()=>injection?(int)get():spec.engineBehavior.fueling==FuelSystem.Diesel?(int)CutMethod.Fuel:(int)CutMethod.Ignition,v=>set((CutMethod)v),"Fuel cut stops injected fuel; ignition cut stops spark while fuel can still pass. Carburetors require ignition cut; diesel compression ignition uses fuel cut. Editable on injected engines with programmable hardware.",programmable&&injection);}
        void BuildAwdTuning(VehicleSpec spec,Tune effective)
        {
            if(spec.drive!=DriveType.AWD)return;var defaults=spec.awd??new AwdSpec();bool forced=spec.fittedParts.Any(p=>p.category==PartCategory.Differential&&p.stage>0);var fixedMode=AxleDriveDynamics.Configuration(spec,effective).mode;
            Func<AwdSpec> get=()=>{var data=AxleDriveDynamics.Configuration(spec,T);if(forced)data.mode=fixedMode;return data;};Action<Action<AwdSpec>> set=edit=>{var data=get();edit(data);T.awd=data;T.awdConfigured=true;};var mode=get().mode;
            TuneChoice("Drivetrain","awdMode","AWD center",new[]{"Split","Open","Locked","Demand","Active","LSD"},()=> (int)get().mode,v=>set(d=>{d.mode=(AwdMode)v;if(d.mode==AwdMode.OpenCenter)d.frontBias=Mathf.Clamp(d.frontBias,.05f,.95f);}),"Open center sends rotation toward a disconnected/free branch. Locked center couples axle speed; on-demand progressively connects the secondary axle. Upgraded center hardware fixes the mode.",!forced);
            if(mode==AwdMode.LimitedSlipCenter)TuneReadout("Drivetrain","centerLimits","Installed bias "+get().torqueBiasRatio.ToString("0.0")+":1 · preload "+SharedUnitSettings.Torque(get().centerPreloadNm,"F0"));
            if(mode==AwdMode.FixedSplit||mode==AwdMode.OpenCenter||mode==AwdMode.ActiveBias)TuneRange("Drivetrain","frontBias","Front torque share",()=>get().frontBias*100,v=>set(d=>d.frontBias=v/100),mode==AwdMode.OpenCenter?5:0,mode==AwdMode.OpenCenter?95:100,defaults.frontBias*100,1,.1f,"%","Rear receives the remainder. Open center retains both output shares; bias is not a universal traction gain.");
            if(mode==AwdMode.ActiveBias){TuneRange("Drivetrain","minimumFront","Minimum front share",()=>get().minimumFront*100,v=>set(d=>d.minimumFront=v/100),()=>0,()=>get().maximumFront*100,()=>Mathf.Min(defaults.minimumFront,get().maximumFront)*100,1,.1f,"%","Lower bound for the active center controller.");TuneRange("Drivetrain","maximumFront","Maximum front share",()=>get().maximumFront*100,v=>set(d=>d.maximumFront=v/100),()=>get().minimumFront*100,()=>100,()=>Mathf.Max(defaults.maximumFront,get().minimumFront)*100,1,.1f,"%","Upper bound for the active controller; cannot be lower than the minimum.");}
            if(mode==AwdMode.OnDemand){TuneSwitch("Drivetrain","primaryFront","Front-primary drive",()=>get().primaryFront,v=>set(d=>d.primaryFront=v),"Off selects rear-primary drive.");TuneRange("Drivetrain","secondaryShare","Secondary axle limit",()=>get().maximumSecondary*100,v=>set(d=>d.maximumSecondary=v/100),0,100,defaults.maximumSecondary*100,1,.1f,"%","Maximum share available to the secondary coupling.");TuneRange("Drivetrain","couplingPreload","Coupling preload",()=>get().preload*100,v=>set(d=>d.preload=v/100),0,100,defaults.preload*100,1,.1f,"%","Throttle preload engages the secondary axle before large primary slip.");TuneRange("Drivetrain","slipThreshold","Slip threshold",()=>get().slipThreshold*100,v=>set(d=>d.slipThreshold=v/100),.1f,50,defaults.slipThreshold*100,.1f,.01f,"%","Primary axle slip needed for secondary coupling engagement.");}
            if(mode==AwdMode.OnDemand||mode==AwdMode.ActiveBias)TuneRange("Drivetrain","response","Coupling response",()=>get().responseSeconds,v=>set(d=>d.responseSeconds=v),.01f,2,defaults.responseSeconds,.01f,.001f,"s","Faster response engages torque sooner; it does not create extra grip.");
            if(mode!=AwdMode.LockedCenter)TuneSwitch("Drivetrain","isolateAxle","Isolate failed axle",()=>get().isolateFailedAxle,v=>set(d=>d.isolateFailedAxle=v),"Models hardware that can isolate a disconnected axle. Without it, an open center can lose drive through the free branch.");
            if(mode==AwdMode.OpenCenter){TuneRange("Drivetrain","shaftInertia","Free-shaft inertia",()=>get().freeShaftInertiaKgM2,v=>set(d=>d.freeShaftInertiaKgM2=v),.005f,.5f,defaults.freeShaftInertiaKgM2,.01f,.001f,"kg m²","Remaining disconnected shaft rotational inertia.");float factor=(float)DisplayUnits.Torque(1,SharedUnitSettings.Current.torque);TuneRange("Drivetrain","shaftDrag","Free-shaft friction",()=>get().freeShaftDragNm*factor,v=>set(d=>d.freeShaftDragNm=v/factor),0,10*factor,defaults.freeShaftDragNm*factor,.1f,.01f,DisplayUnits.TorqueLabel(SharedUnitSettings.Current.torque),"Free-shaft resisting torque; displayed in the selected torque unit.");TuneRange("Drivetrain","shaftViscous","Viscous drag (per rad/s)",()=>get().freeShaftViscousDrag*factor,v=>set(d=>d.freeShaftViscousDrag=v/factor),0,.05f*factor,defaults.freeShaftViscousDrag*factor,.001f,.0001f,DisplayUnits.TorqueLabel(SharedUnitSettings.Current.torque),"Resisting torque per rad/s of free-shaft speed.");}
        }
    }
}
