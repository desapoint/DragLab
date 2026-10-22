'use strict';
// Prove that opt-in diagnostic hooks leave normal browser playback unchanged.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),cp=require('node:child_process'),assert=require('node:assert/strict');
const dir=__dirname,root=path.resolve(dir,'../..'),out=path.resolve(process.argv[2]||path.join(root,'reports/audio-signal-diagnostics-20261021'));
const current=vm.runInThisContext(fs.readFileSync(path.join(dir,'draglab_dsp_core_drivetrain.js'),'utf8')+'\ncreateDragLabDSP()');
const original=vm.runInThisContext(cp.execFileSync('git',['show','dd32554:tools/audio-workbench/draglab_dsp_core_drivetrain.js'],{cwd:root,encoding:'utf8'})+'\ncreateDragLabDSP()');
const lines=[];
for(const id of ['tempest','v8','na','diesel']){
 const p=JSON.parse(fs.readFileSync(path.join(dir,'draglab-physical-preset-'+id+'.json'),'utf8')).settings,n=Math.round(15.9*24000);
 const a=new current.EngineDSP(24000,p),b=new original.EngineDSP(24000,p);a.setInput({auto:true});b.setInput({auto:true});
 const l=new Float32Array(n),r=new Float32Array(n),x=new Float32Array(n),y=new Float32Array(n);a.render(l,r);b.render(x,y);assert.deepEqual(l,x);assert.deepEqual(r,y);
 lines.push('PASS '+id+' full Free sequence at 24 kHz remains sample-identical to dd32554 without diagnostic hooks');
}
fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'unmodified-mode-verification.txt'),lines.join('\n')+'\n','utf8');console.log(lines.join('\n'));
