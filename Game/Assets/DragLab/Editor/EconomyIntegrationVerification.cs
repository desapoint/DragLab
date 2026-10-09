using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    // Integration fixtures use production transactions, disk persistence and the shared race.
    // Cash/damage and deadline advances are explicit fixture inputs, not economy pacing evidence.
    public static class EconomyIntegrationVerification
    {
        sealed class Fixture
        {
            public PlayerProfile p;
            public long now;
            public readonly MechanicalCatalog cat;
            public readonly OperatingPolicy costs;
            public readonly ValuationPolicy values;
            public readonly GarageBusinessPolicy business;
            public readonly Action<bool, string> check;
            public readonly string model;
            public Fixture(string model, string folder, Action<bool, string> check)
            {
                this.model = model; this.check = check; now = DateTime.UtcNow.Ticks;
                cat = MechanicsTransactions.Catalog();
                costs = Resources.Load<OperatingPolicyDefinition>("Data/Economy/OperatingPolicy").policy;
                values = Resources.Load<ValuationPolicyDefinition>("Data/Economy/ValuationPolicy").policy;
                business = Resources.Load<GarageBusinessPolicyDefinition>("Data/Economy/GarageBusinessPolicy").policy;
                p = PlayerProfile.New(now); p.SelectedCar.vehicleId = model; p.selectedVehicleId = model;
                p.cash = 100000; p.reputation = 99;
                p.driving.ApplyPreset(DrivingPreset.Automatic);
                p.driving.ApplySimulationPreset(SimulationDifficulty.Simulation);
                MechanicsOwnership.Ensure(p, cat);
                ProfileStore.VerificationPath = Path.Combine(folder, model, "profile-v1.json");
            }
            public void Check(bool ok, string label) { check(ok, model + ": " + label); }
            public void Commit(Action<PlayerProfile> operation)
            {
                PlayerProfile next; string error;
                bool ok = MechanicsTransactions.Try(p, cat, p.revision, operation, ProfileStore.TrySave, out next, out error);
                Check(ok, "durable transaction / " + error); p = next;
            }
            public void Reject(Action<PlayerProfile> operation, string label, bool diskFailure = false)
            {
                string before = JsonUtility.ToJson(p); PlayerProfile next; string error;
                bool ok = MechanicsTransactions.Try(p, cat, p.revision, operation, q => !diskFailure, out next, out error);
                Check(!ok && ReferenceEquals(next, p) && before == JsonUtility.ToJson(p), label + " / " + error);
            }
            public void Reload(string label, bool interrupted = false)
            {
                string before = JsonUtility.ToJson(p); p = ProfileStore.Load(interrupted);
                Check(MechanicsOwnership.Validate(p, cat) == null, label + " ownership valid");
                if (!interrupted) Check(SnapshotEquality.Profile(before, p, cat), label + " exact state apart from JSON double spelling");
            }
            public void Pay(PaymentRequest request)
            {
                var quote = PaidEconomy.Quote(p, cat, costs, values, request, now);
                Commit(q => PaidEconomy.Pay(q, cat, costs, values, quote, now, request.offer));
            }
            public PartInstance Part(string car, PartCategory category)
            {
                string id = p.cars.Find(c => c.instanceId == car).installed.slots.Find(s => s.slot == category.ToString()).partInstanceId;
                return p.parts.Find(i => i.instanceId == id);
            }
            public RaceSimulation Race(string carId, CareerEventDefinition e)
            {
                var c = p.cars.Find(v => v.instanceId == carId); var b = MechanicsOwnership.Resolve(p, c, cat);
                var race = RaceSessionFactory.Create(b.spec, b.tune, p.driving.Copy(), e.opponent.spec, e.opponentTune,
                    new DriverSettings { reactionSeconds = .22 }, e.distanceM,
                    p.parts.FindAll(i => i.ownerId == c.instanceId || i.ownerId == c.engineAssemblyId));
                race.player.SetFluids(c.fluids);
                race.player.SetMassInputs(b.tune.driverMassKg, b.tune.payloadMassKg, b.tune.payloadPositionM, b.tune.payloadHeightM, c.fuel.litres);
                race.player.state.fuelUsedLitres = c.fuel.cumulativeUsedLitres; return race;
            }
        }
        [MenuItem("DragLab/Verify Economy Save Integration")]
        public static void RunStandalone()
        {
            string root = Directory.GetParent(Application.dataPath).Parent.FullName;
            string folder = Path.Combine(root, "reports/phases/3h"); Directory.CreateDirectory(folder);
            string fixtures = Path.Combine(folder, "fixtures", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            var log = new StringBuilder(); var matrix = new StringBuilder("model,cars,engines,parts,career_records,jobs,cash,partial_fuel,revision\n"); int checks = 0;
            Action<bool, string> check = (ok, label) => {
                log.AppendLine((ok ? "PASS: " : "FAIL: ") + label);
                File.WriteAllText(Path.Combine(folder, "integration-progress.txt"), log.ToString(), Encoding.UTF8);
                if (!ok) throw new InvalidOperationException(label); checks++;
            };
            string previous = ProfileStore.VerificationPath;
            try
            {
                foreach (var asset in Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v => v.spec.id))
                {
                    var f = new Fixture(asset.spec.id, fixtures, check); string a = f.p.SelectedCar.instanceId;
                    var offer = new VehicleOffer { model = asset.spec.id, price = asset.purchasePrice, reputation = asset.reputationRequired, stock = asset.CreateStockTune() };
                    f.Pay(new PaymentRequest { kind = PaymentKind.Car, offer = offer }); string b = f.p.SelectedCar.instanceId;
                    f.Check(a != b && f.p.cars.Count == 2, "paid duplicate model has independent ownership");
                    f.Commit(p => {
                        var c = p.cars.Find(v => v.instanceId == a); c.fuel.litres = 7.25; c.fuel.cumulativeUsedLitres = 3.125;
                        var piston = p.parts.Find(i => i.instanceId == c.installed.slots.Find(s => s.slot == "Pistons").partInstanceId);
                        piston.condition.health = .82f; piston.condition.elements[0].health = .79f;
                        c.fluids.oilLitres -= .25; c.fluids.coolantLitres -= .25;
                    });
                    f.Pay(new PaymentRequest { kind = PaymentKind.Part, carId = a, targetId = f.cat.Id(f.model, PartCategory.HeadIntake, 1), install = true });
                    string setup = ""; f.Commit(p => setup = SavedSetups.Save(p, a, "Retained built engine", SetupScope.Engine));
                    string missing = f.Part(b, PartCategory.HeadIntake).instanceId;
                    f.Commit(p => PartOperations.Remove(p, b, PartCategory.HeadIntake));
                    f.Reload("partial tanks, individual wear, duplicate models and missing kit");
                    f.Check(!MechanicsOwnership.Resolve(f.p, f.p.cars.Find(c => c.instanceId == b), f.cat).Drivable,
                        "missing required kit remains non-drivable without manufactured OEM");
                    f.Commit(p => PartOperations.Install(p, f.cat, b, missing));
                    string failed = f.Part(b, PartCategory.Pistons).instanceId;
                    f.Commit(p => { var c = p.parts.Find(i => i.instanceId == failed).condition; c.health = .2f; c.failed = true; c.failureReason = "Known integration fixture failure"; });
                    f.Commit(p => GarageBusiness.Assign(p, f.business, a, "photo-session", f.now));
                    f.Pay(new PaymentRequest { kind = PaymentKind.Rebuild, carId = b });
                    f.Reload("simultaneous display and rebuild reservations");
                    var job = f.p.business.jobs.Single(); var rebuild = f.p.cars.Find(c => c.instanceId == b).engineRebuild;
                    f.Check(CareerProgression.EntryError(f.p, f.p.cars.Find(c => c.instanceId == a), null) != null &&
                        CareerProgression.EntryError(f.p, f.p.cars.Find(c => c.instanceId == b), null) != null, "both reservations block entry");
                    f.Reject(p => AssemblyOperations.Detach(p, a), "assigned engine cannot detach");
                    f.Reject(p => AssemblyOperations.Detach(p, b), "rebuild engine cannot detach");
                    f.Reject(p => GarageBusiness.Finish(p, job.id, f.now), "early job claim rejects atomically");
                    f.Reject(p => EngineRebuilds.Collect(p, b, rebuild.id, f.now), "early rebuild collection rejects atomically");
                    f.now = Math.Max(job.readyUtcTicks, rebuild.readyUtcTicks);
                    f.Reject(p => GarageBusiness.Finish(p, job.id, f.now), "failed job save publishes no payout/release", true);
                    int cash = f.p.cash;
                    f.Commit(p => GarageBusiness.Finish(p, job.id, f.now));
                    f.Check(f.p.cash == cash + job.reward, "offline job pays captured reward");
                    f.Reject(p => GarageBusiness.Finish(p, job.id, f.now), "repeat job claim rejected");
                    f.Reject(p => EngineRebuilds.Collect(p, b, rebuild.id, f.now), "failed rebuild save retains paid reservation", true);
                    cash = f.p.cash; f.Commit(p => EngineRebuilds.Collect(p, b, rebuild.id, f.now));
                    f.Check(f.p.cash == cash && f.p.parts.Find(i => i.instanceId == failed).location == PartLocation.Inventory &&
                        f.p.parts.Find(i => i.instanceId == failed).condition.failed, "collection charges no second payment and retains failed original");
                    f.Reload("released jobs and retained failed originals");

                    var e = Resources.LoadAll<CareerEventDefinition>("Data/Events").Single(v => v.stableId == "first-pass");
                    var race = f.Race(a, e); string run = MechanicsOwnership.NewId("3h-interrupted");
                    f.Commit(p => { CareerProgression.Enter(p, run, a, e, false); MechanicsTransactions.ApplyCheckpoint(p, RunPersistence.Capture(run, a, 0, race.player)); });
                    for (int i = 0; i < 900; i++) race.Step(new DriverInput());
                    f.Commit(p => MechanicsTransactions.ApplyCheckpoint(p, RunPersistence.Capture(run, a, 900, race.player)));
                    cash = f.p.cash; double fuel = f.p.cars.Find(c => c.instanceId == a).fuel.litres;
                    f.Reload("mid-career interruption", true);
                    f.Check(f.p.cash == cash && f.p.careerRuns.Find(r => r.runId == run).outcome == "Interrupted / no payout" &&
                        Math.Abs(f.p.cars.Find(c => c.instanceId == a).fuel.litres - fuel) < 1e-10, "interruption retains fuel, fee and no reward");
                    long revision = f.p.revision; f.Reload("repeat interrupted recovery", true);
                    f.Check(f.p.revision == revision && f.p.cash == cash, "interruption recovery is idempotent");

                    race = f.Race(a, e); run = MechanicsOwnership.NewId("3h-finish");
                    f.Commit(p => { CareerProgression.Enter(p, run, a, e, false); MechanicsTransactions.ApplyCheckpoint(p, RunPersistence.Capture(run, a, 0, race.player)); });
                    int ticks = 0; while (race.phase != RacePhase.Finished && ticks++ < 25000) race.Step(new DriverInput());
                    f.Check(ticks < 25000 && race.player.state.finished && !race.player.state.disqualified, "real shared race completes");
                    var capture = RunPersistence.Capture(run, a, ticks, race.player);
                    f.Reject(p => { MechanicsTransactions.ApplyCheckpoint(p, capture); CareerProgression.Settle(p, run, race); }, "failed result save retains unsettled reward/resources", true);
                    f.Commit(p => { MechanicsTransactions.ApplyCheckpoint(p, capture); CareerProgression.Settle(p, run, race); });
                    cash = f.p.cash; int records = f.p.cars.Find(c => c.instanceId == a).completedRuns;
                    f.Reject(p => CareerProgression.Settle(p, run, race), "duplicate result cannot pay twice");
                    f.Reload("finish saved before shutdown closure", true);
                    f.Check(f.p.cash == cash && f.p.cars.Find(c => c.instanceId == a).completedRuns == records &&
                        f.p.careerRuns.Find(r => r.runId == run).complete && f.p.runCheckpoints.All(r => r.settled), "restart preserves paid finish and closes only resources");

                    string engine = f.p.cars.Find(c => c.instanceId == a).engineAssemblyId;
                    var quote = Valuations.Capture(f.p, f.cat, f.values, ValuationKind.Chassis, f.p.cars.Find(c => c.instanceId == a).chassisId, "recycler", f.now);
                    f.Reject(p => PaidEconomy.Sell(p, f.cat, f.values, quote, f.now), "failed salvage save retains tree and cash", true);
                    f.Commit(p => PaidEconomy.Sell(p, f.cat, f.values, quote, f.now)); f.Reload("partial chassis salvage / retained engine");
                    f.Check(f.p.cars.All(c => c.instanceId != a) && f.p.engines.Any(v => v.instanceId == engine && string.IsNullOrEmpty(v.parentVehicleId)), "salvage keeps actual stored engine and loose failed parts");
                    var sale = Valuations.Capture(f.p, f.cat, f.values, ValuationKind.Engine, engine, "builder", f.now);
                    string[] disposed = sale.IncludedIds.ToArray(); f.Commit(p => PaidEconomy.Sell(p, f.cat, f.values, sale, f.now));
                    f.Reload("sold engine descendants / surviving blueprint");
                    f.Check(!f.p.engines.Any(v => v.instanceId == engine) && !f.p.parts.Any(i => disposed.Contains(i.instanceId)) && f.p.savedSetups.Any(s => s.id == setup), "sold descendants never respawn; blueprint remains virtual");
                    f.Reject(p => PaidEconomy.Sell(p, f.cat, f.values, sale, f.now), "repeat assembly sale rejected");
                    var plan = SavedSetups.Preview(f.p, b, setup);
                    f.Check(plan.errors.Count == 0 && plan.Missing > 0, "sold configuration previews paid missing components");
                    f.Reject(p => SavedSetups.Apply(p, b, setup, plan.revision, false, plan.cost), "missing-part setup cannot fabricate unbought hardware");
                    cash = f.p.cash; f.Commit(p => SavedSetups.Apply(p, b, setup, plan.revision, true, plan.cost));
                    f.Check(f.p.cash == cash - plan.cost, "explicit setup apply buys missing parts once");
                    f.Reload("rehydrated configuration with paid new identities");

                    // Two complete saves produce a current backup; corrupt only this isolated fixture.
                    f.Check(ProfileStore.TrySave(f.p) && ProfileStore.TrySave(f.p), "matching backup is durable");
                    string expected = JsonUtility.ToJson(f.p);
                    File.WriteAllText(ProfileStore.FilePath, "fixture crash during write", Encoding.UTF8);
                    f.p = ProfileStore.Load(); f.Check(SnapshotEquality.Profile(expected, f.p, f.cat), "corrupt primary recovers matching cash/resources/ownership/jobs");
                    f.Check(ProfileStore.TrySave(f.p), "recovered backup can be republished");
                    File.WriteAllText(ProfileStore.FilePath, "{\"saveVersion\":999}", Encoding.UTF8); ProfileStore.Load();
                    f.Check(!ProfileStore.TrySave(f.p) && File.ReadAllText(ProfileStore.FilePath).Contains("999"), "future-schema fallback never overwrites original");
                    File.WriteAllText(ProfileStore.FilePath, expected, Encoding.UTF8); f.p = ProfileStore.Load();
                    matrix.AppendLine(string.Join(",", f.model, f.p.cars.Count, f.p.engines.Count, f.p.parts.Count, f.p.careerRuns.Count, f.p.business.jobs.Count, f.p.cash,
                        f.p.SelectedCar.fuel.litres.ToString("R", System.Globalization.CultureInfo.InvariantCulture), f.p.revision));
                }
                // Required static legacy earned save: verify migration durability without current fixtures.
                ProfileStore.VerificationPath = Path.Combine(fixtures, "legacy", "profile-v1.json"); Directory.CreateDirectory(Path.GetDirectoryName(ProfileStore.FilePath));
                File.Copy(Path.Combine(root, "reports/phases/2a/before/legacy-earned-profile-v1.json"), ProfileStore.FilePath);
                var migrated = ProfileStore.Load(); string ids = string.Join("|", migrated.parts.Select(i => i.instanceId));
                check(migrated.saveVersion == MechanicsOwnership.SaveVersion && File.Exists(ProfileStore.FilePath + ".legacy-v1"), "legacy earned profile migrates and retains original");
                check(ids == string.Join("|", ProfileStore.Load().parts.Select(i => i.instanceId)) && migrated.cash == ProfileStore.Load().cash, "legacy repeat load preserves IDs and earned cash");
                var old = PlayerProfile.New(DateTime.UtcNow.Ticks); MechanicsOwnership.Ensure(old, MechanicsTransactions.Catalog()); old.saveVersion = 4; old.business = null; old.careerRuns = null;
                ProfileStore.VerificationPath = Path.Combine(fixtures, "schema4", "profile-v1.json"); Directory.CreateDirectory(Path.GetDirectoryName(ProfileStore.FilePath));
                File.WriteAllText(ProfileStore.FilePath, JsonUtility.ToJson(old), Encoding.UTF8); migrated = ProfileStore.Load();
                check(migrated.saveVersion == MechanicsOwnership.SaveVersion && migrated.business.version == 1 && migrated.careerRuns.Count == 0 && File.Exists(ProfileStore.FilePath + ".legacy-v4"), "schema4 gains career/business contracts without invented payouts");
                check(SnapshotEquality.Profile(JsonUtility.ToJson(migrated), ProfileStore.Load(), MechanicsTransactions.Catalog()), "schema4 migration is repeatable");
            }
            finally { ProfileStore.VerificationPath = previous; }
            File.WriteAllText(Path.Combine(folder, "integration-verification.txt"), log + "Checks: " + checks + "\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "integration-matrix.csv"), matrix.ToString(), Encoding.UTF8);
            Debug.Log("DRAGLAB 3H ECONOMY INTEGRATION PASS: " + checks);
        }
    }
}
