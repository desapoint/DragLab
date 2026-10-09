using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class MenuPreviewVerification
    {
        public static void Run()
        {
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/menu-preview"));Directory.CreateDirectory(folder);
            var report=new StringBuilder();int count=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);report.AppendLine("PASS: "+label);count++;};
            var catalog=MechanicsTransactions.Catalog();var p=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(p,catalog);
            string bytes=JsonUtility.ToJson(p);var source=InspectionBuild.Capture(p,p.SelectedCar);
            var watch=Stopwatch.StartNew();var immediate=CarInspection.CalculatePrepared(source,CarInspection.PrepareCar(source),false);double fastMs=watch.Elapsed.TotalMilliseconds;
            var prepared=CarInspection.PrepareCar(source);watch.Restart();var background=Task.Run(()=>CarInspection.CalculatePrepared(source,prepared)).GetAwaiter().GetResult();double fullMs=watch.Elapsed.TotalMilliseconds;
            check(!immediate.probeComplete && background.probeComplete && background.completed,"Background prepared-car probe completes without accessing Unity APIs");
            check(immediate.peakHp==background.peakHp&&immediate.peakTorque==background.peakTorque,"Immediate dyno matches full authoritative probe");
            check(JsonUtility.ToJson(p)==bytes,"Fast and background previews preserve all owned state");
            report.AppendLine("Isolated starter preview wall ms: immediate="+fastMs.ToString("F2")+" full="+fullMs.ToString("F2"));
            SharedAudioSettings.VerificationPath=Path.Combine(folder,"audio-settings-test.json");
            try {
                File.WriteAllText(SharedAudioSettings.VerificationPath,"{\"version\":1,\"masterVolume\":0.42}",new UTF8Encoding(false));SharedAudioSettings.ReloadForVerification();
                check(Math.Abs(SharedAudioSettings.MasterVolume-.42)<1e-6&&SharedAudioSettings.OpponentVolume==1,"Legacy settings retain master and default opponent gain");
                SharedAudioSettings.SetOpponentVolume(.23f);SharedAudioSettings.ReloadForVerification();
                check(Math.Abs(SharedAudioSettings.OpponentVolume-.23)<1e-6&&Math.Abs(SharedAudioSettings.MasterVolume-.42)<1e-6,"Opponent volume persists independently");
                string saved=File.ReadAllText(SharedAudioSettings.VerificationPath,Encoding.UTF8);SharedAudioSettings.SetOpponentVolume(.67f,false);
                check(saved==File.ReadAllText(SharedAudioSettings.VerificationPath,Encoding.UTF8),"Audio slider preview does not write until final gesture save");SharedAudioSettings.Save();
            } finally {SharedAudioSettings.VerificationPath=null;SharedAudioSettings.ClearVerificationCache();}
            report.AppendLine("Checks: "+count);File.WriteAllText(Path.Combine(folder,"verification.txt"),report.ToString(),new UTF8Encoding(false));
            UnityEngine.Debug.Log("Menu preview checks: "+count);
        }
    }
}
