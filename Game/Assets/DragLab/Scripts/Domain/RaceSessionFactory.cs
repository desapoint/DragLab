namespace DragLab.Domain
{
    // Domain construction boundary shared by the player and future owned builds.
    public static class RaceSessionFactory
    {
        public static RaceSimulation Create(VehicleSpec player, Tune playerTune, DriverSettings playerDriver,
            VehicleSpec opponent, Tune opponentTune, DriverSettings opponentDriver, double distance, System.Collections.Generic.IEnumerable<Mechanics.PartInstance> playerComponents = null)
        {
            return new RaceSimulation(new CarSimulation(player.Copy(), playerTune, playerDriver, playerComponents),
                new CarSimulation(opponent.Copy(), opponentTune, opponentDriver), distance);
        }
    }
}
