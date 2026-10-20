using System;
using System.Collections.Generic;

namespace DragLab.Domain
{
    // Development input replay, never a career recording or a PCM/audio replay.
    [Serializable] public sealed class RaceAudioTestTick
    {
        public DriverInput input;
        public bool beginCountdown,lineLock;
        public string stage;
    }
    [Serializable] public sealed class RaceAudioCutCounts
    {
        public int fuel,ignition,launchFuel,launchIgnition,softPartial;
        public bool Observe(CarSimulation car)
        {
            var s=car.state;bool launch=s.launchControlActive;
            var style=launch?car.engine.launchLimiter:car.engine.limiter;
            double band=style==LimiterStyle.Soft?car.engine.data.softBandRpm:car.spec.LimiterDrop;
            bool reduced=style==LimiterStyle.Soft?s.combustionFraction<.98:s.combustionFraction<=.001;
            if(style==LimiterStyle.None||s.transmissionTorqueCut||!reduced||s.rpm<s.activeLimitRpm-band-200)return false;
            if(launch){if(s.activeCutMethod==CutMethod.Fuel)launchFuel++;else if(s.activeCutMethod==CutMethod.Ignition)launchIgnition++;}
            else{if(s.activeCutMethod==CutMethod.Fuel)fuel++;else if(s.activeCutMethod==CutMethod.Ignition)ignition++;}
            if(style==LimiterStyle.Soft&&s.combustionFraction>.001)softPartial++;
            return true;
        }
    }
    [Serializable] public sealed class RaceAudioTestFixture
    {
        public string id,title,configuration;
        public Mechanics.PartInstance[] components;
        public RaceAudioCutCounts cuts=new RaceAudioCutCounts();
        public VehicleSpec spec;
        public Tune tune;
        public DriverSettings driver;
        public bool tireTest;
        public double elapsed;
        public int shifts,limiterTicks,releases,pops;
        public List<RaceAudioTestTick> ticks=new List<RaceAudioTestTick>();
        public RaceSimulation CreateRace()
        {
            var rival=driver.Copy();rival.tractionAssist=true;
            var race=RaceSessionFactory.Create(spec,tune,driver,spec,tune,rival,402.336,components!=null&&components.Length>0?components:null);
            race.BeginWarmup();race.player.state.rpm=spec.idleRpm;
            return race;
        }
    }
}
