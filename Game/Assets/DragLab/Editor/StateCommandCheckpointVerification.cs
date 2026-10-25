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
    /// <summary>Isolated state-command and existing storage regression checkpoints.</summary>
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
            VerifyTypedSaveMigration("S05A");
            VerifyLegacyStorage("S05A");
        }

        public static void RunTypedFluidRefill()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            SqlitePluginSetup.Configure();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S05B"));
            Directory.CreateDirectory(root);
            string fixtures = Path.Combine(root, "refill-fixtures-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
            var log = new StringBuilder();
            int count = 0;
            try
            {
                TypedFluidRefillCommitChecks.Run((ok, label) =>
                {
                    log.AppendLine((ok ? "PASS " : "FAIL ") + label);
                    if (!ok) throw new InvalidOperationException(label);
                    count++;
                }, fixtures);
                log.AppendLine("RESULT PASS count=" + count);
                Debug.Log("S05b focused typed refill checks passed: " + count);
            }
            catch (Exception error)
            {
                log.AppendLine("RESULT FAIL count=" + count);
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                File.WriteAllText(Path.Combine(root, "typed-refill-checks.txt"), log.ToString(), new UTF8Encoding(false));
            }
            VerifyTypedSaveMigration("S05B");
            VerifyLegacyStorage("S05B");
        }

        public static void RunTypedFluidRefillCoordinator()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            SqlitePluginSetup.Configure();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S06A"));
            Directory.CreateDirectory(root);
            string fixtures = Path.Combine(root, "coordinator-fixtures-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
            var log = new StringBuilder();
            int count = 0;
            try
            {
                TypedFluidRefillCoordinatorChecks.Run((ok, label) =>
                {
                    log.AppendLine((ok ? "PASS " : "FAIL ") + label);
                    if (!ok) throw new InvalidOperationException(label);
                    count++;
                }, fixtures);
                log.AppendLine("RESULT PASS count=" + count);
                Debug.Log("S06a pending refill coordinator checks passed: " + count);
            }
            catch (Exception error)
            {
                log.AppendLine("RESULT FAIL count=" + count);
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                File.WriteAllText(Path.Combine(root, "coordinator-checks.txt"), log.ToString(), new UTF8Encoding(false));
            }
            VerifyLegacyStorage("S06A");
        }

        public static void RunSelectedProfileOwner()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            SqlitePluginSetup.Configure();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S06B"));
            Directory.CreateDirectory(root);
            string fixtures = Path.Combine(root, "owner-fixtures-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
            var log = new StringBuilder();
            int count = 0;
            try
            {
                SelectedProfileOwnerChecks.Run((ok, label) =>
                {
                    log.AppendLine((ok ? "PASS " : "FAIL ") + label);
                    if (!ok) throw new InvalidOperationException(label);
                    count++;
                }, fixtures);
                log.AppendLine("RESULT PASS count=" + count);
                Debug.Log("S06b selected profile owner checks passed: " + count);
            }
            catch (Exception error)
            {
                log.AppendLine("RESULT FAIL count=" + count);
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                File.WriteAllText(Path.Combine(root, "owner-checks.txt"), log.ToString(), new UTF8Encoding(false));
            }
            VerifyTypedSaveMigration("S06B");
            VerifyLegacyStorage("S06B");
        }

        public static void RunTypedFluidRefillPilot()
            => RunRefillCheckpoint("S06C");

        // Later checkpoints must select their own report root explicitly, so a
        // regression cannot silently replace an earlier checkpoint's evidence.
        public static void RunTypedFluidRefillPilotCheckpoint()
            => RunRefillCheckpoint(SelectedCheckpoint());

        public static void RunNativeReadyProductGroupsCheckpoint()
        {
            string checkpoint = SelectedCheckpoint();
            VerifyReadyProductGroups(checkpoint);
            RunRefillCheckpoint(checkpoint);
        }

        public static void RunNativeReadyProductGroupsOnlyCheckpoint()
            => VerifyReadyProductGroups(SelectedCheckpoint());

        static void VerifyReadyProductGroups(string checkpoint)
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            VerifyScopedManagedChecks(checkpoint, "ready-product-groups", NativeReadyProductGroupChecks.Run);
            VerifyScopedManagedChecks(checkpoint, "shop-supply-projection", ShopSupplyProjectionChecks.Run);
        }

        static string SelectedCheckpoint()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--draglab-save-checkpoint");
            if (at < 0 || at + 1 >= args.Length ||
                !System.Text.RegularExpressions.Regex.IsMatch(args[at + 1], @"\AS[0-9]{2}[A-Z]?[0-9]{0,2}\z"))
                throw new ArgumentException("Select a safe checkpoint report name with --draglab-save-checkpoint (for example S06D1).");
            return args[at + 1];
        }

        static void RunRefillCheckpoint(string checkpoint)
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            SqlitePluginSetup.Configure();
            VerifyScopedManagedChecks(checkpoint, "pilot", TypedFluidRefillPilotChecks.Run);
            VerifyScopedManagedChecks(checkpoint, "owner-regression", SelectedProfileOwnerChecks.Run);
            VerifyTypedSaveMigration(checkpoint);
            VerifyLegacyStorage(checkpoint);
        }

        static void VerifyScopedManagedChecks(string checkpoint, string name,
            Action<Action<bool, string>, string> run)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/" + checkpoint));
            Directory.CreateDirectory(root);
            string fixtures = Path.Combine(root, name + "-fixtures-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
            var log = new StringBuilder();
            int count = 0;
            try
            {
                run((ok, label) =>
                {
                    log.AppendLine((ok ? "PASS " : "FAIL ") + label);
                    if (!ok) throw new InvalidOperationException(label);
                    count++;
                }, fixtures);
                log.AppendLine("RESULT PASS count=" + count);
                Debug.Log(checkpoint + " " + name + " checks passed: " + count);
            }
            catch (Exception error)
            {
                log.AppendLine("RESULT FAIL count=" + count);
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                File.WriteAllText(Path.Combine(root, name + "-checks.txt"), log.ToString(), new UTF8Encoding(false));
            }
        }

        static void VerifyTypedSaveMigration(string checkpoint)
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            SqlitePluginSetup.Configure();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/" + checkpoint));
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
