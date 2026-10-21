'use strict';
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const dir=__dirname;
const html=fs.readFileSync(path.join(dir,'DragLab_Audio_Workbench_DrivetrainFix.html'),'utf8');
const core=html.slice(html.indexOf('function createDragLabDSP(){'),html.indexOf('\nconst DSP=createDragLabDSP();'));
const Lib=vm.runInNewContext(core+'\ncreateDragLabDSP();');
const settings=id=>{
 const p=JSON.parse(fs.readFileSync(path.join(dir,'draglab-physical-preset-'+id+'.json'),'utf8')).settings;
 p.forwardGears=6;p.shiftStrategy='TorqueCut';p.popProbability=0;p.overrunPopRate=0;return p;
};
const tick=(e,time,dt=.01)=>{for(let t=0;t<time-1e-8;t+=dt)e.stepPhysics(dt);};
function assertNear(x,y,epsilon,message){assert(Math.abs(x-y)<=epsilon,message+': '+x+' vs '+y);}
for(const id of ['tempest','v8','na','diesel']){
 const p=settings(id);
 // Off throttle should not accelerate a stationary car or hold a high idle.
 let e=new Lib.EngineDSP(24000,p);e.setInput({mode:'Race',throttle:0});tick(e,2);
 assertNear(e.rpm,p.idleRpm,3,id+' idle rpm');assertNear(e.speed,0,1e-6,id+' idle speed');
 e.setInput({mode:'Idle',throttle:1});tick(e,.75);assert(e.rpm>p.idleRpm+550,id+': neutral acceleration missing');
 const hi=e.rpm;e.setInput({throttle:0});tick(e,.85);assert(e.rpm<hi-250,id+': release did not decelerate');
 tick(e,6);assertNear(e.rpm,p.idleRpm,15,id+' settled rpm');
 e=new Lib.EngineDSP(24000,p);e.setInput({mode:'Race',throttle:1});tick(e,2);
 const movingSpeed=e.speed, movingRpm=e.rpm;
 assert(movingSpeed>0,id+' should launch');
 e.setInput({throttle:0});tick(e,1.5);assert(e.speed<movingSpeed,id+' phantom acceleration with pedal up');assert(e.rpm<=movingRpm+15,id+' rpm climbs on zero throttle');
 // Shift cap prevents audio/event retriggers and increments, including turbo BOV.
 e=new Lib.EngineDSP(24000,p);e.run=true;e.gear=6;e.rpm=3500;e.throttle=e.targetThrottle=1;e.spool=.9;e.boost=.7;
 const bov=e.bovCount,pop=e.afterCount;
 assert.equal(e.shift(),false,id+' top gear should refuse');assert.equal(e.gear,6);assert.equal(e.shiftTimer,0);
 assert.equal(e.bovCount,bov);assert.equal(e.afterCount,pop);
 console.log('PASS',id,'off-throttle deceleration','top gear clamp');
}
const p=settings('tempest');
function charged(strategy, pre=.75){const e=new Lib.EngineDSP(24000,{...p,shiftStrategy:strategy});e.setInput({mode:'Race',throttle:1});e.throttle=1;e.targetThrottle=1;e.lastPlate=1;e.throttlePlate=1;e.rpm=5100;e.speed=22;e.gear=2;e.boost=pre;e.spool=.9;return e;}
{
 const e=charged('TorqueCut');const before=e.boost;assert.equal(e.shift(),true);tick(e,.14);assert.equal(e.bovCount,0,'torque cut created fictitious BOV');assert(e.boost>before*.65,'torque cut lost too much boost');assert.equal(e.gear,3);
 console.log('PASS','torque cut: no compulsory BOV, boost mostly held',e.boost.toFixed(3));
}
{
 const e=charged('ThrottleLift');const before=e.boost;assert.equal(e.shift(),true);tick(e,.12);assert.equal(e.bovCount,1,'lift shift did not open BOV');assert(e.boost<before*.45,'lift shift did not dump manifold boost');assert(e.spool>.55,'shaft spool unrealistically collapsed');
 tick(e,.65);assert(e.boost>.1,'manifold boost does not recover');
 console.log('PASS','throttle lift: BOV, pressure release and spool inertia',e.boost.toFixed(3));
}
{
 const e=charged('ThrottleLift',.04);e.shift();tick(e,.12);assert.equal(e.bovCount,0,'unboosted BOV should be silent');
 console.log('PASS','no BOV without sufficient pre-lift manifold pressure');
}
{
 const e=new Lib.EngineDSP(24000,p);e.setInput({auto:true});tick(e,2.7);assert(e.targetThrottle>.5,'script failed to set pedal');
 e.setInput({throttle:0});assert.equal(e.autoScenario,false,'manual pedal did not cancel scripted input');tick(e,.5);assertNear(e.throttle,0,.02,'manual takeover leaves throttle stuck');
 console.log('PASS','manual input stops scripted throttle');
}
{
 // Validate audio remains present while RPM descends under both shift strategies.
 const audio=(strategy)=>{const e=charged(strategy);e.setInput({mode:'Race',throttle:1});e.shift();let values=[],last=e.rpm;for(let i=0;i<9;i++){let l=new Float32Array(960),r=new Float32Array(960);e.render(l,r);let rms=Math.sqrt(l.reduce((a,x)=>a+x*x,0)/l.length);assert(Number.isFinite(rms));values.push(rms);}assert(e.rpm<last-50,strategy+' rpm did not descend');assert(values.slice(2,7).every(x=>x>1e-6),strategy+' silence during shift');return values;};
 audio('TorqueCut');audio('ThrottleLift');console.log('PASS','combustion audio through both shift strategies');
}
assert(!core.includes("this.release(amount)"),'old forced BOV remains');
assert(html.includes("id=\"releaseThrottle\""));
assert(html.includes("id=\"pedalMeter\""));
assert(html.includes("'shiftStrategy','Shift control'"));
console.log('PASS browser embedded DSP and controls: all drivetrain regressions');
