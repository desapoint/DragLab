'use strict';
// Inspect the actual live-integration recording, not a synthesized replacement.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'..'),dir=path.resolve(root,process.argv[2]||'reports/reactive-afterfire-20261021');
const baselineDir=path.join(root,'reports/tempest-envelope-sequence-20261021');
const bytes=fs.readFileSync(path.join(dir,'tempest-game-before-after.wav'));
const base=fs.readFileSync(path.join(baselineDir,'before-context.f32'));
const before=new Float32Array(base.buffer,base.byteOffset,base.length/4),frames=before.length/2,rate=48000;
const log=[];let checks=0;
function check(ok,text){log.push((ok?'PASS ':'FAIL ')+text);checks++;if(!ok)throw Error(text);}
check(bytes.toString('ascii',0,4)==='RIFF'&&bytes.readUInt32LE(4)===bytes.length-8&&bytes.toString('ascii',8,12)==='WAVE','complete RIFF length');
check(bytes.toString('ascii',12,16)==='fmt '&&bytes.readUInt16LE(20)===1&&bytes.readUInt16LE(22)===2&&bytes.readUInt32LE(24)===rate&&bytes.readUInt16LE(34)===24,'48 kHz 24-bit stereo PCM');
check(bytes.toString('ascii',36,40)==='data'&&bytes.readUInt32LE(40)===(frames*2+rate)*6,'two complete native sequences plus exactly one second of separation');
const after=new Float32Array(before.length);let peak=0,gap=true,prefix=0,difference=0,beforeError=0,oldPower=0,newPower=0,stereo=false;
for(let i=0;i<before.length;i++){
 const old=bytes.readIntLE(44+i*3,3)/8388607;
 const current=bytes.readIntLE(44+(before.length+rate*2+i)*3,3)/8388607;after[i]=current;
 peak=Math.max(peak,Math.abs(old),Math.abs(current));
 beforeError=Math.max(beforeError,Math.abs(old-before[i]));
 difference+=(old-current)**2;oldPower+=old*old;newPower+=current*current;
 if(i<Math.floor(11.63*rate)*2)prefix=Math.max(prefix,Math.abs(old-current));
 if(i%2===0)stereo||=bytes.readIntLE(44+(before.length+rate*2+i)*3,3)!==bytes.readIntLE(47+(before.length+rate*2+i)*3,3);
}
for(let i=0;i<rate*2;i++)gap&&=bytes.readIntLE(44+(before.length+i)*3,3)===0;
check(gap,'exact one-second silent A/B separator');
check(beforeError<=1/8388607,'before is the historical native engine mix, with no additional gain or processing');
check(prefix===0,'opening engine/intake/turbo audio remains identical before the first combustion event');
check(difference>oldPower*1e-6,'live backfire revision is audible in the full native mix');
check(peak<.900001&&newPower>0&&stereo,'unclipped nonzero output retains stereo');
log.push(`MEASURE peak ${peak}; original RMS ${Math.sqrt(oldPower/before.length)}; live RMS ${Math.sqrt(newPower/before.length)}; before quantization error ${beforeError}`);
const fixedFile=path.join(baselineDir,'after-context.f32');
if(fs.existsSync(fixedFile)){
 const b=fs.readFileSync(fixedFile),fixed=new Float32Array(b.buffer,b.byteOffset,b.length/4);
 let p=0,a=0,f=0;
 for(let i=Math.round(11.5*rate)*2;i<Math.round(15.5*rate)*2;i++){p++;a+=after[i]*after[i];f+=fixed[i]*fixed[i];}
 log.push(`MEASURE launch-window RMS: previous fixed-strength preview ${Math.sqrt(f/p)}; current live ${Math.sqrt(a/p)}; level difference ${10*Math.log10(a/f)} dB. Listening approval is separate.`);
}
log.push(`FILE SHA256 ${crypto.createHash('sha256').update(bytes).digest('hex')}; BEFORE 0 s; AFTER ${frames/rate+1} s; duration ${(frames*2+rate)/rate} s`);
log.push('Historical local PCM is used only for this listening comparison, not required by runtime/chemistry/source tests. No audio is generated or modified by this verifier.');
log.push('Checks: '+checks);
fs.writeFileSync(path.join(dir,'wav-verification.txt'),log.join('\n')+'\n','utf8');console.log(log.join('\n'));
