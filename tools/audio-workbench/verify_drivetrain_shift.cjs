'use strict';
const fs=require('fs'),vm=require('vm'),assert=require('node:assert/strict'),path=require('path');
const root=__dirname;
function getDSP(filename){let text=fs.readFileSync(path.join(root,filename),'utf8');if(filename.endsWith('.html')){text=text.slice(text.indexOf('function createDragLabDSP(){'),text.indexOf('\nconst DSP=createDragLabDSP();'));}return vm.runInNewContext(text+'\ncreateDragLabDSP()');}
const D=getDSP('draglab_dsp_core_drivetrain.js');
function rmsSample(engine,ms=40){let n=Math.round(24000*ms/1000),left=new Float32Array(n),right=new Float32Array(n);engine.render(left,right);let sum=0;for(let i=0;i<n;i++){assert(Number.isFinite(left[i]));assert(Number.isFinite(right[i]));sum+=left[i]**2+right[i]**2;}return Math.sqrt(sum/(2*n));}
function testEngine(D,id){const p=JSON.parse(fs.readFileSync(path.join(root,'draglab-physical-preset-'+id+'.json'),'utf8')).settings;
for(const k of Object.keys(p.stages))p.stages[k]=false;
for(const k of ['combustion','runner','headers','collector','exhaustBand','bodyEQ','sourceSaturation','fittedExhaust','dc','softclip'])p.stages[k]=true;
p.popProbability=0;p.overrunPopRate=0;p.shiftSeconds=.32;p.shiftDipMs=35;p.shiftRecoveryMs=110;p.shiftPressureFloor=.35;
const e=new D.EngineDSP(24000,p);e.run=true;e.gear=1;e.throttle=e.targetThrottle=.88;e.rpm=p.redlineRpm-1250;
const factor=60/(2*Math.PI*.29)*3.6*4.056;e.speed=e.rpm/factor;
let initial=[];for(let i=0;i<6;i++)initial.push(rmsSample(e));
const before=initial.slice(-3).reduce((a,b)=>a+b,0)/3;
assert(before>1e-6, id+': baseline signal too quiet');
const rpmBefore=e.rpm;
e.shift();let during=[];for(let i=0;i<9;i++){during.push(rmsSample(e));}
const rpmAfter=e.rpm;
assert(rpmAfter<rpmBefore-100,id+': RPM did not descend at gear change, '+rpmBefore+' -> '+rpmAfter);
assert(e.gear===2,id+': expected 2nd gear');
// The middle of the shift used to be almost entirely silent. Compare to normal engine signal.
const mid=Math.min(...during.slice(2,7));
assert(mid>before*.09,`${id}: engine disappears during shift: min RMS ${mid}, baseline ${before} frames ${during}`);
assert(during.every(x=>Number.isFinite(x)),id+': invalid samples');
assert(!e.cut,id+': shift still incorrectly sets sustained limiter cut');
console.log('PASS',id, 'idle RMS',before.toFixed(6),'shift min/baseline',(mid/before).toFixed(3),'RPM',Math.round(rpmBefore),'→',Math.round(rpmAfter));
}
for(const id of ['tempest','v8','na','diesel'])testEngine(D,id);
// Check standalone HTML embeds exactly the same new DSP behavior.
for(const id of ['tempest','v8'])testEngine(getDSP('DragLab_Audio_Workbench_DrivetrainFix.html'),id);
// Tuning controls must change the pressure waveform, not only the displayed slider.
function variant(floor){let p=JSON.parse(fs.readFileSync(path.join(root,'draglab-physical-preset-v8.json'),'utf8')).settings;
Object.keys(p.stages).forEach(k=>p.stages[k]=false);p.stages.combustion=true;p.stages.dc=true;p.stages.fittedExhaust=true;
p.popProbability=0;p.shiftDipMs=90;p.shiftRecoveryMs=80;p.shiftPressureFloor=floor;
let e=new D.EngineDSP(24000,p);e.rpm=4500;e.run=false;e.throttle=e.targetThrottle=.8;
for(let i=0;i<8;i++)rmsSample(e);e.shift();let arr=[];for(let i=0;i<3;i++)arr.push(rmsSample(e));return arr;}
const reduced=variant(.1),full=variant(1);
assert(reduced[1]<full[1]*.8,`Shift floor parameter did not materially affect engine output: ${reduced} vs ${full}`);
console.log('PASS', 'shiftPressureFloor sensitivity',reduced[1].toFixed(5),'vs',full[1].toFixed(5));
