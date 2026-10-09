using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class RacePresentationVerification
    {
        public static void Run()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/race-audio-worker"));
            Directory.CreateDirectory(folder); var report = new StringBuilder(); int count = 0;
            Action<bool,string> check = (ok,label) => { if (!ok) throw new Exception(label); report.AppendLine("PASS: " + label); count++; };
            foreach (var vehicle in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) {
                Func<RaceSimulation> create = () => new RaceSimulation(new CarSimulation(vehicle.spec,vehicle.CreateStockTune(),new DriverSettings()),new CarSimulation(vehicle.spec,vehicle.CreateStockTune(),new DriverSettings()),402.336);
                var serial=create(); var parallel=create(); parallel.EnableOpponentWorker();
                try {
                    for (int tick=0; tick<5000 && serial.phase!=RacePhase.Finished; tick++) {
                        var input=new DriverInput{throttle=tick%300<250?1:.5}; serial.Step(input); parallel.Step(input);
                        if (tick%120==0) check(JsonUtility.ToJson(serial.player.CaptureDynamics())==JsonUtility.ToJson(parallel.player.CaptureDynamics()) && JsonUtility.ToJson(serial.opponent.CaptureDynamics())==JsonUtility.ToJson(parallel.opponent.CaptureDynamics()),vehicle.spec.id+" sample-exact worker parity / "+tick);
                    }
                    check(serial.phase==parallel.phase&&serial.playerWon==parallel.playerWon,vehicle.spec.id+" identical finish/outcome");
                } finally { parallel.CloseOpponentWorker(); }
                double[] costs=new double[2];
                for(int mode=0;mode<2;mode++) {var race=create(); if(mode==1)race.EnableOpponentWorker(); var watch=Stopwatch.StartNew();try{for(int i=0;i<1800;i++)race.Step(new DriverInput{throttle=1});}finally{race.CloseOpponentWorker();}costs[mode]=watch.Elapsed.TotalMilliseconds;}
                report.AppendLine(vehicle.spec.id+" 1800 ticks wall ms: serial="+costs[0].ToString("F1")+" worker="+costs[1].ToString("F1"));
            }
            var state=new CarState{speed=20,wheelRadiusM=.3,wheelContactInitialized=true};
            for(int i=0;i<4;i++){state.contact.omega[i]=100;state.contact.slip[i]=.33;state.contact.force[i]=3000;state.contact.normalLoadN[i]=3500;state.contact.friction[i]=1;}
            var a=TireSoundFrame.From(state,true,1); state.contact.friction[0]=.5;state.contact.normalLoadN[0]=5000;
            var b=TireSoundFrame.From(state,true,1); var one=new float[44100];var two=new float[44100];
            new TireSoundSynth().Fill(one,1,44100,a);new TireSoundSynth().Fill(two,1,44100,b);
            check(!one.SequenceEqual(two),"Grip/load change rubber spectrum at the same wheel sliding speed");
            check(SharedAudioSettings.OpponentGain(0)<1 && SharedAudioSettings.OpponentGain(70)<SharedAudioSettings.OpponentGain(0),"Opponent attenuation falls with physical separation");
            report.AppendLine("Checks: "+count);File.WriteAllText(Path.Combine(folder,"verification.txt"),report.ToString(),new UTF8Encoding(false));
            UnityEngine.Debug.Log("Race presentation checks: "+count);
        }
    }
}
