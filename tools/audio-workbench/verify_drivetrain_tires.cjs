'use strict';
const fs=require('fs'),vm=require('vm'),assert=require('node:assert/strict'),path=require('path');
const html=fs.readFileSync(path.join(__dirname,'DragLab_Audio_Workbench_DrivetrainFix.html'),'utf8');
const dspText=html.slice(html.indexOf('function createDragLabDSP(){'),html.indexOf('\nconst DSP=createDragLabDSP();'));
const D=vm.runInNewContext(dspText+'\ncreateDragLabDSP()');
function profile(){const p=JSON.parse(fs.readFileSync(path.join(__dirname,'draglab-physical-preset-v8.json'),'utf8')).settings;Object.keys(p.stages).forEach(k=>p.stages[k]=false);for(let k of ['tires','tireFL','tireFR','tireRL','tireRR'])p.stages[k]=true;p.popProbability=0;p.masterVolume=.7;p.categoryVolume=0;p.tireGain=2;p.listenerX=0;p.listenerY=4;return p;}
function run(slip,axle='Rear',speed=45,cat=0,seed=11){let p=profile();p.seed=seed;p.categoryVolume=cat;const e=new D.EngineDSP(24000,p);e.setInput({mode:'Idle',tireTestEnabled:true,tireTestSlip:slip,tireTestWheelSpeed:speed,tireTestAxle:axle});const n=24000*1.2,L=new Float32Array(n),R=new Float32Array(n);e.render(L,R);let rms=0;for(let i=n>>1;i<n;i++){assert(Number.isFinite(L[i])&&Number.isFinite(R[i]));rms+=(L[i]*L[i]+R[i]*R[i]);}rms=Math.sqrt(rms/n);return {rms,meter:e.meter,signal:L};}
const zero=run(0),rear=run(.8,'Rear'),front=run(.8,'Front'),all=run(.8,'All'),low=run(.25,'Rear'),tireCat=run(.8,'Rear',45,1),again=run(.8,'Rear');
console.log({zero:zero.rms,rear:rear.rms,front:front.rms,all:all.rms,low:low.rms,cat0:rear.rms,cat1:tireCat.rms,repeat:again.rms});
assert(rear.rms>1e-5,'Cannot hear rear axle tire spin');
assert(all.rms>rear.rms*1.05,'All-four axle selector does not engage four wheels');
assert(front.rms>1e-5,'Cannot hear front axle tires');
assert(zero.rms<rear.rms*.05,'Zero slip has noise');
assert(low.rms<rear.rms*.6,'Slip slider not modulating signal');
assert(Math.abs(tireCat.rms-rear.rms)<1e-8,'Engine category fader incorrectly changes tire bus');
assert(rear.meter.slipSource==='forced' && rear.meter.slip>.75);
assert(rear.signal.every((v,i)=>v===again.signal[i]),'Forced wheelspin not deterministic with same seed');
assert(html.includes('id="forceTireSlip"')&&html.includes('id="tireTestSlip"')&&html.includes('id="tireTestSpeed"')&&html.includes('id="tireTestAxle"'),'Missing tire controls');
console.log('PASS direct tire slip, front/rear/all, zero, modulation, bus separation, determinism, meters and UI');
