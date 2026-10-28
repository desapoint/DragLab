using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DragLab.Domain
{
    // Real time/identities in normal games. The opt-in development capture player
    // has an isolated save and fixed entropy; no Unity API runs in initializers.
    public static partial class RuntimeEntropy
    {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR
        public static readonly bool Capture = Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-deterministic-capture") >= 0
            && (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-verify") >= 0
                || Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-bench-verify") >= 0
                || Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-driving-verify") >= 0);
#else
        public static readonly bool Capture = false;
#endif
        static readonly Dictionary<string,int> sequences = new Dictionary<string,int>();
        // Optional diagnostic implementation is absent from release players.
        static partial void ObserveGeneratedIdentity(string scope,Guid value);
        public static DateTime UtcNow => Capture ? new DateTime(2026,10,16,12,0,0,DateTimeKind.Utc) : DateTime.UtcNow;
        public static Guid NewGuid(string scope = "identity")
        {
            Guid value;
            if (!Capture) value=Guid.NewGuid();
            else lock(sequences) {
                sequences.TryGetValue(scope,out int next);sequences[scope]=++next;
                using(var hash=MD5.Create()) value=new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes(scope+"/"+next)));
            }
            ObserveGeneratedIdentity(scope,value);
            return value;
        }
        public static int MarketSeed => Capture ? 16102026 : Guid.NewGuid().GetHashCode();
    }
}
