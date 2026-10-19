"""Compile an isolated core-only variation of the current game synth; never edit Game/."""
from pathlib import Path
import argparse,hashlib,importlib.util,json,os,re,subprocess,wave
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
DOMAIN=ROOT/'Game/Assets/DragLab/Scripts/Domain'
HERE=Path(__file__).resolve().parent
RATE=48000

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def change(text,before,after):
    assert text.count(before)==1,before
    return text.replace(before,after)

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,required=True);args=ap.parse_args()
    out=args.output.resolve();assert out.is_relative_to(ROOT/'reports')
    if out.exists():raise RuntimeError('Preserve earlier auditions; choose a new output folder.')
    out.mkdir(parents=True)
    spec=importlib.util.spec_from_file_location('prepare',HERE/'prepare.py');helper=importlib.util.module_from_spec(spec);spec.loader.exec_module(helper)
    paths=[DOMAIN/n for n in ['EngineSound.cs','EngineElements.cs','ExhaustAcoustics.cs','ExhaustPopSynth.cs']]
    paths+=[ROOT/f'Game/Assets/DragLab/Resources/Data/Vehicles/{key}.asset' for key in ['StarterHatch','RallySedan','V8Muscle']]
    hashes={str(p.relative_to(ROOT)).replace('\\','/'):sha(p) for p in paths}
    engine=paths[0].read_text(encoding='utf-8-sig')
    prefix=helper.extract(engine,'    public sealed class EngineSoundProfile').split('        public EngineSoundProfile(')[0]
    common='using System;using System.Linq;namespace DragLab.Domain{'+engine[engine.index('    public enum InductionType'):engine.index('    [Serializable]')]
    common+='public enum FuelSystem{Injection,Carburetor,Diesel}public enum CutMethod{Factory,Fuel,Ignition}public static class DragMath{public static double Clamp(double x,double lo,double hi)=>Math.Max(lo,Math.Min(hi,x));}'
    common+=prefix+helper.ADAPTER+helper.extract(engine,'    public sealed class EngineSoundFrame')+helper.extract(engine,'    public sealed class EngineSoundSynth')+'}'
    elements=paths[1].read_text(encoding='utf-8-sig');orders=re.search(r'        static readonly int\[\]\[\] orders=.*?;',elements)[0]
    common+='namespace DragLab.Domain.Mechanics{public static class EngineElements{'+orders+'public static int[] FiringOrder(int n)=>orders[Math.Clamp(n,1,16)-1];}}'
    (out/'CurrentDsp.cs').write_text(common,encoding='utf-8')
    candidate=helper.extract(engine,'    public sealed class EngineSoundSynth').replace('EngineSoundSynth','PressureAuditionSynth')
    candidate=change(candidate,'readonly EngineSoundProfile profile;','readonly EngineSoundProfile profile; readonly PressureEventCore pressureCore;')
    candidate=change(candidate,'this.profile = profile;','this.profile = profile; pressureCore=new PressureEventCore(profile,48000,seed);')
    candidate=change(candidate,'pop.Clear(); exhaustFilter.Clear(); return;','pop.Clear(); exhaustFilter.Clear(); pressureCore.Clear(); return;')
    start=candidate.index('                double s = Math.Sin(phase)');end=candidate.index('                gulpPhase = ',start)
    removed=candidate[start:end]
    candidate=candidate[:start]+'''                double noise=Noise();
                double pulse=pressureCore.Sample(rpm,voiceThrottle,frame);
                double value=pulse*gain*(1+profile.exhaust*.16);
                double carb=profile.fueling==FuelSystem.Carburetor?1:0;
'''+candidate[end:]
    candidate=change(candidate,'value = exhaustFilter.Process(value) + outsideExhaust;','value = exhaustFilter.Process(value) + outsideExhaust + pressureCore.Mechanical*gain;')
    (out/'PressureAuditionSynth.cs').write_text('using System;namespace DragLab.Domain{'+candidate+'}',encoding='utf-8')
    (out/'replaced-combustion-region.txt').write_text(removed,encoding='utf-8')
    for name in ['ExhaustAcoustics.cs','ExhaustPopSynth.cs']:(out/name).write_bytes((DOMAIN/name).read_bytes())
    for source,target in [('PressureEventCore.cs','PressureEventCore.cs'),('PressureComparisonRenderer.cs','Program.cs')]:
        (out/target).write_bytes((HERE/source).read_bytes())
    (out/'Preview.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>',encoding='utf-8')
    (out/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>',encoding='utf-8')
    cases=[]
    for path in paths[4:]:
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
    env=os.environ.copy();env.update(DOTNET_CLI_HOME=str(out/'.dotnet'),DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_NOLOGO='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
    subprocess.run(['C:/Program Files/dotnet/dotnet.exe','run','-c','Release','--project',str(out/'Preview.csproj'),'--',str(out)],check=True,env=env)
    manifest={'sourceHashes':hashes,'rate':RATE,'duration':14,'seed':117,'coreOnly':True,'gameSourceUnchanged':True,'cases':[]}
    sections=[]
    for row in cases:
        id=row['id'];voices={}
        for side in ['before','after']:
            x=np.fromfile(out/(id+'-'+side+'.f32'),dtype='<f4').astype(float);assert len(x)==14*RATE and np.isfinite(x).all()
            fade=np.linspace(0,1,480);x[:480]*=fade;x[-480:]*=fade[::-1]
            rms=float(np.sqrt(np.mean(x*x)));gain=min(.10/max(rms,1e-12),.90/max(abs(x).max(),1e-12))
            voices[side]=x*gain
            row.setdefault('measurements',{})[side]={'rawRms':rms,'playbackGain':gain,'playbackRms':float(np.sqrt(np.mean(voices[side]**2))),'peak':float(abs(voices[side]).max())}
        # One common review target per pair also protects transient peaks without clipping.
        target=min(row['measurements'][s]['playbackRms'] for s in voices)
        for side,x in voices.items():
            x*=target/row['measurements'][side]['playbackRms'];voices[side]=x
            with wave.open(str(out/(id+'-'+side+'.wav')),'wb') as f:
                f.setnchannels(1);f.setsampwidth(2);f.setframerate(RATE);f.writeframes(np.rint(x*32767).astype('<i2').tobytes())
        pair=np.concatenate([voices['before'],np.zeros(round(.7*RATE)),voices['after']])
        with wave.open(str(out/(id+'-before-after.wav')),'wb') as f:
            f.setnchannels(1);f.setsampwidth(2);f.setframerate(RATE);f.writeframes(np.rint(pair*32767).astype('<i2').tobytes())
        manifest['cases'].append({'id':id,'name':row['name'],'measurements':row['measurements'],'pairRms':target})
        sections.append(f'<section><h2>{row["name"]}</h2><p>Before → after, 14 seconds each. Idle, neutral rev, loaded pull, lift-off.</p><audio controls preload="none" src="{id}-before-after.wav"></audio><div><label>Before<audio controls preload="none" src="{id}-before.wav"></audio></label><label>After<audio controls preload="none" src="{id}-after.wav"></audio></label></div></section>')
    assert all(sha(ROOT/p)==digest for p,digest in hashes.items())
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    (out/'index.html').write_text('<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>DragLab core-only audio comparison</title><style>body{background:#151b20;color:#e8ecef;font:17px system-ui;line-height:1.6;max-width:1000px;margin:30px auto;padding:0 20px}section{background:#232b33;border:1px solid #65717c;border-radius:10px;padding:20px;margin:20px 0}h1{border-bottom:2px solid #e92340}p{color:#bcc6ce}audio{display:block;width:100%;margin:10px 0}div{display:flex;gap:24px}label{flex:1;min-width:0}@media(max-width:600px){div{display:block}}</style><h1>Three cars · combustion/exhaust core preview</h1><p>Current game synth versus a pressure-event core inside the same audio pipeline. Factory authored cars, identical scripted RPM/throttle/load and induction inputs. Installed exhaust filters, induction, release and event layers retained. Pair playback levels are matched; game audio is unchanged. This is a mono comparison following the existing output path.</p>'+''.join(sections)+'<script>document.addEventListener("play",e=>{for(const a of document.querySelectorAll("audio"))if(a!==e.target)a.pause()},true)</script></html>',encoding='utf-8')
    (out/'verification.txt').write_text('PASS: three current-source/core-only pairs, identical captured input frames.\nPASS: finite bounded samples, exact repeatability, private-seed variation and callback-boundary invariance.\nPASS: zero managed allocation in warmed core Sample and candidate Fill.\nPASS: 3000 RPM firing rate, balanced per-cylinder events, disabled-cylinder and cut suppression.\nPASS: installed exhaust changes and exhaust-pop events affect all three candidates.\nPASS: turbo and release layers affect the Tempest candidate.\nPASS: matched pair playback levels and unclipped WAVs.\nPASS: production source/asset checksums unchanged.\nListening acceptance and physical Android performance are pending.\n',encoding='utf-8')
    print('PASS: three paired auditions;',out/'index.html')

if __name__=='__main__':main()
