using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        DriverSettings draftDriver, baselineDriver;
        UnitPreferences draftUnits, baselineUnits;
        float draftMaster, draftOpponent, baselineMaster, baselineOpponent;
        int draftAi, baselineAi;
        bool conflicted, draftReverseTouchShifting, baselineReverseTouchShifting;
        float nextPreferenceCheck;
        readonly List<SwitchSurface> switchSurfaces = new List<SwitchSurface>();
        readonly List<Action> updateControls = new List<Action>();
        readonly List<VisualElement> pages = new List<VisualElement>();
        readonly Dictionary<string, string> settingLabels = new Dictionary<string, string>();
        readonly List<RacingSurface> dialogSurfaces = new List<RacingSurface>();
        Action dismissDialog;
        string dialogRoute;
        VisualElement returnFocus;
        internal bool ModalOpen => root != null && !root.Q("modalLayer").ClassListContains("hidden");
        internal bool EditingText
        {
            get { if (!Visible) return false; for (var item = root.focusController.focusedElement as VisualElement; item != null; item = item.parent) if (item is FloatField || item is TextField) return true; return false; }
        }
        internal bool SettingsDirty => draftDriver != null && Changes().Count > 0;
        internal DriverSettings DraftDriver => draftDriver;

        void InitializeSettings()
        {
            AddSurface(root.Q("settingsBody"), RacingSurface.Kind.Panel);
            AddSurface(root.Q("modalCard"), RacingSurface.Kind.Panel);
            root.Query<ScrollView>().ForEach(scroll =>
            {
                var scroller = scroll.verticalScroller;
                scroller.AddToClassList("settings-scrollbar"); scroller.slider.AddToClassList("settings-scrollbar-slider");
                scroller.lowButton.AddToClassList("scrollbar-arrow"); scroller.highButton.AddToClassList("scrollbar-arrow");
            });
            foreach (string id in new[] { "revertSettings", "cancelSettings", "applySettings" })
                AddSurface(root.Q(id), id == "applySettings" ? RacingSurface.Kind.Action : RacingSurface.Kind.SecondaryAction);
            root.Q<Button>("revertSettings").clicked += () => { ReadApplied(); UpdateControls(); };
            root.Q<Button>("cancelSettings").clicked += () => { ReadApplied(); game.QueueNativeNavigation(game.SettingsReturnRoute); };
            root.Q<Button>("applySettings").clicked += () => ConfirmApply(null);
            root.Q<Button>("modalClose").clicked += CloseDialog;
            for (int i = 0; i < categories.Length; i++)
            { var page = new VisualElement { name = "page" + categoryIds[i] }; page.AddToClassList("settings-page"); pages.Add(page); root.Q<ScrollView>("settingsScroll").Add(page); }
            BuildDriving(); BuildSound(); BuildUnits(); BuildControls(); BuildGeneral();
        }
        void ReadApplied()
        {
            baselineDriver = game.settings.Copy(); draftDriver = baselineDriver.Copy();
            baselineUnits = JsonUtility.FromJson<UnitPreferences>(JsonUtility.ToJson(SharedUnitSettings.Current));
            draftUnits = JsonUtility.FromJson<UnitPreferences>(JsonUtility.ToJson(baselineUnits));
            baselineMaster = draftMaster = SharedAudioSettings.MasterVolume;
            baselineOpponent = draftOpponent = SharedAudioSettings.OpponentVolume;
            baselineReverseTouchShifting=draftReverseTouchShifting=DrivingPreferences.ReverseTouchShifting;
            baselineAi = draftAi = game.Profile.difficulty; conflicted = false;
        }
        void SyncSettingsEntry(string route)
        {
            if (ModalOpen && route != dialogRoute) CloseDialog();
            if (route != "Settings") { draftDriver = null; return; }
            if (draftDriver == null) { ReadApplied(); UpdateControls(); }
            if (Time.realtimeSinceStartup < nextPreferenceCheck) return;
            nextPreferenceCheck = Time.realtimeSinceStartup + .25f;
            ReconcileApplied();
        }
        void ReconcileApplied()
        {
            if (draftDriver == null) return;
            bool changed = false;
            foreach (var field in typeof(DriverSettings).GetFields()) changed |= MergeField(field, baselineDriver, draftDriver, game.settings);
            foreach (var field in typeof(UnitPreferences).GetFields()) changed |= MergeField(field, baselineUnits, draftUnits, SharedUnitSettings.Current);
            changed |= MergeValue(ref baselineMaster, ref draftMaster, SharedAudioSettings.MasterVolume);
            changed |= MergeValue(ref baselineOpponent, ref draftOpponent, SharedAudioSettings.OpponentVolume);
            changed |= MergeValue(ref baselineAi, ref draftAi, game.Profile.difficulty);
            changed |= MergeValue(ref baselineReverseTouchShifting, ref draftReverseTouchShifting, DrivingPreferences.ReverseTouchShifting);
            if (changed) UpdateControls();
        }
        bool MergeField(FieldInfo field, object baseline, object draft, object current)
        {
            object old = field.GetValue(baseline), applied = field.GetValue(current), proposed = field.GetValue(draft);
            if (Equals(old, applied)) return false;
            if (Equals(old, proposed)) field.SetValue(draft, applied);
            else if (!Equals(proposed, applied)) conflicted = true;
            field.SetValue(baseline, applied); return true;
        }
        bool MergeValue<T>(ref T baseline, ref T draft, T current)
        {
            if (Equals(baseline, current)) return false;
            if (Equals(baseline, draft)) draft = current; else if (!Equals(draft, current)) conflicted = true;
            baseline = current; return true;
        }
        void UpdateControls()
        {
            if (draftDriver == null) return;
            foreach (var update in updateControls) update();
            root.Q("raceTemperaturesChoices").SetEnabled(!draftDriver.optimalThermals);
            root.Q<Label>("settingsStatus").text = conflicted ? "Applied settings changed. Revert to reload." : SettingsDirty ? "Unsaved changes" : "";
            root.Q("applySettings").SetEnabled(SettingsDirty && !conflicted);
            root.Q("revertSettings").SetEnabled(SettingsDirty || conflicted);
        }
        void Changed(bool simulation = false)
        { if (simulation) draftDriver.simulationDifficulty = SimulationDifficulty.Custom; UpdateControls(); }
        void ShowCategory()
        { for (int i = 0; i < pages.Count; i++) pages[i].EnableInClassList("hidden", i != category); }
        void Navigate(string route)
        {
            if (ModalOpen) return;
            if (game.ScreenName == "Settings" && route != "Settings" && SettingsDirty)
            {
                OpenDialog("Unsaved settings", "Apply changes before leaving?", new[] {
                    new DialogAction("Stay", CloseDialog),
                    new DialogAction("Discard", () => { CloseDialog(); ReadApplied(); game.QueueNativeNavigation(route,true); }),
                    new DialogAction("Apply…", () => { CloseDialog(); ConfirmApply(route); }, true) });
                return;
            }
            game.QueueNativeNavigation(route,true);
        }
        internal bool GuardLegacyNavigation(string route)
        {
            if (game.ScreenName != "Settings" || route == "Settings" || !SettingsDirty) return false;
            InterfaceComparison.SetReplacement("Settings", true); Navigate(route); return true;
        }
        List<string> Changes()
        {
            var changes = new List<string>();
            if (draftDriver == null) return changes;
            foreach (var field in typeof(DriverSettings).GetFields())
                if (!Equals(field.GetValue(baselineDriver), field.GetValue(draftDriver))) changes.Add((settingLabels.TryGetValue(field.Name, out var label) ? label : field.Name) + ": " + (field.Name=="optimalThermals"?Display(!baselineDriver.optimalThermals):Display(field.GetValue(baselineDriver))) + " → " + (field.Name=="optimalThermals"?Display(!draftDriver.optimalThermals):Display(field.GetValue(draftDriver))));
            if (baselineAi != draftAi) changes.Add("Opponent: " + new[] { "Easy", "Normal", "Hard" }[baselineAi] + " → " + new[] { "Easy", "Normal", "Hard" }[draftAi]);
            if(baselineReverseTouchShifting!=draftReverseTouchShifting)changes.Add("Touch shifting: "+(baselineReverseTouchShifting?"Down":"Up")+" to upshift → "+(draftReverseTouchShifting?"Down":"Up")+" to upshift");
            if (baselineMaster != draftMaster) changes.Add("Master: " + baselineMaster.ToString("P0") + " → " + draftMaster.ToString("P0"));
            if (baselineOpponent != draftOpponent) changes.Add("Opponent volume: " + baselineOpponent.ToString("P0") + " → " + draftOpponent.ToString("P0"));
            foreach (var field in typeof(UnitPreferences).GetFields()) if (!Equals(field.GetValue(baselineUnits), field.GetValue(draftUnits))) changes.Add(field.Name + ": " + Display(field.GetValue(baselineUnits)) + " → " + Display(field.GetValue(draftUnits)));
            return changes;
        }
        static string Display(object value)
        {
            if (value is bool flag) return flag ? "On" : "Off";
            if (value is float number) return number.ToString("0.##");
            if (value is WearMode wear) return wear == WearMode.Partial ? "25%" : wear == WearMode.None ? "None" : "Full";
            if (value is FuelUsageMode fuel) return fuel == FuelUsageMode.Reduced ? "25%" : fuel == FuelUsageMode.None ? "None" : "Realistic";
            if (value is ConsumptionMode consumption) return consumption == ConsumptionMode.Reduced ? "25%" : consumption == ConsumptionMode.None ? "None" : "Realistic";
            if (value is ConditionEffectMode condition) return condition == ConditionEffectMode.Reduced ? "25%" : condition == ConditionEffectMode.Assisted ? "Assisted" : "Full";
            return System.Text.RegularExpressions.Regex.Replace(value.ToString(), "([a-z])([A-Z])", "$1 $2");
        }
        void ConfirmApply(string destination)
        {
            ReconcileApplied();
            if (!SettingsDirty || conflicted) return;
            string summary = string.Join("\n", Changes());
            OpenDialog("Apply settings?", summary, new[] { new DialogAction("Cancel", CloseDialog), new DialogAction("Apply", () => {
                ReconcileApplied();
                if (conflicted) { CloseDialog(); UpdateControls(); return; }
                if (summary != string.Join("\n", Changes())) { CloseDialog(); ConfirmApply(destination); return; }
                if (!game.ApplyNativeSettings(draftDriver, draftAi, draftUnits, draftMaster, draftOpponent, out var error, draftReverseTouchShifting))
                { root.Q<ScrollView>("modalContent").Add(new Label(error) { name = "settingsError" }); return; }
                CloseDialog(); ReadApplied(); UpdateControls(); if (destination != null) game.QueueNativeNavigation(destination,true);
            }, true) });
        }
        sealed class DialogAction
        {
            internal string label,id; internal Action action; internal bool primary;
            internal DialogAction(string label, Action action, bool primary = false,string id=null) { this.label = label; this.action = action; this.primary = primary; this.id=id; }
        }
        void OpenDialog(string title, string text, DialogAction[] actions, Action dismiss = null, Action<VisualElement> populate = null)
        {
            root.Q("partPurchaseReview")?.RemoveFromHierarchy();
            returnFocus = root.focusController.focusedElement as VisualElement;
            dismissDialog = dismiss;
            dialogRoute = game.ScreenName;
            root.Q<Label>("modalTitle").text = title;
            foreach (var surface in dialogSurfaces) surfaces.Remove(surface); dialogSurfaces.Clear();
            var content = root.Q<ScrollView>("modalContent"); content.Clear(); content.scrollOffset = Vector2.zero; content.horizontalScrollerVisibility = ScrollerVisibility.Hidden; if (!string.IsNullOrEmpty(text)) content.Add(new Label(text)); populate?.Invoke(content);
            var buttons = root.Q("modalActions");
            buttons.Clear();
            foreach (var action in actions)
            {
                var button = new Button(action.action) { text = action.label, name = action.id ?? "dialog" + action.label.Replace("…", "").Replace(" ", "") };
                button.AddToClassList("action-button"); if (action.primary) button.AddToClassList("primary-action");
                buttons.Add(button); AddSurface(button, action.primary ? RacingSurface.Kind.Action : RacingSurface.Kind.SecondaryAction); dialogSurfaces.Add(button.Q<RacingSurface>());
            }
            root.Q("modalLayer").RemoveFromClassList("hidden"); root.Q("navBar").SetEnabled(false); root.Q("settingsTabs").SetEnabled(false); root.Q("settingsBody").SetEnabled(false); root.Q("settingsActions").SetEnabled(false);
            root.Q(className:"workspace").SetEnabled(false);
            ExcludeTabTraversal();
            root.Q<Button>("modalClose").Focus();
        }
        void CloseDialog()
        {
            root.Q("partPurchaseReview")?.RemoveFromHierarchy();
            ClearPartCartSwitches();
            var cancel = dismissDialog; dismissDialog = null; cancel?.Invoke();
            root.Q("modalLayer").AddToClassList("hidden");
            dialogRoute = null; root.Q(className:"workspace").SetEnabled(true);
            foreach (string id in new[] { "navBar", "settingsTabs", "settingsBody", "settingsActions" }) root.Q(id).SetEnabled(true);
            UpdateControls(); if(!NativeMobile.Touch)returnFocus?.Focus();
        }
        VisualElement Row(int page, string id, string label, string help = null)
        {
            var row = new VisualElement { name = id + "Row" }; row.AddToClassList("setting-row"); pages[page].Add(row);
            var caption = new VisualElement(); caption.AddToClassList("setting-caption"); row.Add(caption); caption.Add(new Label(label));
            settingLabels[id] = label; if(id=="heatManagement") settingLabels["optimalThermals"]="Heat management";
            if (help != null)
            {
                var info = new Button(() => OpenDialog(label, help, new[] { new DialogAction("Close", CloseDialog) })) { text = "i", tooltip = label, name = id + "Info" }; info.AddToClassList("info-button"); caption.Add(info);
            }
            var controls = new VisualElement { name = id + "Choices" }; controls.AddToClassList("setting-control"); row.Add(controls); return controls;
        }
        void Choice(int page, string id, string label, string[] labels, Func<int> get, Action<int> set, string help = null, bool simulation = false)
        {
            var group = Row(page, id, label, help); group.AddToClassList("choice-group"); var buttons = new List<Button>();
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i; var button = new Button(() => { set(index); Changed(simulation); }) { text = labels[i], name = id + i }; button.AddToClassList("choice-button");
                button.AddToClassList(i==0?"choice-first":i==labels.Length-1?"choice-last":"choice-middle");
                group.Add(button); AddSurface(button, i==0?RacingSurface.Kind.ChoiceFirst:i==labels.Length-1?RacingSurface.Kind.ChoiceLast:RacingSurface.Kind.ChoiceMiddle); buttons.Add(button);
            }
            updateControls.Add(() => { for (int i = 0; i < buttons.Count; i++) buttons[i].EnableInClassList("selected", get() == i); });
        }
        void Switch(int page, string id, string label, Func<bool> get, Action<bool> set, string help = null, bool simulation = false)
        {
            var group = Row(page, id, label, help);
            var toggle = new Toggle { name = id }; toggle.AddToClassList("setting-toggle"); group.Add(toggle);
            var input = toggle.Q(className: "unity-toggle__input"); var surface = new SwitchSurface(toggle); input.Add(surface); switchSurfaces.Add(surface);
            toggle.RegisterValueChangedCallback(evt => { set(evt.newValue); Changed(simulation); });
            updateControls.Add(() => { toggle.SetValueWithoutNotify(get()); surface.SetValue(get()); });
        }
        void Range(int page, string id, string label, float min, float max, Func<float> get, Action<float> set, bool percent, string help = null, string unit = null, bool wholeNumbers = false, bool simulation = false, bool inverted = false)
        {
            var group = Row(page, id, label, help); var slider = new Slider(min, max) { name = id, inverted = inverted }; slider.AddToClassList("setting-slider"); group.Add(slider);
            var fill = new SliderFill(slider); slider.Q(className: "unity-base-slider__tracker").Add(fill);
            var number = new FloatField { name = id + "Value", isDelayed = true }; number.AddToClassList("setting-number"); group.Add(number);
            if (percent) group.Add(new Label("%")); else group.Add(new Label(unit??"s"));
            slider.RegisterValueChangedCallback(evt => { set(wholeNumbers?Mathf.Round(evt.newValue):evt.newValue); Changed(simulation); });
            number.RegisterValueChangedCallback(evt => { float value = evt.newValue / (percent ? 100 : 1); if (float.IsNaN(value) || float.IsInfinity(value)) { UpdateControls(); return; } set(wholeNumbers?Mathf.Round(Mathf.Clamp(value,min,max)):Mathf.Clamp(value, min, max)); Changed(simulation); });
            updateControls.Add(() => { slider.SetValueWithoutNotify(get()); fill.MarkDirtyRepaint(); number.SetValueWithoutNotify(get() * (percent ? 100 : 1)); });
        }
        void BuildDriving()
        {
            Choice(0, "drivingPreset", "Driving preset", new[] { "Automatic", "Shift myself", "Full manual" }, DrivingChoice, i => draftDriver.ApplyPreset((DrivingPreset)i), "Changes shifting, launch and driving assists together. Individual settings below remain editable; Apply is still required.");
            Choice(0, "simulationDifficulty", "Simulation preset", new[] { "Arcade", "Balanced", "Simulation" }, () => draftDriver.simulationDifficulty == SimulationDifficulty.Custom ? -1 : (int)draftDriver.simulationDifficulty, i => draftDriver.ApplySimulationPreset((SimulationDifficulty)i), "Changes simulation rules together. Keeps launch and shift modes; individual changes create a custom setup.");
            var presets = new VisualElement { name = "presetGroup" }; presets.AddToClassList("preset-group");
            presets.Add(new Label("Presets") { name = "presetHeading" });
            presets.Add(root.Q("drivingPresetRow")); presets.Add(root.Q("simulationDifficultyRow")); pages[0].Insert(0,presets);
            var individual = new Label("Individual settings") { name = "individualHeading" }; individual.AddToClassList("settings-group-label"); pages[0].Add(individual);
            Choice(0, "shifting", "Shifting", new[] { "Automatic", "Shift myself", "Manual clutch" }, () => (int)draftDriver.shifting, i => draftDriver.shifting = (ShiftMode)i, "E / Q select gears. Transmission hardware determines shift speed and downshift protection.");
            Choice(0, "launch", "Launch", new[] { "Automatic", "Assisted", "Manual" }, () => (int)draftDriver.launch, i => draftDriver.launch = (LaunchMode)i, "Staging holds neutral and brakes. Hold W / Space to rev. E engages first after green. For manual clutch, hold Shift then release it.");
            Choice(0, "opponentDifficulty", "Opponent", new[] { "Easy", "Normal", "Hard" }, () => draftAi, i => draftAi = i, "Changes opponent reaction time. Both cars use the same physics.");
            Choice(0,"marketAvailability","Market difficulty",new[]{"Easy · 10","Normal · 5","Hard · 0"},()=>draftDriver.guaranteedFittingParts==10?0:draftDriver.guaranteedFittingParts==5?1:draftDriver.guaranteedFittingParts==0?2:-1,i=>draftDriver.guaranteedFittingParts=new[]{10,5,0}[i],"Minimum fitting offers in each new/used parts pool across your entire garage, not per car. Engines stay random. Takes effect at the next stock refresh.",true);
            Range(0,"guaranteedFittingParts","Minimum fitting offers",0,20,()=>draftDriver.guaranteedFittingParts,v=>draftDriver.guaranteedFittingParts=(int)v,false,"Shared across all owned cars. Zero disables fitting guarantees; global used supply remains random. Apply does not reroll current stock.","offers",true,true,true);
            Choice(0, "wearMode", "Mechanical wear", new[] { "None", "Partial · 25%", "Full" }, () => (int)draftDriver.wearMode, i => draftDriver.wearMode = (WearMode)i, "None prevents new wear and abuse damage; it does not repair existing faults. Partial slows wear and abuse damage to 25%; failures remain possible.", true);
            Choice(0, "fuelUsage", "Fuel consumption", new[] { "None", "Reduced · 25%", "Realistic" }, () => 2-(int)draftDriver.fuelUsage, i => draftDriver.fuelUsage = (FuelUsageMode)(2-i), "Fuel quantity still adds weight. Consumption includes idle and anti-lag.", true);
            Choice(0, "fluidDepletion", "Oil / coolant use", new[] { "None", "Reduced · 25%", "Realistic" }, () => 2-(int)draftDriver.fluidDepletion, i => draftDriver.fluidDepletion = (ConsumptionMode)(2-i), "Changes depletion, including leaks. Existing faults remain.", true);
            Choice(0, "fluidAging", "Oil aging", new[] { "None", "Reduced · 25%", "Realistic" }, () => 2-(int)draftDriver.fluidAging, i => draftDriver.fluidAging = (ConsumptionMode)(2-i), null, true);
            Choice(0, "conditionEffects", "Condition effects", new[] { "Assisted", "Reduced · 25%", "Full" }, () => 2-(int)draftDriver.conditionEffects, i => draftDriver.conditionEffects = (ConditionEffectMode)(2-i), "Assisted disables gradual performance loss; it does not repair failed components.", true);
            Choice(0, "serviceAssist", "Fluid service", new[] { "Auto top-up", "Manual" }, () => 1-(int)draftDriver.serviceAssist, i => draftDriver.serviceAssist = (ServiceAssist)(1-i), "Top-up before race keeps faults and wear; paid servicing follows the existing economy rules.", true);
            Switch(0, "heatManagement", "Heat management", () => !draftDriver.optimalThermals, v => draftDriver.optimalThermals = !v, "Off keeps temperatures optimal throughout driving. Your race-start temperature preference is remembered while disabled.", true);
            Choice(0, "raceTemperatures", "Race-start temperature", new[] { "Optimal", "Fully cooled", "Live cooldown" }, () => Array.IndexOf(new[]{RaceTemperatureMode.Optimal,RaceTemperatureMode.FullyCooled,RaceTemperatureMode.Live},draftDriver.raceTemperatures), i => draftDriver.raceTemperatures = new[]{RaceTemperatureMode.Optimal,RaceTemperatureMode.FullyCooled,RaceTemperatureMode.Live}[i], "Live cooldown uses real parked / offline time. Optimal affects race start only; driving still builds heat.", true);
            Switch(0, "warmupBeforeRace", "Pre-race warm-up", () => draftDriver.warmupBeforeRace, v => draftDriver.warmupBeforeRace = v, "Idle or burn out before the lights. Uses your fuel and wear rules.", true);
            Switch(0, "throttleAssist", "Automatic throttle", () => draftDriver.throttleAssist, v => draftDriver.throttleAssist = v);
            Switch(0, "clutchAssist", "Clutch assist", () => draftDriver.clutchAssist, v => draftDriver.clutchAssist = v, null, true);
            Switch(0, "stallPrevention", "Stall prevention", () => draftDriver.stallPrevention, v => draftDriver.stallPrevention = v, null, true);
            Switch(0, "tractionAssist", "Traction control", () => draftDriver.tractionAssist, v => draftDriver.tractionAssist = v, null, true);
            Switch(0, "revMatchAssist", "Rev matching", () => draftDriver.revMatchAssist, v => draftDriver.revMatchAssist = v, null, true);
            Switch(0, "shutdownBrakeAssist", "Finish-line braking", () => draftDriver.shutdownBrakeAssist, v => draftDriver.shutdownBrakeAssist = v, "Gradually brakes after the finish and backs off if a wheel starts locking. Manual braking remains under your control.");
            Range(0, "clutchReleaseSeconds", "Digital clutch release", .02f, .6f, () => draftDriver.clutchReleaseSeconds, v => draftDriver.clutchReleaseSeconds = v, false);
        }
        int DrivingChoice()
        {
            for (int i=0;i<3;i++)
            {
                var choice=draftDriver.Copy(); choice.ApplyPreset((DrivingPreset)i);
                if(choice.shifting==draftDriver.shifting && choice.launch==draftDriver.launch && choice.throttleAssist==draftDriver.throttleAssist && choice.tractionAssist==draftDriver.tractionAssist && choice.revMatchAssist==draftDriver.revMatchAssist) return i;
            }
            return -1;
        }
        void BuildSound()
        {
            Range(1, "masterVolume", "Master volume", 0, 1, () => draftMaster, v => draftMaster = v, true, "Shared by races and both test benches. 0% mutes all sound. Takes effect after Apply.");
            Range(1, "opponentVolume", "Opponent volume", 0, 1, () => draftOpponent, v => draftOpponent = v, true, "Opponent engine and tires also follow master volume and distance attenuation.");
        }
        void BuildUnits()
        {
            Choice(2, "speed", "Speed", new[] { "km/h", "mph", "m/s" }, () => (int)draftUnits.speed, i => draftUnits.speed = (SpeedUnit)i);
            Choice(2, "pressure", "Pressure", new[] { "bar", "psi", "kPa" }, () => (int)draftUnits.pressure, i => draftUnits.pressure = (PressureUnit)i, "Boost includes bar and psi. Tire outer radius, including rubber, remains in inches.");
            Choice(2, "torque", "Torque", new[] { "Nm", "lb-ft" }, () => (int)draftUnits.torque, i => draftUnits.torque = (TorqueUnit)i);
        }
        void BuildControls()
        {
            Choice(3,"touchShiftDirection","Touch shifting",new[]{"Up → upshift","Down → upshift"},()=>draftReverseTouchShifting?1:0,i=>draftReverseTouchShifting=i==1,"Flick vertically on the shifter. The opposite direction shifts down. Apply to save.");
            if(NativeMobile.Touch){
                Row(3,"touchPedals","Pedals").Add(new Label("Hold · slide up to ease off"));
                Row(3,"touchShifts","Shifting").Add(new Label("Flick the shifter ↑ / ↓"));
                Row(3,"touchTuning","Tuning").Add(new Label("− / + precise steps · ↺ reset · tap value to type"));
            }
            foreach (var binding in new[] { new[] { "Throttle", "W / Space" }, new[] { "Brake", "S" }, new[] { "Clutch", "Left Shift" }, new[] { "Shift up / engage first", "E" }, new[] { "Shift down", "Q" }, new[] { "Launch / restart", "Enter" }, new[] { "Pause", "Esc" }, new[] { "Return to garage", "G · paused / finished" }, new[] { "Repeat race", "R · finished" }, new[] { "Compare interface", "F8" } })
            {
                var controls=Row(3,"binding"+binding[0].Replace(" ",""),binding[0]);controls.Add(new Label(binding[1]));
                if(binding[0]=="Compare interface")controls.parent.AddToClassList("desktop-only");
            }
            var pedals = new Button(() => OpenDialog("Pedals", "Hold W / Space through gear changes. Manual clutch: Shift + E / Q, then release Shift.\nMouse / touch pedals accept partial pressure; different fingers can hold separate pedals.", new[] { new DialogAction("Close", CloseDialog) })) { text = "Pedal controls" }; pedals.AddToClassList("action-button"); pages[3].Add(pedals); AddSurface(pedals, RacingSurface.Kind.Button);
        }
        void BuildGeneral()
        {
            var reset = new Button(() => {
                long revision = game.BeginNativeReset();
                OpenDialog("Reset game?", "Clear progression, cars, parts, inventory, tunes and records.\nStart with " + DisplayUnits.Money(4700) + " and choose your first car at the dealership. Applied driving, sound and unit settings stay.", new[] {
                    new DialogAction("Cancel", CloseDialog), new DialogAction("Reset game", () => {
                        if (!game.CommitGameReset(revision)) { CloseDialog(); OpenDialog("Reset failed", game.NativeNotice, new[] { new DialogAction("Close", CloseDialog) }); return; }
                        CloseDialog(); ReconcileApplied(); UpdateControls();
                    }, true) }, game.CancelNativeReset);
            }) { name = "resetGame", text = "Reset game…" };
            reset.AddToClassList("action-button"); reset.AddToClassList("reset-action");
            var controls=Row(4, "gameData", "Game data");controls.AddToClassList("reset-control");controls.Add(reset);AddSurface(reset, RacingSurface.Kind.DangerAction);
        }
    }
}
