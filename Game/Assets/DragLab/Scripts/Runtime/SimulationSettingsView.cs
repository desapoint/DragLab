using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    // The game settings and isolated physical bench edit the same domain rules.
    public static class SimulationSettingsView
    {
        internal static readonly System.Collections.Generic.Dictionary<string, Rect> presetRects = new System.Collections.Generic.Dictionary<string, Rect>();
        internal static Rect fuelUsageRect;
        internal static Rect heatManagementRect, raceTemperaturesRect, warmupRect;
        static ConsumptionMode Consumption(string label, ConsumptionMode value) { return (ConsumptionMode)GUILayout.SelectionGrid((int)value, new[] { "Realistic / 100%", "Reduced / 25%", "None / 0%" }, 3); }
        public static bool Draw(DriverSettings settings, bool bench = false)
        {
            string before = JsonUtility.ToJson(settings);
            GUILayout.Label("SIMULATION DIFFICULTY / " + settings.simulationDifficulty);
            GUILayout.BeginHorizontal();
            foreach (var preset in new[] { SimulationDifficulty.Arcade, SimulationDifficulty.Balanced, SimulationDifficulty.Simulation })
            {
                if (GUILayout.Button(preset.ToString())) settings.ApplySimulationPreset(preset);
                if (Event.current.type == EventType.Repaint) presetRects[preset.ToString()] = RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Presets keep your launch and shift modes. AI difficulty is separate.");
            string afterPreset = JsonUtility.ToJson(settings);
            GUILayout.Label("Mechanical wear rate");
            settings.wearMode = (WearMode)GUILayout.SelectionGrid((int)settings.wearMode, new[] { "None", "Partial / 25%", "Full / 100%" }, 3);
            GUILayout.Label("Fuel usage");
            int fuelChoice = settings.fuelUsage == FuelUsageMode.None ? 0 : settings.fuelUsage == FuelUsageMode.Reduced ? 1 : 2;
            int nextFuel = GUILayout.SelectionGrid(fuelChoice, new[] { "None / 0%", "Reduced / 25%", "Realistic / 100%" }, 3);
            settings.fuelUsage = nextFuel == 0 ? FuelUsageMode.None : nextFuel == 1 ? FuelUsageMode.Reduced : FuelUsageMode.Realistic;
            if (Event.current.type == EventType.Repaint) fuelUsageRect = RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());
            GUILayout.Label("Fuel quantity still determines weight. This setting changes consumption, including idle and anti-lag.");
            GUILayout.Label("Oil / coolant depletion (does not repair leaks)");
            settings.fluidDepletion = Consumption("depletion", settings.fluidDepletion);
            GUILayout.Label("Oil aging / contamination");
            settings.fluidAging = Consumption("aging", settings.fluidAging);
            GUILayout.Label("Condition effects on performance / efficiency");
            settings.conditionEffects = (ConditionEffectMode)GUILayout.SelectionGrid((int)settings.conditionEffects, new[] { "Full", "Reduced / 25%", "Assisted / no gradual loss" }, 3);
            GUILayout.Label("Fluid service assist (retains faults / wear)");
            settings.serviceAssist = (ServiceAssist)GUILayout.SelectionGrid((int)settings.serviceAssist, new[] { "Manual", "Auto top-up before race" }, 2);
            settings.optimalThermals = !GUILayout.Toggle(!settings.optimalThermals, "Heat management (temperatures evolve while driving)");
            if(Event.current.type==EventType.Repaint)heatManagementRect=RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());
            GUILayout.Label("Temperatures at race entry (heat during driving remains separate)");
            bool temperaturesEnabled=GUI.enabled;GUI.enabled=temperaturesEnabled&&!settings.optimalThermals;
            settings.raceTemperatures=(RaceTemperatureMode)GUILayout.SelectionGrid((int)settings.raceTemperatures,new[]{"Fully cooled / ambient","Optimal / ready","Live / elapsed time"},3);
            if(Event.current.type==EventType.Repaint)raceTemperaturesRect=RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());
            GUI.enabled=temperaturesEnabled;
            if(settings.optimalThermals)GUILayout.Label("Heat management OFF: temperatures stay optimal throughout driving; race-entry choices are disabled.");
            settings.warmupBeforeRace=GUILayout.Toggle(settings.warmupBeforeRace,"Optional pre-race warm-up / burnouts before starting lights");
            if(Event.current.type==EventType.Repaint)warmupRect=RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());
            GUILayout.Label("Live retains heat and cools during real parked/offline time. Warm-up consumes fuel and wears parts using your selected rules.");
            settings.clutchAssist = GUILayout.Toggle(settings.clutchAssist, "Assist manual clutch engagement and shifts");
            settings.stallPrevention = GUILayout.Toggle(settings.stallPrevention, "Prevent engine stalls");
            settings.tractionAssist = GUILayout.Toggle(settings.tractionAssist, "Traction control");
            settings.revMatchAssist = GUILayout.Toggle(settings.revMatchAssist, "Rev-match assist");
            if (bench)
            {
                GUILayout.Label("Test wear multiplier: " + settings.wearMultiplier.ToString("F2") + "x / effective " + (settings.WearRate * 100).ToString("F0") + "%");
                settings.wearMultiplier = GUILayout.HorizontalSlider(settings.wearMultiplier, 0, 100);
                GUILayout.BeginHorizontal();
                foreach (float multiplier in new[] { 1f, 10f, 100f }) if (GUILayout.Button(multiplier + "x")) settings.wearMultiplier = multiplier;
                GUILayout.EndHorizontal();
            }
            if (afterPreset != JsonUtility.ToJson(settings)) settings.simulationDifficulty = SimulationDifficulty.Custom;
            GUILayout.Label("None prevents new wear and abuse damage; it does not repair broken or worn parts.");
            GUILayout.Label("Partial slows wear and abuse damage to 25%; failures remain possible. Temperatures are separate.");
            return before != JsonUtility.ToJson(settings);
        }
    }
}
