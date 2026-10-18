using System;
using System.Runtime.InteropServices;
namespace DragLab.Domain.Mechanics
{
    // Keep unrepresented wear in the existing persisted double remainder.
    // Quantize only the display/condition float, without discarding small losses
    // or charging a whole float step before that much life has been consumed.
    public static class ComponentWear
    {
        [StructLayout(LayoutKind.Explicit)]
        struct FloatBits{[FieldOffset(0)]public float number;[FieldOffset(0)]public int bits;}
        static void Subtract(ref float health,ref double remainder,double loss)
        {
            if(!MechanicsSchema.Finite(loss)||loss<0)throw new ArgumentOutOfRangeException(nameof(loss));
            if(loss==0||health<=0)return;
            double pending=remainder+loss;
            if(pending>=health){health=0;remainder=0;return;}
            float next=(float)(health-pending);
            if((double)health-next>pending){var value=new FloatBits{number=next};value.bits++;next=value.number;}
            remainder=Math.Max(0,pending-((double)health-next));health=next;
        }
        public static void Apply(ComponentCondition condition,double loss)=>Subtract(ref condition.health,ref condition.serviceWearRemainder,loss);
        public static void Apply(EngineElement element,double loss)=>Subtract(ref element.health,ref element.wearRemainder,loss);
    }
}
