"""Offline 5.0 V8 audition. Reads production sources; writes only a new reports folder."""
from pathlib import Path
import argparse, copy, hashlib, importlib.util, json, os, re, subprocess, wave
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
DOMAIN=ROOT/'Game/Assets/DragLab/Scripts/Domain'

def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,required=True);parser.add_argument('--variant',choices=['balanced','deep'],default='deep');args=parser.parse_args()
    out=args.output.resolve();assert out.is_relative_to(ROOT/'reports')
    if out.exists():raise RuntimeError('Use a new output folder to preserve prior auditions.')
    out.mkdir(parents=True)
    helper_spec=importlib.util.spec_from_file_location('matrix_prepare',Path(__file__).with_name('prepare.py'))
    helper=importlib.util.module_from_spec(helper_spec);helper_spec.loader.exec_module(helper)
    paths=[DOMAIN/n for n in ['EngineSound.cs','EngineElements.cs','ExhaustAcoustics.cs','ExhaustPopSynth.cs']]
    asset=ROOT/'Game/Assets/DragLab/Resources/Data/Vehicles/V8Muscle.asset';paths.append(asset)
    hashes={str(p.relative_to(ROOT)):digest(p) for p in paths}
    text=asset.read_text(encoding='utf-8-sig');profile=helper.scalar(text,4)
    for key in ['sound','induction','engineBehavior']:
        section=re.search(r'^    '+key+r':\n(.*?)(?=^    [^ \n]|\Z)',text,re.M|re.S)[1]
        profile[key]=helper.scalar(section,6)
        if key=='sound':
            parts=[]
            for block in re.split(r'^        - kind: ',section,flags=re.M)[1:]:
                kind,rest=block.split('\n',1);part=helper.scalar(rest,10);part['kind']=int(kind);parts.append(part)
            profile[key]['exhaustSystem']={'version':1,'parts':parts}
    assert profile['sound']['cylinders']==8 and profile['sound']['layout']==2 and profile['displacementLitres']==5
    profile['audition']={'topology':0,'exhaustPreset':-1,'exhaustStage':int(profile['sound']['exhaust']),'valve':1}
    candidate=copy.deepcopy(profile)
    # Lower drive into the nonlinear exhaust stage as bass is reinforced. Merely
    # boosting bass into saturation can create extra bright harmonics instead.
    controls=dict(exhaustBassGain=2.15,exhaustBrightness=.42,rawPulseGain=.65,engineGain=.42) if args.variant=='balanced' else dict(exhaustBassGain=2.8,exhaustBrightness=.28,rawPulseGain=.45,engineGain=.32)
    candidate['sound'].update(controls)
    (out/'profiles.json').write_text(json.dumps({'before':profile,'after':candidate},indent=2),encoding='utf-8')
    engine=paths[0].read_text(encoding='utf-8-sig')
    enums=engine[engine.index('    public enum InductionType'):engine.index('    [Serializable]')]
    prefix=helper.extract(engine,'    public sealed class EngineSoundProfile').split('        public EngineSoundProfile(')[0]
    common='using System;using System.Linq;\nnamespace DragLab.Domain {\n'+enums+'\npublic enum FuelSystem{Injection,Carburetor,Diesel}\npublic enum CutMethod{Factory,Fuel,Ignition}\npublic static class DragMath{public static double Clamp(double x,double lo,double hi)=>Math.Max(lo,Math.Min(hi,x));}\n'
    common+=prefix+helper.ADAPTER+helper.extract(engine,'    public sealed class EngineSoundFrame')+helper.extract(engine,'    public sealed class EngineSoundSynth')+'\n}\n'
    order=re.search(r'        static readonly int\[\]\[\] orders=.*?;',paths[1].read_text(encoding='utf-8-sig'),re.S)[0]
    common+='namespace DragLab.Domain.Mechanics {public static class EngineElements{'+order+'public static int[] FiringOrder(int n)=>orders[Math.Clamp(n,1,16)-1];}}\n'
    (out/'CurrentDsp.cs').write_text(common,encoding='utf-8')
    for name in ['ExhaustAcoustics.cs','ExhaustPopSynth.cs']:(out/name).write_bytes((DOMAIN/name).read_bytes())
    (out/'Program.cs').write_bytes(Path(__file__).with_name('V8PreviewRenderer.cs').read_bytes())
    (out/'Preview.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup></Project>',encoding='utf-8')
    (out/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>',encoding='utf-8')
    env=os.environ.copy();env['DOTNET_CLI_HOME']=str(out/'.dotnet');env['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
    env.update(DOTNET_NOLOGO='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false',DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='1')
    subprocess.run(['C:/Program Files/dotnet/dotnet.exe','run','--configuration','Release','--project',str(out/'Preview.csproj'),'--',str(out)],env=env,check=True)
    metrics=[]
    def wav(name,values):
        with wave.open(str(out/name),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(48000);f.writeframes(np.rint(values*32767).astype('<i2').tobytes())
    for scene in ['idle','rev','pass']:
        pair=[np.fromfile(out/f'{scene}-{side}.f32',dtype='<f4').astype(np.float64) for side in ['before','after']]
        for x in pair:
            n=480;x[:n]*=np.linspace(0,1,n);x[-n:]*=np.linspace(1,0,n)
        gains=[.12/np.sqrt(np.mean(x*x)) for x in pair]
        headroom=min(1,*[.9/(np.max(np.abs(x))*g) for x,g in zip(pair,gains)])
        scaled=[x*g*headroom for x,g in zip(pair,gains)]
        for side,x,g in zip(['before','after'],scaled,gains):
            assert np.isfinite(x).all() and np.max(np.abs(x))<=.900001
            wav(f'{scene}-{side}.wav',x)
            power=np.abs(np.fft.rfft(x*np.hanning(len(x))))**2;hz=np.fft.rfftfreq(len(x),1/48000)
            low=float(power[(hz>=40)&(hz<250)].sum());high=float(power[(hz>=300)&(hz<1000)].sum())
            metrics.append({'scene':scene,'side':side,'rms':float(np.sqrt(np.mean(x*x))),'peak':float(np.max(np.abs(x))),'low40to250_over300to1000':low/max(1e-20,high),'spectralCentroidHz':float((hz*power).sum()/power.sum()),'gain':g*headroom})
        assert abs(np.sqrt(np.mean(scaled[0]**2))-np.sqrt(np.mean(scaled[1]**2)))<1e-10
        if scene!='idle':assert metrics[-1]['low40to250_over300to1000']>metrics[-2]['low40to250_over300to1000']*1.3,'Upper edge was not reduced relative to the low bank voice'
        wav(f'{scene}-comparison.wav',np.concatenate([scaled[0],np.zeros(33600),scaled[1]]))
    assert all(digest(p)==hashes[str(p.relative_to(ROOT))] for p in paths),'Production source was modified'
    manifest={'engine':'Bison 5.0 crossplane V8','variant':args.variant,'sampleRate':48000,'previewOnly':True,'sourceHashes':hashes,'proposedSoundControls':candidate['sound'],'metrics':metrics,'input':'Identical scripted idle, neutral blips and three-gear loaded pass; not a recorded physical race. RPM/firing rates are unchanged.','level':'Equal RMS for each before/after pair, common peak headroom; 10 ms edge fades.'}
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    rows=''.join(f'<section><h2>{title}</h2><div><label>Current<audio controls preload="none" src="{scene}-before.wav"></audio></label><label>Deeper preview<audio controls preload="none" src="{scene}-after.wav"></audio></label></div><a href="{scene}-comparison.wav">Current → deeper, combined clip</a></section>' for scene,title in [('idle','Warm idle'),('rev','Neutral revs'),('pass','Loaded pass and lift')])
    (out/'index.html').write_text('<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>5.0 V8 growl audition</title><style>body{background:#151b20;color:#e7e9eb;font:17px system-ui;max-width:1000px;margin:40px auto;padding:0 20px}h1{border-bottom:2px solid #ed233d;padding-bottom:18px}section{background:#20282f;border:1px solid #606b76;border-radius:10px;padding:20px;margin:22px 0}section div{display:flex;flex-wrap:wrap;gap:30px}label{display:grid;gap:12px}a{color:#ffa0ab}p{line-height:1.6;color:#bcc7cf}</style><h1>Bison 5.0 · deeper V8 audition</h1><p>Preview only. In-game audio is unchanged. Stronger low bank pulse, softer upper edge, reduced raw buzz. Same firing rate, RPM/load trace, exhaust hardware and private random seed. Each pair has equal RMS level.</p><p>Healthy scripted warm idle, neutral blips and three-gear acceleration/lift. This demonstrates timbre, not a new performance calibration or recorded race.</p>'+rows+'<script>document.addEventListener("play",e=>{for(const a of document.querySelectorAll("audio"))if(a!==e.target)a.pause()},true)</script></html>',encoding='utf-8')
    (out/'verification.txt').write_text('PASS: six finite bounded repeatable source-DSP renders.\nPASS: identical input/RPM/firing order/private seed/exhaust hardware.\nPASS: per-pair equal RMS, peaks at or below 0.9, edge fades.\nPASS: five production source/asset hashes unchanged.\nListening approval and physical-phone playback are pending.\n',encoding='utf-8')
    print(out/'index.html')
    for m in metrics:print(m)

if __name__=='__main__':main()
