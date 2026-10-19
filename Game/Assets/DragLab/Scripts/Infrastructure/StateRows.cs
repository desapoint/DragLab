using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain;
namespace DragLab.Infrastructure
{
    // Entity payloads keep evolving detail fields; arrays are separate identity-addressed rows.
    // JSON numbers/strings are copied verbatim, preserving the existing codec's precision.
    internal static class StateRows
    {
        internal sealed class Row {public string path,key,json;public int order;public byte[] digest;public string Id=>path+"\n"+key;}
        sealed class Cached {public string raw,value;public Row[] children;}
        static readonly Dictionary<string,Cached> cache=new Dictionary<string,Cached>(StringComparer.Ordinal);
        static readonly object cacheGate=new object();
        static readonly HashSet<string> split=new HashSet<string>{"cars","engines","parts","looseEngineElements","savedSetups","runCheckpoints","economyReceipts","careerRuns","careerWins","listings","dealer","storage","lines","supply","elements","tires","slots"};
        // Market stock is transient, reseeded together as one inventory epoch.
        // Keep it bounded in deterministic chunks instead of storing thousands
        // of generated member/element identities as separate SQLite rows.
        // Owned hardware, wear, cart holds and garage ownership remain split.
        static readonly Dictionary<string,int> marketChunkSizes=new Dictionary<string,int>(StringComparer.Ordinal){
            {"$/inventory/market/parts",128},
            {"$/inventory/market/engines",2},
            {"$/inventory/dealer",8},
            {"$/vehicleStock/listings",2}
        };
        internal static bool MarketChunkPath(string path)=>marketChunkSizes.ContainsKey(path);
        static string CaptureMarketChunks(string json,string path,List<Row> rows,int chunkSize)
        {
            // Preserve the original JsonUtility payload bytes verbatim. Splitting
            // does not alter offer IDs, part/member state, prices or RNG.
            var items=Items(json);int chunk=0;
            for(int index=0;index<items.Length;index+=chunkSize)
            {
                var payload=new StringBuilder("[");
                int end=Math.Min(index+chunkSize,items.Length);
                for(int i=index;i<end;i++){if(i>index)payload.Append(',');payload.Append(items[i]);}
                payload.Append(']');
                rows.Add(new Row{path=path,key="chunk:"+chunk,order=chunk,json=payload.ToString()});
                chunk++;
            }
            return "{\"$draglabRows\":"+Quote(path)+"}";
        }
        internal static string Quote(string text){if(text==null)return "null";var b=new StringBuilder("\"");foreach(char c in text){switch(c){case '\"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u"+((int)c).ToString("x4"));else b.Append(c);break;}}return b.Append('\"').ToString();}
        static string Segment(string text)=>Uri.EscapeDataString(text);
        static int End(string text,int start){bool quoted=false,escape=false;int depth=0;for(int i=start;i<text.Length;i++){char c=text[i];if(quoted){if(escape)escape=false;else if(c=='\\')escape=true;else if(c=='\"')quoted=false;}else if(c=='\"')quoted=true;else if(c=='{'||c=='[')depth++;else if(c=='}'||c==']'){if(depth==0)return i;depth--;}else if(c==','&&depth==0)return i;}return text.Length;}
        internal static List<KeyValuePair<string,string>> Fields(string text)
        {
            var fields=new List<KeyValuePair<string,string>>();int i=1;while(i<text.Length-1){while(i<text.Length&&(char.IsWhiteSpace(text[i])||text[i]==','))i++;if(i>=text.Length-1)break;int begin=i++;bool escape=false;while(i<text.Length){char c=text[i++];if(escape)escape=false;else if(c=='\\')escape=true;else if(c=='\"')break;}string key=Unquote(text.Substring(begin,i-begin));while(i<text.Length&&(char.IsWhiteSpace(text[i])||text[i]==':'))i++;int end=End(text,i);fields.Add(new KeyValuePair<string,string>(key,text.Substring(i,end-i).Trim()));i=end;}return fields;
        }
        internal static string[] Items(string text){var items=new List<string>();int i=1;while(i<text.Length-1){while(i<text.Length&&(char.IsWhiteSpace(text[i])||text[i]==','))i++;if(i>=text.Length-1)break;int end=End(text,i);items.Add(text.Substring(i,end-i).Trim());i=end;}return items.ToArray();}
        static string Unquote(string json){var b=new StringBuilder();for(int i=1;i<json.Length-1;i++){char c=json[i];if(c!='\\'){b.Append(c);continue;}c=json[++i];switch(c){case 'u':b.Append((char)int.Parse(json.Substring(i+1,4),System.Globalization.NumberStyles.HexNumber));i+=4;break;case 'n':b.Append('\n');break;case 'r':b.Append('\r');break;case 't':b.Append('\t');break;case 'b':b.Append('\b');break;case 'f':b.Append('\f');break;default:b.Append(c);break;}}return b.ToString();}
        static string Key(string json,int order){if(json.StartsWith("{")){var fields=Fields(json);foreach(string name in new[]{"instanceId","id","runId"}){var field=fields.FirstOrDefault(f=>f.Key==name);if(field.Value!=null&&field.Value!="null"&&field.Value!="\"\"")return "id:"+Unquote(field.Value);}}return "row:"+order;}
        internal static List<Row> Capture(PlayerProfile profile)=>CaptureJson(JsonUtility.ToJson(profile));
        internal static List<Row> CaptureJson(string json)
        {
            lock(cacheGate){if(cache.Count>20000)cache.Clear();
            var rows=new List<Row>();foreach(var field in Fields(json)){if(field.Key=="driving"||field.Key=="difficulty")continue;rows.Add(new Row{path="$",key=field.Key,order=rows.Count,json=CaptureValue(field.Value,"$/"+Segment(field.Key),field.Key,rows)});}return rows;}
        }
        static string CaptureValue(string json,string path,string field,List<Row> rows)
        {
            if(json.IndexOf('[')<0)return json;
            if(json.Length<512)return CaptureUncached(json,path,field,rows);
            if(cache.TryGetValue(path,out var old)&&old.raw==json){rows.AddRange(old.children);return old.value;}int begin=rows.Count;string value=CaptureUncached(json,path,field,rows);cache[path]=new Cached{raw=json,value=value,children=rows.Skip(begin).ToArray()};return value;
        }
        static string CaptureUncached(string json,string path,string field,List<Row> rows)
        {
            if(json.StartsWith("[")&&marketChunkSizes.TryGetValue(path,out int chunkSize))
                return CaptureMarketChunks(json,path,rows,chunkSize);
            if(json.StartsWith("[")&&split.Contains(field)){int order=0;foreach(string item in Items(json)){string key=Key(item,order);rows.Add(new Row{path=path,key=key,order=order++,json=CaptureValue(item,path+"/"+Segment(key),"",rows)});}return "{\"$draglabRows\":"+Quote(path)+"}";}
            if(json.StartsWith("{"))return "{"+string.Join(",",Fields(json).Select(f=>Quote(f.Key)+":"+CaptureValue(f.Value,path+"/"+Segment(f.Key),f.Key,rows)))+"}";return json;
        }
        internal static string Restore(IEnumerable<Row> source)
        {
            var rows=source.GroupBy(r=>r.path).ToDictionary(g=>g.Key,g=>g.OrderBy(r=>r.order).ToArray(),StringComparer.Ordinal);
            string Value(string json,int depth){if(depth>80)throw new System.IO.InvalidDataException("Database state nesting exceeds the supported bound.");if(!json.StartsWith("{"))return json;var fields=Fields(json);if(fields.Count==1&&fields[0].Key=="$draglabRows"){string path=Unquote(fields[0].Value);return "["+(rows.TryGetValue(path,out var items)?string.Join(",",items.Select(r=>{
                // An older database contains regular row:/id: records at these
                // paths. A newly saved database contains complete JSON-array
                // chunks. Both restore the exact same logical profile.
                if(r.key.StartsWith("chunk:",StringComparison.Ordinal)&&MarketChunkPath(path)){
                    if(r.json.Length<2||r.json[0]!='['||r.json[r.json.Length-1]!=']')
                        throw new System.IO.InvalidDataException("Invalid market stock chunk.");
                    return r.json.Substring(1,r.json.Length-2);
                }
                return Value(r.json,depth+1);
            })):"")+"]";}return "{"+string.Join(",",fields.Select(f=>Quote(f.Key)+":"+Value(f.Value,depth+1)))+"}";}
            if(!rows.TryGetValue("$",out var root))throw new System.IO.InvalidDataException("Missing database profile root.");return "{"+string.Join(",",root.Select(r=>Quote(r.key)+":"+Value(r.json,0)))+"}";
        }
    }
}
