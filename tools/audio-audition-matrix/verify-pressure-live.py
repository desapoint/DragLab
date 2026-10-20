"""Compile actual production audio code, test full catalogue and retain review WAVs."""
from pathlib import Path
import argparse,hashlib,importlib.util,json,os,re,subprocess,wave
import numpy as np

ROOT=Path(__file__).resolve().parents[2];HERE=Path(__file__).resolve().parent;DOMAIN=ROOT/'Game/Assets/DragLab/Scripts/Domain'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def replace(s,a,b):
    assert s.count(a)==1,a
    return s.replace(a,b)
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);args=ap.parse_args();out=args.output.resolve()
    assert out.is_relative_to(ROOT/'reports')
    if out.exists():raise RuntimeError('Use a fresh output directory.')
    out.mkdir(parents=True);spec=importlib.util.spec_from_file_location('prepare',HERE/'prepare.py');helper=importlib.util.module_from_spec(spec);spec.loader.exec_module(helper)
    watched=[DOMAIN/n for n in ['EngineSound.cs','PressureCombustionCore.cs','TurboAcoustics.cs','ExhaustAcoustics.cs','ExhaustPopSynth.cs','EngineElements.cs','InductionDynamics.cs','EngineAcoustics.cs']]
    watched+=sorted((ROOT/'Game/Assets/DragLab/Resources/Data/Vehicles').glob('*.asset'));hashes={str(p.relative_to(ROOT)).replace('\\','/'):sha(p) for p in watched}
    engine=watched[0].read_text(encoding='utf-8-sig');prefix=helper.extract(engine,'    public sealed class EngineSoundProfile').split('        public EngineSoundProfile(')[0]
    adapter=helper.ADAPTER.replace('            cylinders=', '            turboSize=N(o,"size",1);\n            cylinders=')
    adapter=adapter.replace('            exhaust=(int)', '            turboFlowCapacityKgS=N(d,"turboUnitFlowKgS",.22)*turboSize*Math.Max(1,turboCount);\n            exhaust=(int)')
    adapter=adapter.replace('            idleBoostGain=', '            compressorNoiseGain=N(s,"compressorNoiseGain",1);\n            var acousticSpec=s.TryGetProperty("acoustics",out var ac)?System.Text.Json.JsonSerializer.Deserialize<EngineAcousticSpec>(ac.GetRawText(),new System.Text.Json.JsonSerializerOptions{IncludeFields=true}):null;\n            acoustics=new EngineAcousticProfile(acousticSpec,cylinders,layout,litres,redlineRpm,fueling);\n            idleBoostGain=')
    common='using System;using System.Linq;namespace DragLab.Domain{'+engine[engine.index('    public enum InductionType'):engine.index('    [Serializable]')]
    common+='public enum FuelSystem{Injection,Carburetor,Diesel}public enum CutMethod{Factory,Fuel,Ignition}public static class DragMath{public static double Clamp(double x,double lo,double hi)=>Math.Max(lo,Math.Min(hi,x));}'
    common+=prefix+adapter+helper.extract(engine,'    public sealed class EngineSoundFrame')+helper.extract(engine,'    public sealed class EngineSoundSynth')+'}'
    elements=(DOMAIN/'EngineElements.cs').read_text(encoding='utf-8-sig');orders=re.search(r'        static readonly int\[\]\[\] orders=.*?;',elements)[0]
    common+='namespace DragLab.Domain.Mechanics{public static class EngineElements{'+orders+'public static int[] FiringOrder(int n)=>orders[Math.Clamp(n,1,16)-1];}}'
    (out/'CurrentDsp.cs').write_text(common,encoding='utf-8')
    old=subprocess.run(['git','show','892ea8c:Game/Assets/DragLab/Scripts/Domain/EngineSound.cs'],cwd=ROOT,capture_output=True,encoding='utf-8',check=True).stdout
    frozen=helper.extract(old,'    public sealed class EngineSoundSynth').replace('EngineSoundSynth','FrozenApprovedSynth')
    frozen=replace(frozen,'readonly EngineSoundProfile profile;','readonly EngineSoundProfile profile; readonly PressureEventCore pressureCore;')
    frozen=replace(frozen,'this.profile = profile;','this.profile = profile; pressureCore=new PressureEventCore(profile,48000,seed);')
    frozen=replace(frozen,'pop.Clear(); exhaustFilter.Clear(); return;','pop.Clear(); exhaustFilter.Clear(); pressureCore.Clear(); return;')
    a=frozen.index('                double s = Math.Sin(phase)');b=frozen.index('                gulpPhase =',a)
    frozen=frozen[:a]+'''                double noise=Noise();double pulse=pressureCore.Sample(rpm,voiceThrottle,frame);
                double value=pulse*gain*(1+profile.exhaust*.16);double carb=profile.fueling==FuelSystem.Carburetor?1:0;
'''+frozen[b:]
    frozen=replace(frozen,'value = exhaustFilter.Process(value) + outsideExhaust;','value = exhaustFilter.Process(value) + outsideExhaust + pressureCore.Mechanical*gain;')
    (out/'FrozenApprovedSynth.cs').write_text('using System;namespace DragLab.Domain{'+frozen+'}',encoding='utf-8')
    inputs=(HERE/'PressureComparisonRenderer.cs').read_text(encoding='utf-8-sig')
    methods=helper.extract(inputs,'    static double Smooth')+'\n'+helper.extract(inputs,'    static EngineSoundFrame[] Frames').replace('static EngineSoundFrame[] Frames','public static EngineSoundFrame[] Frames')
    (out/'ComparisonInputs.cs').write_text('using System;using DragLab.Domain;public static class ComparisonInputs{const int Rate=48000,Block=400,Seconds=14;'+methods+'}',encoding='utf-8')
    for name in ['PressureCombustionCore.cs','TurboAcoustics.cs','ExhaustAcoustics.cs','ExhaustPopSynth.cs','EngineAcoustics.cs']:(out/name).write_bytes((DOMAIN/name).read_bytes())
    for name,target in [('PressureEventCore.cs','PressureEventCore.cs'),('PressureLiveRenderer.cs','Program.cs')]:(out/target).write_bytes((HERE/name).read_bytes())
    cases=[]
    for path in sorted((ROOT/'Game/Assets/DragLab/Resources/Data/Vehicles').glob('*.asset')):
        text=path.read_text(encoding='utf-8-sig');p=helper.scalar(text,4)
        for key in ['sound','induction','engineBehavior']:
            section=re.search(r'^    '+key+r':\n(.*?)(?=^    [^ \n]|\Z)',text,re.M|re.S)[1];p[key]=helper.scalar(section,6)
            if key=='sound':
                parts=[]
                for block in re.split(r'^        - kind: ',section,flags=re.M)[1:]:
                    kind,rest=block.split('\n',1);item=helper.scalar(rest,10);item['kind']=int(kind);parts.append(item)
                p[key]['exhaustSystem']={'version':1,'parts':parts}
        p['audition']={'topology':int(p['induction']['factory']),'exhaustPreset':-1,'exhaustStage':0,'valve':int(p['induction']['factoryValve'])}
        cases.append({'id':path.stem,'name':re.search(r'^    displayName: (.+)$',text,re.M)[1],'profile':p})
    (out/'cases.json').write_text(json.dumps(cases,indent=2),encoding='utf-8')
    (out/'Preview.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>',encoding='utf-8')
    (out/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>',encoding='utf-8')
    env=os.environ.copy();env.update(DOTNET_CLI_HOME=str(out/'.dotnet'),DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_NOLOGO='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
    subprocess.run(['C:/Program Files/dotnet/dotnet.exe','run','-c','Release','--project',str(out/'Preview.csproj'),'--',str(out)],check=True,env=env)
    for key in ['StarterHatch','RallySedan','V8Muscle']:
        x=np.fromfile(out/(key+'-live.f32'),dtype='<f4');assert np.isfinite(x).all() and abs(x).max()<1
        with wave.open(str(out/(key+'-live.wav')),'wb') as f:
            f.setnchannels(1);f.setsampwidth(2);f.setframerate(48000);f.writeframes(np.rint(x*32767).astype('<i2').tobytes())
    assert all(sha(ROOT/p)==digest for p,digest in hashes.items())
    (out/'source-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8');print('PASS: production audio coverage; '+str(out/'verification.json'))
if __name__=='__main__':main()
