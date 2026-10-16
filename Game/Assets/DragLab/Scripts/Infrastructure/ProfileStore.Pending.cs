using System;
using System.Threading.Tasks;
using DragLab.Domain;
using UnityEngine;
namespace DragLab.Infrastructure
{
    public static partial class ProfileStore
    {
        internal static Task<long> BeginValidatedSave(PlayerProfile candidate)
        {
            if(writeBlocked)throw new InvalidOperationException("Save writes blocked; existing database preserved.");
            RaceCheckpointStore.Drain();string warning;
            RaceCheckpointStore.Merge(FilePath,candidate,false,out warning);
            DrivingPreferences.Ensure(candidate);SqliteStateStore.Prepare();
            string path=FilePath,build=Application.version;
            return Task.Run(()=>{var snapshot=SqliteStateStore.CaptureManaged(candidate,build);SqliteStateStore.Write(path,snapshot);return snapshot.committed;});
        }
        internal static void CompleteValidatedSave(PlayerProfile candidate,Task<long> save)
        {
            candidate.storageCommit=save.GetAwaiter().GetResult();RaceCheckpointStore.Retire(FilePath,false);Status="Saved.";
        }
    }
}
