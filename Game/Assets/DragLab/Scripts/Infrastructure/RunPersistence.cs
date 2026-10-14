using System;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Infrastructure
{
    public static class RunPersistence
    {
        public const int PeriodTicks=120;
        public static RunCheckpoint Capture(string runId,string ownerId,long tick,CarSimulation car,bool closed=false,string reason="")
        {
            return new RunCheckpoint{runId=runId,ownedVehicleId=ownerId,tick=tick,thermalUtcTicks=DateTime.UtcNow.Ticks,hasFuel=true,fuel=car.FuelSnapshot(),fluids=car.state.fluids.Copy(),components=car.mechanics.parts.Select(p=>p.Copy()).ToList(),settled=closed,closedReason=reason};
        }
        public static bool ConditionAdvances(ComponentCondition a,ComponentCondition b)
        {
            // The containing tire part does not own mileage, slip energy, wear, or
            // failure history. Garage tire replacements are transactions between
            // races; an active race journal must retain the same four tire IDs.
            bool tires=a.tires!=null&&a.tires.Length==4;
            if(tires) {
                if(b.tires==null||b.tires.Length!=4||b.serviceCount<a.serviceCount)return false;
                for(int i=0;i<4;i++) {
                    var previous=a.tires[i];var current=b.tires[i];
                    if(previous==null||current==null||previous.id!=current.id
                       ||current.health>previous.health||previous.failed&&!current.failed
                       ||HistoryLess(current.distanceM,previous.distanceM)
                       ||HistoryLess(current.slipEnergyJ,previous.slipEnergyJ)
                       ||HistoryLess(current.consumedLife,previous.consumedLife))return false;
                }
                return true;
            }
            if(b.health>a.health || a.failed&&!b.failed || HistoryLess(b.runningSeconds,a.runningSeconds) || HistoryLess(b.distanceM,a.distanceM) || HistoryLess(b.stressExposure,a.stressExposure) || HistoryLess(b.slipEnergyJ,a.slipEnergyJ) || b.serviceCount!=a.serviceCount || b.reconditionCount!=a.reconditionCount || b.clutchOilLitres>a.clutchOilLitres+1e-9 || b.clutchOilCondition>a.clutchOilCondition+1e-9 || HistoryLess(b.leakSeverity,a.leakSeverity) || HistoryLess(b.adjustmentError,a.adjustmentError))return false;
            return true;
        }
        // Unity JSON can round an unchanged accumulated double by one ULP. This tolerance
        // is for history serialization only; it never permits health/failure repair.
        public static bool HistoryLess(double next,double old){return next<old-Math.Max(1e-10,Math.Abs(old)*1e-12);}
        public static int FailureCount(CarSimulation car)
        {return car.mechanics.parts.Sum(p=>(p.condition.failed?1:0)+(p.condition.elements==null?0:p.condition.elements.Count(e=>e.Failed)))+(car.state.engineFailed?1:0)+(car.state.drivetrainFailed?1:0);}
        public static bool RecoverInterrupted(PlayerProfile profile)
        {
            bool changed=false;
            foreach(var run in profile.runCheckpoints.Where(r=>!r.settled)){run.settled=true;run.closedReason="Interrupted / no race resumed or reward added";CareerProgression.Interrupted(profile,run.runId);changed=true;}
            if(changed)profile.revision++;return changed;
        }
    }
}
