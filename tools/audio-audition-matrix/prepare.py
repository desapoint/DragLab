"""Create a local, isolated audio review matrix. Never writes Game/ or player state."""
from pathlib import Path
import argparse, copy, hashlib, json, re

ROOT=Path(__file__).resolve().parents[2]
DOMAIN=ROOT/'Game/Assets/DragLab/Scripts/Domain'
LABELS=['Naturally aspirated','Single turbo','Supercharger','Twin turbo','Twin supercharger','Twincharged']
VALVES=['Factory','Recirculated','Hiss','Chirp','Flutter']
EXHAUSTS=['Factory','Quiet OEM preset','Chambered','Absorptive sport','Open']

def extract(text, declaration):
    start=text.index(declaration);at=text.index('{',start)+1;depth=1
    while depth:
        depth+=(text[at]=='{')-(text[at]=='}');at+=1
    return text[start:at]

def scalar(section, indent):
    return {k:float(v) for k,v in re.findall(r'^'+' '*indent+r'(\w+):\s*(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)\s*$',section,re.M)}

def assets():
    result=[]
    for path in sorted((ROOT/'Game/Assets/DragLab/Resources/Data/Vehicles').glob('*.asset')):
        text=path.read_text(encoding='utf-8-sig');p=scalar(text,4)
        p['key']=path.stem;p['name']=re.search(r'^    displayName: (.+)$',text,re.M)[1]
        for key in ['sound','induction','engineBehavior','combustion']:
            section=re.search(r'^    '+key+r':\n(.*?)(?=^    [^ \n]|\Z)',text,re.M|re.S)[1]
            p[key]=scalar(section,6)
            if key=='sound':
                parts=[]
                for block in re.split(r'^        - kind: ',section,flags=re.M)[1:]:
                    kind,rest=block.split('\n',1);item=scalar(rest,10);item['kind']=int(kind);parts.append(item)
                p[key]['exhaustSystem']={'version':1,'parts':parts}
        result.append(p)
    assert len(result)==13,'Revisit matrix coverage when authored vehicles change.'
    return result

def candidate(engine):
    source=extract(engine,'    public sealed class EngineSoundSynth').replace('EngineSoundSynth','AuditionSynth')
    def change(before,after):
        nonlocal source
        assert source.count(before)==1,before
        source=source.replace(before,after)
    change('uint random = 0x28a7c5u;', '''uint random = 0x28a7c5u;
        uint organicRandom=117;
        double organicGain=1,organicTarget=1,clatterLow,compressorAir;
        double OrganicNoise(){organicRandom^=organicRandom<<13;organicRandom^=organicRandom>>17;organicRandom^=organicRandom<<5;return organicRandom/(double)uint.MaxValue*2-1;}
        static double DeepBank(double cycle,double load){double bank=Tau*cycle*2;double uneven=1+.16*Math.Sin(Tau*cycle)+.07*Math.Sin(Tau*cycle*3);double body=Math.Sin(bank)+.20*Math.Sin(bank*2+.45)+.07*Math.Sin(bank*3+.8);return Math.Tanh(body*(1.65+load*.65))*.74*uneven;}''')
    change('public AuditionSynth(EngineSoundProfile profile) {','public AuditionSynth(EngineSoundProfile profile,uint seed=117) { organicRandom=seed;')
    change('bool largeVoice = profile.cylinders >= 8;','bool largeVoice = profile.cylinders >= 8; bool deepV8=profile.cylinders==8&&profile.layout==EngineLayout.Crossplane;')
    change('firingPhase = Advance(firingPhase, firing, sampleRate);','''double previousFiring=firingPhase; firingPhase = Advance(firingPhase, firing, sampleRate);
                if(firingPhase<previousFiring)organicTarget=1+OrganicNoise()*.025;
                organicGain+=(organicTarget-organicGain)*(1-Math.Exp(-1.0/(sampleRate*.002)));''')
    change('(carbVoice || largeVoice ? .20 : .25)','(deepV8 ? .085 : carbVoice || largeVoice ? .20 : .25)')
    change('EngineLayout.Crossplane ? .42','EngineLayout.Crossplane ? (deepV8?.19:.42)')
    change('(1 - voiceThrottle * .12 - twoStepMix * .22)','(1 - voiceThrottle * .12 + (deepV8?voiceLoad*.16:0) - twoStepMix * .22)')
    change('(.13 + (carbVoice ? .07 : 0)) * bass * Math.Sin(Tau * crankPhase * Math.Max(1, profile.cylinders / 4))','(deepV8 ? (.45+voiceLoad*.42)*bass*DeepBank(crankPhase,voiceLoad) : (.13 + (carbVoice ? .07 : 0)) * bass * Math.Sin(Tau * crankPhase * Math.Max(1, profile.cylinders / 4)))')
    change('(.7 + voiceLoad * .3)','(.7 + voiceLoad * (deepV8?.55:.3))')
    change('(.15 * Math.Sin(phase * 2) + .075 * Math.Sin(phase * 3))','((deepV8?.035:.15) * Math.Sin(phase * 2) + (deepV8?.015:.075) * Math.Sin(phase * 3))')
    change('low += voiceFilter * (pulse - low);','''pulse+=voiceLoad*(deepV8?.08:.035)*(Math.Tanh(pulse*1.35)-pulse);pulse*=organicGain;
                low += voiceFilter * (pulse - low);''')
    change('outsideExhaust += Math.Sin(Tau * clatterPhase) * Math.Pow(Math.Max(0, Math.Cos(Tau * crankPhase * 2)), 12) * profile.mechanicalClatter * .004 * frame.volume;','''double clatter=Math.Sin(Tau*clatterPhase)*Math.Pow(Math.Max(0,Math.Cos(Tau*crankPhase*2)),dieselVoice?12:6);
                clatterLow+=(1-Math.Exp(-Tau*950/sampleRate))*(clatter-clatterLow);
                outsideExhaust+=(dieselVoice?clatter*.004:clatterLow*.0005)*profile.mechanicalClatter*frame.volume;''')
    change('double whistle = Math.Sin(Tau * turboPhase);','''compressorAir+=(1-Math.Exp(-Tau*1700/sampleRate))*(noise-compressorAir);
                    double whistle = Math.Sin(Tau * turboPhase);''')
    change('value += whistle * .09 * profile.turboGain','value += (.60*whistle+.07*Math.Sin(Tau*turboPhase*2)+.32*compressorAir) * .09 * profile.turboGain')
    change('value += whine * .15 * profile.chargerGain','value += (.82*whine+(hz*3<sampleRate*.42?.06*Math.Sin(Tau*chargerPhase*3):0)) * .15 * profile.chargerGain')
    return 'using System;\nnamespace DragLab.Domain {\n'+source+'\n}\n'

ADAPTER=r'''
        public EngineSoundProfile(System.Text.Json.JsonElement a) {
            var s=a.GetProperty("sound");var b=a.GetProperty("engineBehavior");var d=a.GetProperty("induction");var o=a.GetProperty("audition");
            cylinders=(int)N(s,"cylinders",4);litres=N(a,"displacementLitres",N(s,"displacementLitres",1.6));layout=(EngineLayout)N(s,"layout");
            induction=(InductionType)N(o,"topology");turboCount=induction==InductionType.TwinTurbo?2:induction==InductionType.Turbo||induction==InductionType.Twincharged?1:0;
            superchargerCount=induction==InductionType.TwinSupercharger?2:induction==InductionType.Supercharger||induction==InductionType.Twincharged?1:0;
            exhaust=(int)N(o,"exhaustStage");engineStage=0;valve=(BlowOffType)N(o,"valve",1);
            boostBar=(turboCount>0?N(d,"turboBoostBar",.7):N(d,"superchargerBoostBar",.5))*N(o,"boostScale",1);
            spoolSeconds=N(d,"turboSpoolSeconds",.65);chargerRatio=N(s,"superchargerRatio",18)*N(o,"pulley",1);
            engineGain=N(s,"engineGain",.8);turboGain=N(s,"turboGain",.24);chargerGain=N(s,"superchargerGain",.28);releaseGain=N(s,"blowOffGain",.55);
            idleRpm=N(a,"idleRpm",850);redlineRpm=N(a,"redlineRpm",6500);fueling=(FuelSystem)N(b,"fueling");
            camOverlap=N(b,"camOverlap");mixtureVariation=N(b,"mixtureVariation");mechanicalClatter=N(b,"mechanicalClatter");
            bassGain=N(s,"exhaustBassGain",1);if(bassGain<=0)bassGain=1;voiceBrightness=N(s,"exhaustBrightness",1);if(voiceBrightness<=0)voiceBrightness=1;
            rawPulseGain=N(s,"rawPulseGain",1);if(rawPulseGain<=0)rawPulseGain=1;popGain=N(s,"exhaustPopGain",1);popSharpness=N(s,"exhaustPopSharpness",1);popDecay=N(s,"exhaustPopDecay",1);
            idleBoostGain=Math.Pow(10,N(s,"idleBoostDb",6)/20);
            int preset=(int)N(o,"exhaustPreset",-1);exhaustSystem=preset<0?System.Text.Json.JsonSerializer.Deserialize<ExhaustSetup>(s.GetProperty("exhaustSystem").GetRawText(),new System.Text.Json.JsonSerializerOptions{IncludeFields=true}):ExhaustSetup.Preset(preset);
        }
        static double N(System.Text.Json.JsonElement e,string k,double fallback=0)=>e.TryGetProperty(k,out var v)?v.GetDouble():fallback;
        public double FiringHz(double rpm)=>rpm*cylinders/120;
    }
'''

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,default=ROOT/'reports/audio-matrix-20261015');args=parser.parse_args()
    out=args.output.resolve();out.mkdir(parents=True,exist_ok=True)
    assert out.is_relative_to(ROOT/'reports'),'Audition output must stay under reports/.'
    engine=(DOMAIN/'EngineSound.cs').read_text(encoding='utf-8-sig')
    prefix=extract(engine,'    public sealed class EngineSoundProfile').split('        public EngineSoundProfile(')[0]
    common='using System;\nusing System.Linq;\nnamespace DragLab.Domain {\n'+engine[engine.index('    public enum InductionType'):engine.index('    [Serializable]')]
    common+='public enum FuelSystem {Injection,Carburetor,Diesel}\npublic enum CutMethod{Factory,Fuel,Ignition}\npublic static class DragMath{public static double Clamp(double x,double lo,double hi)=>Math.Max(lo,Math.Min(hi,x));}\n'
    common+=prefix+ADAPTER+extract(engine,'    public sealed class EngineSoundFrame')+'\n'+extract(engine,'    public sealed class EngineSoundSynth')+'\n}\n'
    # Actual order values retained; all healthy matrix frames have equal cylinder contribution.
    elements=(DOMAIN/'EngineElements.cs').read_text(encoding='utf-8-sig')
    order=re.search(r'        static readonly int\[\]\[\] orders=.*?;',elements)[0]
    common+='namespace DragLab.Domain.Mechanics {public static class EngineElements{'+order+'public static int[] FiringOrder(int n)=>orders[Math.Clamp(n,1,16)-1];}}\n'
    (out/'CurrentDsp.cs').write_text(common,encoding='utf-8');(out/'AuditionSynth.cs').write_text(candidate(engine),encoding='utf-8')
    for name in ['ExhaustAcoustics.cs','ExhaustPopSynth.cs']:(out/name).write_bytes((DOMAIN/name).read_bytes())
    (out/'Program.cs').write_bytes((Path(__file__).parent/'Renderer.cs').read_bytes())
    (out/'Preview.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup></Project>',encoding='utf-8')
    (out/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>',encoding='utf-8')
    rows=[];seen=set();vehicles=assets()
    def add(p,fuel,topology,exhaust,group='Engine combinations',size=1,pulley=1,valve=1,boost=1):
        key=(p['key'],fuel,topology,exhaust,size,pulley,valve,boost)
        if key in seen:return
        seen.add(key);profile=copy.deepcopy(p)
        if fuel in ['Throttle-body EFI','Port EFI']:
            profile['engineBehavior'].update(fueling=0,mixtureVariation=.045 if fuel=='Throttle-body EFI' else .025,idleControl=.88 if fuel=='Throttle-body EFI' else .96)
        profile['audition']={'topology':topology,'exhaustPreset':exhaust-1,'exhaustStage':0,'valve':valve,'size':size,'pulley':pulley,'boostScale':boost}
        cyl=int(profile['sound']['cylinders']);layout=['Inline','V','Crossplane'][int(profile['sound']['layout'])]
        idx=f'{len(rows)+1:04d}'
        rows.append({'id':idx,'engine':p['name'],'engineKey':p['key'],'cylinders':cyl,'layout':layout,'fuel':fuel,'fuelKind':'Diesel' if fuel=='Diesel common rail' else 'Petrol','induction':LABELS[topology],'exhaust':EXHAUSTS[exhaust],'turboSize':size,'pulley':pulley,'boostScale':boost,'valve':VALVES[valve] if topology in [1,3,5] and fuel!='Diesel common rail' else 'Not applicable','group':group,'profile':profile})
        originalFuel=['Factory injection','Carburetor','Diesel common rail'][int(p['engineBehavior']['fueling'])]
        rows[-1]['factoryReference']=not p['key'].startswith(('lab-','layout-')) and fuel==originalFuel and topology==int(p['induction']['factory']) and exhaust==0 and size==1 and pulley==1 and boost==1 and (valve==int(p['induction']['factoryValve']) or topology not in [1,3,5])
    for p in vehicles:
        kind=int(p['engineBehavior']['fueling'])
        fuels=['Carburetor','Throttle-body EFI','Port EFI'] if kind==1 else ['Diesel common rail' if kind==2 else 'Factory injection']
        for fuel in fuels:
            for topology in range(6):
                for exhaust in range(5):add(p,fuel,topology,exhaust)
            for topology in [1,3,5]:
                for size in [.75,1,1.4]:
                    for valve in ([1] if kind==2 else [1,2,3,4]):add(p,fuel,topology,0,'Turbo size and release',size=size,valve=valve)
            for topology in [2,4,5]:
                for pulley in [.85,.9,1,1.2]:add(p,fuel,topology,0,'Pulley range',pulley=pulley)
            for topology in [1,3,5]:
                for boost in [.8,1,1.2]:add(p,fuel,topology,0,'Boost range',boost=boost)
    # Supplemental generator coverage: not additional buyable engines or validated mechanical builds.
    for count in range(1,17):
        p=copy.deepcopy(next(p for p in vehicles if p['key']=='StarterHatch'))
        p.update(key=f'lab-{count:02}',name=f'Laboratory {count}-cylinder',displacementLitres=min(12,count*.5),idleRpm=850,redlineRpm=6500)
        p['sound'].update(cylinders=count,layout=0 if count<=6 else 1)
        for fuel,kind in [('Carburetor',1),('Factory injection',0),('Diesel common rail',2)]:
            p['engineBehavior']['fueling']=kind;p['combustion']['fuelKind']=1 if kind==2 else 0;add(p,fuel,0,0,'Cylinder laboratory')
    for count,layout in [(6,1),(8,1),(8,2),(10,1),(12,1),(16,1)]:
        p=copy.deepcopy(next(p for p in vehicles if p['key']=='StarterHatch'))
        p.update(key=f'layout-{count}-{layout}',name=f'Laboratory {"crossplane" if layout==2 else "V"}{count}',displacementLitres=min(12,count*.5))
        p['sound'].update(cylinders=count,layout=layout);add(p,'Factory injection',0,0,'Layout laboratory')
    assert len([r for r in rows if r['factoryReference']])==13
    assert len([r for r in rows if r['group']=='Engine combinations'])==17*6*5
    assert {r['cylinders'] for r in rows if r['group']=='Cylinder laboratory'}==set(range(1,17))
    for p in vehicles:
        own=[r for r in rows if r['engineKey']==p['key']]
        assert {r['induction'] for r in own}==set(LABELS)
        assert {r['exhaust'] for r in own}==set(EXHAUSTS)
        if p['engineBehavior']['fueling']==2:assert {r['fuelKind'] for r in own}=={'Diesel'} and {r['valve'] for r in own}=={'Not applicable'}
    (out/'cases.json').write_text(json.dumps(rows,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    paths=[DOMAIN/'EngineSound.cs',DOMAIN/'ExhaustAcoustics.cs',DOMAIN/'ExhaustPopSynth.cs',DOMAIN/'EngineElements.cs',DOMAIN/'FuelingHardware.cs',DOMAIN/'CatalogVariants.cs']+sorted((ROOT/'Game/Assets/DragLab/Resources/Data/Vehicles').glob('*.asset'))
    hashes={str(p.relative_to(ROOT)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
    (out/'source-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
    print(json.dumps({'cases':len(rows),'authored_engines':len(vehicles),'groups':{g:sum(r['group']==g for r in rows) for g in sorted({r['group'] for r in rows})}},indent=2))

if __name__=='__main__':main()
