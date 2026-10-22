using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Editor
{
    // Read-only authored catalogue/resolver audit. Never opens a player save,
    // edits authored assets, changes calibration, or touches the physical bench.
    public static class PartStatisticsAudit
    {
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        sealed class Finding { public string severity,code;public int count;public readonly List<string> examples=new List<string>(); }
        static readonly Dictionary<string,Finding> findings=new Dictionary<string,Finding>();
        static int checks,combinations,rejected;
        static string Q(object x)=>"\""+Convert.ToString(x,Inv).Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ")+"\"";
        static void Flag(string severity,string code,string example){string key=severity+"/"+code;if(!findings.TryGetValue(key,out var f))findings[key]=f=new Finding{severity=severity,code=code};f.count++;if(f.examples.Count<6)f.examples.Add(example);}
        static void Check(bool ok,string code,string reference){checks++;if(!ok)Flag("error",code,reference);}
        static double[] Curve(BuildResolution b){var profile=new InductionProfile(b.spec,b.tune);return Enumerable.Range(0,25).Select(i=>InductionDynamics.SteadyOutput(profile,b.spec.idleRpm+(b.spec.redlineRpm-b.spec.idleRpm)*i/24.0).engineTorqueNm).ToArray();}
        static double PeakHp(BuildResolution b,double[] curve)=>curve.Select((n,i)=>n*(b.spec.idleRpm+(b.spec.redlineRpm-b.spec.idleRpm)*i/24.0)/7127).Max();
        static bool ChangesEngineOutput(PartCategory c)=>new[]{PartCategory.HeadIntake,PartCategory.Induction,PartCategory.Exhaust,PartCategory.Pistons,PartCategory.Camshaft,PartCategory.FuelSystem,PartCategory.Ignition,PartCategory.Controls}.Contains(c);
        static void Sensitivity(MechanicalCatalog catalog,VehicleDefinition[] vehicles,StringBuilder evidence)
        {
            foreach(var v in vehicles){
                string id=v.spec.id;var map=catalog.Recipe(id);var tune=v.CreateStockTune();
                // These are actual authored catalogue combinations, not fabricated
                // low-capacity products: strongest fitting head + weakest fuel set.
                var head=catalog.parts.Where(p=>p.category==PartCategory.HeadIntake&&catalog.Fits(v.spec,p)).OrderByDescending(p=>p.power).First();
                var fuel=catalog.parts.Where(p=>p.category==PartCategory.FuelSystem&&p.maxPowerHp>0&&catalog.Fits(v.spec,p)).OrderBy(p=>p.maxPowerHp).First();
                map[PartCategory.HeadIntake]=head.id;map[PartCategory.FuelSystem]=fuel.id;var build=catalog.Resolve(id,map,tune);
                if(build.Drivable){var profile=new InductionProfile(build.spec,build.tune);double difference=0,at=0,helper=0,physical=0;
                    for(int i=0;i<=40;i++){double rpm=build.spec.idleRpm+(build.spec.redlineRpm-build.spec.idleRpm)*i/40.0,a=DragMath.Torque(build.spec,build.tune,rpm),b=InductionDynamics.SteadyOutput(profile,rpm).engineTorqueNm;if(Math.Abs(a-b)>difference){difference=Math.Abs(a-b);at=rpm;helper=a;physical=b;}}
                    Check(difference<.02,"torque-helper-capacity-disagreement",id+": "+fuel.id);
                    if(at>0){var state=InductionDynamics.SteadyOutput(profile,at);Check(Math.Abs(InductionDynamics.Torque(profile,state,at,1)-physical)<.02,"running-capacity-path-disagreement",id);}
                    evidence.AppendLine(string.Join(",",new object[]{id,"authored-head-fuel-combination",head.id+" + "+fuel.id,helper,physical,at}.Select(Q)));
                }
                // Detached authoring probes demonstrate which editable fields
                // are consumed. They never alter the shared catalogue or assets.
                var probe=new MechanicalCatalog(new[]{v.spec});var stock=probe.Resolve(id,probe.Recipe(id),tune);var part=probe.Find(probe.Id(id,PartCategory.Flywheel));
                double oldMass=new WheelProfile(stock.spec,stock.tune).vehicleMass,oldInertia=FlywheelDynamics.Inertia(stock.spec,stock.tune);
                var original=part.Copy();part.massKg+=2;var massBuild=probe.Resolve(id,probe.Recipe(id),tune);double massDelta=new WheelProfile(massBuild.spec,massBuild.tune).vehicleMass-oldMass;
                Check(PartCatalogOverrides.Validate(original,part)==null,"invalid-mass-probe",id);
                if(Math.Abs(massDelta)<1e-6)Flag("review","editable-flywheel-mass-ignored",id+": +2 kg absolute mass; actual car delta "+massDelta.ToString("G6",Inv));
                part.massKg=original.massKg;part.inertiaKgM2*=1.5f;var inertiaBuild=probe.Resolve(id,probe.Recipe(id),tune);double newInertia=FlywheelDynamics.Inertia(inertiaBuild.spec,inertiaBuild.tune);
                Check(PartCatalogOverrides.Validate(original,part)==null,"invalid-inertia-probe",id);
                if(Math.Abs(newInertia-oldInertia)<1e-10)Flag("review","editable-flywheel-inertia-ignored",id+": stated inertia +50%; actual "+oldInertia.ToString("G6",Inv)+" -> "+newInertia.ToString("G6",Inv));
                Check(Math.Abs(massDelta-2)<1e-5&&Math.Abs(newInertia-oldInertia*1.5)<1e-7,"flywheel-absolute-ratings-consumed",id);
                part.inertiaKgM2=original.inertiaKgM2;part.massScale*=.9f;
                Check(PartCatalogOverrides.Validate(original,part)!=null,"reject-ambiguous-flywheel-scale-edit",id);
                part.massKg=part.inertiaKgM2=0;var scaleBuild=probe.Resolve(id,probe.Recipe(id),tune);
                Check(new WheelProfile(scaleBuild.spec,scaleBuild.tune).vehicleMass<oldMass&&FlywheelDynamics.Inertia(scaleBuild.spec,scaleBuild.tune)<oldInertia,"legacy-flywheel-scale-fallback",id);
                var piston=probe.Find(probe.Id(id,PartCategory.Pistons));piston.rpmLimit=v.spec.redlineRpm*.85f;var lowRpm=probe.Resolve(id,probe.Recipe(id),tune);
                if(lowRpm.spec.redlineRpm*lowRpm.spec.engineBehavior.safeRpmMultiplier>lowRpm.safeRpm+1)Flag("review","editable-internal-rpm-floor",id+": weakest "+lowRpm.safeRpm.ToString("F0",Inv)+" vs global protection "+(lowRpm.spec.redlineRpm*lowRpm.spec.engineBehavior.safeRpmMultiplier).ToString("F0",Inv));
                Check(Math.Abs(lowRpm.spec.redlineRpm*lowRpm.spec.engineBehavior.safeRpmMultiplier-lowRpm.safeRpm)<1,"exact-lower-rpm-rating",id);
                evidence.AppendLine(string.Join(",",new object[]{id,"authoring-flywheel-mass-inertia",original.id,massDelta,oldInertia,newInertia}.Select(Q)));
            }
        }
        public static void Run()
        {
            findings.Clear();checks=combinations=rejected=0;
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../..")),folder=Path.Combine(root,"reports/part-statistics-fixes-20261021");
            string[] args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]=="--draglab-stats-audit-output")folder=Path.GetFullPath(args[i+1]);
            Directory.CreateDirectory(folder);var utf8=new UTF8Encoding(false);var rng=JsonUtility.ToJson(UnityEngine.Random.state);
            var definitions=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v=>v.spec.id,StringComparer.Ordinal).ToArray();
            var catalog=new MechanicalCatalog(definitions.Select(v=>v.spec));
            string initialCatalog=JsonUtility.ToJson(new PartCatalogOverrideData{parts=catalog.parts});
            File.WriteAllText(Path.Combine(folder,"catalog-snapshot.json"),initialCatalog,utf8);
            Check(catalog.parts.Count==catalog.parts.Select(p=>p.id).Distinct().Count(),"duplicate-id","Catalogue IDs");
            foreach(var p in catalog.parts){
                string error=PartCatalogOverrides.Validate(p,p);Check(error==null,"authored-validation",p.id+": "+error);
                Check(PartSemantics.Stats(p).All(s=>MechanicsSchema.Finite(s.value)),"non-finite-part-stat",p.id);
                if(p.category==PartCategory.CylinderBlock&&p.torqueLimitNm==0&&p.rpmLimit==0)Flag("review","block-strength-unrated",p.id);
                if(p.category==PartCategory.Exhaust&&p.stage==0&&p.description!=null&&p.massKg==22)Flag("info","generic-exhaust-mass",p.id);
            }
            var cars=new StringBuilder("model,cylinders,litres,authored_hp,resolved_hp,resolved_torque,mass_kg,compression,safe_rpm,simulation_safe_rpm,spring_rpm,stock_warnings\n");
            File.WriteAllText(Path.Combine(folder,"audio-signals.csv"),"model,max_load,max_airflow_kg_s,cut_ticks\n",utf8);
            var effects=new StringBuilder("model,part,category,drivable,hp,torque,mass_kg,safe_rpm,compression,error,warning\n");
            foreach(var v in definitions){
                string id=v.spec.id;var recipe=catalog.Recipe(id);var tune=v.CreateStockTune();var stock=catalog.Resolve(id,recipe,tune);var baseline=Curve(stock);
                double hp=PeakHp(stock,baseline),mass=new WheelProfile(stock.spec,stock.tune).vehicleMass;
                var authored=new BuildResolution{spec=v.spec.Copy(),tune=tune.Copy()};double authoredHp=PeakHp(authored,Curve(authored));
                Check(stock.Drivable,"stock-undrivable",id+": "+string.Join("; ",stock.errors));
                Check(stock.spec.Validate()==null,"invalid-stock-spec",id+": "+stock.spec.Validate());
                Check(baseline.All(n=>MechanicsSchema.Finite(n)&&n>=0)&&hp>0,"invalid-stock-output",id);
                if(Math.Abs(hp-authoredHp)>Math.Max(1,authoredHp*.02))Flag("review","stock-vs-authored-output",id+": "+authoredHp.ToString("F2",Inv)+" -> "+hp.ToString("F2",Inv)+" HP");
                if(stock.warnings.Count>0)Flag("review","stock-operating-warnings",id+": "+string.Join("; ",stock.warnings));
                cars.AppendLine(string.Join(",",new object[]{id,v.spec.sound.cylinders,v.spec.DisplacementLitres,authoredHp,hp,baseline.Max(),mass,stock.spec.combustion.compressionRatio,stock.safeRpm,stock.spec.redlineRpm*stock.spec.engineBehavior.safeRpmMultiplier,stock.spec.valvetrain.springSafeRpm,string.Join("; ",stock.warnings)}.Select(Q)));
                foreach(var p in catalog.parts.Where(p=>catalog.Fits(v.spec,p))){
                    combinations++;var map=new Dictionary<PartCategory,string>(recipe){[p.category]=p.id};BuildResolution b;
                    try{b=catalog.Resolve(id,map,tune);}catch(Exception e){Flag("error","resolve-exception",id+" / "+p.id+": "+e.Message);continue;}
                    if(!b.Drivable){rejected++;effects.AppendLine(string.Join(",",new object[]{id,p.id,p.category,false,0,0,0,b.safeRpm,b.spec.combustion.compressionRatio,string.Join("; ",b.errors),string.Join("; ",b.warnings)}.Select(Q)));continue;}
                    Check(b.spec.Validate()==null,"invalid-fitting-build",id+" / "+p.id+": "+b.spec.Validate());
                    var curve=Curve(b);double peak=PeakHp(b,curve);var wheels=new WheelProfile(b.spec,b.tune);
                    Check(curve.All(n=>MechanicsSchema.Finite(n)&&n>=0)&&peak>0,"invalid-fitting-output",id+" / "+p.id);
                    Check(MechanicsSchema.Finite(wheels.vehicleMass)&&wheels.vehicleMass>0&&MechanicsSchema.Finite(wheels.totalInertia)&&wheels.totalInertia>0,"invalid-fitting-mass-inertia",id+" / "+p.id);
                    if(!ChangesEngineOutput(p.category))Check(curve.Zip(baseline,(a,z)=>Math.Abs(a-z)).Max()<.02,"unrelated-part-changes-engine-output",id+" / "+p.id+": "+hp.ToString("F2",Inv)+" -> "+peak.ToString("F2",Inv)+" HP");
                    if(b.safeRpm+1<b.spec.redlineRpm*b.spec.engineBehavior.safeRpmMultiplier)Flag("review","protection-above-weakest-internal",id+" / "+p.id+": "+b.safeRpm.ToString("F1",Inv)+" vs "+(b.spec.redlineRpm*b.spec.engineBehavior.safeRpmMultiplier).ToString("F1",Inv));
                    effects.AppendLine(string.Join(",",new object[]{id,p.id,p.category,true,peak,curve.Max(),wheels.vehicleMass,b.safeRpm,b.spec.combustion.compressionRatio,"",string.Join("; ",b.warnings)}.Select(Q)));
                }
                // Real engine dynamics and audio publication at idle, rev, load,
                // lift and cuts. No gameplay profile/resources are involved.
                var car=new CarSimulation(stock.spec,stock.tune,new DriverSettings{wearMode=WearMode.None,throttleAssist=false,tractionAssist=false});
                var audio=new EngineSoundController(stock.spec,stock.tune);double maxLoad=0,maxFlow=0;int cuts=0;
                for(int i=0;i<720;i++){
                    if(i==240)car.Launch(0);
                    car.Step(DragMath.FixedStep,new DriverInput{throttle=i<120?0:i<540?1:0,shiftUp=i==360||i==480});
                    var f=audio.Update(car.state,true,1,DragMath.FixedStep);maxLoad=Math.Max(maxLoad,f.load);maxFlow=Math.Max(maxFlow,f.airflowKgS);if(f.cut)cuts++;
                    Check(MechanicsSchema.Finite(f.rpm)&&MechanicsSchema.Finite(f.load)&&f.load>=0&&f.load<=1&&MechanicsSchema.Finite(f.airflowKgS)&&(!f.active||f.airflowKgS>=0)&&f.combustionFraction>=0&&f.combustionFraction<=1,"invalid-audio-excitation",id+" tick "+i);
                }
                File.AppendAllText(Path.Combine(folder,"audio-signals.csv"),string.Join(",",new object[]{id,maxLoad,maxFlow,cuts}.Select(Q))+"\n",utf8);
            }
            var ranges=new StringBuilder("category,count,mass_min_kg,mass_max_kg,rpm_min,rpm_max,torque_min_nm,torque_max_nm,power_min,power_max,grip_min,grip_max,life_min_m,life_max_m\n");
            var sensitivity=new StringBuilder("model,probe,parts,first_value,second_value,third_value\n");Sensitivity(catalog,definitions,sensitivity);File.WriteAllText(Path.Combine(folder,"sensitivity-matrix.csv"),sensitivity.ToString(),utf8);
            foreach(var group in catalog.parts.GroupBy(p=>p.category).OrderBy(g=>g.Key))ranges.AppendLine(string.Join(",",new object[]{group.Key,group.Count(),group.Min(p=>p.massKg),group.Max(p=>p.massKg),group.Min(p=>p.rpmLimit),group.Max(p=>p.rpmLimit),group.Min(p=>p.torqueLimitNm),group.Max(p=>p.torqueLimitNm),group.Min(p=>p.power),group.Max(p=>p.power),group.Min(p=>p.grip),group.Max(p=>p.grip),group.Min(p=>p.tireLifeDistanceM),group.Max(p=>p.tireLifeDistanceM)}.Select(Q)));
            Check(initialCatalog==JsonUtility.ToJson(new PartCatalogOverrideData{parts=catalog.parts}),"catalog-mutated","Read-only audit");Check(rng==JsonUtility.ToJson(UnityEngine.Random.state),"gameplay-rng-mutated","Read-only audit");
            File.WriteAllText(Path.Combine(folder,"stock-matrix.csv"),cars.ToString(),utf8);File.WriteAllText(Path.Combine(folder,"part-effects.csv"),effects.ToString(),utf8);File.WriteAllText(Path.Combine(folder,"category-matrix.csv"),ranges.ToString(),utf8);
            File.WriteAllText(Path.Combine(folder,"finding-matrix.csv"),"severity,code,count,examples\n"+string.Join("\n",findings.Values.OrderBy(f=>f.severity).ThenBy(f=>f.code).Select(f=>string.Join(",",new object[]{f.severity,f.code,f.count,string.Join(" | ",f.examples)}.Select(Q))))+"\n",utf8);
            int errors=findings.Values.Where(f=>f.severity=="error").Sum(f=>f.count);
            string summary="Models: "+definitions.Length+"\nCatalogue definitions: "+catalog.parts.Count+"\nCategories: "+catalog.parts.Select(p=>p.category).Distinct().Count()+"\nFitting combinations: "+combinations+"\nExpected combination rejections: "+rejected+"\nChecks: "+checks+"\nFlagged invariant failures: "+errors+"\nReview groups: "+findings.Count+"\nNo player saves, calibration or physical bench changed.\n";
            File.WriteAllText(Path.Combine(folder,"verification.txt"),summary,utf8);Debug.Log("Part statistics audit completed:\n"+summary);
        }
        public static void RunExistingEffects()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/part-statistics-fixes-20261021"));
            string[] args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]=="--draglab-stats-audit-output")folder=Path.GetFullPath(args[i+1]);
            Directory.CreateDirectory(folder);
            try{int count=PartEffectsVerification.Run(Path.Combine(folder,"isolated-existing-checks"));File.WriteAllText(Path.Combine(folder,"existing-effects-verification.txt"),"PASS existing PartEffectsVerification: "+count+" checks. Reports isolated; original report files and player saves untouched.\n",new UTF8Encoding(false));}
            catch(Exception e){File.WriteAllText(Path.Combine(folder,"existing-effects-verification.txt"),"FAILED existing PartEffectsVerification: "+e+"\n",new UTF8Encoding(false));throw;}
        }
    }
}
