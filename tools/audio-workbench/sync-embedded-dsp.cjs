'use strict';
// Keep the downloadable standalone HTML and inspection/test modules identical.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const dir=__dirname,file=path.join(dir,'DragLab_Audio_Workbench_DrivetrainFix.html');
let html=fs.readFileSync(file,'utf8');
const dsp=fs.readFileSync(path.join(dir,'draglab_dsp_core_drivetrain.js'),'utf8').trimEnd();
const start=html.indexOf('function createDragLabDSP(){'),end=html.indexOf('\nconst DSP=createDragLabDSP();');
assert(start>=0&&end>start);html=html.slice(0,start)+dsp+html.slice(end);
for(const [name,marker]of [['unity_reference_replay.js','UNITY REFERENCE DSP'],['unity_reference_ui.js','UNITY REFERENCE UI'],['audio_signal_diagnostics.js','SIGNAL DIAGNOSTICS DSP'],['audio_signal_diagnostics_ui.js','SIGNAL DIAGNOSTICS UI']]){
 const begin='/* BEGIN '+marker+' */',finish='/* END '+marker+' */';
 const content=begin+'\n'+fs.readFileSync(path.join(dir,name),'utf8').trimEnd()+'\n'+finish;
 const from=html.indexOf(begin),to=html.indexOf(finish);
 if(from>=0){assert(to>from);html=html.slice(0,from)+content+html.slice(to+finish.length);}
 else html=html.replace('controlsHtml();makeStages();bindControls();bindUnityReference();bindSignalDiagnostics();drawScopes();',content+'\ncontrolsHtml();makeStages();bindControls();bindUnityReference();bindSignalDiagnostics();drawScopes();');
}
fs.writeFileSync(file,html,'utf8');console.log('Synchronized standalone HTML DSP and reference modules.');
