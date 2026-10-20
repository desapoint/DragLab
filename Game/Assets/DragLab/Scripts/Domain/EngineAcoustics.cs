using System;

namespace DragLab.Domain
{
    public enum EngineAcousticPreset { Automatic, Compact, ClassicV8, ModernV, HighRev, Diesel }

    [Serializable]
    public sealed class EngineAcousticSpec
    {
        public EngineAcousticPreset preset;
        public float runnerScale=1, collectorScale=1, resonanceScale=1;
        public float attackScale=1, blowdownScale=1, rarefactionScale=1;
        public float stereoWidth=.55f, turbineDamping=.38f;
        public float[] runnerLengthsMetres;
        public EngineAcousticSpec Copy()
        {var copy=(EngineAcousticSpec)MemberwiseClone();copy.runnerLengthsMetres=runnerLengthsMetres==null?null:(float[])runnerLengthsMetres.Clone();return copy;}
        static bool Valid(float v,float lo,float hi)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&v>=lo&&v<=hi;
        public string Validate(int cylinders)
        {
            if(!Enum.IsDefined(typeof(EngineAcousticPreset),preset))return "Invalid acoustic preset.";
            foreach(float v in new[]{runnerScale,collectorScale,resonanceScale,attackScale,blowdownScale,rarefactionScale})
                if(!Valid(v,.5f,2))return "Acoustic geometry/envelope scales must be 0.5–2.";
            if(!Valid(stereoWidth,0,1)||!Valid(turbineDamping,0,.8f))return "Invalid bank width or turbine damping.";
            if(runnerLengthsMetres!=null&&runnerLengthsMetres.Length>0){
                if(runnerLengthsMetres.Length!=cylinders)return "Supply one acoustic runner length per cylinder.";
                foreach(float v in runnerLengthsMetres)if(!Valid(v,.1f,2))return "Acoustic runners must be 0.1–2 metres.";
            }
            return null;
        }
    }

    // Immutable playback snapshot. Authored geometry changes sound only, never fitment/physics.
    public sealed class EngineAcousticProfile
    {
        public readonly EngineAcousticPreset preset;
        public readonly double collectorScale,resonanceScale,attackSeconds,blowdownSeconds,rarefactionSeconds,stereoWidth,turbineDamping;
        readonly double[] runners;
        public double Runner(int cylinder)=>runners[cylinder];
        public EngineAcousticProfile(EngineAcousticSpec source,int cylinders,EngineLayout layout,double litres,double redline,FuelSystem fuel)
        {
            var s=source??new EngineAcousticSpec();var error=s.Validate(cylinders);if(error!=null)throw new ArgumentException(error);
            preset=s.preset==EngineAcousticPreset.Automatic?fuel==FuelSystem.Diesel?EngineAcousticPreset.Diesel:
                cylinders==8&&layout==EngineLayout.Crossplane?EngineAcousticPreset.ClassicV8:
                redline>=7800?EngineAcousticPreset.HighRev:layout==EngineLayout.Inline?EngineAcousticPreset.Compact:EngineAcousticPreset.ModernV:s.preset;
            double runner=1,collector=1,resonance=1,attack=1,blowdown=1,rarefaction=1;
            switch(preset){
                case EngineAcousticPreset.Compact:runner=.90;collector=.92;resonance=1.12;blowdown=.92;break;
                case EngineAcousticPreset.ClassicV8:runner=1.08;collector=1.08;resonance=.94;blowdown=1.10;rarefaction=1.06;break;
                case EngineAcousticPreset.ModernV:runner=.94;collector=.96;resonance=1.05;attack=.94;break;
                case EngineAcousticPreset.HighRev:runner=.82;collector=.84;resonance=1.20;attack=.82;blowdown=.85;rarefaction=.90;break;
                case EngineAcousticPreset.Diesel:runner=1.04;collector=1.05;resonance=.88;attack=.86;blowdown=1.16;rarefaction=1.12;break;
            }
            collectorScale=collector*s.collectorScale;resonanceScale=resonance*s.resonanceScale;
            attackSeconds=.00020*attack*s.attackScale;blowdownSeconds=.0030*blowdown*s.blowdownScale;rarefactionSeconds=.0092*rarefaction*s.rarefactionScale;
            stereoWidth=layout==EngineLayout.Inline||cylinders<2?0:s.stereoWidth;turbineDamping=s.turbineDamping;
            double[] lengths={.50,.61,.43,.49,.57,.46,.38,.55};runners=new double[cylinders];double size=Math.Pow(litres/5.0,1.0/3);
            for(int i=0;i<cylinders;i++)runners[i]=s.runnerLengthsMetres!=null&&s.runnerLengthsMetres.Length==cylinders?s.runnerLengthsMetres[i]:lengths[i%8]*size*runner*s.runnerScale;
        }
    }
}
