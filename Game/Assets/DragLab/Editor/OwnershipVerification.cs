using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class OwnershipVerification
    {
        public static void RunStandalone(){Debug.Log("OWNERSHIP PASS "+Run(Directory.GetParent(Application.dataPath).Parent.FullName));}
        public static int Run(string root)
        {
            int count = 0; var log = new StringBuilder();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); count++; log.AppendLine("PASS: " + label); };
            var catalog = MechanicsTransactions.Catalog(); long now = DateTime.UtcNow.Ticks;
            var profile = PlayerProfile.New(now); profile.cash = 12345; profile.reputation = 99;
            profile.SelectedCar.tune.engineLevel = 3; profile.SelectedCar.tune.tireLevel = 2;
            profile.SelectedCar.lastPass = new PassRecord { elapsedSeconds = 15, tune = new Tune { engineLevel = 2 }, driving = new DriverSettings() };
            MechanicsOwnership.Ensure(profile, catalog); string initial = JsonUtility.ToJson(profile);
            MechanicsOwnership.Ensure(profile, catalog);
            check(initial == JsonUtility.ToJson(profile), "Migration is idempotent; no new parts/IDs on repeat.");
            check(profile.cash == 12345 && profile.reputation == 99 && profile.SelectedCar.lastPass.elapsedSeconds == 15 && profile.SelectedCar.lastPass.tune.engineLevel == 2, "Migration retains cash, career and historical pass configuration.");
            check(MechanicsOwnership.Resolve(profile, profile.SelectedCar, catalog).tune.PowerMultiplier == 1.48f, "Legacy mechanical rank becomes installed head/intake definition.");
            profile.cars.Add(new OwnedCar { vehicleId = "starter-hatch" }); MechanicsOwnership.Ensure(profile, catalog);
            check(profile.cars[0].instanceId != profile.cars[1].instanceId && profile.cars[0].engineAssemblyId != profile.cars[1].engineAssemblyId && profile.parts.Select(p => p.instanceId).Distinct().Count() == profile.parts.Count, "Two same-model vehicles own independent chassis, engines and parts.");
            var car = profile.cars[0]; var binding = car.installed.slots.Find(s => s.slot == "Pistons");
            var item = profile.parts.Find(p => p.instanceId == binding.partInstanceId); item.condition.health = .42f; item.condition.failed = true; item.condition.failureReason = "injected fixture";
            MechanicsOwnership.Remove(profile, car, "Pistons"); MechanicsOwnership.Replace(profile, profile.cars[1], item, catalog);
            check(item.condition.health == .42f && item.condition.failed && item.ownerId == profile.cars[1].engineAssemblyId && MechanicsOwnership.Validate(profile, catalog) == null, "Donor move retains identity and failed condition without duplicating ownership.");
            MechanicsOwnership.Ensure(profile, catalog);
            check(!MechanicsOwnership.Resolve(profile, car, catalog).Drivable && !car.installed.slots.Any(s => s.slot == "Pistons"), "Reload/migration does not invent a removed OEM component.");
            string before = JsonUtility.ToJson(profile); PlayerProfile committed; string error;
            check(!MechanicsTransactions.Try(profile, catalog, profile.revision, p => p.cash -= 100, p => false, out committed, out error) && ReferenceEquals(committed, profile) && before == JsonUtility.ToJson(profile), "Failed persistence rolls back cash and every owned instance.");
            check(!MechanicsTransactions.Try(profile, catalog, profile.revision + 1, p => p.cash += 999, p => true, out committed, out error), "Stale transaction revision rejected.");
            check(!MechanicsTransactions.Try(profile, catalog, profile.revision, p => p.parts.Add(p.parts[0].Copy()), p => true, out committed, out error), "Duplicate instance transaction rejected.");
            check(MechanicsTransactions.Try(profile, catalog, profile.revision, p => p.cash -= 1, p => true, out committed, out error) && committed.revision == profile.revision + 1 && profile.cash == 12345, "Successful transaction publishes only validated candidate.");
            var inherited=item.condition.Copy();inherited.elements[0].definitionId="";var nullable=item.condition.Copy();nullable.elements[0].definitionId=null;
            check(EngineElements.CanAdvance(nullable,inherited)&&EngineElements.CanAdvance(inherited,nullable),"Absent and empty per-member specification both inherit the pack across JSON round trips.");inherited.elements[0].definitionId="not-the-same-specification";check(!EngineElements.CanAdvance(nullable,inherited),"A real per-member specification change is still rejected by checkpoint validation.");
            var checkpoint = new RunCheckpoint { runId = "fixture-run", ownedVehicleId = profile.cars[1].instanceId, tick = 120, settled = true };
            var condition = item.Copy(); condition.condition.health = .4f; condition.condition.runningSeconds = 1; checkpoint.components.Add(condition);
            check(MechanicsTransactions.Try(profile, catalog, profile.revision, p => MechanicsTransactions.ApplyCheckpoint(p, checkpoint), p => true, out committed, out error), "Owned run checkpoint commits condition once: "+error);
            check(!MechanicsTransactions.Try(committed, catalog, committed.revision, p => MechanicsTransactions.ApplyCheckpoint(p, checkpoint), p => true, out _, out error), "Settled checkpoint cannot repeat.");
            checkpoint.runId = "heal-attempt"; checkpoint.components[0].condition.health = 1;
            check(!MechanicsTransactions.Try(committed, catalog, committed.revision, p => MechanicsTransactions.ApplyCheckpoint(p, checkpoint), p => true, out _, out error), "Checkpoint cannot heal or rewind condition.");
            string folder = Path.Combine(root, "reports/phases/2c"); Directory.CreateDirectory(folder);
            string previous = ProfileStore.VerificationPath;
            try
            {
                ProfileStore.VerificationPath = Path.Combine(folder, "migration-fixture", "profile-v1.json"); Directory.CreateDirectory(Path.GetDirectoryName(ProfileStore.FilePath));
                // Fresh isolated fixture on every verification; actual player path is never touched.
                foreach (string suffix in new[] { "", ".bak", ".legacy-v1", ".tmp" }) if (File.Exists(ProfileStore.FilePath + suffix)) File.Delete(ProfileStore.FilePath + suffix);
                string legacy = File.ReadAllText(Path.Combine(root, "reports/phases/2a/before/legacy-earned-profile-v1.json")); File.WriteAllText(ProfileStore.FilePath, legacy);
                var loaded = ProfileStore.Load(); string identities = string.Join(",", loaded.parts.Select(p => p.instanceId));
                check(loaded.saveVersion == MechanicsOwnership.SaveVersion && File.ReadAllText(ProfileStore.FilePath + ".legacy-v1") == legacy, "Original earned v1 save backed up byte-for-byte before migration.");
                check(string.Join(",", ProfileStore.Load().parts.Select(p => p.instanceId)) == identities, "Migration IDs are durable before any gameplay save; immediate second load repeats them.");
                check(ProfileStore.TrySave(loaded), "Migrated profile atomically saves.");
                var reloaded = ProfileStore.Load(); check(string.Join(",", reloaded.parts.Select(p => p.instanceId)) == identities && reloaded.cash == loaded.cash && reloaded.cars[0].lastPass != null, "Repeated disk load preserves earned records/IDs with no duplicated OEM parts.");
                check(ProfileStore.TrySave(reloaded), "Second complete save writes a recoverable backup.");
                File.WriteAllText(ProfileStore.FilePath, "interrupted invalid fixture");
                var recovered = ProfileStore.Load(); check(recovered.parts.Count == reloaded.parts.Count && recovered.parts[0].instanceId == reloaded.parts[0].instanceId, "Corrupt primary recovers complete previous ownership ledger.");
                File.WriteAllText(ProfileStore.FilePath, "{\"saveVersion\":999}");
                ProfileStore.Load(); check(!ProfileStore.TrySave(recovered) && File.ReadAllText(ProfileStore.FilePath).Contains("999"), "Unsupported future schema cannot be overwritten by fallback/recovered profile.");
                File.WriteAllText(ProfileStore.FilePath, JsonUtility.ToJson(reloaded)); ProfileStore.Load();
            }
            finally { ProfileStore.VerificationPath = previous; }
            File.WriteAllText(Path.Combine(folder, "ownership-verification.txt"), log + "Checks: " + count + "\n");
            return count;
        }
    }
}
