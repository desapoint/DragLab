using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Infrastructure;
namespace DragLab.Editor
{
    public static class SqliteStateEditorVerification
    {
        public static void Run()=>Verify(0);
        public static void Timings()=>Verify(2);
        static void Verify(int limit)
        {
            SqlitePluginSetup.Configure();
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/sqlite-state"));Directory.CreateDirectory(root);string folder=Path.Combine(root,"fixtures",DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));var log=new StringBuilder();int checks=0;
            try{SqliteStateVerification.Run(folder,Resources.LoadAll<VehicleDefinition>("Data/Vehicles"),(ok,label)=>{checks++;log.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new InvalidOperationException(label);},text=>log.AppendLine(text),limit);log.AppendLine("Checks: "+checks+" / failures: 0");}
            catch(Exception error){log.AppendLine(error.ToString());throw;}
            finally{File.WriteAllText(Path.Combine(root,limit==0?"verification.txt":"timing-verification.txt"),log.ToString(),new UTF8Encoding(false));}
            Debug.Log("SQLite state verification passed: "+checks);
        }
    }
}
