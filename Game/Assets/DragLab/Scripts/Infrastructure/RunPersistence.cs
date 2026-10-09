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
            return new RunCheckpoint{runId=runId,ownedVehicleId=ownerId,tick=tick,hasFuel=true,fuel=car.FuelSnapshot(),fluids=car.state.fluids.Copy(),components=car.mechanics.parts.Select(p=>p.Copy()).ToList(),settled=closed,closedReason=reason};
        }
        public static bool ConditionAdvances(ComponentCondition a,ComponentCondition b)
        {
            if(b.health>a.health || a.failed&&!b.failed || HistoryLess(b.runningSeconds,a.runningSeconds) || HistoryLess(b.distanceM,a.distanceM) || HistoryLess(b.stressExposure,a.stressExposure) || HistoryLess(b.slipEnergyJ,a.slipEnergyJ) || b.serviceCount!=a.serviceCount || b.reconditionCount!=a.reconditionCount || b.clutchOilLitres>a.clutchOilLitres+1e-9 || b.clutchOilCondition>a.clutchOilCondition+1e-9 || HistoryLess(b.leakSeverity,a.leakSeverity) || HistoryLess(b.adjustmentError,a.adjustmentError))return false;
            if(a.tires!=null&&a.tires.Length>0){if(b.tires==null||a.tires.Length!=b.tires.Length)return false;for(int i=0;i<a.tires.Length;i++){var x=a.tires[i];var y=b.tires[i];if(y.health>x.health || x.failed&&!y.failed || HistoryLess(y.distanceM,x.distanceM) || HistoryLess(y.slipEnergyJ,x.slipEnergyJ) || HistoryLess(y.consumedLife,x.consumedLife))return false;}}
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
            foreach(var run in profile.runCheckpoints.Where(r=>!r.settled)){run.settled=true;run.closedReason="Interrupted / no race resumed or reward added";changed=true;}
            if(changed)profile.revision++;return changed;
        }
    }
}
