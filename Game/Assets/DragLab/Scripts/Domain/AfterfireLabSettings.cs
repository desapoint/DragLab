using System;

namespace DragLab.Domain
{
    // Sound controls transcribed from the user-approved backfire_lab_v3.html.
    // Deliberately excludes its demonstration sequencer, normalization and room.
    public struct AfterfireLabSettings
    {
        public double crack,edge,pulse,thump,size,bark,tube,tune,decay,jet,flameTime,scatter,smooth,air,space;
        public static AfterfireLabSettings Preset(int i)
        {
            var p=new AfterfireLabSettings{crack=79,edge=78,pulse=4.8,thump=84,size=76,bark=75,tube=53,tune=350,decay=245,jet=12,flameTime=130,scatter=18,smooth=72,air=47,space=16};
            switch(i)
            {
                case 1:p.space=20;p.crack=65;p.edge=63;p.pulse=7;p.thump=90;p.size=92;p.bark=85;p.tube=68;p.tune=235;p.decay=330;p.jet=7;p.flameTime=90;p.scatter=30;break;
                case 2:p.space=12;p.crack=96;p.edge=93;p.pulse=2.6;p.thump=72;p.size=58;p.bark=75;p.tube=68;p.tune=565;p.decay=125;p.jet=52;p.flameTime=100;p.scatter=58;break;
                case 3:p.space=23;p.crack=83;p.edge=80;p.pulse=3.7;p.thump=61;p.size=55;p.bark=59;p.tube=80;p.tune=710;p.decay=290;p.jet=72;p.flameTime=370;p.scatter=30;break;
                case 4:p.space=24;p.crack=100;p.edge=100;p.pulse=2;p.thump=100;p.size=74;p.bark=88;p.tube=42;p.tune=315;p.decay=180;p.jet=9;p.flameTime=120;p.scatter=5;break;
                case 5:p.space=7;p.crack=91;p.edge=87;p.pulse=2.8;p.thump=65;p.size=56;p.bark=70;p.tube=38;p.tune=480;p.decay=105;p.jet=26;p.flameTime=130;p.scatter=20;break;
                case 6:p.space=9;p.crack=70;p.edge=82;p.pulse=2.4;p.thump=34;p.size=34;p.bark=48;p.tube=37;p.tune=780;p.decay=82;p.jet=20;p.flameTime=90;p.scatter=88;break;
                case 7:p.space=39;p.crack=76;p.edge=60;p.pulse=9.5;p.thump=100;p.size=100;p.bark=95;p.tube=87;p.tune=185;p.decay=500;p.jet=9;p.flameTime=130;p.scatter=10;break;
                case 8:p.space=14;p.crack=88;p.edge=90;p.pulse=2.6;p.thump=43;p.size=24;p.bark=61;p.tube=78;p.tune=1030;p.decay=118;p.jet=5;p.flameTime=65;p.scatter=16;break;
                case 9:p.space=16;p.crack=85;p.edge=71;p.pulse=7.5;p.thump=100;p.size=100;p.bark=100;p.tube=72;p.tune=255;p.decay=390;p.jet=18;p.flameTime=180;p.scatter=23;break;
            }
            return p;
        }
        public static int HardwarePreset(EngineSoundProfile p)
        {
            // Exact approved envelopes; installed filters already supply the
            // detailed exhaust-hardware response. No Shotgun or mode-flag blend.
            if(p.turboCount>0)return 3; // Turbo Pop & Flame, including Tempest.
            if(p.cylinders>=8&&p.litres>=6)return 9;
            if(p.fueling==FuelSystem.Carburetor)return 1;
            if(p.redlineRpm>=7500)return 8;
            if(p.cylinders>=8)
                foreach(var part in p.exhaustSystem.parts)
                    if(part.kind==ExhaustPartKind.ChamberedSilencer&&part.amount>.5)return 1;
            return 0;
        }
        public static AfterfireLabSettings Hardware(EngineSoundProfile p)=>Preset(HardwarePreset(p));
    }
}
