using System;
using System.Threading;
using UnityEngine;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Presentation
{
    public sealed class EngineAudio : MonoBehaviour
    {
        sealed class Session
        {
            public readonly EngineSoundController controller;
            public readonly EngineSoundSynth synth;
            public readonly EngineSoundProfile profile;
            public readonly int rate;
            public readonly TracksidePropagation outdoor;
            public TracksidePropagation.Frame listener;
            public int propagationGeneration,observedPropagationGeneration;
            public EngineSoundFrame frame;
            public Session(VehicleSpec spec, Tune tune,int rate)
            {
                this.rate=rate;
                controller = new EngineSoundController(spec, tune);
                profile=new EngineSoundProfile(spec,tune);synth=new EngineSoundSynth(profile,sampleRate:rate);
                outdoor=new TracksidePropagation(rate);
                listener=new TracksidePropagation.Scene(spec.induction.ambientTemperatureK,.5,spec.induction.ambientPressureBar*100000).At();
                frame = new EngineSoundFrame(spec.idleRpm, 0, 0, 0, 0, active: false);
            }
            public Session(Session previous,int rate)
            {
                this.rate=rate;controller=previous.controller;profile=previous.profile;
                synth=new EngineSoundSynth(profile,sampleRate:rate);frame=Volatile.Read(ref previous.frame);
                outdoor=new TracksidePropagation(rate);listener=Volatile.Read(ref previous.listener);
            }
        }
        AudioSource source;
        AudioClip clockClip;
        TireAudio tires;
        public int TireOutputChannels=>tires==null?0:tires.OutputChannels;
        public float TireOutputRms=>tires==null?0:tires.OutputRms;
        Session session;
        int sampleRate;
        float pitch;
        float outputRms;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        long callbackTicks,peakCallbackTicks,callbackCount;
        int callbackChannels;
        public int OutputChannels=>Volatile.Read(ref callbackChannels);
        public double LastCallbackMilliseconds=>Volatile.Read(ref callbackTicks)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        public double PeakCallbackMilliseconds=>Volatile.Read(ref peakCallbackTicks)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        public long CallbackCount=>Volatile.Read(ref callbackCount);
#endif
        public long AfterfireOverflow => session==null?0:session.synth.AfterfireDiagnostics(session.rate).HistoryOverflows;
        public long AfterfireTailSteals => session==null?0:session.synth.AfterfireDiagnostics(session.rate).TailSteals;
        public long AfterfireDelivered => session==null?0:session.synth.AfterfireDiagnostics(session.rate).Delivered;
        public int ChemicalEvents => session?.frame.afterfire?.Sequence??0;
        public float OutputRms => session != null && (session.frame.active || session.frame.afterfire != null) ? Volatile.Read(ref outputRms) : 0;
        public bool Ready { get { return source != null && source.clip != null && session != null; } }
        // RPM ratio for diagnostics; DSP itself keeps a fixed playback rate.
        public float CurrentPitch { get { return pitch; } }
        public float CurrentVolume { get { return source != null ? source.volume * (float)session.frame.volume * SharedAudioSettings.MasterVolume * SharedAudioSettings.EngineVolume : 0; } }
        public double EstimatedBoost { get { return session != null ? session.controller.Boost : 0; } }
        public int BlowOffCount { get { return session != null ? session.controller.ReleaseCount : 0; } }
        public void Initialize()
        {
            if (!Application.isPlaying || source != null) return;
            SharedAudioSettings.EnsureLoaded();
            sampleRate = AudioSettings.outputSampleRate;
            Configure(new VehicleSpec(), new Tune());
            clockClip = AudioClip.Create("DragLab DSP clock (silent)", sampleRate, 2, sampleRate, false);
            source = gameObject.AddComponent<AudioSource>(); source.clip = clockClip;
            source.loop = true; source.playOnAwake = false; source.spatialBlend = 0; source.volume = 0; source.Play();
            var tireObject=new GameObject("Tire contact audio");tireObject.transform.SetParent(transform,false);
            tires=tireObject.AddComponent<TireAudio>();tires.Initialize();
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
        }
        void AudioConfigurationChanged(bool changed) { Volatile.Write(ref sampleRate, AudioSettings.outputSampleRate); }
        void Update(){PrepareDeviceRate();}
        void PrepareDeviceRate()
        {
            var current=Volatile.Read(ref session);int rate=Volatile.Read(ref sampleRate);
            if(current!=null&&rate>=8000&&rate<=192000&&current.rate!=rate)
                Volatile.Write(ref session,new Session(current,rate));
        }
        public void Configure(VehicleSpec spec, Tune tune)
        {
            if(tires!=null)tires.Configure(spec,tune);
            // Each callback captures a consistent session when hardware changes on the main thread.
            int rate=Volatile.Read(ref sampleRate);if(rate<=0)rate=AudioSettings.outputSampleRate;
            Volatile.Write(ref session, new Session(spec, tune,rate)); pitch = (float)spec.idleRpm / 3000;
        }
        public void SetTracksideGeometry(double sourceMetres,double listenerMetres,double laneOffset=0)
        {
            var current=Volatile.Read(ref session);if(current==null)return;
            var old=Volatile.Read(ref current.listener);double x=DragMath.Clamp(sourceMetres-listenerMetres,-900,900),y=12+laneOffset;
            if(Math.Abs(x-old.longitudinal)<.02&&Math.Abs(y-old.lateral)<.01)return;
            Volatile.Write(ref current.listener,old.scene.At(x,y));
        }
        public void Render(CarState state, bool active, float volume, double loadOverride = -1, bool? tiresActive = null)
        {
            if (!Ready) return;
            PrepareDeviceRate();
            var current = session;
            if(tires!=null)tires.SetListener(Volatile.Read(ref current.listener));
            Volatile.Write(ref current.frame, current.controller.Update(state, active, volume, Time.unscaledDeltaTime, loadOverride));
            source.volume = active ? 1 : 0; pitch = (float)state.rpm / 3000;
            if(tires!=null)tires.Render(state,tiresActive??active,volume);
        }
        void OnAudioFilterRead(float[] data, int channels)
        {
            var current = Volatile.Read(ref session); int rate = Volatile.Read(ref sampleRate);
            // A device change may arrive before the next main-thread Update.
            // Briefly silence that block; never build tables/filters in this callback.
            if (current == null || rate != current.rate || channels <= 0) { Array.Clear(data, 0, data.Length);Volatile.Write(ref outputRms,0);return; }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            Volatile.Write(ref callbackChannels,channels);
#endif
            var signal=Volatile.Read(ref current.frame);
            int generation=Volatile.Read(ref current.propagationGeneration);
            if(generation!=current.observedPropagationGeneration){current.outdoor.Clear();current.observedPropagationGeneration=generation;}
            current.synth.Fill(data, channels, rate, signal, SharedAudioSettings.DspMasterVolume*SharedAudioSettings.DspEngineVolume,SharedAudioSettings.DspCompressorNoise);
            if(signal.active||signal.afterfire!=null)current.outdoor.Process(data,channels,Volatile.Read(ref current.listener));else current.outdoor.Clear();
            double energy = 0; foreach (float sample in data) energy += sample * sample;
            Volatile.Write(ref outputRms, data.Length > 0 ? (float)Math.Sqrt(energy / data.Length) : 0);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            long elapsed=System.Diagnostics.Stopwatch.GetTimestamp()-started;
            Volatile.Write(ref callbackTicks,elapsed);if(elapsed>Volatile.Read(ref peakCallbackTicks))Volatile.Write(ref peakCallbackTicks,elapsed);Interlocked.Increment(ref callbackCount);
#endif
        }
        public void Silence()
        {
            if (source != null) source.volume = 0;
            if(tires!=null)tires.Silence();
            var current = session;
            if (current != null){Interlocked.Increment(ref current.propagationGeneration);Volatile.Write(ref current.frame, current.controller.Update(new CarState(), false, 0, 0));}
        }
        void OnDestroy()
        {
            SharedAudioSettings.Save();
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
            Silence(); if (source != null) source.Stop(); if (clockClip != null) Destroy(clockClip);
        }
    }
}
