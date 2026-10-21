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
        int rate,outputChannels;
        float rms;
        DriveType drive;
        TireSoundProfile profile;
        TracksidePropagation outdoor;
        TracksidePropagation.Frame listener,sourceListener;
        int propagationGeneration,observedPropagationGeneration;
        public int OutputChannels=>Volatile.Read(ref outputChannels);
        public float OutputRms=>source!=null&&source.volume>0?Volatile.Read(ref rms):0;
        public void Configure(VehicleSpec spec,Tune tune){drive=spec.drive;profile=new TireSoundProfile(spec,tune);Silence();Volatile.Write(ref synth,new TireSoundSynth());}
        public void Initialize()
        {
            rate=AudioSettings.outputSampleRate;
            outdoor=new TracksidePropagation(rate);listener=new TracksidePropagation.Scene().At(sourceHeight:.08);
            clock=AudioClip.Create("Tire DSP clock (silent)",rate,2,rate,false);
            source=gameObject.AddComponent<AudioSource>();source.clip=clock;source.loop=true;source.spatialBlend=0;source.playOnAwake=false;source.volume=0;source.Play();
            AudioSettings.OnAudioConfigurationChanged+=Reconfigure;
        }
        void Reconfigure(bool changed){Volatile.Write(ref rate,AudioSettings.outputSampleRate);}
        void Update()
        {
            int target=Volatile.Read(ref rate);var medium=Volatile.Read(ref outdoor);
            if(medium!=null&&target>=8000&&target<=192000&&medium.SampleRate!=target)Volatile.Write(ref outdoor,new TracksidePropagation(target));
        }
        public void SetListener(TracksidePropagation.Frame value)
        {
            if(ReferenceEquals(sourceListener,value))return;
            sourceListener=value;Volatile.Write(ref listener,value.TireHeight());
        }
        public void Render(CarState state,bool active,double volume){Volatile.Write(ref frame,TireSoundFrame.From(state,active,volume,drive,profile));source.volume=active?1:0;}
        public void Silence(){Interlocked.Increment(ref propagationGeneration);if(source!=null)source.volume=0;Volatile.Write(ref frame,null);}
        void OnAudioFilterRead(float[] data,int channels)
        {
            Volatile.Write(ref outputChannels,channels);
            var medium=Volatile.Read(ref outdoor);int currentRate=Volatile.Read(ref rate);
            if(medium==null||medium.SampleRate!=currentRate){Array.Clear(data,0,data.Length);Volatile.Write(ref rms,0);return;}
            var current=Volatile.Read(ref frame);
            int generation=Volatile.Read(ref propagationGeneration);
            if(generation!=observedPropagationGeneration){medium.Clear();observedPropagationGeneration=generation;}
            Volatile.Read(ref synth).Fill(data,channels,currentRate,current,SharedAudioSettings.DspMasterVolume*SharedAudioSettings.DspTireVolume);
            if(current!=null&&current.active)medium.Process(data,channels,Volatile.Read(ref listener));else medium.Clear();
            double energy=0;foreach(float sample in data)energy+=sample*sample;
            Volatile.Write(ref rms,data.Length==0?0:(float)Math.Sqrt(energy/data.Length));
        }
        void OnDestroy(){AudioSettings.OnAudioConfigurationChanged-=Reconfigure;if(source!=null)source.Stop();if(clock!=null)Destroy(clock);}
    }
}
