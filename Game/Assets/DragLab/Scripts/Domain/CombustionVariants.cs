using System;
namespace DragLab.Domain.Mechanics
{
    public static class CombustionVariants
    {
        public static void Add(MechanicalCatalog c, VehicleSpec s)
        {
            foreach(var crown in new[]{PistonCrown.Flat,PistonCrown.Dished,PistonCrown.Domed}) {
                if(CombustionHardware.Diesel(s))continue;
                var p=c.Find(c.Id(s.id,PartCategory.Pistons,1)).Copy();
                p.id=s.id+"/Pistons/"+crown;p.name=crown+" forged piston set";p.pistonCrown=crown;
                p.crownVolumeCc=crown==PistonCrown.Dished?12:crown==PistonCrown.Domed?-8:0;
                p.valveReliefMm=crown==PistonCrown.Domed?1.5f:3.5f;p.price=crown==PistonCrown.Flat?720:840;
                p.description="Matched bore and crown-volume set. Dish volume lowers compression and boost knock risk; a dome raises compression and fuel requirement. Valve relief depth also changes cam clearance. Strength alone adds no HP.";c.parts.Add(p);
            }
            if(CombustionHardware.Diesel(s))foreach(float bowl in new[]{8f,18f}){var p=c.Find(c.Id(s.id,PartCategory.Pistons,1)).Copy();p.id=s.id+"/Pistons/Bowl"+bowl;p.name=bowl<12?"high-compression forged diesel bowl set":"deep-bowl forged diesel piston set";p.pistonCrown=PistonCrown.DieselBowl;p.crownVolumeCc=bowl;p.price=1100;p.description="Diesel combustion bowl geometry. Bowl volume changes compression, efficiency and cold combustion; requires this diesel bore/family. Not a gasoline piston conversion.";c.parts.Add(p);}
            if(s.engineBehavior.fueling==FuelSystem.Carburetor) {
                var cam=c.Find(c.Id(s.id,PartCategory.Camshaft,1)).Copy();cam.id=s.id+"/Camshaft/RollerStreet";cam.name="roller-conversion street camshaft";cam.followerInterface="roller";cam.price=1000;c.parts.Add(cam);
                var follower=c.Find(c.Id(s.id,PartCategory.Followers,1)).Copy();follower.id=s.id+"/Followers/RollerConversion";follower.name="roller-conversion follower set";follower.followerInterface="roller";follower.price=650;c.parts.Add(follower);
            }
        }
    }
}
