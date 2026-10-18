using System;
using System.IO;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
namespace DragLab.Infrastructure
{
    internal static class DrivingPreferences
    {
        [Serializable] internal sealed class Data {public int version=1;public DriverSettings driving=new DriverSettings();public int difficulty=1;public bool reverseTouchShifting,onboardingComplete;}
        internal static string FilePath=>Path.Combine(Path.GetDirectoryName(ProfileStore.FilePath),"driving-settings.json");
        static string controlsPath;
        static bool reverseTouchShifting;
        internal static bool OnboardingComplete=>Read(FilePath).onboardingComplete;
        internal static bool ReverseTouchShifting { get { if(controlsPath!=FilePath)ReloadControls();return reverseTouchShifting; } }
        internal static void PublishControls(string json){reverseTouchShifting=JsonUtility.FromJson<Data>(json).reverseTouchShifting;controlsPath=FilePath;}
        internal static void ReloadControls(){string path=FilePath;reverseTouchShifting=Read(path).reverseTouchShifting;controlsPath=path;}
        internal static string Encode(DriverSettings driving,int difficulty,bool? reverse=null,bool? onboardingComplete=null)=>JsonUtility.ToJson(new Data{driving=driving.Copy(),difficulty=difficulty,reverseTouchShifting=reverse??Read(FilePath).reverseTouchShifting,onboardingComplete=onboardingComplete??Read(FilePath).onboardingComplete});
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
