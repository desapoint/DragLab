from pathlib import Path
p=Path('Game/Assets/DragLab/Scripts/Domain/PhysicalDrivingSession.cs');s=p.read_text(encoding='utf-8-sig').replace('var next = Copy(snapshot); next.inputs.Clear();', 'var next = Copy(snapshot); next.tune = car.tune.Copy(); next.inputs.Clear();');p.write_text(s,encoding='utf-8')
p=Path('Game/Assets/DragLab/Scripts/Runtime/PhysicalBenchMass.cs');s=p.read_text(encoding='utf-8-sig').replace('Session.snapshot.tune = car.tune.Copy(); ', '');p.write_text(s,encoding='utf-8')
p=Path('Game/Assets/DragLab/Scripts/Runtime/CarStatsSummary.cs');s=p.read_text(encoding='utf-8-sig');anchor='            Add(changedSummary, "Cooling / heat transfer"';i=s.index(anchor);s=s[:i]+'''            Add(changedSummary, "Driver mass", "Driver", before.mass.driverKg, after.mass.driverKg, "kg", 0);
            Add(changedSummary, "Ballast", "Ballast / payload", before.mass.payloadKg, after.mass.payloadKg, "kg", 0);
            Add(changedSummary, "Fuel mass", "Current fuel mass", before.mass.fuelKg, after.mass.fuelKg, "kg", 0);
            Add(changedSummary, "CG position", "CG from front axle", before.mass.centerOfGravityFromFrontM, after.mass.centerOfGravityFromFrontM, "m", 0, "F2");
            Add(changedSummary, "CG height", "CG height", before.mass.centerOfGravityHeightM, after.mass.centerOfGravityHeightM, "m", 0, "F2");
'''+s[i:];p.write_text(s,encoding='utf-8')

p=Path('Game/Assets/DragLab/Editor/MassLoadVerification.cs');s=p.read_text(encoding='utf-8-sig');needle='            }\n            Directory.CreateDirectory';insert='''                var snapshot = new RunSnapshot { vehicle = b.spec.Copy(), tune = b.tune.Copy(), driving = new DriverSettings { wearMode = WearMode.None } };
                var run = new PhysicalDrivingSession(snapshot); var commands = PhysicalScenarios.Timeline(PhysicalScenario.FullPullShiftCoastBrake, 123);
                foreach(var command in commands.Take(500)) run.Step(command);
                string original = run.Csv(); double speedBefore = run.car.state.speed, rpmBefore = run.car.state.rpm;
                run.car.SetMassInputs(100, 200, 2, .8, 10); var next = run.ContinueRecording();
                check(next.car.state.speed == speedBefore && next.car.state.rpm == rpmBefore && next.car.mass.payloadKg == 200, "Mass checkpoint preserves movement and captures actual ballast " + v.spec.id);
                check(original == PhysicalDrivingSession.Replay(run.snapshot).Csv(), "Old trace remains repeatable after live mass change " + v.spec.id);
                for(int tick=0; tick<120; tick++) next.Step(new TickInput { tick=tick, input=new DriverInput { throttle=.6 } });
                check(next.Csv() == PhysicalDrivingSession.Replay(next.snapshot).Csv(), "New mass segment replays exactly " + v.spec.id);
                var decoded = PhysicalDrivingSession.Replay(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(next.snapshot)));
                check(Math.Abs(decoded.car.state.distance-next.car.state.distance)<1e-7 && decoded.car.mass.fuelKg == next.car.mass.fuelKg, "JSON mass continuation preserves fuel and distance " + v.spec.id);
                var bodyMap = catalog.Recipe(v.spec.id); bodyMap[PartCategory.Body] = catalog.Id(v.spec.id,PartCategory.Body,1);
                var body = catalog.Resolve(v.spec.id,bodyMap,v.CreateStockTune()); var bodyCar=new CarSimulation(body.spec,body.tune,new DriverSettings());
                check(Math.Abs(m.dryInstalledKg-bodyCar.mass.dryInstalledKg-MassDynamics.ReferenceBodyMass(b.spec)*(1-body.tune.MassMultiplier))<1e-5, "Body upgrade reduces structure only, not fuel or driver " + v.spec.id);
                var interiorMap=catalog.Recipe(v.spec.id); double interiorKg=catalog.Find(interiorMap[PartCategory.Interior]).massKg; interiorMap.Remove(PartCategory.Interior);var stripped=catalog.Resolve(v.spec.id,interiorMap,v.CreateStockTune());
                check(Math.Abs(m.dryInstalledKg-new CarSimulation(stripped.spec,stripped.tune,new DriverSettings()).mass.dryInstalledKg-interiorKg)<1e-5 && stripped.Drivable, "Interior removal subtracts installed hardware once " + v.spec.id);
''';assert needle in s;s=s.replace(needle,insert+needle);p.write_text(s,encoding='utf-8')

p=Path('Game/Assets/DragLab/Editor/MassLoadVerification.cs');s=p.read_text(encoding='utf-8-sig').replace('public static int Run(string root)', 'public static void RunStandalone() { int checks = Run(Path.GetFullPath(Path.Combine(Application.dataPath, "../.."))); Debug.Log("MASS VERIFICATION PASS: " + checks); }\n        public static int Run(string root)');p.write_text(s,encoding='utf-8')
p=Path('tools/Invoke-Unity.ps1');s=p.read_text(encoding='utf-8-sig').replace("'SliceTwoAudit','SceneCheck'", "'SliceTwoAudit','MassVerify','SceneCheck'").replace("    'SceneCheck' {", "    'MassVerify' { 'DragLab.Editor.MassLoadVerification.RunStandalone' }\n    'SceneCheck' {");p.write_text(s,encoding='utf-8')
