namespace DragLab.Domain
{
    public static class StartingPreferences
    {
        public static DriverSettings Create(DrivingPreset driving=DrivingPreset.Sequential,SimulationDifficulty simulation=SimulationDifficulty.Simulation,int market=1)
        {
            var result=new DriverSettings();
            result.ApplyPreset(driving);
            result.ApplySimulationPreset(simulation);
            // Market is deliberately chosen last, independently from simulation difficulty.
            result.guaranteedFittingParts=market==0?10:market==2?0:5;
            return result;
        }
    }
}
