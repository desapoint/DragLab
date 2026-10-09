using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.Profiling;

namespace DragLab.Runtime
{
    // Opt-in measurement only: never changes simulation, saving, frame rate or quality.
    [DefaultExecutionOrder(-32000)]
    public sealed class FramePacingProbe : MonoBehaviour
    {
        static FramePacingProbe instance;
        static readonly string[] Names = { "raceUpdate", "benchUpdate", "simulation", "checkpoint", "raceGUI", "benchGUI", "scenarioPreparation", "export", "importAndVerification" };
        readonly double[] costs = new double[9];
        struct Row { public int frame,gc; public string phase; public double ms,a,b,c,d,e,f,g,h,i; public long bytes,unityBytes; }
        readonly List<Row> rows = new List<Row>(100000);
        string folder;
        long last, allocated;
        int collections;
        ProfilerRecorder gcAllocated;
        public static string Phase = "startup";
        public struct Scope : IDisposable
        {
            readonly int slot;
            readonly long start;
            public Scope(int slot) { this.slot = slot; start = instance == null ? 0 : Stopwatch.GetTimestamp(); }
            public void Dispose() { if (start != 0 && instance != null) instance.costs[slot] += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency; }
        }
        public static Scope Measure(int slot) { return new Scope(slot); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Attach()
        {
            var args = Environment.GetCommandLineArgs();
            if (!UnityEngine.Debug.isDebugBuild || Array.IndexOf(args, "--draglab-frame-probe") < 0) return;
            int index = Array.IndexOf(args, "--draglab-verify");
            if (index < 0) index = Array.IndexOf(args, "--draglab-driving-verify");
            if (index < 0 || index + 1 >= args.Length) return; // Isolated fixtures only.
            instance = new GameObject("Opt-in frame pacing diagnostics").AddComponent<FramePacingProbe>();
            instance.folder = Path.GetFullPath(args[index + 1]);
            instance.gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
            DontDestroyOnLoad(instance.gameObject);
        }
        void Update()
        {
            long now = Stopwatch.GetTimestamp(), bytes = GC.GetAllocatedBytesForCurrentThread();
            int gc = GC.CollectionCount(0);
            if (last != 0 && rows.Count < 100000) rows.Add(new Row{frame=Time.frameCount,phase=Phase,ms=(now-last)*1000.0/Stopwatch.Frequency,a=costs[0],b=costs[1],c=costs[2],d=costs[3],e=costs[4],f=costs[5],g=costs[6],h=costs[7],i=costs[8],bytes=bytes-allocated,gc=gc-collections,unityBytes=gcAllocated.Valid?gcAllocated.LastValue:-1});
            Array.Clear(costs,0,costs.Length); last=now; allocated=GC.GetAllocatedBytesForCurrentThread(); collections=gc;
        }
        void OnApplicationQuit()
        {
            if (folder == null) return;
            Directory.CreateDirectory(folder);
            using(var writer=new StreamWriter(Path.Combine(folder,"frame-pacing.csv"),false,new UTF8Encoding(false))) {
                writer.WriteLine("frame,phase,frameMs,"+string.Join("Ms,",Names)+"Ms,mainThreadAllocatedBytes,gc0,unityGCAllocatedBytes");
                foreach(var row in rows)writer.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7:F3},{8:F3},{9:F3},{10:F3},{11:F3},{12},{13},{14}",row.frame,row.phase,row.ms,row.a,row.b,row.c,row.d,row.e,row.f,row.g,row.h,row.i,row.bytes,row.gc,row.unityBytes));
            }
            gcAllocated.Dispose();
        }
    }
}
