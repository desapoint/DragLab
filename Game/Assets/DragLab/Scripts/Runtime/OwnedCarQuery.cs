using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal enum OwnedCarSort { OwnedOrder, Name, Rank, PP, HP, Torque, Weight, Condition, Drivetrain, Type, Transmission }
    internal sealed class OwnedCarEntry
    {
        internal string Id, Name, Model, Drive, Type, Gearbox, Induction, Status, StockCondition;
        internal int Order;
        internal double? Hp, Torque, Weight, Condition;
        internal BenchmarkResult Rating;
        internal bool RatingPending;
        internal bool LockedCenter;
        internal bool? Affordable;
        internal string DriveLabel=>LockedCenter?"4×4":Drive;
        internal double? Metric(int i)=>i==0?(Rating==null?(double?)null:Rating.points):i==1?Hp:i==2?Torque:i==3?Weight:Condition;
        internal static string GearboxName(TransmissionSpec transmission)=>transmission.type==TransmissionType.Manual?transmission.manualLayout==ManualGearboxLayout.Sequential?"Sequential":"Manual":transmission.type.ToString();
    }
    internal sealed class OwnedCarQuery
    {
        internal string Search="", Rank="All", Drive="All", Type="All", Gearbox="All", Induction="All", Status="All", Affordability="All",StockCondition="All";
        internal double?[] Minimum=new double?[5],Maximum=new double?[5]; // PP, HP, Nm, kg, %.
        internal OwnedCarSort Sort;
        internal bool Descending;
        internal OwnedCarQuery Copy()=>new OwnedCarQuery {Search=Search,Rank=Rank,Drive=Drive,Type=Type,Gearbox=Gearbox,Induction=Induction,Status=Status,Affordability=Affordability,StockCondition=StockCondition,Minimum=(double?[])Minimum.Clone(),Maximum=(double?[])Maximum.Clone(),Sort=Sort,Descending=Descending};
        internal int FilterCount=>new[] {Rank,Drive,Type,Gearbox,Induction,Status,Affordability,StockCondition}.Count(s=>s!="All")+Enumerable.Range(0,5).Count(i=>Minimum[i].HasValue||Maximum[i].HasValue);
        static bool Choice(string filter,string value)=>filter=="All"||filter==value;
        internal bool Matches(OwnedCarEntry car)
        {
            if(!Choice(StockCondition,car.StockCondition)||!Choice(Rank,car.Rating?.performanceClass)||!Choice(Type,car.Type)||!Choice(Gearbox,car.Gearbox)||!Choice(Induction,car.Induction)||!Choice(Status,car.Status))return false;
            if(Affordability!="All"&&(!car.Affordable.HasValue||car.Affordable.Value!=(Affordability=="Affordable")))return false;
            if(Drive!="All"&&(Drive=="4×4"?!car.LockedCenter:Drive!=car.Drive))return false;
            string haystack=string.Join(" ",car.Name,car.Model,car.Id,car.Type,car.Drive,car.LockedCenter?"4x4 4×4 4WD":"",car.Gearbox,car.Induction);
            foreach(string term in (Search??"").Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
                if(CultureInfo.CurrentCulture.CompareInfo.IndexOf(haystack,term,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)<0)return false;
            for(int i=0;i<5;i++)if(Minimum[i].HasValue||Maximum[i].HasValue) {
                var value=car.Metric(i);if(!value.HasValue||Minimum[i].HasValue&&value<Minimum[i]||Maximum[i].HasValue&&value>Maximum[i])return false;
            }
            return true;
        }
        double? SortNumber(OwnedCarEntry car)=>Sort==OwnedCarSort.Rank?car.Rating==null?(double?)null:"FEDCBAS".IndexOf(car.Rating.performanceClass):Sort==OwnedCarSort.PP?car.Metric(0):Sort==OwnedCarSort.HP?car.Hp:Sort==OwnedCarSort.Torque?car.Torque:Sort==OwnedCarSort.Weight?car.Weight:Sort==OwnedCarSort.Condition?car.Condition:null;
        internal int Compare(OwnedCarEntry a,OwnedCarEntry b)
        {
            int result;
            if(Sort==OwnedCarSort.OwnedOrder)return Descending?b.Order.CompareTo(a.Order):a.Order.CompareTo(b.Order);
            if(Sort>=OwnedCarSort.Rank&&Sort<=OwnedCarSort.Condition) {
                var av=SortNumber(a);var bv=SortNumber(b);
                if(!av.HasValue||!bv.HasValue)return av.HasValue?-1:bv.HasValue?1:a.Order.CompareTo(b.Order);
                result=av.Value.CompareTo(bv.Value);
            } else {
                string av=Sort==OwnedCarSort.Name?a.Name:Sort==OwnedCarSort.Drivetrain?a.DriveLabel:Sort==OwnedCarSort.Type?a.Type:a.Gearbox;
                string bv=Sort==OwnedCarSort.Name?b.Name:Sort==OwnedCarSort.Drivetrain?b.DriveLabel:Sort==OwnedCarSort.Type?b.Type:b.Gearbox;
                result=CultureInfo.CurrentCulture.CompareInfo.Compare(av,bv,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace);
            }
            return result==0?a.Order.CompareTo(b.Order):Descending?-result:result;
        }
    }
}
