using System;
using System.IO;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
namespace DragLab.Infrastructure
{
    internal static class DrivingPreferences
    {
        [Serializable] internal sealed class Data {public int version=1;public DriverSettings driving=new DriverSettings();public int difficulty=1;}
        internal static string FilePath=>Path.Combine(Path.GetDirectoryName(ProfileStore.FilePath),"driving-settings.json");
        internal static string Encode(DriverSettings driving,int difficulty)=>JsonUtility.ToJson(new Data{driving=driving.Copy(),difficulty=difficulty});
        static Data Read(string path)
        {
            if(!File.Exists(path))return new Data();var data=JsonUtility.FromJson<Data>(File.ReadAllText(path,System.Text.Encoding.UTF8));
            if(data==null||data.version!=1||data.driving==null||data.difficulty<0||data.difficulty>2)throw new NotSupportedException("Unsupported or invalid driving preferences; original preserved.");
            foreach(var field in typeof(DriverSettings).GetFields())if(field.FieldType.IsEnum&&!Enum.IsDefined(field.FieldType,field.GetValue(data.driving)))throw new InvalidDataException("Invalid saved driving setting.");return data;
        }
        internal static string AddToJson(string json,string path=null){var data=Read(path??FilePath);json="{"+string.Join(",",StateRows.Fields(json).Where(f=>f.Key!="driving"&&f.Key!="difficulty").Select(f=>StateRows.Quote(f.Key)+":"+f.Value))+"}";return json.Substring(0,json.Length-1)+",\"driving\":"+JsonUtility.ToJson(data.driving)+",\"difficulty\":"+data.difficulty+"}";}
        internal static void Ensure(PlayerProfile profile){if(!File.Exists(FilePath))SqliteStateStore.AtomicText(FilePath,Encode(profile.driving,profile.difficulty));}
        internal static void SaveIfChanged(PlayerProfile profile){string json=Encode(profile.driving,profile.difficulty);if(File.Exists(FilePath)){Read(FilePath);if(File.ReadAllText(FilePath,System.Text.Encoding.UTF8)==json)return;}SqliteStateStore.AtomicText(FilePath,json);}
    }
}
