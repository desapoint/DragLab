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
            public EngineSoundFrame frame;
            public Session(VehicleSpec spec, Tune tune)
            {
                controller = new EngineSoundController(spec, tune);
                synth = new EngineSoundSynth(new EngineSoundProfile(spec, tune));
                frame = new EngineSoundFrame(spec.idleRpm, 0, 0, 0, 0, active: false);
            }
        }
        AudioSource source;
        AudioClip clockClip;
        TireAudio tires;
        public float TireOutputRms=>tires==null?0:tires.OutputRms;
        Session session;
        int sampleRate;
        float pitch;
        float outputRms;
        public float OutputRms => session != null && session.frame.active ? Volatile.Read(ref outputRms) : 0;
        public bool Ready { get { return source != null && source.clip != null && session != null; } }
        // RPM ratio for diagnostics; DSP itself keeps a fixed playback rate.
        public float CurrentPitch { get { return pitch; } }
        public float CurrentVolume { get { return source != null ? source.volume * (float)session.frame.volume * SharedAudioSettings.MasterVolume : 0; } }
        public double EstimatedBoost { get { return session != null ? session.controller.Boost : 0; } }
        public int BlowOffCount { get { return session != null ? session.controller.ReleaseCount : 0; } }
        public void Initialize()
        {
            if (!Application.isPlaying || source != null) return;
            SharedAudioSettings.EnsureLoaded();
            sampleRate = AudioSettings.outputSampleRate;
            Configure(new VehicleSpec(), new Tune());
            clockClip = AudioClip.Create("DragLab DSP clock (silent)", sampleRate, 1, sampleRate, false);
            source = gameObject.AddComponent<AudioSource>(); source.clip = clockClip;
            source.loop = true; source.playOnAwake = false; source.spatialBlend = 0; source.volume = 0; source.Play();
            var tireObject=new GameObject("Tire contact audio");tireObject.transform.SetParent(transform,false);
            tires=tireObject.AddComponent<TireAudio>();tires.Initialize();
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
        }
        void AudioConfigurationChanged(bool changed) { Volatile.Write(ref sampleRate, AudioSettings.outputSampleRate); }
        public void Configure(VehicleSpec spec, Tune tune)
        {
            if(tires!=null)tires.Configure(spec.drive);
            // Each callback captures a consistent session when hardware changes on the main thread.
            Volatile.Write(ref session, new Session(spec, tune)); pitch = (float)spec.idleRpm / 3000;
        }
        public void Render(CarState state, bool active, float volume, double loadOverride = -1, bool? tiresActive = null)
        {
            if (!Ready) return;
            var current = session;
            Volatile.Write(ref current.frame, current.controller.Update(state, active, volume, Time.unscaledDeltaTime, loadOverride));
            source.volume = active ? 1 : 0; pitch = (float)state.rpm / 3000;
            if(tires!=null)tires.Render(state,tiresActive??active,volume);
        }
        void OnAudioFilterRead(float[] data, int channels)
        {
            var current = Volatile.Read(ref session); int rate = Volatile.Read(ref sampleRate);
            if (current == null || rate <= 0 || channels <= 0) { Array.Clear(data, 0, data.Length); return; }
            current.synth.Fill(data, channels, rate, Volatile.Read(ref current.frame), SharedAudioSettings.DspMasterVolume);
            double energy = 0; foreach (float sample in data) energy += sample * sample;
            Volatile.Write(ref outputRms, data.Length > 0 ? (float)Math.Sqrt(energy / data.Length) : 0);
        }
        public void Silence()
        {
            if (source != null) source.volume = 0;
            if(tires!=null)tires.Silence();
            var current = session;
            if (current != null) Volatile.Write(ref current.frame, current.controller.Update(new CarState(), false, 0, 0));
        }
        void OnDestroy()
        {
            SharedAudioSettings.Save();
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
            Silence(); if (source != null) source.Stop(); if (clockClip != null) Destroy(clockClip);
        }
    }
}
