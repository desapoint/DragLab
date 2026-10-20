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
    [Serializable] public sealed class RaceAudioTestFixture
    {
        public string id,title;
        public VehicleSpec spec;
        public Tune tune;
        public DriverSettings driver;
        public bool tireTest;
        public double elapsed;
        public int shifts,limiterTicks,releases;
        public List<RaceAudioTestTick> ticks=new List<RaceAudioTestTick>();
        public RaceSimulation CreateRace()
        {
            var rival=driver.Copy();rival.tractionAssist=true;
            var race=RaceSessionFactory.Create(spec,tune,driver,spec,tune,rival,402.336);
            race.BeginWarmup();race.player.state.rpm=spec.idleRpm;
            return race;
        }
    }
}
