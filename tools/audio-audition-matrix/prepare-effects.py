"""Compile old/new DSP against actual captured physical event frames, outside Unity."""
from pathlib import Path
import hashlib, importlib.util, json, os, re, subprocess

root=Path(__file__).resolve().parents[2];out=root/'reports/audio-matrix-20261015'
capture=out/'effects-capture/index.json'
assert capture.is_file(),'Run DragLab.Editor.EngineEffectsAudition.Run in Unity first.'
rows=json.loads(capture.read_text(encoding='utf-8-sig'))['cases']
assert len(rows)==124 and len({r['engineKey'] for r in rows})==13
for row in rows:
    row['cut']=re.search(r'; (Fuel|Ignition) cut',row['note'])[1]
spec=importlib.util.spec_from_file_location('audition',Path(__file__).parent/'prepare.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
domain=root/'Game/Assets/DragLab/Scripts/Domain'
current=(domain/'EngineSound.cs').read_text(encoding='utf-8-sig')
legacy=subprocess.run(['git','show',module.BASELINE_REF+':Game/Assets/DragLab/Scripts/Domain/EngineSound.cs'],cwd=root,capture_output=True,check=True,encoding='utf-8').stdout
prefix=module.extract(current,'    public sealed class EngineSoundProfile').split('        public EngineSoundProfile(')[0]
fields=re.findall(r'public readonly (\w+) ([^;]+);',prefix)
adapter='public EngineSoundProfile(System.Text.Json.JsonElement a){\nvar n=a.GetProperty("scalars").EnumerateArray().ToDictionary(e=>e.GetProperty("name").GetString(),e=>double.Parse(e.GetProperty("value").GetString(),System.Globalization.CultureInfo.InvariantCulture));\n'
for kind,names in fields:
    for name in names.split(','):
        name=name.strip()
        if kind=='ExhaustSetup':adapter+='exhaustSystem=System.Text.Json.JsonSerializer.Deserialize<ExhaustSetup>(a.GetProperty("exhaustSystem").GetRawText(),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});\n'
        else:adapter+=f'{name}=({kind})n["{name}"];\n'
adapter+='}\npublic double FiringHz(double rpm)=>rpm*cylinders/120;\n}\n'
common='using System;\nusing System.Linq;\nnamespace DragLab.Domain{\n'+current[current.index('    public enum InductionType'):current.index('    [Serializable]')]
common+='public enum FuelSystem{Injection,Carburetor,Diesel}\npublic enum CutMethod{Factory,Fuel,Ignition}\npublic static class DragMath{public static double Clamp(double x,double lo,double hi)=>Math.Max(lo,Math.Min(hi,x));}\n'
common+=prefix+adapter+module.extract(current,'    public sealed class EngineSoundFrame')+'\n'+module.extract(current,'    public sealed class EngineSoundSynth')+'\n'+module.extract(legacy,'    public sealed class EngineSoundSynth').replace('EngineSoundSynth','LegacyEngineSoundSynth')+'\n}\n'
elements=(domain/'EngineElements.cs').read_text(encoding='utf-8-sig');order=re.search(r'        static readonly int\[\]\[\] orders=.*?;',elements)[0]
common+='namespace DragLab.Domain.Mechanics{public static class EngineElements{'+order+'public static int[] FiringOrder(int n)=>orders[Math.Clamp(n,1,16)-1];}}\n'
harness=out/'effects-render-harness';harness.mkdir(exist_ok=True)
(harness/'CurrentDsp.cs').write_text(common,encoding='utf-8')
for name in ['ExhaustAcoustics.cs','ExhaustPopSynth.cs']:(harness/name).write_bytes((domain/name).read_bytes())
(harness/'Program.cs').write_bytes((Path(__file__).parent/'EffectsRenderer.cs').read_bytes())
(harness/'Preview.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup></Project>',encoding='utf-8')
(harness/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>',encoding='utf-8')
paths=[domain/'EngineSound.cs',domain/'PowertrainDynamics.cs',domain/'DragSimulation.cs',domain/'ExhaustAcoustics.cs',domain/'ExhaustPopSynth.cs',root/'Game/Assets/DragLab/Editor/EngineEffectsAudition.cs']
(out/'effects-source-hashes.json').write_text(json.dumps({str(p.relative_to(root)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths},indent=2),encoding='utf-8')
(out/'effects-index.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
env=os.environ.copy();env['DOTNET_CLI_HOME']=str(root/'reports/audio-integration-20261015/parity-harness/.dotnet');env['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
subprocess.run(['C:/Program Files/dotnet/dotnet.exe','run','--configuration','Release','--project',str(harness/'Preview.csproj'),'--',str(out)],env=env,check=True)
