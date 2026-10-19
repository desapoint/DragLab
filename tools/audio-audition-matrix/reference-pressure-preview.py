"""Analyse a local reference, then generate fresh event-driven V8 audio (no sampled playback)."""
from pathlib import Path
import argparse, hashlib, importlib.util, json, os, re, subprocess, wave
import numpy as np
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[2]
DOMAIN=ROOT/'Game/Assets/DragLab/Scripts/Domain'
RATE=48000
BANDS=[(10,40),(40,80),(80,160),(160,320),(320,640),(640,1280),(1280,2560),(2560,6000)]

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def read_wav(path):
    with wave.open(str(path)) as f:
        assert f.getsampwidth()==2,'Provide PCM16 analysis audio.'
        rate=f.getframerate();x=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').astype(np.float64)/32768
        x=x.reshape(-1,f.getnchannels()).mean(axis=1)
    return x,rate

def features(x,rate):
    n=min(32768,2**int(np.floor(np.log2(len(x)))));hop=n//4
    frames=np.lib.stride_tricks.sliding_window_view(x,n)[::hop]
    power=(abs(np.fft.rfft(frames*np.hanning(n)))**2).mean(axis=0);hz=np.fft.rfftfreq(n,1/rate)
    selected=(hz>=10)&(hz<6000);total=power[selected].sum()
    bands=[float(power[(hz>=lo)&(hz<hi)].sum()/total) for lo,hi in BANDS]
    block=int(rate*.02);env=np.sqrt(np.mean(x[:len(x)//block*block].reshape(-1,block)**2,axis=1))
    return {'duration':len(x)/rate,'rms':float(np.sqrt(np.mean(x*x))),'peak':float(abs(x).max()),'centroidHz':float((hz[selected]*power[selected]).sum()/total),'bands':bands,'envelopeCV':float(env.std()/env.mean())},hz,power

def normalise(x,target=.12):
    x=x.copy();fade=min(480,len(x)//8);x[:fade]*=np.linspace(0,1,fade);x[-fade:]*=np.linspace(1,0,fade)
    gain=target/max(1e-12,np.sqrt(np.mean(x*x)));assert abs(x).max()*gain<.94,'Signal needs explicit headroom adjustment'
    return x*gain

def write_wav(path,x,rate=RATE):
    assert np.isfinite(x).all() and abs(x).max()<1
    with wave.open(str(path),'wb') as f:
        f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes(np.rint(x*32767).astype('<i2').tobytes())

def trace(scene,seconds,seed,idle):
    rng=np.random.default_rng(seed);t=np.arange(round(seconds*120))/120
    knots=np.arange(0,seconds+.5,.5);wander=np.interp(t,knots,rng.normal(0,12,len(knots)))
    rpm=idle+wander+7*np.sin(2*np.pi*.83*t)+4*np.sin(2*np.pi*1.41*t+.7)
    gas=np.full(len(t),.035);load=np.full(len(t),.025)
    if scene=='rev':
        for center,width,top in [(3.0,1.25,2400),(7.4,1.4,3400)]:
            u=np.maximum(0,1-abs(t-center)/width);u=u*u*(3-2*u)
            rpm+=(top-idle)*u;gas=np.maximum(gas,u*.7);load=gas*.08
    return np.stack([rpm,gas,load],axis=1)

def pressure_voice(control,config,seed):
    """Two generated pulse banks; no reference samples/phase/envelope enter here."""
    if config.get('voiceModel') in ('bank-growl','open-exhaust'):return growl_voice(control,config,seed)
    rng=np.random.default_rng(seed);seconds=len(control)/120;n=round(seconds*RATE)
    rpm=np.interp(np.arange(n)/RATE,np.arange(len(control))/120,control[:,0])
    # Four-stroke V8: one firing per 90 crank degrees, eight per 720 degrees.
    count=np.floor(np.cumsum(rpm)/(60*RATE)*4).astype(np.int64)
    events=np.flatnonzero(np.diff(count,prepend=-1)>0);banks=np.zeros((2,n));records=[]
    order=config['firingOrder'];kernel_time=np.arange(round(.032*RATE))/RATE
    for j,at in enumerate(events):
        cylinder=order[j%8];bank=(cylinder-1)%2
        jitter=rng.uniform(-1,1)*config['timingVariationCrankDegrees']/(rpm[at]*6)
        start=at+round((config['headerDelaysMs'][cylinder-1]/1000+jitter)*RATE)
        if start>=n:continue
        strength=config['cylinderStrength'][cylinder-1]*(1+rng.normal(0,config['pulseStrengthVariation']))
        # Healthy combustion remains present; variation never invents a failed cylinder.
        strength=max(.55,strength)*(1+.10*np.sin(2*np.pi*(j//8)*.37))
        frame=control[min(len(control)-1,at//400)];excitation=max(frame[2],frame[1]*.65)
        strength*=1+max(-.025,excitation-.025)*1.15
        decay=config['pulseDecayMs']/1000*np.clip((config['idleRpm']/rpm[at])**.35,.45,1.15)
        t=kernel_time;rise=config.get('pulseRiseMs',.25)/1000
        envelope=(1-np.exp(-t/rise))*np.exp(-t/decay)
        noise=rng.normal(0,1,len(t));pressure=envelope*(1+noise*config['pulseNoise'])-.28*(1-np.exp(-t/.001))*np.exp(-t/.008)
        length=min(len(pressure),n-start);banks[bank,start:start+length]+=strength*pressure[:length]
        records.append((at/RATE,cylinder,bank,rpm[at],strength))
    banks=np.tanh(banks*.75)
    # Modelled acoustic resonators. These are generated kernels, never measured IRs.
    impulse_time=np.arange(8192)/RATE;response=np.zeros(len(impulse_time))
    for resonator in config['resonators']:
        f=resonator['hz'];q=resonator['q'];r=np.sin(2*np.pi*f*impulse_time)*np.exp(-np.pi*f/q*impulse_time)
        z=np.exp(-2j*np.pi*f*impulse_time);r*=resonator['gain']/max(1e-12,abs((r*z).sum()));response+=r
    fft_size=2**int(np.ceil(np.log2(n+len(response)-1)))
    hz=np.fft.rfftfreq(fft_size,1/RATE);h=np.fft.rfft(response,n=fft_size)
    highpass=(1j*hz/(config['highPassHz']+1j*hz))**2
    lowpass=(config['lowPassHz']/(config['lowPassHz']+1j*hz))**2
    h*=highpass*lowpass
    layers=[]
    for bank in range(2):
        noise=rng.normal(0,config['continuousTurbulence'],n)
        x=np.fft.irfft(np.fft.rfft(banks[bank]+noise,n=fft_size)*h,n=fft_size)[:n]
        layers.append(x)
    # Different bank pickup strengths retain the unequal bank firing rhythm.
    result=layers[0]*config['bankMix'][0]+layers[1]*config['bankMix'][1]
    result-=result.mean()
    return result,np.asarray(records),layers

def growl_voice(control,config,seed):
    """Preview-only pressure source with correlated cycles and separate exhaust paths."""
    rng=np.random.default_rng(seed);n=len(control)*400
    rpm=np.interp(np.arange(n)/RATE,np.arange(len(control))/120,control[:,0])
    count=np.floor(np.cumsum(rpm)/(60*RATE)*4).astype(np.int64)
    events=np.flatnonzero(np.diff(count,prepend=-1)>0)
    # Separate streams keep the source events independent of filter/noise implementation.
    cycle_rng=np.random.default_rng(seed+1009);cycles=np.zeros(len(events)//8+2)
    for i in range(1,len(cycles)):cycles[i]=np.clip(.64*cycles[i-1]+cycle_rng.normal(0,.65),-1.8,1.8)
    banks=np.zeros((2,n));texture_banks=np.zeros((2,n));records=[];fill=max(control[0,2],control[0,1]*.65)
    open_exhaust=config.get('voiceModel')=='open-exhaust'
    last=0;t=np.arange(round(.04*RATE))/RATE
    noise_kernel=np.exp(-np.arange(72)/(RATE*config.get('textureColourMs',.45)/1000));noise_kernel/=np.sqrt(np.sum(noise_kernel**2))
    for j,at in enumerate(events):
        cylinder=config['firingOrder'][j%8];bank=(cylinder-1)%2
        frame=control[min(len(control)-1,at//400)];target=max(frame[2],frame[1]*.65)
        dt=(at-last)/RATE;last=at
        fill+=(target-fill)*(1-np.exp(-dt/(.035 if target>fill else .16)))
        rough=config['cycleVariation']*np.clip(config['idleRpm']/rpm[at],.42,1.0)
        strength=config['cylinderStrength'][cylinder-1]*(1+rough*cycles[j//8]+rng.normal(0,.035))
        strength=max(.55,strength)*(.78+1.1*np.sqrt(max(0,fill)))
        jitter=rng.uniform(-1,1)*config['timingVariationCrankDegrees']/(rpm[at]*6)
        start=at+round((config['headerDelaysMs'][cylinder-1]/1000+jitter)*RATE)
        if start>=n:continue
        decay=config['pulseDecayMs']/1000*(.9+.2*fill)*np.clip((config['idleRpm']/rpm[at])**.12,.78,1.1)
        decay*=1+.07*cycles[j//8]+.04*(cylinder-4.5)/3.5
        rise=config['pulseRiseMs']/1000
        envelope=(1-np.exp(-t/rise))**2*np.exp(-t/decay)
        coloured=np.convolve(rng.normal(0,1,len(t)+len(noise_kernel)-1),noise_kernel,mode='valid')
        # Broad pressure front and recovery, with coloured texture rather than white hiss.
        pulse=envelope if open_exhaust else envelope*(1+coloured*config['pulseNoise'])
        pulse-=.40*(1-np.exp(-t/.0025))*np.exp(-t/(decay*1.8))
        if open_exhaust:
            # A distinct broadband pressure-detail layer avoids passing every sound
            # through the same low-frequency resonances. No constant noise bed.
            taper=np.minimum(1,(len(t)-1-np.arange(len(t)))/(RATE*.006))
            pulse*=taper
            detail=envelope*coloured*(1+.8*fill)*taper
            length=min(len(detail),n-start)
            texture_banks[bank,start:start+length]+=strength*detail[:length]
        length=min(len(pulse),n-start);banks[bank,start:start+length]+=strength*pulse[:length]
        records.append((at/RATE,cylinder,bank,rpm[at],strength))
    layers=[];impulse_t=np.arange(8192)/RATE
    fft_size=2**int(np.ceil(np.log2(n+len(impulse_t)-1)));hz=np.fft.rfftfreq(fft_size,1/RATE)
    for bank in range(2):
        if open_exhaust:
            # Direct pressure plus short, progressively damped reflections: no
            # bank of ringing sine kernels and no steep shared low-pass cascade.
            body=(1j*hz/(config['highPassHz']+1j*hz))**2
            body*=config['lowPassHz']/(config['lowPassHz']+1j*hz)
            detail=(1j*hz/(config['textureHighPassHz']+1j*hz))**2
            detail*=config['textureLowPassHz']/(config['textureLowPassHz']+1j*hz)
            path=np.ones_like(hz,dtype=complex)
            damping=config['reflectionDampingHz']/(config['reflectionDampingHz']+1j*hz)
            for bounce,gain in enumerate([-.18,.08,-.035],start=1):
                path+=gain*damping**bounce*np.exp(-2j*np.pi*hz*config['reflectionMs'][bank]*bounce/1000)
            path*=np.exp(-2j*np.pi*hz*config['outletDelayMs'][bank]/1000)
            source=np.tanh(banks[bank]*.9)
            spectrum=np.fft.rfft(source,n=fft_size)*body
            spectrum+=config['textureMix']*np.fft.rfft(texture_banks[bank],n=fft_size)*detail
            layers.append(np.fft.irfft(spectrum*path,n=fft_size)[:n])
            continue
        response=np.zeros(len(impulse_t))
        for resonator in config['resonators']:
            f=resonator['hz']*config['bankResonanceScale'][bank];q=resonator['q']
            r=np.sin(2*np.pi*f*impulse_t)*np.exp(-np.pi*f/q*impulse_t)
            response+=r*resonator['gain']/max(1e-12,abs((r*np.exp(-2j*np.pi*f*impulse_t)).sum()))
        h=np.fft.rfft(response,n=fft_size)
        h*=(1j*hz/(config['highPassHz']+1j*hz))**2
        h*=(config['lowPassHz']/(config['lowPassHz']+1j*hz))**2
        # Each outlet gets its own causal pressure reflections and soft saturation.
        h*=1-.22*np.exp(-2j*np.pi*hz*config['reflectionMs'][bank]/1000)
        h*=np.exp(-2j*np.pi*hz*config['outletDelayMs'][bank]/1000)
        source=np.tanh(banks[bank]*1.35)
        x=np.fft.irfft(np.fft.rfft(source,n=fft_size)*h,n=fft_size)[:n]
        layers.append(x)
    result=sum(layer*config['bankMix'][i] for i,layer in enumerate(layers))
    result-=result.mean()
    return result,np.asarray(records),layers

def production_baseline(folder,control):
    folder.mkdir();spec=importlib.util.spec_from_file_location('prepare',Path(__file__).with_name('prepare.py'));helper=importlib.util.module_from_spec(spec);spec.loader.exec_module(helper)
    source=(DOMAIN/'EngineSound.cs').read_text(encoding='utf-8-sig')
    asset=(ROOT/'Game/Assets/DragLab/Resources/Data/Vehicles/V8Muscle.asset').read_text(encoding='utf-8-sig');profile=helper.scalar(asset,4)
    for key in ['sound','induction','engineBehavior']:
        section=re.search(r'^    '+key+r':\n(.*?)(?=^    [^ \n]|\Z)',asset,re.M|re.S)[1];profile[key]=helper.scalar(section,6)
        if key=='sound':
            parts=[]
            for block in re.split(r'^        - kind: ',section,flags=re.M)[1:]:
                kind,rest=block.split('\n',1);p=helper.scalar(rest,10);p['kind']=int(kind);parts.append(p)
            profile[key]['exhaustSystem']={'version':1,'parts':parts}
    # A hypothetical 5.7-litre old V8 for both voices, not an authored-car edit.
    profile.update(displacementLitres=5.7,idleRpm=750);profile['audition']={'topology':0,'exhaustPreset':-1,'exhaustStage':0,'valve':1}
    (folder/'profile.json').write_text(json.dumps(profile,indent=2),encoding='utf-8');control.astype('<f8').tofile(folder/'trace.f64')
    enums=source[source.index('    public enum InductionType'):source.index('    [Serializable]')]
    prefix=helper.extract(source,'    public sealed class EngineSoundProfile').split('        public EngineSoundProfile(')[0]
    common='using System;using System.Linq;namespace DragLab.Domain{'+enums+'public enum FuelSystem{Injection,Carburetor,Diesel} public enum CutMethod{Factory,Fuel,Ignition} public static class DragMath{public static double Clamp(double x,double lo,double hi)=>Math.Max(lo,Math.Min(hi,x));}'+prefix+helper.ADAPTER+helper.extract(source,'    public sealed class EngineSoundFrame')+helper.extract(source,'    public sealed class EngineSoundSynth')+'}'
    orders=re.search(r'        static readonly int\[\]\[\] orders=.*?;',(DOMAIN/'EngineElements.cs').read_text(encoding='utf-8-sig'),re.S)[0]
    common+='namespace DragLab.Domain.Mechanics{public static class EngineElements{'+orders+'public static int[] FiringOrder(int n)=>orders[Math.Clamp(n,1,16)-1];}}'
    (folder/'CurrentDsp.cs').write_text(common,encoding='utf-8')
    for name in ['ExhaustAcoustics.cs','ExhaustPopSynth.cs']:(folder/name).write_bytes((DOMAIN/name).read_bytes())
    (folder/'Program.cs').write_bytes(Path(__file__).with_name('ReferenceBaselineRenderer.cs').read_bytes())
    (folder/'Preview.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>',encoding='utf-8')
    (folder/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>',encoding='utf-8')
    env=os.environ.copy();env.update(DOTNET_CLI_HOME=str(folder/'.dotnet'),DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_NOLOGO='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
    subprocess.run(['C:/Program Files/dotnet/dotnet.exe','run','-c','Release','--project',str(folder/'Preview.csproj'),'--',str(folder)],check=True,env=env)
    return np.fromfile(folder/'current.f32',dtype='<f4').astype(float)

def plot(path,series):
    im=Image.new('RGB',(1100,510),'#151b20');d=ImageDraw.Draw(im);colors=['#aeb8c2','#f4626b','#60c8cf']
    font=Path('C:/Windows/Fonts/segoeui.ttf')
    if font.exists():d.font=ImageFont.truetype(str(font),14)
    labels=['10–40','40–80','80–160','160–320','320–640','640–1280','1280–2560','2560–6000']
    d.text((24,18),'Energy by frequency band — fraction of 10–6000 Hz energy',fill='white')
    for i,(name,stats) in enumerate(series):d.text((24+i*330,48),name,fill=colors[i])
    for b,label in enumerate(labels):
        x=60+b*130;d.text((x-12,465),label,fill='#c9d1d9')
        for j,(_,stats) in enumerate(series):
            height=stats['bands'][b]/.9*345;left=x+j*25;d.rectangle((left,450-height,left+19,450),fill=colors[j])
    d.text((24,490),'Hz · shapes support comparison; they do not establish perceptual similarity or exact engine RPM.',fill='#aab4bd');im.save(path)

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--reference',type=Path,required=True);ap.add_argument('--output',type=Path,required=True)
    ap.add_argument('--config',type=Path,default=Path(__file__).with_name('reference-pressure-profile.json'))
    ap.add_argument('--previous',type=Path,help='Earlier audition folder for level-matched listening comparisons')
    args=ap.parse_args()
    out=args.output.resolve();assert out.is_relative_to(ROOT/'reports')
    if out.exists():raise RuntimeError('Choose a new output directory; preserve earlier candidates.')
    out.mkdir(parents=True);config=json.loads(args.config.read_text(encoding='utf-8-sig'))
    level=lambda x:normalise(x,config.get('playbackRms',.12))
    watched=[DOMAIN/n for n in ['EngineSound.cs','EngineElements.cs','ExhaustAcoustics.cs','ExhaustPopSynth.cs']]+[ROOT/'Game/Assets/DragLab/Resources/Data/Vehicles/V8Muscle.asset'];hashes={str(p.relative_to(ROOT)):sha(p) for p in watched}
    ref,ref_rate=read_wav(args.reference);assert abs(len(ref)/ref_rate-30)<.01
    ref_stats,_,_=features(ref,ref_rate);steady_stats,_,_=features(ref[5*ref_rate:20*ref_rate],ref_rate);control=trace('idle',30,config['seed'],config['idleRpm'])
    original=production_baseline(out/'production-dsp',control)
    proposed,events,banks=pressure_voice(control,config,config['seed']);repeat,_,_=pressure_voice(control,config,config['seed']);other,_,_=pressure_voice(control,config,491)
    assert np.array_equal(proposed,repeat) and not np.array_equal(proposed,other)
    assert np.isfinite(proposed).all() and (events[:,4]>.4).all()
    assert events[:,1].astype(int).tolist()==[config['firingOrder'][i%8] for i in range(len(events))]
    expected=float(control[:,0].mean()/15*30);assert abs(len(events)-expected)<2
    assert max(abs(np.bincount(events[:,2].astype(int))-len(events)/2))<3
    low,low_events,_=pressure_voice(control[:120],config,117);loaded=control[:120].copy();loaded[:,1:]=[1,.9]
    high,high_events,_=pressure_voice(loaded,config,117)
    assert np.array_equal(low_events[:,:4],high_events[:,:4]) and np.sqrt(np.mean(high*high))>np.sqrt(np.mean(low*low))*1.1
    # Only measurement/playback level comes from the reference; not waveform or phase.
    ref48=np.interp(np.arange(30*RATE)/RATE,np.arange(len(ref))/ref_rate,ref)
    voices={'reference':level(ref48),'current':level(original),'prototype':level(proposed)}
    stats={}
    for name,x in voices.items():write_wav(out/(name+'.wav'),x);stats[name]=features(x,RATE)[0]
    rev_control=trace('rev',12,config['seed'],config['idleRpm']);rev,rev_events,_=pressure_voice(rev_control,config,config['seed']);write_wav(out/'prototype-revs.wav',level(rev))
    comparison={}
    if args.previous:
        for scene,file,new in [('idle','prototype.wav',voices['prototype']),('revs','prototype-revs.wav',level(rev))]:
            old,rate=read_wav(args.previous/file);assert rate==RATE and len(old)==len(new)
            write_wav(out/('previous-'+scene+'.wav'),level(old))
            write_wav(out/('previous-then-revised-'+scene+'.wav'),np.concatenate([level(old[:12*RATE]),np.zeros(33600),level(new[:12*RATE])]))
            prior=features(old,RATE)[0];revised=features(new,RATE)[0]
            # Same RPM and firing schedule; timbre changes must not pitch-shift the engine.
            if scene=='idle':
                prior_events=np.loadtxt(args.previous/'events.csv',delimiter=',',skiprows=1)
                assert np.array_equal(prior_events[:,:4],events[:,:4])
            if config.get('voiceModel')=='open-exhaust':
                assert revised['centroidHz']<320,'Direct-path audition became excessively bright'
                assert .005<sum(revised['bands'][5:])<.10,'Pressure-detail layer is buried or excessive'
            elif config.get('voiceModel')=='bank-growl':
                assert revised['centroidHz']<170,'Growl preview lost its darker balance'
                assert sum(revised['bands'][5:])<.01,'Too much upper-band energy for this audition'
            else:
                assert revised['centroidHz']<prior['centroidHz']*.85
                assert sum(revised['bands'][5:])<sum(prior['bands'][5:])*.35
            comparison[scene]={'previous':prior,'revised':revised,'sameControlTrace':True}
    write_wav(out/'current-then-prototype.wav',np.concatenate([voices['current'][:12*RATE],np.zeros(33600),voices['prototype'][:12*RATE]]))
    write_wav(out/'reference-then-prototype.wav',np.concatenate([level(ref48[5*RATE:17*RATE]),np.zeros(33600),level(voices['prototype'][:12*RATE])]))
    assert all(sha(p)==hashes[str(p.relative_to(ROOT))] for p in watched)
    before_error=float(abs(np.array(stats['current']['bands'])-np.array(steady_stats['bands'])).sum());after_error=float(abs(np.array(stats['prototype']['bands'])-np.array(steady_stats['bands'])).sum())
    assert after_error<before_error*.4,'Coarse spectral shape did not improve toward the supplied reference'
    metadata={'referenceURL':'https://www.youtube.com/watch?v=2CJ1jBMwR38','referenceRangeSeconds':[0,30],'steadyAnalysisRangeSeconds':[5,20],'comparisonReferenceRangeSeconds':[5,17],'referenceInputSha256':sha(args.reference),'config':config,'controls':'same independently generated idle RPM trace for current/prototype; nominal 750 RPM is a modelling choice, not measured truth','events':len(events),'sameSeedExact':True,'differentSeedChangesOutput':True,'loadChangesStrengthNotTiming':True,'sourceHashes':hashes,'measurements':stats,'revEventCount':len(rev_events),'referenceRawMeasurements':ref_stats,'referenceSteadyMeasurements':steady_stats,'bandL1Error':{'current':before_error,'prototype':after_error}}
    metadata['revisionComparison']=comparison
    (out/'analysis.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8');np.savetxt(out/'events.csv',events,delimiter=',',header='seconds,cylinder,bank,rpm,strength',comments='')
    plot(out/'bands.png',[('reference 5–20 s',steady_stats),('current',stats['current']),('prototype',stats['prototype'])])
    rows=''.join('<section><h2>'+title+'</h2><audio controls preload="none" src="'+file+'"></audio><p>'+detail+'</p></section>' for title,file,detail in [('Reference · first 30 seconds','reference.wav','Your supplied recording, downmixed and level-matched for this local analysis.'),('Current production synth · same idle input','current.wav','Unmodified game DSP, hypothetical 5.7 L profile, same RPM/throttle/load control trace as the prototype.'),('Generated pressure-pulse prototype','prototype.wav','Fresh seeded excitation, eight cylinders/two banks, modelled damped exhaust resonances. No reference samples used.'),('Prototype · gentle neutral blips','prototype-revs.wav','An extrapolation: the supplied excerpt is idle only. Actual firing-event rate follows RPM.')])
    if comparison:
        rows='<section><h2>Previous preview → revised voice</h2><p>12 seconds each, with a short gap; equal RMS playback. RPM and firing order are unchanged.</p><p>Idle</p><audio controls preload="none" src="previous-then-revised-idle.wav"></audio><p>Neutral revs</p><audio controls preload="none" src="previous-then-revised-revs.wav"></audio></section>'+rows
    (out/'index.html').write_text('<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Old V8 reference synthesis</title><style>body{max-width:1000px;margin:35px auto;padding:0 20px;background:#151b20;color:#e6eaed;font:17px system-ui;line-height:1.6}section{background:#232b33;border:1px solid #67737e;border-radius:10px;margin:22px 0;padding:20px}h1{border-bottom:2px solid #ed233d}audio{width:100%;max-width:620px}p{color:#bdc7d1}img{max-width:100%}a{color:#ff9ca7}</style><h1>Old V8 · reference-led procedural audition</h1><p>Preview only. In-game sound is unchanged. Listen to the reference, current generator and new pressure model at matched RMS level. The recording does not reveal exact RPM, cam, pipes or microphone response. The whole 0–30 s excerpt is retained; steadier 5–20 s is used for the chart.</p>'+rows+'<img src="bands.png" alt="Reference/current/prototype frequency-band energy"><p><a href="reference-then-prototype.wav">Reference 5–17 s → prototype, 12 seconds each</a> · <a href="current-then-prototype.wav">Current → prototype, 12 seconds each</a></p><script>document.addEventListener("play",e=>{for(const a of document.querySelectorAll("audio"))if(a!==e.target)a.pause()},true)</script></html>',encoding='utf-8')
    (out/'verification.txt').write_text('PASS: exact 30-second reference, source checksum retained.\nPASS: deterministic fresh synthesis and private-seed variation.\nPASS: eight-cylinder firing order, balanced bank counts, RPM-linked event count.\nPASS: higher load changes pulse strength without changing firing times.\nPASS: finite samples, matched RMS playback, no clipped export.\nPASS: coarse spectral-band error improves against steady reference.\nPASS: production source/asset bytes unchanged.\nHuman listening approval and real-time/mobile DSP performance remain unverified.\n',encoding='utf-8')
    if comparison:
        with (out/'verification.txt').open('a',encoding='utf-8') as f:
            f.write('PASS: idle firing schedule unchanged from previous audition.\n')
            if config.get('voiceModel')=='open-exhaust':f.write('PASS: idle/rev pressure-detail fraction 0.5-10%; spectral centroid below 320 Hz.\n')
            else:f.write('PASS: idle/rev darker-balance ceilings (centroid <170 Hz; upper-band fraction <1%).\n' if config.get('voiceModel')=='bank-growl' else 'PASS: idle and rev spectral centroids reduced by at least 15%; energy above 640 Hz reduced by at least 65%.\n')
    print('PASS:',len(events),'idle firings; independent private-seed replay and source immutability.');print(json.dumps(stats,indent=2));print(out/'index.html')

if __name__=='__main__':main()
