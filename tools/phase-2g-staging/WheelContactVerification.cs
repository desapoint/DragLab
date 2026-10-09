using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Domain;
namespace DragLab.Editor
{
    public static class WheelContactVerification
    {
        public static void RunStandalone() { int count=Run(Path.GetFullPath(Path.Combine(Application.dataPath,"../..")));Debug.Log("WHEEL CONTACT PASS: "+count); }
        public static int Run(string root)
        {
            int count=0;var log=new StringBuilder();Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);count++;log.AppendLine("PASS: "+label);};
            Func<double,WheelContactInput> input=mass=>new WheelContactInput { massKg=mass,normalLoadN=new[]{mass*DragMath.Gravity*.25,mass*DragMath.Gravity*.25,mass*DragMath.Gravity*.25,mass*DragMath.Gravity*.25} };
            var empty=new WheelContactState();var p=input(1200);for(int i=0;i<240;i++)WheelContactDynamics.Step(empty,p,DragMath.FixedStep);
            check(empty.speed==0&&empty.KineticEnergy(p.massKg,p.inertiaKgM2)==0,"No spontaneous motion or energy at rest");
            var s=new WheelContactState();p.rearTorqueNm=2400;for(int i=0;i<240;i++)WheelContactDynamics.Step(s,p,DragMath.FixedStep);
            check(s.speed>1&&s.slip[2]>.1&&s.omega[2]>s.omega[0],"Applied shaft torque creates independently measured driven-wheel spin");
            check(Math.Abs(s.KineticEnergy(p.massKg,p.inertiaKgM2)+s.contactWorkJ+s.brakeWorkJ+s.resistanceWorkJ-s.driveWorkJ)<1e-6,"Contact impulse work closes kinetic-energy balance");
            check(s.brakeWorkJ==0,"Spinning without brake input produces no friction-brake work");
            double spin=s.omega[2];p.rearTorqueNm=0;WheelContactDynamics.Step(s,p,DragMath.FixedStep);
            check(s.omega[2]>s.speed/p.radiusM&&s.omega[2]<spin,"Opening torque path preserves spinning-wheel inertia while contact slows it");
            for(int i=0;i<240;i++)WheelContactDynamics.Step(s,p,DragMath.FixedStep);
            check(Math.Abs(s.slip[2])<.01,"Unpowered wheelspin regains grip through contact, without a slip reset");
            double energy=s.KineticEnergy(p.massKg,p.inertiaKgM2);p.brakeNm=new[]{1800.0,1800,1800,1800};for(int i=0;i<480;i++)WheelContactDynamics.Step(s,p,DragMath.FixedStep);
            check(s.speed<.01&&s.brakeWorkJ>0&&s.KineticEnergy(p.massKg,p.inertiaKgM2)<energy,"Wheel brake torque stops body through bounded tire contact and dissipates energy");
            var zeroGrip=new WheelContactState();var noGrip=input(1200);noGrip.friction=new double[4];noGrip.rearTorqueNm=1000;for(int i=0;i<120;i++)WheelContactDynamics.Step(zeroGrip,noGrip,DragMath.FixedStep);
            check(zeroGrip.speed==0&&zeroGrip.omega[2]>0,"Zero-grip surface spins wheels without inventing body propulsion");
            var light=new WheelContactState();var heavy=new WheelContactState();var l=input(1000);var h=input(2000);l.rearTorqueNm=h.rearTorqueNm=500;
            for(int i=0;i<240;i++){WheelContactDynamics.Step(light,l,DragMath.FixedStep);WheelContactDynamics.Step(heavy,h,DragMath.FixedStep);}
            check(light.speed>heavy.speed*1.8,"Actual translating mass governs acceleration without adding wheel equivalent mass twice");
            var smallI=new WheelContactState();var largeI=new WheelContactState();var a=input(1200);var b=input(1200);a.rearTorqueNm=b.rearTorqueNm=500;b.inertiaKgM2=8;
            for(int i=0;i<240;i++){WheelContactDynamics.Step(smallI,a,DragMath.FixedStep);WheelContactDynamics.Step(largeI,b,DragMath.FixedStep);}
            check(smallI.speed>largeI.speed,"Independent larger rotational inertia reduces acceleration");
            var open=new WheelContactState();var limited=new WheelContactState();var locked=new WheelContactState();var op=input(1200);var lp=input(1200);var sp=input(1200);
            foreach(var x in new[]{op,lp,sp}){x.rearTorqueNm=600;x.friction[2]=.05;}
            lp.rearDifferential=new DifferentialSpec {kind=DifferentialKind.LimitedSlip,torqueBiasRatio=3,preloadNm=30};sp.rearDifferential=new DifferentialSpec {kind=DifferentialKind.Spool};
            for(int i=0;i<240;i++){WheelContactDynamics.Step(open,op,DragMath.FixedStep);WheelContactDynamics.Step(limited,lp,DragMath.FixedStep);WheelContactDynamics.Step(locked,sp,DragMath.FixedStep);}
            check(open.omega[2]>open.omega[3]*2,"Open axle permits distinct left/right angular speeds on split grip");
            check(limited.speed>open.speed&&locked.speed>open.speed,"Limited-slip and spool transfer useful torque toward gripped wheel");
            check(Math.Abs(locked.omega[2]-locked.omega[3])<3,"Spool constrains left/right rotation while allowing contact micro-step residual");
            var repeat=new WheelContactState();for(int i=0;i<240;i++)WheelContactDynamics.Step(repeat,op,DragMath.FixedStep);
            check(JsonUtility.ToJson(repeat)==JsonUtility.ToJson(open),"Identical torque/contact inputs repeat every state exactly");
            var copy=open.Copy();copy.omega[2]=0;check(open.omega[2]>0,"Wheel checkpoints deeply isolate rotational arrays");
            bool rejected=false;try{var bad=input(1200);bad.friction[0]=double.NaN;WheelContactDynamics.Step(new WheelContactState(),bad,DragMath.FixedStep);}catch(ArgumentException){rejected=true;}check(rejected,"Nonfinite contact inputs are rejected before simulation");
            string folder=Path.Combine(root,"reports/phases/2g");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"verification.txt"),log+"Checks: "+count);return count;
        }
    }
}
