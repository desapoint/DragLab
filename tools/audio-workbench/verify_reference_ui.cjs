'use strict';
// DOM-independent UI wiring and actual AudioWorklet processor contract. Does not
// launch a browser or substitute for listening / visual browser acceptance.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const dir=__dirname,folder=process.argv[2]||path.resolve(dir,'../../reports/unity-audio-reference-20261021');
const html=fs.readFileSync(path.join(dir,'DragLab_Audio_Workbench_DrivetrainFix.html'),'utf8'),ui=fs.readFileSync(path.join(dir,'unity_reference_ui.js'),'utf8');
const factory=vm.runInThisContext(fs.readFileSync(path.join(dir,'draglab_dsp_core_drivetrain.js'),'utf8')+'\ncreateDragLabDSP');
const referenceFactory=vm.runInThisContext(fs.readFileSync(path.join(dir,'unity_reference_replay.js'),'utf8')+'\ncreateUnityReferenceReplay');
new vm.Script(html.slice(html.indexOf('<script>')+8,html.indexOf('</script>',html.indexOf('<script>'))));
const source=fs.existsSync(path.join(folder,'03-i4-turbo-hiss.audio-trace.json'))?'03-i4-turbo-hiss.audio-trace.json':fs.readdirSync(folder).filter(n=>n.endsWith('.audio-trace.json')).sort()[0];
const trace=JSON.parse(fs.readFileSync(path.join(folder,source),'utf8'));
const elements=new Map(),calls=[];const element=id=>{if(!elements.has(id))elements.set(id,{disabled:false,value:'free',style:{},textContent:'',pause(){calls.push('wav pause');},click(){},addEventListener(){}});return elements.get(id);};
const context=vm.createContext({DSP:factory(),createUnityReferenceReplay:referenceFactory,params:JSON.parse(fs.readFileSync(path.join(dir,'draglab-physical-preset-tempest.json'),'utf8')).settings,
 $:element,document:{querySelector:s=>element(s),createElement:()=>({click(){},style:{}})},URL:{createObjectURL:()=> 'blob:test',revokeObjectURL(){}},
 stopAudio:()=>calls.push('stop'),startAudio:async()=>calls.push('start'),postSettings:x=>calls.push(['settings',x]),postInput:x=>calls.push(['input',x]),
 refreshControls(){},partEditor(){},refreshStages(){},logEvent(){},renderWav:x=>calls.push(['free render',x]),alert:x=>{throw Error(x);},renderBusy:false,console});
vm.runInContext(html.slice(html.indexOf('const groups={'),html.indexOf('\nconst stageGroups='))+'\n'+ui,context);
context.bindUnityReference();assert(element('referenceMode').disabled);context.loadUnityTrace(trace);
assert.equal(element('referenceMode').value,'unity');assert(element('shift').disabled&&element('throttle').disabled&&element('preset').disabled);
assert(element('field-cylinders').disabled&&element('field-cutMethod').disabled&&element('field-popProbability').disabled);
assert(!element('field-afterDecay').disabled&&!element('field-turboGain').disabled,'acoustic controls were locked');
assert(element('referenceInfo').textContent.includes(trace.title));
const initialSound=JSON.stringify(context.params);
const driver={id:'controls',title:'Driver inputs',ticks:Array.from({length:10},(_,i)=>({input:{throttle:i/10,shiftUp:i===4,shiftDown:i===8}}))};
context.loadUnityInputs(driver);assert.equal(element('referenceMode').value,'inputs');
assert(element('shiftDown').disabled&&element('throttle').disabled&&!element('raceMode').disabled&&!element('preset').disabled);
assert.equal(JSON.stringify(context.params),initialSound,'loading driver inputs replaced sound tuning');
assert(context.makeAuditionEngine(48000,context.params).recording);
element('soundSource').value='captured';element('soundSource').onchange();assert.equal(context.params.seed,trace.seeds.engine);
element('soundSource').value='browser';element('soundSource').onchange();assert.equal(JSON.stringify(context.params),initialSound);
element('referenceMode').value='unity';element('referenceMode').onchange();
let e=context.makeAuditionEngine(48000,context.params);assert(e.trace);assert.equal(e.trace.frames.length,trace.frames.length);
assert(e.referenceOptions.preserveSound);
context.attachUnityReferenceWav({},'native.wav');assert.equal(element('unityWav').src,'blob:test');element('unityWav').onplay();assert.equal(calls.at(-1),'stop');
element('referenceMode').value='free';element('referenceMode').onchange();assert(!element('throttle').disabled&&!element('field-cylinders').disabled);assert(!context.makeAuditionEngine(48000,context.params).trace);
vm.runInContext('renderUnityReference=(compare)=>logEvent(compare);',context);let compared=null;context.logEvent=x=>compared=x;
element('referenceMode').value='unity';element('renderAB').onclick();assert.equal(compared,true);
element('referenceMode').value='free';element('renderSingle').onclick();assert.equal(calls.at(-1)[0],'free render');
let Processor;
const raw=html.match(/try\{const script=`([\s\S]*?registerProcessor\('draglab-dsp',DragLabProcessor\);)`;/)[1];
const compiled=raw.replace('${createDragLabDSP.toString()}',factory.toString()).replace('${createUnityReferenceReplay.toString()}',referenceFactory.toString());
const worklet=vm.createContext({sampleRate:48000,AudioWorkletProcessor:class{constructor(){this.port={postMessage:m=>calls.push(m)};}},registerProcessor:(name,type)=>{assert.equal(name,'draglab-dsp');Processor=type;}});
vm.runInContext(compiled,worklet);
const settings=context.params,processor=new Processor({processorOptions:{settings,trace}}),left=new Float32Array(128),right=new Float32Array(128);
for(let i=0;i<50;i++)processor.process([],[[left,right]]);
assert(processor.engine.referenceCursor===6400&&processor.engine.rpm===trace.frames[15].engine.rpm);assert(calls.some(x=>x?.type==='meter'&&x.meter.reference));
processor.port.onmessage({data:{type:'input',input:{throttle:1,pop:'manual',bov:true}}});assert.equal(processor.engine.afterCount,0);
processor.port.onmessage({data:{type:'settings',settings,trace:null,hard:true}});assert(!processor.engine.trace);assert.equal(processor.engine.rpm,settings.idleRpm,'Free inherited replay RPM');
processor.port.onmessage({data:{type:'settings',settings,trace,hard:true}});assert(processor.engine.trace&&processor.engine.referenceCursor===0);
processor.port.onmessage({data:{type:'settings',settings,trace:driver,mode:'inputs',hard:true}});
processor.port.onmessage({data:{type:'input',input:{mode:'Race',throttle:1,pop:'manual',bov:true}}});
for(let i=0;i<8;i++)processor.process([],[[left,right]]);
assert(processor.engine.recording&&!processor.engine.unityFrame);assert.equal(processor.engine.inputCursor,1024);assert.equal(processor.engine.targetThrottle,.1);
processor.port.onmessage({data:{type:'settings',settings,trace,mode:'unity',options:{preserveSound:true},hard:true}});assert(processor.engine.referenceOptions.preserveSound);
// Execute the real key binding function with DOM/audio stubs, not a copied map.
const handlers={},keys=vm.createContext({window:{addEventListener:(k,f)=>handlers[k]=f},document:{addEventListener(){}},$:element,postInput:x=>calls.push(['key',x]),referenceActive:()=>false,sendControls(){},stopAudio(){},renderTireWav(){}});
vm.runInContext(html.slice(html.indexOf('function bindControls(){'),html.indexOf('\nfunction ',html.indexOf('function bindControls(){')+1))+'\nbindControls();',keys);
handlers.keydown({repeat:false,target:{tagName:'BODY'},code:'KeyE'});assert.equal(calls.at(-1)[1].shift,true);
handlers.keydown({repeat:false,target:{tagName:'BODY'},code:'KeyQ'});assert.equal(calls.at(-1)[1].shiftDown,true);
const length=calls.length;handlers.keydown({repeat:false,target:{tagName:'INPUT'},code:'KeyQ'});assert.equal(calls.length,length);
keys.referenceActive=()=>true;handlers.keydown({repeat:false,target:{tagName:'BODY'},code:'KeyE'});assert.equal(calls.length,length);
const report='PASS standalone HTML parses and embeds inspected modules\nPASS trace import and three-mode switching\nPASS physical controls locked in full replay; sound controls editable\nPASS driver-only mode permits browser geometry and race/neutral selection\nPASS importing inputs preserves sound tuning; captured sound can be restored to Free tuning\nPASS matching Unity WAV and generator playback are exclusive\nPASS reference and free WAV render buttons route correctly\nPASS actual AudioWorklet advances recorded frames and publishes meters\nPASS worklet switches driver/full/free modes and forwards sound-preservation options\nPASS actual E/Q key bindings, editable-field exclusion and replay keyboard lock\nLIMIT browser visual/device/listening review remains manual; no real browser automation was performed.\n';
fs.writeFileSync(path.join(process.argv[3]||folder,'ui-contract-verification.txt'),report,'utf8');console.log(report);
