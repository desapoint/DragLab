'use strict';
// Offline, repeatable acoustic isolation. No Unity/game files or historical WAVs
// are changed. Uses the actual standalone Free presets, not older JSON defaults.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict'),cp=require('node:child_process');
const dir=__dirname,root=path.resolve(dir,'../..'),baseline=path.join(root,'reports/unity-audio-reference-20261021');
const out=path.resolve(process.argv[2]||path.join(root,'reports/audio-replay-modes-20261021'));fs.mkdirSync(out,{recursive:true});
const html=fs.readFileSync(path.join(dir,'DragLab_Audio_Workbench_DrivetrainFix.html'),'utf8');
const D=vm.runInThisContext(fs.readFileSync(path.join(dir,'draglab_dsp_core_drivetrain.js'),'utf8')+'\ncreateDragLabDSP()');
const R=vm.runInThisContext(fs.readFileSync(path.join(dir,'unity_reference_replay.js'),'utf8')+'\ncreateUnityReferenceReplay')(D);
const original=cp.execFileSync('git',['show','40d475f:tools/audio-workbench/draglab_dsp_core_drivetrain.js'],{cwd:root,encoding:'utf8'});
const oldD=vm.runInThisContext(original+'\ncreateDragLabDSP()');
const presets=new Function('DSP',`const stockParts=stage=>DSP.standardParts(stage).map(kind=>({kind,...DSP.partDefaults[kind],amount:1}));${html.slice(html.indexOf('function tempest(){'),html.indexOf('\nconst clamp='))};return presetById;`)(D);
const clone=x=>JSON.parse(JSON.stringify(x));const gates=[];function pass(s){gates.push('PASS '+s);console.log(gates.at(-1));}
function render(e,n){const l=new Float32Array(n),r=new Float32Array(n);e.render(l,r);for(let i=0;i<n;i++)assert(Number.isFinite(l[i])&&Number.isFinite(r[i])&&Math.abs(l[i])<1&&Math.abs(r[i])<1);return{l,r};}
function pcm(x){const b=Buffer.alloc(x.l.length*6);for(let i=0;i<x.l.length;i++){b.writeIntLE(Math.round(x.l[i]*8388607),i*6,3);b.writeIntLE(Math.round(x.r[i]*8388607),i*6+3,3);}return b;}
function wav(file,x){const b=pcm(x),h=Buffer.alloc(44);h.write('RIFF');h.writeUInt32LE(b.length+36,4);h.write('WAVEfmt ',8);h.writeUInt32LE(16,16);h.writeUInt16LE(1,20);h.writeUInt16LE(2,22);h.writeUInt32LE(48000,24);h.writeUInt32LE(288000,28);h.writeUInt16LE(6,32);h.writeUInt16LE(24,34);h.write('data',36);h.writeUInt32LE(b.length,40);fs.writeFileSync(path.join(out,file),Buffer.concat([h,b]));}
function rms(x){let s=0;for(let i=0;i<x.l.length;i++)s+=x.l[i]**2+x.r[i]**2;return Math.sqrt(s/(x.l.length*2));}
function ab(a,b,match=false){const l=new Float32Array(a.l.length+48000+b.l.length),r=new Float32Array(l.length),target=Math.min(rms(a),rms(b));for(const [x,at]of [[a,0],[b,a.l.length+48000]]){const g=match?target/rms(x):1;for(let i=0;i<x.l.length;i++){l[at+i]=x.l[i]*g;r[at+i]=x.r[i]*g;}}return{l,r};}
const rows=[];
for(const [id,preset]of [['03-i4-turbo-hiss','tempest'],['08-v8-carb','v8']]){
 const t=JSON.parse(fs.readFileSync(path.join(baseline,id+'.audio-trace.json'),'utf8')),inputs=JSON.parse(fs.readFileSync(path.join(baseline,id+'.race.json'),'utf8')),p=presets(preset),n=t.totalSamples;
 const oldFree=new oldD.EngineDSP(48000,p),free=new D.EngineDSP(48000,p);oldFree.setInput({auto:true});free.setInput({auto:true});
 const f=render(free,Math.round(15.9*48000));assert.deepEqual(f,render(oldFree,f.l.length));wav(id+'.free.wav',f);pass(id+' Free full sequence is sample-identical to supplied original DSP');
 const captured=R.settingsFromTrace(presets('tempest'),t),aEngine=new R.ReferenceEngine(48000,captured,t),bEngine=new R.ReferenceEngine(48000,p,t,{preserveSound:true});
 const a=render(aEngine,n),b=render(bEngine,n);assert.deepEqual(aEngine.referenceEvents,bEngine.referenceEvents);assert.equal(bEngine.p.seed,p.seed);assert.equal(bEngine.p.masterVolume,p.masterVolume);
 const archived=fs.readFileSync(path.join(baseline,id+'.browser.wav'));assert(archived.subarray(44).equals(pcm(a)),id+' changed historical reference PCM');pass(id+' default reference PCM unchanged; preserved-Free pass has identical physical event schedule');
 wav(id+'.captured-tuning.wav',a);wav(id+'.unity-free-tuning.wav',b);wav(id+'.tuning-AB.wav',ab(a,b));wav(id+'.tuning-AB-matched.wav',ab(a,b,true));
 const ie=new R.InputEngine(48000,p,inputs);ie.setInput({mode:'Race'});const c=render(ie,(inputs.ticks.length+241)*400);wav(id+'.unity-inputs.wav',c);
 assert(!ie.unityFrame&&!ie.unityTires);assert.equal(ie.inputEvents.length,inputs.ticks.filter(x=>x.input.shiftUp||x.input.shiftDown).length);pass(id+' driver-only replay consumes every shift command without captured physical/audio state');
 for(const suffix of ['.audio-trace.json','.race.json','.unity.wav','.unity-source.wav','.unity-engine.wav','.unity-tires.wav','.browser.wav','.unity-browser-AB.wav','.comparison.png'])fs.copyFileSync(path.join(baseline,id+suffix),path.join(out,id+suffix));
 rows.push({id,preset,referenceSeconds:n/48000,freeRms:rms(f),capturedRms:rms(a),preservedRms:rms(b),inputRms:rms(c),inputShifts:ie.inputEvents.length});
}
// Exercise genuine downshifts and held-pedal/gear bounds independently of fixture content.
for(const id of ['tempest','v8','na','diesel']){const e=new D.EngineDSP(48000,presets(id));e.setInput({throttle:.8});e.gear=3;e.rpm=3000;e.setInput({shiftDown:true});assert.equal(e.gear,2);assert.equal(e.targetThrottle,.8);assert.equal(e.shiftDirection,-1);e.setInput({shift:true});assert.equal(e.gear,2);e.shiftTimer=0;e.gear=1;e.setInput({shiftDown:true});assert.equal(e.gear,1);e.gear=e.p.forwardGears;e.setInput({shift:true});assert.equal(e.gear,e.p.forwardGears);pass(id+' downshift, busy rejection, gear bounds and held throttle');}
// Poison every excluded state/input: driver-only replay must be sample-identical.
const tiny={id:'driver-contract',ticks:Array.from({length:180},(_,i)=>({input:{throttle:i<90?.9:0,shiftUp:i===30,shiftDown:i===120,brake:1,launch:true,clutch:1}}))};
const poisoned=clone(tiny);for(const t of poisoned.ticks)Object.assign(t.input,{brake:0,clutch:0,launch:false});poisoned.frames=[{rpm:99999}];poisoned.reactions=[{time:0}];poisoned.releases=[{time:0}];
const a=new R.InputEngine(48000,presets('tempest'),tiny),b=new R.InputEngine(48000,presets('tempest'),poisoned);a.setInput({mode:'Race'});b.setInput({mode:'Race',throttle:0,shift:true,pop:'manual',bov:true,auto:true});assert.deepEqual(render(a,72000),render(b,72000));pass('driver-only replay ignores brakes/clutch/launch, captured state/events and manual injected events');
const first=render(new R.InputEngine(48000,presets('v8'),tiny),8000),restarted=new R.InputEngine(48000,presets('v8'),tiny);render(restarted,9000);restarted.setInput({reset:true});assert.deepEqual(first,render(restarted,8000));pass('driver restart reproduces initial PCM');
const rate=new R.InputEngine(44100,presets('tempest'),tiny);render(rate,Math.ceil((tiny.ticks.length+241)*400/48000*44100)+2);assert(rate.meter.referenceDone);assert.equal(rate.inputEvents.length,2);pass('44.1 kHz driver timeline consumes shifts once and ends');
assert.throws(()=>R.validateInputs({id:'bad',ticks:[{input:{throttle:NaN,shiftUp:false,shiftDown:false}}]}));pass('malformed driver recording rejected');
fs.writeFileSync(path.join(out,'comparison.json'),JSON.stringify(rows,null,2)+'\n','utf8');
fs.writeFileSync(path.join(out,'verification.txt'),gates.join('\n')+'\nLIMIT Offline/contract checks; browser/device listening remains user review. No game changes.\n','utf8');
cp.execFileSync(process.execPath,[path.join(dir,'package_unity_reference.cjs'),out],{stdio:'inherit'});
const links=rows.map(x=>`<h2>${x.id}</h2><p><a href="${x.id}.html">Interactive three-mode workbench</a></p>`+[['Free scripted sequence','.free.wav'],['Unity simulation, original Free tuning','.unity-free-tuning.wav'],['Unity throttle / shifts only, browser Race drive','.unity-inputs.wav'],['Tuning A/B: captured then Free (natural levels)','.tuning-AB.wav'],['Same tuning A/B, RMS matched by attenuation','.tuning-AB-matched.wav']].map(([title,suffix])=>`<p>${title}</p><audio controls preload="none" src="${x.id+suffix}"></audio>`).join('')).join('');
fs.writeFileSync(path.join(out,'listen.html'),`<!doctype html><meta charset="utf-8"><title>Replay modes comparison</title><style>body{background:#111b29;color:#eaf3ff;font:16px system-ui;max-width:900px;margin:30px auto}a{color:#5ad4ee}audio{width:100%}</style><h1>Replay and sound-tuning isolation</h1><p>Preview only. Free uses its original 15.9-second script. The two Unity modes use the recorded input clock; driver-only replay calculates its own RPM/boost/events and therefore is not time/outcome equivalent. Tuning A/B uses identical captured physics/events, one second silence between A and B. No game audio changes.</p>${links}`,'utf8');
console.log('Review: '+path.join(out,'listen.html'));
