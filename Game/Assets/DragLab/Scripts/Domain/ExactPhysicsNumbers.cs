using System;
using System.IO;
using System.Linq;
using System.Reflection;
namespace DragLab.Domain {
 // Unity JSON decimal decoding can change doubles by one ULP. Checkpoint bits
 // preserve deterministic continuation; readable decimal fields remain in JSON.
 public static class ExactPhysicsNumbers {
  static FieldInfo[] Fields(Type t){return t.GetFields(BindingFlags.Instance|BindingFlags.Public).Where(f=>f.FieldType==typeof(double)).OrderBy(f=>f.Name,StringComparer.Ordinal).ToArray();}
  static void Visit(CarDynamicsCheckpoint c,Action<object,FieldInfo> scalar,Action<double[],int> array){
   if(c.state==null||c.state.contact==null||c.state.contact.Validate()!=null)throw new ArgumentException("Invalid exact checkpoint shape.");
   foreach(var obj in new object[]{c,c.state,c.state.contact,c.state.fluids})foreach(var field in Fields(obj.GetType()))scalar(obj,field);
   foreach(var values in new[]{c.state.cylinderContribution,c.state.cylinderFuelDelivery,c.state.contact.omega,c.state.contact.angle,c.state.contact.slip,c.state.contact.force,c.state.contact.slipWorkJ,c.state.contact.driveTorqueNm,c.state.contact.brakeTorqueNm,c.state.contact.contactTorqueNm})for(int i=0;i<values.Length;i++)array(values,i);
   if(c.components!=null)foreach(var part in c.components){if(part==null||part.condition==null)throw new ArgumentException("Invalid checkpoint component.");foreach(var field in Fields(part.condition.GetType()))scalar(part.condition,field);if(part.condition.clutch!=null)foreach(var field in Fields(part.condition.clutch.GetType()))scalar(part.condition.clutch,field);if(part.condition.elements!=null)foreach(var element in part.condition.elements)foreach(var field in Fields(element.GetType()))scalar(element,field);if(part.condition.tires!=null)foreach(var tire in part.condition.tires){if(tire==null)throw new ArgumentException("Invalid tire checkpoint.");foreach(var field in Fields(typeof(TireCondition)))scalar(tire,field);}}
  }
  public static string Encode(CarDynamicsCheckpoint c){using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes)){Visit(c,(obj,f)=>writer.Write((double)f.GetValue(obj)),(a,i)=>writer.Write(a[i]));return Convert.ToBase64String(bytes.ToArray());}}
  public static void Decode(CarDynamicsCheckpoint c){if(string.IsNullOrEmpty(c.exactNumbers))return;try{using(var bytes=new MemoryStream(Convert.FromBase64String(c.exactNumbers)))using(var reader=new BinaryReader(bytes)){Visit(c,(obj,f)=>f.SetValue(obj,reader.ReadDouble()),(a,i)=>a[i]=reader.ReadDouble());if(bytes.Position!=bytes.Length)throw new ArgumentException("Invalid exact checkpoint length.");}}catch(FormatException){throw new ArgumentException("Invalid exact checkpoint encoding.");}catch(EndOfStreamException){throw new ArgumentException("Truncated exact checkpoint.");}}
 }
}
