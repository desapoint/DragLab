using DragLab.Domain;
namespace DragLab.Editor {
 // A test assigning body speed must also supply its independent wheel momentum.
 // Production code never reseeds wheel speed from the road each tick.
 public static class RoadFixture {
  public static void Seed(CarSimulation car,double speed,int gear=-1,bool matchEngine=false){var s=car.state;s.speed=s.contact.speed=speed;s.contact.distance=s.distance;for(int i=0;i<4;i++)s.contact.omega[i]=speed/car.wheels.radii[i];s.wheelContactInitialized=true;if(gear>0)s.gear=gear;s.drivelineWheelRpm=car.DrivenWheelOmega*FlywheelDynamics.RpmPerRadian;AxleDriveDynamics.SyncConnected(car);if(matchEngine)s.rpm=car.GearboxInputRpm;}
 }
}
