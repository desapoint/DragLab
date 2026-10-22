'use strict';
// Build local workbench auditions and verify controlled signal substitutions.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict'),crypto=require('node:crypto'),cp=require('node:child_process');
const dir=__dirname,root=path.resolve(dir,'../..'),source=path.join(root,'reports/unity-audio-reference-20261021');
const out=path.resolve(process.argv[2]||path.join(root,'reports/audio-signal-diagnostics-20261021'));fs.mkdirSync(out,{recursive:true});
const read=f=>fs.readFileSync(path.join(dir,f),'utf8'),factory=(f,n)=>vm.runInThisContext(read(f)+'\n'+n);
const D=factory('draglab_dsp_core_drivetrain.js','createDragLabDSP')(),R=factory('unity_reference_replay.js','createUnityReferenceReplay')(D),G=factory('audio_signal_diagnostics.js','createAudioSignalDiagnostics')(D,R);
const html=read('DragLab_Audio_Workbench_DrivetrainFix.html');
const preset=new Function('DSP',`const stockParts=stage=>DSP.standardParts(stage).map(kind=>({kind,...DSP.partDefaults[kind],amount:1}));${html.slice(html.indexOf('function tempest(){'),html.indexOf('\nconst clamp='))};return presetById;`)(D);
const hash=x=>crypto.createHash('sha256').update(typeof x==='string'?x:JSON.stringify(x)).digest('hex'),copy=x=>JSON.parse(JSON.stringify(x));
const log=[],allRows=[];let checks=0;function pass(label){checks++;log.push('PASS '+label);console.log(log.at(-1));}
function render(engine,n){const l=new Float32Array(n),r=new Float32Array(n);engine.render(l,r);for(let i=0;i<n;i++)assert(Number.isFinite(l[i])&&Number.isFinite(r[i])&&Math.abs(l[i])<1&&Math.abs(r[i])<1,'invalid or clipped PCM');return{l,r};}
function measure(x){let sum=0,peak=0;for(let i=0;i<x.l.length;i++){sum+=x.l[i]**2+x.r[i]**2;peak=Math.max(peak,Math.abs(x.l[i]),Math.abs(x.r[i]));}return{rms:Math.sqrt(sum/(x.l.length*2)),peak};}
function delta(a,b){let sum=0;for(let i=0;i<a.l.length;i++)sum+=(a.l[i]-b.l[i])**2+(a.r[i]-b.r[i])**2;return Math.sqrt(sum/(a.l.length*2));}
function wav(name,x){const b=Buffer.alloc(44+x.l.length*6);b.write('RIFF');b.writeUInt32LE(b.length-8,4);b.write('WAVEfmt ',8);b.writeUInt32LE(16,16);b.writeUInt16LE(1,20);b.writeUInt16LE(2,22);b.writeUInt32LE(48000,24);b.writeUInt32LE(288000,28);b.writeUInt16LE(6,32);b.writeUInt16LE(24,34);b.write('data',36);b.writeUInt32LE(b.length-44,40);for(let i=0;i<x.l.length;i++){b.writeIntLE(Math.round(x.l[i]*8388607),44+i*6,3);b.writeIntLE(Math.round(x.r[i]*8388607),47+i*6,3);}fs.writeFileSync(path.join(out,name),b);}
function concatenate(clips){const count=clips.reduce((n,x)=>n+x.l.length,0)+48000*(clips.length-1),l=new Float32Array(count),r=new Float32Array(count);let at=0;for(const x of clips){l.set(x.l,at);r.set(x.r,at);at+=x.l.length+48000;}return{l,r};}
for(const [id,kind]of [['03-i4-turbo-hiss','tempest'],['08-v8-carb','v8']]){
 const trace=JSON.parse(fs.readFileSync(path.join(source,id+'.audio-trace.json'),'utf8')),inputs=JSON.parse(fs.readFileSync(path.join(source,id+'.race.json'),'utf8')),p=preset(kind),before=hash({trace,inputs,p}),n=trace.totalSamples;
 const capture=G.beginCapture(p,trace,inputs,'Race'),baseline=render(capture,n),timeline=G.finishCapture(capture,p,trace,'Race');
 const timelineHash=hash(timeline),items=[],rows=[],rev=[],shift=[];let previous=null;
 for(const stage of G.passes){
  const engine=G.createEngine(p,trace,timeline,stage.level),pcm=render(engine,n),stats=measure(pcm),filename=id+'.signal-'+stage.id+'.wav';wav(filename,pcm);
  const row={id,pass:stage.id,title:stage.title,description:stage.description,...stats,differenceRms:previous?delta(previous,pcm):0,soundSettingsSha256:hash(p)};
  rows.push(row);items.push({url:filename,filename});
  if(previous){const name=id+'.previous-to-'+stage.id+'.wav';wav(name,concatenate([previous,pcm]));items.at(-1).previousUrl=name;}
  for(const [list,start,end]of [[rev,5,9],[shift,14,19]])list.push({l:pcm.l.subarray(start*48000,end*48000),r:pcm.r.subarray(start*48000,end*48000)});
  if(stage.level===0){assert.deepEqual(pcm,baseline);pass(id+' cached browser baseline is sample-identical to direct driver replay under the same engine-only mask');}
  if(stage.level===12){const reference=G.createEngine(p,trace,timeline,13);render(reference,n);assert.deepEqual(engine.referenceEvents,reference.referenceEvents);pass(id+' actual reaction/BOV time, indices and seeds preserved in restored-effects pass');}
  const allowed=['tires',...(stage.level<9?['intake','turboWhistle','turboFlow','turboTurbine','blower','mechanical','clatter']:[]),...(stage.level<11?['afterPressure','afterTexture','afterPower','afterTravel','afterShell','bov']:[]),...(stage.level<12?['distance','air','reflections','stereo']:[])];
  const q=copy(engine.p);for(const key of Object.keys(p))if(key!=='stages')assert.deepEqual(q[key],p[key],id+' changed acoustic setting '+key);for(const key of Object.keys(p.stages))if(!allowed.includes(key))assert.equal(q.stages[key],p.stages[key]);
  if(stage.level<13){const probe=G.createEngine(p,trace,timeline,stage.level);probe.publish(15*48000);const b=timeline.frames[probe.browserCursor],u=trace.frames[probe.unityCursor].engine;
   for(const [key,at]of [['rpm',1],['throttle',2],['load',3],['spool',4],['cut',8],['lastThrottle',10]])assert.equal(probe[key],stage.level>=at?(key==='lastThrottle'?0:key==='spool'?u.turboSpool:u[key]):b[key]);
   assert.equal('airflow' in probe.diagnosticCoreInputs,stage.level>=5);assert.equal('cylinderContribution' in probe.diagnosticCoreInputs,stage.level>=6);assert.equal(probe.diagnosticGainFraction,stage.level>=7?u.combustionFraction:undefined);
  }
  previous=pcm;console.log('RENDER '+id+' '+stage.id+' RMS '+stats.rms.toFixed(6));
 }
 assert.equal(hash(timeline),timelineHash);assert.equal(hash({trace,inputs,p}),before);pass(id+' all 14 passes retain tuning, source timeline and original inputs without mutation');
 // Restart/re-render, excluded signals and exact settings cache validation.
 const a=G.createEngine(p,trace,timeline,3),b=G.createEngine(p,trace,timeline,3);assert.deepEqual(render(a,90000),render(b,90000));pass(id+' repeated load-stage render is deterministic');
 const altered=copy(trace);for(const f of altered.frames){f.engine.airflowKgS+=10;f.engine.cylinderContribution=f.engine.cylinderContribution.map(()=>0);f.engine.combustionFraction=.2;}
 assert.deepEqual(render(G.createEngine(p,trace,timeline,2),90000),render(G.createEngine(p,altered,timeline,2),90000));pass(id+' later airflow/combustion inputs cannot contaminate earlier substitutions');
 assert.throws(()=>G.createEngine({...p,engineGain:p.engineGain+.01},trace,timeline,0));assert.throws(()=>G.validatePair(trace,{...inputs,id:'wrong'}));pass(id+' mismatched acoustic settings and driver/trace pairs rejected');
 wav(id+'.rev-pass-chain.wav',concatenate(rev));wav(id+'.shift-pass-chain.wav',concatenate(shift));
 fs.writeFileSync(path.join(out,id+'.diagnostic-manifest.json'),JSON.stringify({items,note:`${id} · fixed ${p.label}, seed ${p.seed}; browser Race drive. 14 passes at natural levels; no game changes.`,settings:p,settingsSha256:hash(p),rows},null,2)+'\n','utf8');
 for(const suffix of ['.audio-trace.json','.race.json','.unity.wav','.unity-source.wav','.unity-engine.wav','.unity-tires.wav','.browser.wav','.unity-browser-AB.wav','.comparison.png'])fs.copyFileSync(path.join(source,id+suffix),path.join(out,id+suffix));
 allRows.push(...rows);
}
fs.writeFileSync(path.join(out,'measurements.json'),JSON.stringify(allRows,null,2)+'\n','utf8');
fs.writeFileSync(path.join(out,'verification.txt'),log.join('\n')+'\nChecks: '+checks+'\nLIMIT Measurements and deterministic contracts do not establish subjective sound quality. Device/browser listening remains manual.\n','utf8');
cp.execFileSync(process.execPath,[path.join(dir,'package_unity_reference.cjs'),out],{stdio:'inherit'});
const rows=allRows.map(r=>`${r.id},${r.pass},${r.rms},${r.peak},${r.differenceRms},${r.soundSettingsSha256}`).join('\n');fs.writeFileSync(path.join(out,'measurement-matrix.csv'),'case,pass,rms,peak,difference_rms,fixed_settings_sha256\n'+rows+'\n','utf8');
console.log('Diagnostic workbench: '+path.join(out,'index.html'));
