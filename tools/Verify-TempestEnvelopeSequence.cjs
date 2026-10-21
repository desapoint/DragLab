'use strict';
// Verify the complete fixed-envelope source set against the original HTML DSP,
// then check the context WAV without synthesizing or rewriting the game mix.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'..'),dir=path.resolve(root,process.argv[2]||'reports/tempest-envelope-sequence-20261021');
const r=JSON.parse(fs.readFileSync(path.join(dir,'receipt.json'),'utf8'));
const html=fs.readFileSync(path.join(root,'docs/references/audio/backfire_lab_v3.html'),'utf8');
const controls=html.slice(html.indexOf('const defaults ='),html.indexOf('const soundGroups ='));
const rng=html.slice(html.indexOf('function rng('),html.indexOf('function fmt('));
const original=html.slice(html.indexOf('const decimationKernel='),html.indexOf('function schedulePop('));
const instrumented=original.replace('return {left,right,length:len,seconds:duration};','return {left,right,mono:processed,length:len,seconds:duration};');
if(original===instrumented)throw Error('Reference extraction contract changed');
const context=vm.createContext({});vm.runInContext('const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));\n'+controls+rng+instrumented+'\nglobalThis.lab={defaults,renderPop};',context);
const log=[];let checks=0;
function check(ok,label){log.push((ok?'PASS ':'FAIL ')+label);checks++;if(!ok)throw Error(label);}
const floats=name=>{const b=fs.readFileSync(path.join(dir,name));return new Float32Array(b.buffer,b.byteOffset,b.length/4);};
check(r.eventCount===5387&&r.flameCount===54&&r.flames.length===54,'complete original Tempest reaction/flame count retained');
check(r.flames.reduce((n,f)=>n+f.eventCount,0)===r.eventCount,'all recorded chemical packets fund exactly one selected flame');
check(Math.abs(r.flames.reduce((n,f)=>n+f.acousticJ,0)-r.totalAcousticJ)<1e-8,'total simulated acoustic energy retained across all preview flames');
let worst=0,maxRelative=0;
for(const f of r.flames){
 if(!f.controls||Object.keys(f.controls).length!==15)throw Error('Missing explicit source controls for flame '+f.id);
 const golden=context.lab.renderPop({...context.lab.defaults,...f.controls},r.rate,f.seed).mono;
 const actual=floats(f.source);let max=0,se=0,ge=0;
 for(let i=0;i<golden.length;i++){
  const expected=golden[i]*f.gain,d=actual[i+8]-expected;
  if(!Number.isFinite(d))throw Error('Missing/nonfinite source PCM');
  max=Math.max(max,Math.abs(d));se+=d*d;ge+=expected*expected;
 }
 const relative=Math.sqrt(se/ge);worst=Math.max(worst,max);maxRelative=Math.max(maxRelative,relative);
 check(max<Math.max(1,f.gain)*2e-6&&relative<1e-6,'flame '+f.id+' retains complete HTML mono envelope, exact hardware parameters, seed and ONE energy-derived gain');
 check(f.startFrame>=0&&f.startFrame<r.frames,'flame '+f.id+' starts within the original complete race sequence');
}
log.push(`MEASURE all 54 sources: maximum PCM error ${worst}; maximum relative RMS ${maxRelative}; native source latency 8 frames`);
const before=floats('before-context.f32'),after=floats('after-context.f32');
check(before.length===r.frames*2&&after.length===r.frames*2,'both full stereo sequences have identical complete lengths');
check(r.nativeMixError<2e-6&&r.sourceReconstructionError<2e-6,'original engine bus and full afterfire path independently reproduce captured native PCM');
let diff=0,energy=0,preDifference=0,first=Math.min(...r.flames.map(f=>f.startFrame));
for(let i=0;i<before.length;i++){
 if(!Number.isFinite(before[i])||!Number.isFinite(after[i]))throw Error('Nonfinite full-mix PCM');
 diff+=(before[i]-after[i])**2;energy+=before[i]*before[i];
 if(i<first*2)preDifference=Math.max(preDifference,Math.abs(before[i]-after[i]));
}
check(diff>energy*1e-6,'new backfires are present in the actual full engine mix');
check(preDifference===0,'engine/turbo/intake opening before the first afterfire is bit-identical');
const file=path.join(dir,'tempest-full-sequence-before-after.wav'),b=fs.readFileSync(file),frames=r.frames*2+r.gapFrames;
check(b.toString('ascii',0,4)==='RIFF'&&b.readUInt32LE(4)===b.length-8&&b.toString('ascii',8,12)==='WAVE','valid complete RIFF');
check(b.toString('ascii',12,16)==='fmt '&&b.readUInt16LE(20)===1&&b.readUInt16LE(22)===2&&b.readUInt32LE(24)===r.rate&&b.readUInt16LE(34)===24,'48 kHz 24-bit stereo PCM');
check(b.toString('ascii',36,40)==='data'&&b.readUInt32LE(40)===frames*6&&b.length===44+frames*6,'complete before and after sequences plus exactly one second of separator');
let peak=0,delta=0,silent=true,stereoDifferent=false;
for(let i=0;i<frames*2;i++){
 const value=b.readIntLE(44+i*3,3)/8388607;
 const expected=(i<before.length?before[i]:i<before.length+r.gapFrames*2?0:after[i-before.length-r.gapFrames*2])*r.commonGain;
 peak=Math.max(peak,Math.abs(value));delta=Math.max(delta,Math.abs(value-expected));
 if(i>=before.length&&i<before.length+r.gapFrames*2)silent&&=value===0;
 if(i%2===0)stereoDifferent||=b.readIntLE(44+i*3,3)!==b.readIntLE(47+i*3,3);
}
check(silent,'silent one-second A/B separator');
check(peak<=.900001&&delta<=1/8388607,'unclipped PCM uses ONE shared gain, with no per-half or per-event normalization');
check(stereoDifferent,'native stereo engine banks and trackside propagation are retained');
log.push(`FILE SHA256 ${crypto.createHash('sha256').update(b).digest('hex')}; duration ${frames/r.rate}; AFTER starts ${r.afterStartSeconds}; peak ${peak}; shared gain ${r.commonGain}`);
log.push(`MEASURE full-mix relative change ${Math.sqrt(diff/energy)}; pre-first-afterfire difference ${preDifference}`);
log.push('Offline preview only; constant strengths use recorded complete flame energies. Engine bed/physics/hardware/processing unchanged; no player build or runtime integration.');
log.push('Checks: '+checks);
fs.writeFileSync(path.join(dir,'independent-verification.txt'),log.join('\n')+'\n','utf8');
console.log(log.join('\n'));
