'use strict';
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const dir=__dirname,root=path.resolve(dir,'../..'),folder=process.argv[2]||path.join(root,'reports/unity-audio-reference-20261021');
const dspText=fs.readFileSync(path.join(dir,'draglab_dsp_core_drivetrain.js'),'utf8').trimEnd(),refText=fs.readFileSync(path.join(dir,'unity_reference_replay.js'),'utf8').trimEnd();
// Use the same V8 realm for the two inspected factories; cross-realm calls in
// the sample loop slow this offline checker without providing extra isolation.
const D=vm.runInThisContext(dspText+'\ncreateDragLabDSP()'),R=vm.runInThisContext(refText+'\ncreateUnityReferenceReplay')(D);
const html=fs.readFileSync(path.join(dir,'DragLab_Audio_Workbench_DrivetrainFix.html'),'utf8');
assert.equal(html.slice(html.indexOf('function createDragLabDSP(){'),html.indexOf('\nconst DSP=createDragLabDSP();')),dspText);
assert(html.includes(refText),'standalone HTML reference adapter differs');new vm.Script(html.match(/<script>([\s\S]*?)<\/script>/)[1]);
const base=JSON.parse(fs.readFileSync(path.join(dir,'draglab-physical-preset-tempest.json'),'utf8')).settings;
let checks=0;const log=[];function ok(value,label){assert(value,label);checks++;log.push('PASS '+label);console.log(log.at(-1));}
const cutCore=new D.EngineDSP(48000,base).core;
for(let i=0;i<3000;i++)cutCore.sample(5000,.8,{load:.8,spool:.5,airflow:.8,cut:true,cylinderContribution:[1,1,1,1],combustionFraction:1},D.DEFAULT_STAGES);
ok(cutCore.eventCount>0&&cutCore.pending[0].every(x=>x===0)&&cutCore.pending[1].every(x=>x===0),'recorded full combustion cuts override healthy per-cylinder contributions');
const selected=process.argv[3]?.split(',');
const traces=fs.readdirSync(folder).filter(n=>n.endsWith('.audio-trace.json')&&(!selected||selected.includes(n.replace('.audio-trace.json','')))).sort();ok(traces.length===(selected?selected.length:5),traces.length+' requested real engine/traction configurations');
function wav(file,total){const b=fs.readFileSync(file);assert.equal(b.toString('ascii',0,4),'RIFF');assert.equal(b.readUInt32LE(4),b.length-8);assert.equal(b.toString('ascii',8,16),'WAVEfmt ');assert.equal(b.readUInt16LE(20),1);assert.equal(b.readUInt16LE(22),2);assert.equal(b.readUInt32LE(24),48000);assert.equal(b.readUInt16LE(34),24);assert.equal(b.readUInt32LE(40),total*6);return b;}
function frameValue(b,index){let at=44+index*3;let n=b[at]|b[at+1]<<8|b[at+2]<<16;return n&0x800000?n-0x1000000:n;}
function write24(file,left,right){const data=Buffer.alloc(44+left.length*6);data.write('RIFF');data.writeUInt32LE(data.length-8,4);data.write('WAVEfmt ',8);data.writeUInt32LE(16,16);data.writeUInt16LE(1,20);data.writeUInt16LE(2,22);data.writeUInt32LE(48000,24);data.writeUInt32LE(288000,28);data.writeUInt16LE(6,32);data.writeUInt16LE(24,34);data.write('data',36);data.writeUInt32LE(data.length-44,40);for(let i=0;i<left.length;i++){data.writeIntLE(Math.round(left[i]*8388607),44+i*6,3);data.writeIntLE(Math.round(right[i]*8388607),47+i*6,3);}fs.writeFileSync(file,data);return data;}
function render(trace,settings=R.settingsFromTrace(base,trace),mutate=false){
 const e=new R.ReferenceEngine(48000,settings,trace),hash=crypto.createHash('sha256');let energy=0,peak=0,clipped=0;
 const left=new Float32Array(trace.totalSamples),right=new Float32Array(trace.totalSamples);
 e.stepPhysics=()=>{throw Error('Browser physics was called in reference mode');};
 if(mutate)e.setInput({throttle:1,mode:'Race',shift:true,pop:'manual',bov:true,launch:true,auto:true});
 for(let at=0;at<trace.totalSamples;at+=4096){const n=Math.min(4096,trace.totalSamples-at),l=left.subarray(at,at+n),r=right.subarray(at,at+n);e.render(l,r);hash.update(Buffer.from(l.buffer, l.byteOffset,l.byteLength));hash.update(Buffer.from(r.buffer,r.byteOffset,r.byteLength));for(let i=0;i<n;i++){assert(Number.isFinite(l[i])&&Number.isFinite(r[i]));peak=Math.max(peak,Math.abs(l[i]),Math.abs(r[i]));energy+=l[i]*l[i]+r[i]*r[i];if(Math.abs(l[i])>=1||Math.abs(r[i])>=1)clipped++;}}
 return{e,left,right,hash:hash.digest('hex'),rms:Math.sqrt(energy/(trace.totalSamples*2)),peak,clipped};
}
for(const file of traces){
 const t=R.validate(JSON.parse(fs.readFileSync(path.join(folder,file),'utf8'))),name=t.id;
 const engine=wav(path.join(folder,name+'.unity-engine.wav'),t.totalSamples),tires=wav(path.join(folder,name+'.unity-tires.wav'),t.totalSamples),combined=wav(path.join(folder,name+'.unity.wav'),t.totalSamples);wav(path.join(folder,name+'.unity-source.wav'),t.totalSamples);
 ok(true,name+' four complete, synchronized 48 kHz / 24-bit stereo Unity WAVs');
 let maxSumError=0,tireEnergy=0;for(let i=0;i<t.totalSamples*2;i+=17){const a=frameValue(engine,i),b=frameValue(tires,i),c=frameValue(combined,i);maxSumError=Math.max(maxSumError,Math.abs(a+b-c));tireEnergy+=b*b;}
 ok(maxSumError<=2,name+' combined reference retains the same engine/tire gains without normalization');
 if(name.includes('burnout'))ok(tireEnergy>0,name+' actual per-wheel contact produces audible isolated tire reference');
 ok(t.reactions.every(e=>typeof e.seed==='string'&&BigInt(e.seed).toString()===e.seed),name+' lossless 64-bit event seeds');
 const result=render(t);ok(result.rms>1e-5&&result.clipped===0,name+' browser replay has finite audible unclipped PCM');
 write24(path.join(folder,name+'.browser.wav'),result.left,result.right);
 const gap=48000,al=new Float32Array(t.totalSamples*2+gap),ar=new Float32Array(al.length);
 for(let i=0;i<t.totalSamples;i++){al[i]=frameValue(combined,i*2)/8388607;ar[i]=frameValue(combined,i*2+1)/8388607;}
 al.set(result.left,t.totalSamples+gap);ar.set(result.right,t.totalSamples+gap);write24(path.join(folder,name+'.unity-browser-AB.wav'),al,ar);
 ok(result.e.reactionCursor===t.reactions.length&&result.e.releaseCursor===t.releases.length,name+' no missing or invented reaction/BOV events');
 for(const e of result.e.referenceEvents){const source=(e.kind==='reaction'?t.reactions:t.releases).find(x=>x.index===e.index),expected=Math.max(source.availableSample,Math.ceil(source.time*48000-1e-8));assert.equal(e.sample,expected,name+' exact event time');}
 ok(true,name+' chemical and BOV scheduling respects exact time and causal publication');
 const last=t.frames.at(-1);ok(result.e.rpm===last.engine.rpm&&result.e.gear===last.state.gear&&result.e.boost===last.engine.boost,name+' RPM, gear and boost follow captured game state without browser physics');
 const l=new Float32Array(100),r=new Float32Array(100);result.e.render(l,r);ok(l.every(x=>x===0)&&r.every(x=>x===0)&&result.e.meter.referenceDone,name+' finite end of recording without a phantom continued race');
 if(name==='03-i4-turbo-hiss'){
  const repeat=render(t,undefined,true);ok(result.hash===repeat.hash,name+' deterministic replay; manual throttle/shift/BOV/pop/auto commands cannot contaminate the reference');
  const p=R.settingsFromTrace(base,t);p.stages={...p.stages};Object.keys(p.stages).forEach(k=>p.stages[k]=false);const silent=render(t,p);ok(silent.peak===0,'all-off reference solo is exact silence while event timing is retained');
 }
 log.push('MEASURE '+name+' / browser RMS '+result.rms+' / peak '+result.peak+' / PCM SHA256 '+result.hash);
}
const t=JSON.parse(fs.readFileSync(path.join(folder,traces[0]),'utf8'));
const mutations=[x=>x.schema='unknown',x=>x.frames.pop(),x=>x.frames[1].sample++];
if(t.reactions.length)mutations.push(x=>x.reactions[0].seed=Number(x.reactions[0].seed),x=>x.reactions[0].availableSample++);
for(const mutate of mutations){const broken=JSON.parse(JSON.stringify(t));mutate(broken);assert.throws(()=>R.validate(broken));}
ok(true,'incompatible, incomplete, misaligned or precision-losing traces are rejected');
log.push('Checks: '+checks);fs.writeFileSync(path.join(folder,'browser-verification.txt'),log.join('\n')+'\n','utf8');
