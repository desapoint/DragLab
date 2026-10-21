using System;

namespace DragLab.Domain
{
    // Sound controls transcribed from the user-approved backfire_lab_v3.html.
    // Deliberately excludes its demonstration sequencer, normalization and room.
    public struct AfterfireLabSettings
    {
        public double crack,edge,pulse,thump,size,bark,tube,tune,decay,jet,flameTime,scatter,smooth,air;
        public static AfterfireLabSettings Preset(int i)
        {
            var p=new AfterfireLabSettings{crack=79,edge=78,pulse=4.8,thump=84,size=76,bark=75,tube=53,tune=350,decay=245,jet=12,flameTime=130,scatter=18,smooth=72,air=47};
            switch(i)
            {
                case 1:p.crack=65;p.edge=63;p.pulse=7;p.thump=90;p.size=92;p.bark=85;p.tube=68;p.tune=235;p.decay=330;p.jet=7;p.flameTime=90;p.scatter=30;break;
                case 2:p.crack=96;p.edge=93;p.pulse=2.6;p.thump=72;p.size=58;p.bark=75;p.tube=68;p.tune=565;p.decay=125;p.jet=52;p.flameTime=100;p.scatter=58;break;
                case 3:p.crack=83;p.edge=80;p.pulse=3.7;p.thump=61;p.size=55;p.bark=59;p.tube=80;p.tune=710;p.decay=290;p.jet=72;p.flameTime=370;p.scatter=30;break;
                case 4:p.crack=100;p.edge=100;p.pulse=2;p.thump=100;p.size=74;p.bark=88;p.tube=42;p.tune=315;p.decay=180;p.jet=9;p.flameTime=120;p.scatter=5;break;
                case 5:p.crack=91;p.edge=87;p.pulse=2.8;p.thump=65;p.size=56;p.bark=70;p.tube=38;p.tune=480;p.decay=105;p.jet=26;p.flameTime=130;p.scatter=20;break;
                case 6:p.crack=70;p.edge=82;p.pulse=2.4;p.thump=34;p.size=34;p.bark=48;p.tube=37;p.tune=780;p.decay=82;p.jet=20;p.flameTime=90;p.scatter=88;break;
                case 7:p.crack=76;p.edge=60;p.pulse=9.5;p.thump=100;p.size=100;p.bark=95;p.tube=87;p.tune=185;p.decay=500;p.jet=9;p.flameTime=130;p.scatter=10;break;
                case 8:p.crack=88;p.edge=90;p.pulse=2.6;p.thump=43;p.size=24;p.bark=61;p.tube=78;p.tune=1030;p.decay=118;p.jet=5;p.flameTime=65;p.scatter=16;break;
                case 9:p.crack=85;p.edge=71;p.pulse=7.5;p.thump=100;p.size=100;p.bark=100;p.tube=72;p.tune=255;p.decay=390;p.jet=18;p.flameTime=180;p.scatter=23;break;
            }
            return p;
        }
        static double L(double a,double b,double t)=>a+(b-a)*t;
        public static AfterfireLabSettings Blend(AfterfireLabSettings a,AfterfireLabSettings b,double t)
        {
            t=DragMath.Clamp(t,0,1);
            return new AfterfireLabSettings{crack=L(a.crack,b.crack,t),edge=L(a.edge,b.edge,t),pulse=L(a.pulse,b.pulse,t),thump=L(a.thump,b.thump,t),size=L(a.size,b.size,t),bark=L(a.bark,b.bark,t),tube=L(a.tube,b.tube,t),tune=L(a.tune,b.tune,t),decay=L(a.decay,b.decay,t),jet=L(a.jet,b.jet,t),flameTime=L(a.flameTime,b.flameTime,t),scatter=L(a.scatter,b.scatter,t),smooth=L(a.smooth,b.smooth,t),air=L(a.air,b.air,t)};
        }
        public static AfterfireLabSettings Hardware(EngineSoundProfile p)
        {
            var s=Preset(0);
            if(p.fueling==FuelSystem.Carburetor)s=Blend(s,Preset(1),.65);
            if(p.cylinders>=8)s=Blend(s,Preset(9),DragMath.Clamp((p.litres-3)/4,0,.75));
            if(p.redlineRpm>7000)s=Blend(s,Preset(8),DragMath.Clamp((p.redlineRpm-7000)/2500,0,.8));
            if(p.turboCount>0)s=Blend(s,Preset(3),.45);
            foreach(var part in p.exhaustSystem.parts)
            {
                if(part.kind==ExhaustPartKind.ChamberedSilencer)s=Blend(s,Preset(1),.2*part.amount);
                if(part.kind==ExhaustPartKind.Resonator){s.tube+=8*part.amount;s.tune*=1-.12*part.amount;}
                if(part.kind==ExhaustPartKind.AbsorptiveSilencer)s.smooth+=8*part.amount;
            }
            // Broad local gas-volume colour; installed transfer filters remain
            // responsible for outlet attenuation, metal radiation and reflections.
            s.tune/=Math.Sqrt(p.acoustics.collectorScale);s.tube=DragMath.Clamp(s.tube,0,100);
            return s;
        }
        public static AfterfireLabSettings Reaction(AfterfireLabSettings hardware,EngineSoundProfile profile,ExhaustMixtureModel.Event e)
        {
            var p=hardware;
            // No limiter/two-step/anti-lag flag chooses a sound or schedules hits.
            // These anchors span the same families as the lab, selected instead
            // by measured pressure rise, reacting volume and burn duration.
            double abrupt=DragMath.Clamp((.002-e.riseSeconds)/.002,0,1)*DragMath.Clamp(e.pressurePa/60000,0,1);
            p=Blend(p,Preset(4),.18*abrupt);
            double volume=DragMath.Clamp(Math.Pow(Math.Max(1e-8,e.reactingVolumeM3)/.0004,1.0/3),.5,1.7);
            double heat=Math.Sqrt(DragMath.Clamp(e.gasK/900,.4,2.5));
            p.pulse=DragMath.Clamp(p.pulse*volume*DragMath.Clamp(e.riseSeconds/.0015,.45,1.8)/Math.Sqrt(profile.popSharpness),1,15);
            p.decay=DragMath.Clamp(p.decay*DragMath.Clamp((e.burnSeconds+.014)/.035,.4,1.6)*profile.popDecay,55,620);
            p.flameTime=DragMath.Clamp((e.burnSeconds*1800+45)*profile.popDecay,40,650);
            p.edge=DragMath.Clamp(p.edge+12*(heat-1)+12*(profile.popSharpness-1),0,100);
            p.tune=DragMath.Clamp(p.tune*heat/volume,120,1350);
            p.size=DragMath.Clamp(p.size*volume,0,100);
            p.jet*=DragMath.Clamp(e.flowMps/100,0,1);
            // Real subsequent flame fronts arrive from the chemistry model.
            // The lab's random micro-ignitions and automatic follower are preview
            // choreography, not extra combustion energy in the running game.
            p.scatter=0;return p;
        }
    }
}
