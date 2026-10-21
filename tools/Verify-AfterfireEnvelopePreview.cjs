'use strict';
// Independent original-HTML comparison and PCM inspection; no audio synthesis
// replacement or WAV rewriting. The reference DSP is executed verbatim in a VM.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'..');
const dir=path.resolve(root,process.argv[2]||'reports/afterfire-envelope-preview-20261021');
const receipt=JSON.parse(fs.readFileSync(path.join(dir,'receipt.json'),'utf8'));
const html=fs.readFileSync(path.join(root,'docs/references/audio/backfire_lab_v3.html'),'utf8');
const controls=html.slice(html.indexOf('const defaults ='),html.indexOf('const soundGroups ='));
const random=html.slice(html.indexOf('function rng('),html.indexOf('function fmt('));
const source=html.slice(html.indexOf('const decimationKernel='),html.indexOf('function schedulePop('));
const core=source.replace('return {left,right,length:len,seconds:duration};','return {left,right,mono:processed,length:len,seconds:duration};');
if(core===source)throw Error('Original reference extraction contract changed');
const context=vm.createContext({});
vm.runInContext('const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));\n'+controls+random+core+'\nglobalThis.lab={defaults,presets,renderPop};',context);
const lab=context.lab;
const original=lab.renderPop({...lab.defaults,...lab.presets[receipt.preset]},receipt.rate,receipt.seed);
const floats=name=>{const b=fs.readFileSync(path.join(dir,name));return new Float32Array(b.buffer,b.byteOffset,b.length/4);};
const before=floats('before.f32'),after=floats('after.f32');
const log=[];let checks=0;
function check(yes,label){log.push((yes?'PASS ':'FAIL ')+label);checks++;if(!yes)throw Error(label);}
function energy(x){let e=0;for(const v of x)e+=v*v;return e;}
let maxError=0,errorEnergy=0,sourceEnergy=0;
const latency=receipt.rate<92000?8:0;
for(let i=0;i<original.mono.length;i++){
 const expected=original.mono[i]*receipt.constantGain;
 const actual=after[receipt.leadFrames+latency+i];
 const d=actual-expected;maxError=Math.max(maxError,Math.abs(d));errorEnergy+=d*d;sourceEnergy+=expected*expected;
}
check(maxError<Math.max(1,receipt.constantGain)*2e-6&&errorEnergy/sourceEnergy<1e-12,
 'complete revised burst matches original HTML mono PCM times ONE fixed physical strength, including the full padded tail');
log.push(`MEASURE same seed ${receipt.seed}; preset ${receipt.preset}; native causal latency ${latency} frames; max error ${maxError}; relative RMS ${Math.sqrt(errorEnergy/sourceEnergy)}`);
let dot=0;for(let i=0;i<before.length;i++)dot+=before[i]*after[i];
const fit=dot/energy(after);let difference=0;
for(let i=0;i<before.length;i++)difference+=(before[i]-fit*after[i])**2;
check(Math.sqrt(difference/energy(before))>.01,'before/after difference is a genuine envelope correction, not just a global volume change');
check(before.length===receipt.segmentFrames&&after.length===receipt.segmentFrames,'C# source exports cover the identical complete physical burst window');
for(const x of [before,after])check(x.slice(0,receipt.leadFrames).every(v=>v===0),'750 ms silence precedes the complete burst onset');
function wav(name,matched){
 const file=path.join(dir,name),b=fs.readFileSync(file);
 check(b.toString('ascii',0,4)==='RIFF'&&b.readUInt32LE(4)===b.length-8&&b.toString('ascii',8,12)==='WAVE','valid complete RIFF '+name);
 check(b.toString('ascii',12,16)==='fmt '&&b.readUInt16LE(20)===1&&b.readUInt16LE(22)===2&&b.readUInt32LE(24)===receipt.rate&&b.readUInt16LE(34)===24,'48 kHz 24-bit stereo PCM '+name);
 const frames=receipt.segmentFrames*2+receipt.gapFrames;
 check(b.toString('ascii',36,40)==='data'&&b.readUInt32LE(40)===frames*6&&b.length===44+frames*6,'exact chapter and gap lengths '+name);
 const bg=matched?receipt.beforeMatchGain:1;
 const cg=matched?receipt.commonGain:.9/Math.max(receipt.beforePeak,receipt.afterPeak);
 let max=0,delta=0,ea=0,eb=0,gap=true,leads=true,stereo=true;
 for(let i=0;i<frames;i++){
  const a=b.readIntLE(44+i*6,3),r=b.readIntLE(47+i*6,3),value=a/8388607;
  stereo&&=a===r;max=Math.max(max,Math.abs(value));
  const second=i>=receipt.segmentFrames+receipt.gapFrames,j=i-receipt.segmentFrames-receipt.gapFrames;
  const expected=i<receipt.segmentFrames?before[i]*bg*cg:second?after[j]*cg:0;
  delta=Math.max(delta,Math.abs(expected-value));
  if(i<receipt.leadFrames||(second&&j<receipt.leadFrames))leads&&=a===0;
  if(i>=receipt.segmentFrames&&!second)gap&&=a===0;
  if(i<receipt.segmentFrames)ea+=value*value;else if(second)eb+=value*value;
 }
 check(stereo&&leads&&gap,'both chapters preserve identical mono routing, full lead-ins and a silent one-second separator '+name);
 check(max<=.900001&&delta<=1/8388607,'unclipped output matches the unaltered source arrays times documented scalar gains '+name);
 if(matched)check(Math.abs(Math.sqrt(ea/eb)-1)<1e-6,'matched chapters have equal RMS, without compression, fades or within-burst gain changes');
 log.push(`FILE ${name}; SHA256 ${crypto.createHash('sha256').update(b).digest('hex')}; duration ${frames/receipt.rate}; after begins ${receipt.segmentFrames/receipt.rate+1}; peak ${max}`);
}
wav('01-tempest-envelope-before-after-matched.wav',true);
wav('02-tempest-envelope-before-after-natural.wav',false);
log.push('Scope: isolated FIRST real Tempest flame, all its chemical continuation packets, original seed/timing. No added HTML stereo room, hardware path, bus or outdoor processing in either chapter.');
log.push('Offline preview: full recorded flame energy is known before rendering. Not a claim of causal realtime readiness. No production source or player build is changed.');
log.push('Checks: '+checks);
fs.writeFileSync(path.join(dir,'independent-verification.txt'),log.join('\n')+'\n','utf8');
console.log(log.join('\n'));
