using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using DragLab.Data;
using DragLab.Infrastructure;
using DragLab.Runtime;

namespace DragLab.Editor
{
    /// <summary>Isolated dormant-contract and existing storage regression checkpoint.</summary>
    public static class StateCommandCheckpointVerification
    {
        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            StateCommandContractChecks.RunStandalone();
            VerifyLegacyStorage("S01");
        }

        public static void RunPartIndex()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            OwnedPartIndexChecks.RunStandalone();
            VerifyLegacyStorage("S02");
        }

        public static void RunInstalledLookup()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            InstalledPartLookupChecks.RunStandalone();
            VerifyLegacyStorage("S02B");
        }

        public static void RunOwnershipValidation()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            OwnershipValidationLookupChecks.RunStandalone();
            VerifyLegacyStorage("S02C");
        }

        public static void RunSteadyOutputCache()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            SteadyOutputCacheChecks.RunStandalone();
            VerifyLegacyStorage("S03A");
        }

        public static void RunFluidRefillPlanner()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            FluidRefillPlannerChecks.RunStandalone();
            VerifyLegacyStorage("S04");
        }

        public static void RunTypedStateRecord()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            SqlitePluginSetup.Configure();
            TypedStateRecordChecks.RunStandalone();
            VerifyLegacyStorage("S05A1");
        }

        public static void RunTypedSaveMigration()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            SqlitePluginSetup.Configure();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S05A"));
            Directory.CreateDirectory(root);
            string fixtures = Path.Combine(root, "migration-fixtures-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
            var log = new StringBuilder();
            int count = 0;
            try
            {
                TypedSaveMigrationChecks.Run(fixtures, Resources.LoadAll<VehicleDefinition>("Data/Vehicles"),
                    (ok, label) =>
                    {
                        log.AppendLine((ok ? "PASS " : "FAIL ") + label);
                        if (!ok) throw new InvalidOperationException(label);
                        count++;
                    });
                log.AppendLine("RESULT PASS count=" + count);
                Debug.Log("S05a focused typed save migration checks passed: " + count);
            }
            catch (Exception error)
            {
                log.AppendLine("RESULT FAIL count=" + count);
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                File.WriteAllText(Path.Combine(root, "typed-migration-checks.txt"), log.ToString(), new UTF8Encoding(false));
            }
            VerifyLegacyStorage("S05A");
        }

        static void VerifyLegacyStorage(string checkpoint)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/" + checkpoint));
            Directory.CreateDirectory(root);
            string fixtures = Path.Combine(root, "storage-fixtures-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
            var log = new StringBuilder();
            int count = 0;
            try
            {
                SqlitePluginSetup.Configure();
                SqliteStateVerification.Run(fixtures, Resources.LoadAll<VehicleDefinition>("Data/Vehicles"),
                    (ok, label) =>
                    {
                        log.AppendLine((ok ? "PASS " : "FAIL ") + label);
                        if (!ok) throw new InvalidOperationException(label);
                        count++;
                    }, note => log.AppendLine(note), limit: 2);
                log.AppendLine("RESULT PASS count=" + count);
                Debug.Log(checkpoint + " isolated legacy storage regression passed: " + count);
            }
            catch (Exception error)
            {
                log.AppendLine("RESULT FAIL count=" + count);
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                File.WriteAllText(Path.Combine(root, "legacy-storage-checks.txt"), log.ToString(), new UTF8Encoding(false));
            }
        }
    }
}
