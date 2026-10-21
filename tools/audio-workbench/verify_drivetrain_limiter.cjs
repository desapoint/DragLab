'use strict';
const fs=require('fs'),vm=require('vm'),assert=require('assert/strict'),base=__dirname+'/';
const D=vm.runInThisContext(fs.readFileSync(base+'draglab_dsp_core_drivetrain.js','utf8')+'\ncreateDragLabDSP()');
function test(id,launch){const p=JSON.parse(fs.readFileSync(base+'draglab-physical-preset-'+id+'.json','utf8')).settings;
 Object.keys(p.stages).forEach(k=>p.stages[k]=false);
 for(const k of ['combustion','runner','headers','collector','exhaustBand','bodyEQ','sourceSaturation','fittedExhaust','dc','softclip'])p.stages[k]=true;
 p.popProbability=0;p.limiterDropRpm=0;
 let e=new D.EngineDSP(24000,p);e.rpm=launch?p.launchRpm+25:p.redlineRpm+300;e.launch=launch;e.throttle=e.targetThrottle=1;
 let fire=0,cut=0,energy=0;const a=new Float32Array(2400),b=new Float32Array(2400);
 for(let i=0;i<12;i++){e.render(a,b);if(e.cut)cut++;else fire++;if(i>=5)for(const x of a){assert(Number.isFinite(x));energy+=x*x;}}
 assert(e.limiterCount>=3,`${id}: no recurring limiter`);assert(cut>0&&fire>0,`${id}: failed to alternate combustion/fuel cuts`);assert(energy>0.0001,`${id}: silent at limiter`);
 console.log('PASS',id,launch?'two-step':'redline','hits',e.limiterCount,'cut/firing windows',cut+'/'+fire,'tail energy',energy.toFixed(3));}
for(const x of ['tempest','v8','na','diesel'])test(x,false);for(const x of ['tempest','v8'])test(x,true);
