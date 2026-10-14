using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using DragLab.Domain.Mechanics;
namespace DragLab.Infrastructure
{
    public static class ProfileSaveCodec
    {
        [Serializable] sealed class Envelope { public int saveVersion,codecVersion;public string encoding,payload; }
        const int MaxBytes=128*1024*1024;
        public static byte[] Encode(string json,int version)
        {
            using(var output=new MemoryStream()){
                using(var zip=new GZipStream(output,System.IO.Compression.CompressionLevel.Fastest,true)){var data=Encoding.UTF8.GetBytes(json);zip.Write(data,0,data.Length);}
                return Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope{saveVersion=version,codecVersion=1,encoding="gzip-base64",payload=Convert.ToBase64String(output.ToArray())}));
            }
        }
        public static string ReadJson(string path)=>ProfileStore.DatabasePath(path)?SqliteStateStore.ExportJson(path):Decode(File.ReadAllText(path,Encoding.UTF8));
        public static string Decode(string text)
        {
            var envelope=JsonUtility.FromJson<Envelope>(text);
            if(envelope==null||string.IsNullOrEmpty(envelope.encoding)||envelope.saveVersion>MechanicsOwnership.SaveVersion)return text;
            if(envelope.codecVersion!=1||envelope.encoding!="gzip-base64")throw new NotSupportedException("Unsupported profile encoding; writing is disabled to preserve it.");
            if(envelope.payload==null||envelope.payload.Length>MaxBytes)throw new InvalidDataException("Corrupt profile payload.");
            using(var input=new MemoryStream(Convert.FromBase64String(envelope.payload)))using(var zip=new GZipStream(input,CompressionMode.Decompress))using(var output=new MemoryStream()){
                var buffer=new byte[16384];int read;while((read=zip.Read(buffer,0,buffer.Length))>0){if(output.Length+read>MaxBytes)throw new InvalidDataException("Profile exceeds supported size.");output.Write(buffer,0,read);}
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }
    }
}
