// Extract and execute the supplied reference DSP verbatim. No DOM/browser needed.
// Outputs are local generated test fixtures, not authored audio assets.
'use strict';
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'..'),reference=path.join(root,'docs/references/audio/backfire_lab_v3.html');
const folder=path.resolve(root,process.argv[2]||'reports/afterfire-lab-v3-20261021');fs.mkdirSync(folder,{recursive:true});
const html=fs.readFileSync(reference,'utf8');
const controls=html.slice(html.indexOf('const defaults ='),html.indexOf('const soundGroups ='));
const synthesis=html.slice(html.indexOf('const decimationKernel='),html.indexOf('function schedulePop('));
const random=html.slice(html.indexOf('function rng('),html.indexOf('function fmt('));
// Return the unnormalized mono PCM before demo spatialization. Source algorithm
// itself is unmodified, including Float32 stores, micro-pulses and oversampling.
const instrumented=synthesis
 .replace('const mono=new Float32Array(internalLength)', 'const pressureRaw=new Float32Array(internalLength);\n const mono=new Float32Array(internalLength)')
 .replace('mono[i]=high;', 'mono[i]=high;pressureRaw[i]=shock*(.48+.74*thump);')
 .replace('return {left,right,length:len,seconds:duration};', `const pressurePCM=new Float32Array(len);
 if(os===1)pressurePCM.set(pressureRaw);
 else for(let i=0;i<len;i++){let sum=0;for(let k=-16;k<=16;k++){const j=i*os+k;if(j>=0&&j<internalLength)sum+=pressureRaw[j]*decimationKernel[k+16];}pressurePCM[i]=sum;}
 return {left,right,mono:processed,pressure:pressurePCM,length:len,seconds:duration};`);
if(instrumented===synthesis)throw Error('Reference return changed: update the explicit extraction contract.');
const context=vm.createContext({});vm.runInContext('const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));\n'+controls+random+instrumented+'\nglobalThis.lab={defaults,presets,renderPop};',context);
const lab=context.lab,manifest=[],seed=15124311;
for(const sr of [24000,44100,48000,96000,192000])for(let index=0;index<lab.presets.length;index++){
 const p={...lab.defaults,...lab.presets[index]},data=lab.renderPop(p,sr,seed);
 const name=`reference-${sr}-${index}.f32`,bytes=Buffer.from(data.mono.buffer,data.mono.byteOffset,data.mono.byteLength);
 fs.writeFileSync(path.join(folder,name),bytes);manifest.push({sr,index,seed,name,frames:data.length,preset:p});
 fs.writeFileSync(path.join(folder,`pressure-${sr}-${index}.f32`),Buffer.from(data.pressure.buffer,data.pressure.byteOffset,data.pressure.byteLength));
}
fs.writeFileSync(path.join(folder,'reference-manifest.json'),JSON.stringify(manifest,null,2)+'\n','utf8');
fs.writeFileSync(path.join(folder,'javascript-reference.txt'),`Reference SHA256: ${crypto.createHash('sha256').update(fs.readFileSync(reference)).digest('hex')}\nExecuted renderPop from original HTML; extracted pre-normalization, pre-stereo PCM.\n${manifest.length} deterministic fixtures; all 10 presets at 24 / 44.1 / 48 / 96 / 192 kHz.\nSeed ${seed}. Generated PCM/JSON remain local; rerun this script before Unity verification.\n`,'utf8');
console.log(`Generated ${manifest.length} reference fixtures in ${folder}`);
