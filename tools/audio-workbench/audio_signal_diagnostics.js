// Workbench-only controlled substitutions. The source browser driver is captured
// once; no candidate sound can feed back into its future RPM/load/boost/events.
function createAudioSignalDiagnostics(DSP,REF){
'use strict';
const copy=x=>JSON.parse(JSON.stringify(x)),rate=48000;
const passes=[
 ['browser','00 · Browser inputs baseline','Engine only, dry. Recorded throttle/shifts; cached browser dynamics.'],
 ['rpm','01 · + Unity RPM','Only pulse-speed input changes.'],
 ['pedal','02 · + Unity pedal / plate','Uses recorded pedal as plate, matching the existing adapter; no separately captured plate exists.'],
 ['load','03 · + Unity load','Adds transmitted-torque-derived load.'],
 ['boost','04 · + Unity boost / spool','Adds actual boost and turbo spool.'],
 ['airflow','05 · + Unity airflow','Replaces the browser airflow estimate with captured kg/s, normalized as in full replay.'],
 ['cylinders','06 · + Unity cylinder contributions','Adds individual cylinder contribution values.'],
 ['fraction','07 · + Unity combustion fraction','Feeds aggregate fraction to the pulse path and bus gain as in current replay; individual cylinder values can take precedence in the pulse core.'],
 ['cuts','08 · + Unity cuts / two-step / shift','Adds actual cut state and shift-pressure target.'],
 ['induction','09 · + Intake / induction layers','Restores intake, turbo, blower and clatter/mechanical layers from the frozen tuning.'],
 ['history','10 · Current replay intake history','Sets previous throttle to zero, reproducing the full adapter’s bypassed history.'],
 ['events','11 · + Unity backfires / BOV','Uses actual chemical funding and release history; no browser probability events.'],
 ['outdoor','12 · + Outdoor propagation','Restores frozen distance, air, reflections and stereo processing.'],
 ['endpoint','13 · Current Unity simulation endpoint','Existing full replay with the same sound tuning, including actual tires. Endpoint reference, not a one-signal step.']
].map(([id,title,description],level)=>({id,title,description,level}));
const rawFields=['rpm','throttle','targetThrottle','throttlePlate','lastThrottle','load','boost','spool','speed','gear','cut','twoStep','shiftTimer','shiftPressureTarget','tireSlip'];
const induction=['intake','turboWhistle','turboFlow','turboTurbine','blower','mechanical','clatter'];
const outdoor=['distance','air','reflections','stereo'];
const after=['afterPressure','afterTexture','afterPower','afterTravel','afterShell'];
function settingsFor(base,level){const p=copy(base);p.stages={...DSP.DEFAULT_STAGES,...p.stages};
 if(level<13){p.stages.tires=false;if(level<9)for(const k of induction)p.stages[k]=false;if(level<11)for(const k of [...after,'bov'])p.stages[k]=false;if(level<12)for(const k of outdoor)p.stages[k]=false;}
 return p;
}
function validatePair(trace,recording){REF.validate(trace);REF.validateInputs(recording);if(trace.id!==recording.id||trace.totalSamples!==(recording.ticks.length+241)*400)throw Error('Trace and driver recording must have the same ID and complete timeline.');}
class Capture extends REF.InputEngine{
 constructor(base,recording,mode){super(rate,settingsFor(base,0),recording);this.setInput({mode});this.frames=[];this.lastInput=-2;}
 inspectAudioInputs(){if(this.frames.length&&this.tickCountdown!==0&&this.inputTick===this.lastInput)return;
  this.lastInput=this.inputTick;const f={sample:this.inputCursor,random:this.rng.x};for(const key of rawFields)f[key]=this[key];this.frames.push(f);
 }
}
function beginCapture(base,trace,recording,mode='Race'){validatePair(trace,recording);return new Capture(base,recording,mode);}
function finishCapture(engine,base,trace,mode='Race'){
 if(engine.inputCursor!==trace.totalSamples)throw Error('Incomplete browser control capture.');
 return{schema:'draglab-audio-signal-diagnostic-v1',sampleRate:rate,id:trace.id,totalSamples:trace.totalSamples,mode,settings:copy(base),frames:engine.frames};
}
class DiagnosticEngine extends DSP.EngineDSP{
 constructor(base,trace,timeline,level){
  if(!Number.isInteger(level)||level<0||level>=13)throw Error('Invalid diagnostic substitution level.');
  if(timeline.schema!=='draglab-audio-signal-diagnostic-v1'||timeline.id!==trace.id||timeline.totalSamples!==trace.totalSamples||timeline.sampleRate!==rate||JSON.stringify(base)!==JSON.stringify(timeline.settings))throw Error('Diagnostic cache/settings mismatch.');
  super(rate,settingsFor(base,level));this.trace=trace;this.timeline=timeline;this.level=level;this.sampleCursor=0;this.browserCursor=-1;this.unityCursor=-1;this.referenceOptions={preserveSound:true};this.frontSeeds=new Map();this.zoneVoices=new Map();this.reactionCursor=this.releaseCursor=0;this.referenceEvents=[];this.diagnosticCoreInputs={};
 }
 stepPhysics(){} // Read-only cached controls: substitutions cannot alter driving.
 react(e){REF.ReferenceEngine.prototype.react.call(this,e);}
 publish(sample){
  const frames=this.timeline.frames;let changed=false;
  while(this.browserCursor+1<frames.length&&frames[this.browserCursor+1].sample<=sample){this.browserCursor++;changed=true;}
  const ui=Math.min(this.trace.frames.length-1,Math.floor(sample/400));if(!changed&&ui===this.unityCursor)return;
  const b=frames[this.browserCursor],f=this.trace.frames[ui],e=f.engine;this.unityCursor=ui;
  // Lock stochastic acoustic phase at source-control boundaries. This reproduces
  // the baseline exactly despite the browser sharing physics/audio RNG draws.
  if(changed)this.rng.x=b.random;for(const key of rawFields)this[key]=b[key];
  if(this.level>=1)this.rpm=e.rpm;
  if(this.level>=2)this.throttle=this.targetThrottle=this.throttlePlate=e.throttle;
  if(this.level>=3)this.load=e.load;
  if(this.level>=4){this.boost=e.boost;this.spool=e.turboSpool;}
  if(this.level>=5)this.diagnosticCoreInputs.airflow=Math.max(0,Math.min(1,e.airflowKgS/Math.max(.001,this.trace.hardware.profile.turboFlowCapacityKgS)));
  if(this.level>=6)this.diagnosticCoreInputs.cylinderContribution=e.cylinderContribution;
  if(this.level>=7){this.diagnosticCoreInputs.combustionFraction=e.combustionFraction;this.diagnosticGainFraction=e.combustionFraction;}
  if(this.level>=8){this.cut=e.cut;this.twoStep=e.twoStep;this.shiftPressureTarget=e.combustionFraction;this.shiftTimer=f.state.shiftRemaining;}
  if(this.level>=10)this.lastThrottle=0;
 }
 renderStereo(){
  const sample=this.sampleCursor;if(sample>=this.trace.totalSamples){this.meter.referenceDone=true;return[0,0];}this.publish(sample);
  if(this.level>=11){const clock=sample/rate;
   while(this.reactionCursor<this.trace.reactions.length){const e=this.trace.reactions[this.reactionCursor];if(e.availableSample>sample||e.time>clock)break;this.react(e);this.reactionCursor++;this.referenceEvents.push({kind:'reaction',index:e.index,sample,seed:e.seed});}
   while(this.releaseCursor<this.trace.releases.length){const e=this.trace.releases[this.releaseCursor];if(e.availableSample>sample||e.time>clock)break;this.bovs.push({age:Math.max(0,clock-e.time),strength:e.strength,phase:0,low:0});this.releaseCursor++;this.bovCount++;this.referenceEvents.push({kind:'release',index:e.index,sample});}
  }
  const audio=super.renderStereo();this.sampleCursor++;return audio;
 }
}
function createEngine(base,trace,timeline,level){return level===13?new REF.ReferenceEngine(rate,copy(base),trace,{preserveSound:true}):new DiagnosticEngine(base,trace,timeline,level);}
return{passes,rawFields,settingsFor,validatePair,beginCapture,finishCapture,DiagnosticEngine,createEngine};
}
