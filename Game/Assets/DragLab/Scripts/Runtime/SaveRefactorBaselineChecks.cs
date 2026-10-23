using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
#if UNITY_EDITOR
    /// <summary>Isolated S00 diagnostic. It never reads or writes the active player slot.</summary>
    public static class SaveRefactorBaselineChecks
    {
        const int Seed = 731942;
        const long Clock = 638650000000000000L;
        const int WarmRepeats = 5;
        static readonly Func<long> Allocated = AllocationCounter();
        sealed class SampleValue { public string action; public int cars, spares; public double milliseconds; }
        static readonly List<SampleValue> Samples = new List<SampleValue>();

        static Func<long> AllocationCounter()
        {
            var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            if(method==null)return ()=>-1;
            var counter=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);
            long before=counter();var probe=new byte[1024*1024];probe[0]=1;GC.KeepAlive(probe);
            return counter()>before?counter:(Func<long>)(()=>-1);
        }

        static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S00"));
        static string Fixtures => Path.GetFullPath(Path.Combine(Application.dataPath,"../../tools/fixtures/save-refactor-s00"));
        static PlayerProfile Clone(PlayerProfile p)
        {
            var copy = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));
            copy.catalog = p.catalog;
            copy.storageCommit = p.storageCommit;
            return copy;
        }

        static PlayerProfile MakeProfile(MechanicalCatalog catalog, int cars, int spares, PartDefinition measuredPart)
        {
            string fixturePath = Path.Combine(Fixtures, "fixture-" + cars + "cars-" + spares + "spares.json.gz");
            if (File.Exists(fixturePath))
            {
                string fixtureJson;
                using(var input=File.OpenRead(fixturePath))using(var compressed=new GZipStream(input,CompressionMode.Decompress))using(var reader=new StreamReader(compressed,Encoding.UTF8,true))fixtureJson=reader.ReadToEnd();
                var saved = JsonUtility.FromJson<PlayerProfile>(fixtureJson);
                saved.catalog = catalog;
                string importedJson=JsonUtility.ToJson(saved);
                Directory.CreateDirectory(Root);
                File.WriteAllText(Path.Combine(Root,"import-"+cars+"cars-"+spares+"spares.json"),importedJson,new UTF8Encoding(false));
                if (saved.cars.Count != cars || saved.parts.Count(i => i.location == PartLocation.Inventory) != spares || saved.inventory.market.seed != Seed)
                    throw new InvalidDataException("Saved S00 fixture does not match its workload key: " + fixturePath);
                MechanicsOwnership.Ensure(saved, catalog);
                if (JsonUtility.ToJson(saved) != importedJson)
                {
                    Directory.CreateDirectory(Root);
                    File.WriteAllText(Path.Combine(Root,"fixture-import-diagnostic.json"),importedJson,new UTF8Encoding(false));
                    File.WriteAllText(Path.Combine(Root,"fixture-normalized-diagnostic.json"),JsonUtility.ToJson(saved),new UTF8Encoding(false));
                    throw new InvalidDataException("Saved S00 fixture changed while loading/normalizing: " + fixturePath);
                }
                var savedSelected = saved.SelectedCar;
                if (catalog.Spec(savedSelected.vehicleId).fuel.capacityLitres - savedSelected.fuel.litres < 3 || PartsMarket.Stock(saved, measuredPart)?.units != 1)
                    throw new InvalidDataException("Saved S00 fixture lost its refill capacity or partial-stock cart setup: " + fixturePath);
                return saved;
            }
            throw new FileNotFoundException("Required versioned S00 fixture is absent; do not silently regenerate benchmark identities.",fixturePath);
        }

        static string PhaseRows(Action work)
        {
            var phases = new List<string>();
            Action<string, double> oldMechanics = MechanicsTransactions.Measurement;
            Action<string, double> oldSave = ProfileStore.SaveMeasurement;
            Action<string, double> oldSqlite = SqliteStateStore.Measurement;
            MechanicsTransactions.Measurement = (name, ms) => phases.Add("transaction/" + name + "=" + ms.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            ProfileStore.SaveMeasurement = (name, ms) => phases.Add("save/" + name + "=" + ms.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            SqliteStateStore.Measurement = (name, ms) => phases.Add("sqlite/" + name + "=" + ms.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            try { work(); }
            finally { MechanicsTransactions.Measurement = oldMechanics; ProfileStore.SaveMeasurement = oldSave; SqliteStateStore.Measurement = oldSqlite; }
            return string.Join(";", phases);
        }

        static void RemoveIsolatedSave(string path)
        {
            if(Path.GetDirectoryName(Path.GetFullPath(path))!=Root)throw new InvalidOperationException("Cleanup target is outside the isolated report folder.");
            ProfileStore.Flush();
            SqliteStateStore.Invalidate();
            foreach (string suffix in new[] { "", ".bak", ".run", ".run.bak", ".run.tmp", ".tmp", "-wal", "-shm", "-journal", ".backup-next", ".backup-next-wal", ".backup-next-shm", ".backup-next-journal" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
            foreach(string suffix in new[]{"",".bak",".next"}){string settings=Path.Combine(Root,"driving-settings.json")+suffix;if(File.Exists(settings))File.Delete(settings);}
        }

        static string DatabaseRows(string path)
        {
            using(var db=new SqliteNative(path,true)){
                var text=new StringBuilder();
                foreach(var table in db.Query("SELECT name,sql FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name")){
                    text.Append(table[0].Length).Append(':').Append(table[0]).Append(table[1]?.Length??-1).Append(':').Append(table[1]);
                    string quoted="\""+table[0].Replace("\"","\"\"")+"\"";
                    var rows=db.Query("SELECT * FROM "+quoted).Select(row=>{var encoded=new StringBuilder();foreach(var value in row)encoded.Append(value?.Length??-1).Append(':').Append(value);return encoded.ToString();}).OrderBy(row=>row,StringComparer.Ordinal);
                    foreach(var row in rows)text.Append(row.Length).Append(':').Append(row);
                }
                return Hash(text.ToString());
            }
        }

        static void Sample(StringBuilder csv, int sampleNumber, string action, int cars, int spares, string profileHash, string stockHash, int iteration, bool firstCall, PlayerProfile fixture, Action<PlayerProfile> operation, bool injectFailedSave, bool transactional)
        {
            string savePath = Path.Combine(Root, "save-" + sampleNumber.ToString("D4") + ".sqlite3");
            string oldPath = ProfileStore.VerificationPath;
            ProfileStore.VerificationPath = savePath;
            try
            {
                var isolated = Clone(fixture);
                if (!ProfileStore.TrySave(isolated)) throw new InvalidOperationException("Could not create disposable SQLite baseline: " + ProfileStore.Status);
                ProfileStore.Flush();
                isolated = Clone(isolated);
                string beforeDisk=injectFailedSave?DatabaseRows(savePath):null;
                string beforeRead=!transactional?JsonUtility.ToJson(isolated):null;
                long before = Allocated();
                var timer = Stopwatch.StartNew();
                string phases = PhaseRows(() => {
                    if (!transactional) { operation(isolated); return; }
                    if (!injectFailedSave)
                    {
                        if (!MechanicsTransactions.Try(isolated, fixture.catalog, isolated.revision, candidate => operation(candidate), ProfileStore.TrySave, out _, out var error))
                            throw new InvalidOperationException(error);
                    }
                    else
                    {
                        string original = JsonUtility.ToJson(isolated);
                        bool injected=false;
                        PlayerProfile committed;
                        string error;
                        bool saved = MechanicsTransactions.Try(isolated, fixture.catalog, isolated.revision, candidate => operation(candidate), candidate => {
                            Action<string> previousFailure = SqliteStateStore.FailureInjection;
                            SqliteStateStore.FailureInjection = phase => { if (phase == "before-commit"){injected=true;throw new IOException("S00 injected pre-commit failure");} };
                            try { return ProfileStore.TrySave(candidate); }
                            finally { SqliteStateStore.FailureInjection = previousFailure; }
                        }, out committed, out error);
                        if (!injected || saved || !ReferenceEquals(committed, isolated) || JsonUtility.ToJson(isolated) != original)
                            throw new InvalidOperationException("Injected failed save changed the committed fixture or reported success.");
                    }
                });
                timer.Stop();
                long after = Allocated();
                if(injectFailedSave&&DatabaseRows(savePath)!=beforeDisk)throw new InvalidOperationException("Failed transaction changed durable SQLite rows or metadata.");
                if(!transactional&&JsonUtility.ToJson(isolated)!=beforeRead)throw new InvalidOperationException("Read-only workload mutated its captured profile.");
                Samples.Add(new SampleValue { action = action, cars = cars, spares = spares, milliseconds = timer.Elapsed.TotalMilliseconds });
                csv.Append(action).Append(',').Append(cars).Append(',').Append(spares).Append(',').Append(iteration).Append(',')
                    .Append(firstCall ? "first-call" : "warm-call").Append(',').Append(timer.Elapsed.TotalMilliseconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(before < 0 || after < 0 ? "" : (after - before).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(!transactional ? "" : injectFailedSave ? "0" : SqliteStateStore.LastChangedRows.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(profileHash).Append(',').Append(stockHash).Append(',').Append('"').Append(phases.Replace("\"", "\"\"")).AppendLine("\"");
            }
            finally
            {
                ProfileStore.VerificationPath = oldPath;
                RemoveIsolatedSave(savePath);
            }
        }

        static void RunAction(StringBuilder csv, ref int sampleNumber, PlayerProfile fixture, int cars, int spares, string profileHash, string stockHash, string action, Action<PlayerProfile> operation, bool injectFailedSave = false, bool transactional = true)
        {
            for (int i = 0; i <= WarmRepeats; i++)
            {
                Sample(csv, sampleNumber++, action, cars, spares, profileHash, stockHash, i, i == 0, fixture, operation, injectFailedSave, transactional);
            }
        }

        public static void VerifyRollbackStandalone()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||!Environment.GetCommandLineArgs().Contains("-batchmode"))throw new InvalidOperationException("Run S00 verification only in a separate batch Editor.");
            Directory.CreateDirectory(Root);
            var catalog=new MechanicalCatalog(Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Select(v=>v.spec));
            var definition=catalog.parts.First(d=>d.price>0&&d.category!=PartCategory.CylinderBlock&&!MechanicalCatalog.EngineSlot(d.category));
            var csv=new StringBuilder("action,cars,looseParts,iteration,sampleType,wallMs,callingThreadAllocatedBytes,sqliteRowsChanged,profileSha256,stockSha256,phases\n");int sample=1000;
            foreach(int cars in new[]{1,5,20})foreach(int spares in new[]{0,24,96}){
                var profile=MakeProfile(catalog,cars,spares,definition);
                Sample(csv,sample++,"full-table-rollback",cars,spares,Hash(JsonUtility.ToJson(profile)),Hash(JsonUtility.ToJson(profile.inventory.market)+"\n"+JsonUtility.ToJson(profile.vehicleStock)),0,true,profile,p=>p.cash--,true,true);
            }
            File.WriteAllText(Path.Combine(Root,"rollback-verification.csv"),csv.ToString(),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Root,"rollback-verification.txt"),"PASS: 9 fixtures; before-COMMIT injection reached; committed source unchanged; deterministic fingerprint of every persisted user table/schema unchanged.\n",new UTF8Encoding(false));
            UnityEngine.Debug.Log("S00 ALL-TABLE ROLLBACK PASS: 9 fixtures.");
        }

        public static void RunStandalone()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !Environment.GetCommandLineArgs().Contains("-batchmode"))
                throw new InvalidOperationException("S00 must run in a separate Unity batchmode process, outside Editor play mode.");
            Samples.Clear();
            Directory.CreateDirectory(Root);
            var csv = new StringBuilder("action,cars,looseParts,iteration,sampleType,wallMs,callingThreadAllocatedBytes,sqliteRowsChanged,profileSha256,stockSha256,phases\n");
            var vehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            var catalog = new MechanicalCatalog(vehicles.Select(v => v.spec));
            var def = catalog.parts.First(d => d.price > 0 && d.category != PartCategory.CylinderBlock && !MechanicalCatalog.EngineSlot(d.category));
            var contentHash = Hash(string.Join("\n", vehicles.OrderBy(v => v.spec.id, StringComparer.Ordinal).Select(v => JsonUtility.ToJson(v.spec))) +
                "\n" + string.Join("\n", catalog.parts.OrderBy(d => d.id, StringComparer.Ordinal).Select(d => JsonUtility.ToJson(d))));
            var manifest = new StringBuilder("S00 isolated baseline\nseed=" + Seed + "\nclockTicks=" + Clock + "\ncontentSha256=" + contentHash +
                "\nrepeatPolicy=one first sample plus five repeats per action in an already-warmed Editor process; each uses a fresh prepopulated DB, not same-DB repeated writes; no process/JIT/OS cold claim or tail percentile estimate\nallocationCounter=" + (Allocated() < 0 ? "unavailable" : "GC.GetAllocatedBytesForCurrentThread; calling thread only") +
                "\nunity=" + Application.unityVersion + "\nplatform=" + SystemInfo.operatingSystem + "\ncpu=" + SystemInfo.processorType + " / " + SystemInfo.processorCount + " logical cores\nramMb=" + SystemInfo.systemMemorySize +
                "\nsqliteSchema="+SqliteStateStore.SchemaVersion+"\nprofileContract="+MechanicsOwnership.SaveVersion+"\nmechanicsSchema="+MechanicsSchema.Version+"\nphysicsModel="+MechanicsSchema.Model+"\ntraceVersion="+MechanicsSchema.TraceVersion+"\ncontentRevision="+MechanicalCatalog.Version+"\nfixtureSource=versioned tools/fixtures/save-refactor-s00 gzip files\nallocationProbe=known 1 MiB allocation must advance counter; otherwise unavailable\nstorageMedium=project report drive; physical device model not sampled\n"+
                "\ntargetFrameRate=" + Application.targetFrameRate + "\nvSyncCount=" + QualitySettings.vSyncCount + "\n");
            int sampleNumber = 0;
            foreach (int cars in new[] { 1, 5, 20 })
            foreach (int spares in new[] { 0, 24, 96 })
            {
                var fixture = MakeProfile(catalog, cars, spares, def);
                string profileJson = JsonUtility.ToJson(fixture);
                string profileHash = Hash(profileJson);
                string stockHash = Hash(JsonUtility.ToJson(fixture.inventory.market) + "\n" + JsonUtility.ToJson(fixture.vehicleStock));
                manifest.Append("case=").Append(cars).Append(" cars / ").Append(spares).Append(" loose parts")
                    .Append(" profileSha256=").Append(profileHash).Append(" stockSha256=").Append(stockHash).AppendLine();
                using(var file=File.OpenRead(Path.Combine(Fixtures,"fixture-"+cars+"cars-"+spares+"spares.json.gz")))using(var zipped=new GZipStream(file,CompressionMode.Decompress))using(var reader=new StreamReader(zipped,Encoding.UTF8,true))manifest.Append("rawFixtureSha256=").Append(Hash(reader.ReadToEnd())).AppendLine();
                manifest.Append("epoch=").Append(fixture.vehicleStock.generation).Append(" completedRuns=").Append(fixture.vehicleStock.completedRuns).Append(" partsSeed=").Append(fixture.inventory.market.seed).Append(" settingsSha256=").Append(Hash(JsonUtility.ToJson(fixture.driving)+"/"+fixture.difficulty)).AppendLine();
                manifest.Append("dimensions parts=").Append(fixture.parts.Count).Append(" fittedMembers=").Append(fixture.parts.Sum(i => (i.condition?.elements?.Length ?? 0) + (i.condition?.tires?.Length ?? 0)))
                    .Append(" marketRows=").Append(fixture.inventory.market.parts.Count).Append(" marketUnits=").Append(fixture.inventory.market.parts.Sum(s => s.units))
                    .Append(" dealerRows=").Append(fixture.inventory.dealer.Count).Append(" vehicleOffers=").Append(fixture.vehicleStock.listings.Count)
                    .Append(" runHistory=").Append(fixture.careerRuns.Count).Append(" checkpointHistory=").Append(fixture.runCheckpoints.Count)
                    .Append(" careerWins=").Append(fixture.careerWins.Count).Append(" usedCarHistory=").Append(fixture.cars.Sum(c => c.usedHistory?.Count ?? 0)).AppendLine();
                var carId = fixture.SelectedCar.instanceId;
                RunAction(csv, ref sampleNumber, fixture, cars, spares, profileHash, stockHash, "fluid-quote-and-pay", p => {
                    var policy = VehicleListings.Costs; var values = VehicleListings.Values;
                    var request = new PaymentRequest { kind = PaymentKind.Fuel, carId = carId, litres = 3 };
                    var quote = PaidEconomy.Quote(p, catalog, policy, values, request, Clock + 100);
                    PaidEconomy.Pay(p, catalog, policy, values, quote, Clock + 101);
                });
                var cartLines = new[] { new PartCartLine { definitionId = def.id, quantity = 2, action = CartAction.NewSet } };
                RunAction(csv, ref sampleNumber, fixture, cars, spares, profileHash, stockHash, "cart-quote-stock-express", p => {
                    PartCart.Total(p, carId, cartLines, ValuationPolicy.Defaults(), Clock + 100);
                    if (PartCart.DeliveryFee(p, carId, cartLines) <= 0)
                        throw new InvalidOperationException("Pinned cart workload did not produce an express fee.");
                }, false, false);
                RunAction(csv, ref sampleNumber, fixture, cars, spares, profileHash, stockHash, "cart-quote-and-checkout", p => {
                    var quote = PartCart.Quote(p, carId, cartLines, Clock + 100, 300);
                    PartCart.Checkout(p, quote, Clock + 101);
                });
                var spare = fixture.parts.FirstOrDefault(i => i.location == PartLocation.Inventory);
                if (spare != null)
                {
                    RunAction(csv, ref sampleNumber, fixture, cars, spares, profileHash, stockHash, "inventory-read", p => SharedInventory.Entries(p, InventoryKind.Storage, SharedInventory.Garage).Count(), false, false);
                    RunAction(csv, ref sampleNumber, fixture, cars, spares, profileHash, stockHash, "inventory-move", p => SharedInventory.Move(p, spare.instanceId, InventoryKind.Storage, SharedInventory.Storage));
                }
                RunAction(csv, ref sampleNumber, fixture, cars, spares, profileHash, stockHash, "transaction-clone-normalize-noop", p => { });
                RunAction(csv, ref sampleNumber, fixture, cars, spares, profileHash, stockHash, "failed-save-rollback-parity", p => { p.cash--; }, true);
            }
            File.WriteAllText(Path.Combine(Root, "baseline.csv"), csv.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Root, "manifest.txt"), manifest.ToString(), new UTF8Encoding(false));
            var summary = new StringBuilder("action,cars,looseParts,n,medianMs,maxMs\n");
            foreach (var group in Samples.GroupBy(s => s.action + "," + s.cars + "," + s.spares).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var times = group.Select(s => s.milliseconds).OrderBy(v => v).ToArray();
                double median = times.Length % 2 == 0 ? (times[times.Length / 2 - 1] + times[times.Length / 2]) / 2 : times[times.Length / 2];
                summary.Append(group.Key).Append(',').Append(times.Length).Append(',')
                    .Append(median.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(times[times.Length - 1].ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine();
            }
            File.WriteAllText(Path.Combine(Root, "summary.csv"), summary.ToString(), new UTF8Encoding(false));
            UnityEngine.Debug.Log("S00 baseline written to " + Root + "\n" + manifest);
        }
    }
#endif
}
