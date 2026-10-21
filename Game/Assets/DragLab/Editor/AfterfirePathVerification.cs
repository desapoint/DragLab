using System;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    public static class AfterfirePathVerification
    {
        public static string OutputDirectory;
        static string Root=>OutputDirectory??Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/afterfire-path-20261021"));
        static readonly StringBuilder Log = new StringBuilder();
        static int checks;
        static void Check(bool yes,string label)
        {
            Log.AppendLine((yes?"PASS ":"FAIL ")+label);checks++;
            File.WriteAllText(Path.Combine(Root,"path-verification.txt"),Log+"Checks: "+checks+"\n",new UTF8Encoding(false));
            if(!yes)throw new InvalidOperationException(label);
        }
        public static void AuditBefore() { Audit("before"); }
        static void Audit(string label)
        {
            Directory.CreateDirectory(Root); var report = new StringBuilder("Actual race chemistry and isolated acoustic branches; no gain normalization.\n");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/race-audio-audition"));
            foreach (string prefix in new[] { "22-", "26-" })
            {
                var fixture = JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(Directory.GetFiles(folder, prefix + "*.race.json").Single(), Encoding.UTF8));
                var race = fixture.CreateRace();
                try
                {
                    var car = race.player; var profile = new EngineSoundProfile(car.spec, car.tune); var controller = new EngineSoundController(car.spec, car.tune);
                    controller.Update(car.state, true, .85, DragMath.FixedStep);
                    var model = car.state.audioExhaust.mixture; var synth = new AfterfireAudioSynth(profile, 48000); synth.Connect(model);
                    var energies = new double[8]; var counts = new int[8]; int cursor = 0;
                    double wall = 0, outlet = 0;
                    foreach (var tick in fixture.ticks)
                    {
                        car.state.warmupLineLock = tick.lineLock;
                        if (tick.beginCountdown && !race.BeginCountdown()) throw new InvalidOperationException("Countdown failed");
                        if (race.phase == RacePhase.Finished) race.Shutdown(tick.input); else race.Step(tick.input);
                        controller.Update(car.state, true, .85, DragMath.FixedStep);
                        while (cursor < model.Sequence)
                        {
                            if (!model.Read(++cursor, out var e)) throw new InvalidOperationException("Lost audit event");
                            counts[e.zone]++; energies[e.zone] += e.acousticJ;
                        }
                        for (int i = 0; i < 400; i++) { synth.Sample(); wall += synth.LastWallSample * synth.LastWallSample; outlet += synth.LastOutletSample * synth.LastOutletSample; }
                    }
                    for (int i = 0; i < 48000; i++) { synth.Sample(); wall += synth.LastWallSample * synth.LastWallSample; outlet += synth.LastOutletSample * synth.LastOutletSample; }
                    report.AppendLine(fixture.id + " / wall " + wall.ToString("R") + " / outlet " + outlet.ToString("R") + " / wall-outlet ratio " + (wall / Math.Max(1e-30, outlet)).ToString("R") + " / delivered " + synth.Delivered + " / ET " + car.state.elapsed.ToString("R"));
                    for (int i = 0; i < 8; i++) report.AppendLine("zone " + i + " / reactions " + counts[i] + " / acoustic J " + energies[i].ToString("R"));
                }
                finally { race.CloseOpponentWorker(); }
            }
            File.WriteAllText(Path.Combine(Root, label + "-paths.txt"), report.ToString(), new UTF8Encoding(false));
        }
        static ExhaustMixtureModel.Event At(EngineSoundProfile profile,double metres,double boost=.9)
        {
            double position=metres/RuntimeExhaustState.PathLength(profile);
            return new ExhaustMixtureModel.Event{zone=Math.Min(7,(int)(position*8)),position=position,gasK=900,turboBoost=boost};
        }
        static double Impulse(AfterfirePipeResponse path,int rate)
        {
            double sum=0;for(int i=0;i<rate/5;i++){path.Sample(i==0?1:0,0);sum+=path.Outlet*path.Outlet;}return sum;
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root);Log.Clear();checks=0;
            string file=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/race-audio-audition/26-launch-hard-ignition.race.json"));
            var fixture=JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(file,Encoding.UTF8));
            var profile=new EngineSoundProfile(fixture.spec,fixture.tune);
            Check(profile.turboCount>0,"routing fixture has actual installed turbo hardware");
            double legacy=1.1+(profile.turboCount>0?.18:0)+.8;foreach(var part in profile.exhaustSystem.parts)legacy+=RuntimeExhaustState.PartLength(part);
            Check(RuntimeExhaustState.PathLength(profile)==legacy,"central path dimensions retain bit-identical chemical geometry");
            foreach(int rate in new[]{24000,44100,48000,192000})
            {
                var a=new AfterfirePipeResponse(profile,rate);var b=new AfterfirePipeResponse(profile,rate);
                foreach(double x in new[]{.2,.7,1.099,1.101,1.4})
                {a.Begin(At(profile,x));Check(a.BeforeTurbine==(x<1.1),"actual header/turbine boundary applies at "+x+" m / "+rate);}
                a.Begin(At(profile,.7,0));b.Begin(At(profile,.7,1.2));
                double light=Impulse(a,rate),loaded=Impulse(b,rate);
                Check(loaded<light*.7&&loaded>0,"previously missed header reactions receive load-dependent turbine damping / "+rate);
                a.Begin(At(profile,1.6,0));b.Begin(At(profile,1.6,1.2));
                int arrival=-1;bool directSame=true;
                for(int i=0;i<rate/10;i++)
                {
                    a.Sample(i==0?1:0,0);b.Sample(i==0?1:0,0);
                    if(arrival<0&&Math.Abs(a.Outlet)>1e-12)arrival=i;
                    if(arrival>=0&&i<arrival+8)directSame&=a.Outlet==b.Outlet;
                }
                Check(arrival>=0&&directSame,"post-turbine direct sound is not falsely routed through the turbine / "+rate);
                // The outlet must be an impulse, not a second coloured ringing
                // tail. Runner/collector voicing is checked at the generator now.
                a.Begin(At(profile,.7));int nonzero=0;
                for(int i=0;i<rate/10;i++){a.Sample(i==0?1:0,0);if(a.Outlet!=0)nonzero++;}
                Check(nonzero==1,"outlet routing adds only gain and delay, with no duplicate EQ or reflected tail / "+rate);
                a.Begin(At(profile,.7));b=new AfterfirePipeResponse(profile,rate);b.Begin(At(profile,.7));
                bool wallSame=true;double wall=0;
                for(int i=0;i<rate/10;i++)
                {
                    a.Sample(Math.Sin(i*.8),i==0?1:0);b.Sample(Math.Sin(i*.37),i==0?1:0);
                    wallSame&=a.Wall==b.Wall;wall+=a.Wall*a.Wall;
                }
                Check(wallSame&&wall>0,"wall vibration remains pressure-driven instead of leaking dry stochastic grain / "+rate);
                a.Clear();Check(a.Sample(0,0)==0,"clearing pipe state discards all direct/wall history / "+rate);
                a.Begin(At(profile,.7));for(int i=0;i<1000;i++)a.Sample(.01,0);
                long allocated=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<rate/10;i++)a.Sample(.01,0);
                Check(GC.GetAllocatedBytesForCurrentThread()==allocated,"zero warmed internal-path callback allocations / "+rate);
            }
        }
        public static void VerifyAndBuild()
        {
            Run();Audit("after");
            var before=File.ReadAllLines(Path.Combine(Root,"before-paths.txt"),Encoding.UTF8).Where(x=>x.StartsWith("zone ")).ToArray();
            var after=File.ReadAllLines(Path.Combine(Root,"after-paths.txt"),Encoding.UTF8).Where(x=>x.StartsWith("zone ")).ToArray();
            Check(before.SequenceEqual(after),"all real race zone reaction counts and chemical acoustic energies remain exact");
            AfterfireGrainVerification.OutputDirectory=Root;AfterfireGrainVerification.Run();
            AfterfireBangVerification.OutputDirectory=Root;AfterfireBangVerification.Run();
            RuntimeAudioIntegrationVerification.OutputDirectory=Root;RuntimeAudioIntegrationVerification.TracksideListening=true;
            RuntimeAudioIntegrationVerification.Run();RuntimeAudioIntegrationVerification.CheckRaces();
            PlayerBuild.BuildPlayerOnly();
        }
    }
}
