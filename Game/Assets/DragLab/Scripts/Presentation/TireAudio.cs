using System;
using System.Threading;
using UnityEngine;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Presentation
{
    // Separate tire voice remains audible while a failed/stalled engine coasts or brakes.
    public sealed class TireAudio : MonoBehaviour
    {
        TireSoundSynth synth=new TireSoundSynth();
        TireSoundFrame frame;
        AudioSource source;
        AudioClip clock;
        int rate;
        float rms;
        DriveType drive;
        TireSoundProfile profile;
        public float OutputRms=>source!=null&&source.volume>0?Volatile.Read(ref rms):0;
        public void Configure(VehicleSpec spec,Tune tune){drive=spec.drive;profile=new TireSoundProfile(spec,tune);Silence();Volatile.Write(ref synth,new TireSoundSynth());}
        public void Initialize()
        {
            rate=AudioSettings.outputSampleRate;
            clock=AudioClip.Create("Tire DSP clock (silent)",rate,1,rate,false);
            source=gameObject.AddComponent<AudioSource>();source.clip=clock;source.loop=true;source.spatialBlend=0;source.playOnAwake=false;source.volume=0;source.Play();
            AudioSettings.OnAudioConfigurationChanged+=Reconfigure;
        }
        void Reconfigure(bool changed){Volatile.Write(ref rate,AudioSettings.outputSampleRate);}
        public void Render(CarState state,bool active,double volume){Volatile.Write(ref frame,TireSoundFrame.From(state,active,volume,drive,profile));source.volume=active?1:0;}
        public void Silence(){if(source!=null)source.volume=0;Volatile.Write(ref frame,null);}
        void OnAudioFilterRead(float[] data,int channels)
        {
            var current=Volatile.Read(ref frame);
            Volatile.Read(ref synth).Fill(data,channels,Volatile.Read(ref rate),current,SharedAudioSettings.DspMasterVolume*SharedAudioSettings.DspTireVolume);
            double energy=0;foreach(float sample in data)energy+=sample*sample;
            Volatile.Write(ref rms,data.Length==0?0:(float)Math.Sqrt(energy/data.Length));
        }
        void OnDestroy(){AudioSettings.OnAudioConfigurationChanged-=Reconfigure;if(source!=null)source.Stop();if(clock!=null)Destroy(clock);}
    }
}
