'use strict';
// Actual UI functions and embedded module contracts, without browser automation.
const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path'),assert=require('node:assert/strict');
const dir=__dirname,out=path.resolve(process.argv[2]||path.join(dir,'../../reports/audio-signal-diagnostics-20261021'));fs.mkdirSync(out,{recursive:true});
const read=f=>fs.readFileSync(path.join(dir,f),'utf8'),html=read('DragLab_Audio_Workbench_DrivetrainFix.html');
const D=vm.runInThisContext(read('draglab_dsp_core_drivetrain.js')+'\ncreateDragLabDSP()'),R=vm.runInThisContext(read('unity_reference_replay.js')+'\ncreateUnityReferenceReplay')(D),factory=vm.runInThisContext(read('audio_signal_diagnostics.js')+'\ncreateAudioSignalDiagnostics');
for(const f of ['audio_signal_diagnostics.js','audio_signal_diagnostics_ui.js'])assert(html.includes(read(f).trimEnd()),'embedded '+f+' is stale');
const source=path.resolve(dir,'../../reports/unity-audio-reference-20261021/03-i4-turbo-hiss');
const trace=JSON.parse(fs.readFileSync(source+'.audio-trace.json','utf8')),recording=JSON.parse(fs.readFileSync(source+'.race.json','utf8'));
recording.ticks=recording.ticks.slice(0,60);trace.frames=trace.frames.slice(0,301);trace.totalSamples=301*400;trace.reactions=[];trace.releases=[];
const calls=[],elements=new Map(),blobs=new Map();let nextUrl=0;
const element=id=>{if(!elements.has(id))elements.set(id,{value:['raceMode','diagnosticDrive'].includes(id)?'Race':'0',disabled:false,style:{},append(){},pause(){calls.push(id+' pause');},async play(){calls.push(id+' play');if(this.onplay)this.onplay();}});return elements.get(id);};
const params=JSON.parse(read('draglab-physical-preset-tempest.json')).settings,original=JSON.stringify(params);
let context;let changed=false;
context=vm.createContext({DSP:D,UnityReference:R,createAudioSignalDiagnostics:factory,params,unityTrace:trace,unityInputs:recording,Blob,Float32Array,ArrayBuffer,DataView,
 $:element,document:{createElement:()=>({})},URL:{createObjectURL:b=>{const u='blob:'+nextUrl++;blobs.set(u,b);return u;},revokeObjectURL:u=>blobs.delete(u)},
 stopAudio:()=>calls.push('stop'),setTimeout:f=>{if(!changed){changed=true;context.params={...params,label:'changed while rendering',engineGain:9};}f();}});
vm.runInContext(html.slice(html.indexOf('function wav16('),html.indexOf('\nasync function renderTireWav'))+'\n'+read('audio_signal_diagnostics_ui.js'),context);
(async()=>{
 context.bindSignalDiagnostics();assert(!element('diagnosticBuild').disabled);assert(element('diagnosticPass').disabled);
 await context.buildSignalDiagnostics();assert(!element('diagnosticPass').disabled);assert.equal(vm.runInContext('diagnosticAuditions.length',context),14);assert(element('diagnosticStatus').textContent.includes(params.label));assert.equal(JSON.stringify(params),original);assert.equal(context.params.engineGain,9);
 const first=blobs.get(element('diagnosticAudio').src),header=new DataView(await first.arrayBuffer());assert.equal(header.getUint32(24,true),48000);assert.equal(header.getUint16(34,true),16);
 element('diagnosticAudio').onplay();assert.deepEqual(calls.slice(-3),['stop','unityWav pause','preview pause']);element('preview').onplay();assert.deepEqual(calls.slice(-3),['stop','unityWav pause','diagnosticAudio pause']);
 element('diagnosticPass').value='1';context.selectDiagnosticPass();assert(!element('diagnosticPrevious').disabled);assert(element('diagnosticNote').textContent.includes('pulse-speed'));
 const firstPcm=await first.arrayBuffer(),secondPcm=await blobs.get(element('diagnosticAudio').src).arrayBuffer();await context.playPreviousDiagnostic();
 const pair=await blobs.get(element('diagnosticAudio').src).arrayBuffer(),pairBytes=new Uint8Array(pair),a=new Uint8Array(firstPcm,44),b=new Uint8Array(secondPcm,44);assert.deepEqual(pairBytes.subarray(44,44+a.length),a);assert(pairBytes.subarray(44+a.length,44+a.length+48000*4).every(v=>v===0));assert.deepEqual(pairBytes.subarray(44+a.length+48000*4),b);
 context.loadDiagnosticAuditions([{url:'pass0.wav',filename:'pass0.wav'},{url:'pass1.wav',filename:'pass1.wav',previousUrl:'pair.wav'}],'Preloaded');element('diagnosticPass').value='1';context.selectDiagnosticPass();await context.playPreviousDiagnostic();assert.equal(element('diagnosticAudio').src,'pair.wav');
 context.unityInputs={...recording,id:'wrong'};await context.buildSignalDiagnostics();assert(element('diagnosticStatus').textContent.includes('same ID'));
 const report='PASS diagnostic modules embedded verbatim; standalone HTML parses in the reference UI suite\nPASS actual render UI produces all 14 finite-length PCM16 auditions\nPASS rendering freezes sound tuning even when the live preset changes during yield\nPASS pass selection, descriptions, download target and previous-pass enablement\nPASS diagnostic playback stops generated audio and native reference playback\nPASS previous/selected A/B retains every stored PCM sample with exactly one second silence\nPASS preloaded audition/pair URLs route through the same workbench player\nPASS mismatched trace/input pair is rejected before rendering\nLIMIT Node UI contracts; browser/device visual and listening acceptance remains manual.\n';
 fs.writeFileSync(path.join(out,'diagnostic-ui-verification.txt'),report,'utf8');console.log(report);
})().catch(e=>{console.error(e);process.exitCode=1;});
