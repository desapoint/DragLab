/* Standalone browser/Node reference adapter. It consumes Unity snapshots, never
 * re-simulates the drivetrain. Browser acoustic transfer functions remain an
 * approximation; the matching Unity WAV is the authoritative sound reference. */
function createUnityReferenceReplay(DSP){
'use strict';
const copy=x=>JSON.parse(JSON.stringify(x));
function validate(trace){
 if(trace?.schema!=='draglab-unity-audio-trace-v1')throw Error('Expected a DragLab Unity audio trace v1.');
 if(trace.sampleRate!==48000||trace.samplesPerFrame!==400||trace.physicsRate!==120||trace.channels!==2)throw Error('Unsupported Unity trace clock/format.');
 if(!Array.isArray(trace.frames)||!trace.frames.length||trace.frames.length*400!==trace.totalSamples)throw Error('Incomplete Unity frame timeline.');
 if(!trace.hardware?.profile||!trace.listener||!trace.seeds)throw Error('Missing captured hardware, seeds or listener.');
 const finite=x=>typeof x==='number'&&Number.isFinite(x);
 function numbers(x){if(typeof x==='number'&&!finite(x))throw Error('Non-finite trace input.');if(x&&typeof x==='object')Object.values(x).forEach(numbers);}
 numbers(trace);
 trace.frames.forEach((f,i)=>{
  if(f.sample!==i*400||Math.abs(f.time-f.sample/48000)>1e-10||!finite(f.mixtureTime)||f.mixtureTime<0)throw Error('Invalid frame timestamp.');
  if(!f.engine||!f.tires||!f.state||!Array.isArray(f.engine.cylinderContribution))throw Error('Missing audio input snapshot.');
  for(const k of ['rpm','throttle','load','boost','volume','combustionFraction','airflowKgS','turboSpool','afterfireVolume'])if(!finite(f.engine[k]))throw Error('Missing engine input: '+k);
  for(const k of ['fl','fr','rl','rr'])if(!f.tires[k]||!finite(f.tires[k].intensity)||!finite(f.tires[k].slidingSpeed)||!finite(f.tires[k].radiusM))throw Error('Missing wheel contact: '+k);
 });
 function events(items,reaction){
  if(!Array.isArray(items))throw Error('Missing event history.');
  let last=-1,lastTime=-1;items.forEach((e,i)=>{
   if(e.index!==i+(reaction?0:1)||!Number.isInteger(e.availableSample)||e.availableSample%400||e.availableSample<last||e.availableSample>=trace.totalSamples||!finite(e.time)||e.time<lastTime)throw Error('Invalid event ordering/publication time.');
   if(reaction){if(typeof e.seed!=='string'||!/^\d+$/.test(e.seed)||BigInt(e.seed)>0xffffffffffffffffn)throw Error('A reaction seed must be a lossless decimal UInt64 string.');if(!finite(e.acousticJ)||e.acousticJ<0||!e.settings)throw Error('Missing reaction energy/colour.');}
   last=e.availableSample;lastTime=e.time;
  });
 }
 events(trace.reactions,true);events(trace.releases,false);
 return trace;
}
function enumName(trace,type,value){const name=trace.enumNames?.[type]?.[value];if(!name)throw Error('Unknown captured '+type+' value '+value);return name;}
const afterFields={crack:'Crack',edge:'Edge',pulse:'Pulse',thump:'Thump',size:'Size',bark:'Bark',tube:'Tube',tune:'Tune',decay:'Decay',jet:'Jet',flameTime:'Flame',scatter:'Scatter',smooth:'Smooth',air:'Air',space:'Space'};
function settingsFromTrace(base,trace){
 validate(trace);const p=copy(base),h=trace.hardware.profile,a=h.acoustics,g=trace.listener;
 Object.assign(p,{label:trace.title,seed:trace.seeds.engine,cylinders:h.cylinders,displacementLitres:h.litres,layout:enumName(trace,'EngineLayout',h.layout),fueling:enumName(trace,'FuelSystem',h.fueling),induction:enumName(trace,'InductionType',h.induction),blowOff:enumName(trace,'BlowOffType',h.valve),
  idleRpm:h.idleRpm,redlineRpm:h.redlineRpm,forwardGears:trace.spec.gears.length,engineGain:h.engineGain,turboGain:h.turboGain,superchargerGain:h.chargerGain,blowOffGain:h.releaseGain,
  boostBar:h.boostBar,spoolSeconds:h.spoolSeconds,turboSize:h.turboSize,superchargerRatio:h.chargerRatio,exhaustStage:h.exhaust,idleBoostDb:20*Math.log10(h.idleBoostGain),exhaustBassGain:h.bassGain,exhaustBrightness:h.voiceBrightness,rawPulseGain:h.rawPulseGain,
  mechanicalClatter:h.mechanicalClatter,mixtureVariation:h.mixtureVariation,camOverlap:h.camOverlap,compressorNoiseGain:h.compressorNoiseGain,compressorNoise:trace.compressorNoise,popGain:h.popGain,popSharpness:h.popSharpness,popDecay:h.popDecay,
  stereoWidth:a.stereoWidth,turbineDamping:a.turbineDamping,runnerLengthMeters:trace.hardware.runnersM.reduce((x,y)=>x+y,0)/h.cylinders,collectorPathLengthMeters:.0088*343*a.collectorScale,resonatorLengthMeters:.5/a.resonanceScale,
  pressureAttackMs:a.attackSeconds*1000,blowdownMs:a.blowdownSeconds*1000,rarefactionMs:a.rarefactionSeconds*1000,
  exhaustLength:trace.hardware.exhaustPathM,masterVolume:trace.master,categoryVolume:1,tireGain:1,listenerX:g.longitudinal,listenerY:g.lateral,airTempK:g.temperatureK,humidity:g.humidity,reflectionScale:g.reflectionScale,popProbability:0,overrunPopRate:0});
 p.exhaustParts=h.exhaustSystem.parts.map(x=>({...x,kind:enumName(trace,'ExhaustPartKind',x.kind)}));
 const controls=trace.reactions[0]?.settings;if(controls)for(const [key,suffix]of Object.entries(afterFields))p['after'+suffix]=controls[key];
 p.stages={...DSP.DEFAULT_STAGES,...base.stages};return p;
}
class ReferenceEngine extends DSP.EngineDSP{
 constructor(sr,settings,trace,options={}){super(sr,settings);this.trace=validate(trace);this.referenceOptions={preserveSound:!!options.preserveSound};this.referenceCursor=0;this.referenceFrame=-1;this.reactionCursor=this.releaseCursor=0;this.frontSeeds=new Map();this.zoneVoices=new Map();this.referenceEvents=[];
  // Captured individual runners and firing order supersede the browser templates.
  // The controlled tuning comparison explicitly retains the Free engine's
  // prepared geometry/order instead; it still consumes the same physical trace.
  if(!this.referenceOptions.preserveSound){const mean=trace.hardware.runnersM.reduce((a,b)=>a+b,0)/trace.hardware.runnersM.length;
   this.core.runners=trace.hardware.runnersM.map(x=>x*settings.runnerLengthMeters/mean);this.core.firingOrder=trace.hardware.firingOrder.slice();}
 }
 setInput(o){if(o.reset)this.restart();} // Driver/manual event injection is intentionally disabled in replay.
 restart(){const next=new ReferenceEngine(this.sr,this.p,this.trace,this.referenceOptions);Object.assign(this,next);}
 stepPhysics(){} // No second drivetrain, limiter, turbo controller or probability roll.
 publish(sample){
  const at=Math.min(this.trace.frames.length-1,Math.floor(sample/400)),f=this.trace.frames[at];
  if(at===this.referenceFrame)return;
  this.referenceFrame=at;const keep=this.referenceOptions.preserveSound;
  // Captured bus volumes are playback preferences, not physical excitation.
  // Keep the Free preset's own master/category/tire faders in the isolation pass.
  this.unityFrame=keep?{...f.engine,volume:1,afterfireVolume:1}:f.engine;
  this.unityTires=keep?{...f.tires,volume:1}:f.tires;const e=f.engine,st=f.state;
  Object.assign(this,{rpm:e.rpm,throttle:e.throttle,targetThrottle:e.throttle,throttlePlate:e.throttle,load:e.load,boost:e.boost,spool:e.turboSpool,speed:e.speed,gear:st.gear,cut:e.cut,twoStep:e.twoStep,shiftTimer:st.shiftRemaining,shiftPressureTarget:e.combustionFraction});
  this.meter={rpm:e.rpm,boost:e.boost,speed:e.speed*3.6,gear:st.gear,gearCap:this.trace.spec.gears.length,events:this.afterCount,throttle:e.throttle,throttlePlate:e.throttle,spool:e.turboSpool,autoScenario:false,slip:Math.max(...['fl','fr','rl','rr'].map(k=>f.tires[k].slip)),slipSource:'Unity contacts',limiter:st.limiter?1:0,cutActive:e.cut,shiftActive:st.shiftRemaining>0,shiftPressure:e.combustionFraction,twoStep:e.twoStep,reference:true,referenceSeconds:sample/48000,referenceDuration:this.trace.totalSamples/48000,referenceStage:f.stage};
 }
 react(e){
  // One continuing energy reservoir per actual flame/location. Never create a
  // fresh bang for every integration packet, nor pre-sum a future whole flame.
  if(e.acousticJ<1e-12)return;
  const key=e.frontId+':'+e.zone;let voice=this.zoneVoices.get(key);
  if(!this.frontSeeds.has(e.frontId)){const s=BigInt(e.seed);this.frontSeeds.set(e.frontId,Number((s^(s>>32n))&0xffffffffn));}
  if(!voice||voice.i>voice.end+32){
   const p=copy(this.p),anchor=this.trace.reactions[0]?.settings||e.settings;
   if(!this.referenceOptions.preserveSound)for(const [field,suffix]of Object.entries(afterFields)){const control='after'+suffix;p[control]=Math.max(.001,e.settings[field]*(p[control]/Math.max(.001,anchor[field])));}
   voice=new DSP.Backfire(this.sr,p,this.frontSeeds.get(e.frontId));if(!this.referenceOptions.preserveSound)voice.gain=p.popGain*this.trace.hardware.afterfireReferenceGain;
   const strength=Math.sqrt(e.acousticJ/.2);for(const k of ['crackE','bodyE','barkE','tailE','colourE','diffE'])voice[k]=strength;
   for(const pulse of voice.pulses)for(const k of ['y','d','z','zd'])pulse[k]*=strength;
   this.zoneVoices.set(key,voice);this.after.push(voice);this.afterCount++;this.emitEvent('Unity flame '+e.frontId+' / zone '+e.zone);
  }else for(const k of ['crackE','bodyE','barkE','tailE','colourE','diffE'])voice[k]=Math.sqrt(voice[k]*voice[k]+e.acousticJ/.2);
  voice.end=Math.max(voice.end,voice.i+Math.ceil(Math.max(.15,e.burnSeconds*6+.15)*voice.fs));
 }
 renderStereo(){
  const sample=Math.floor(this.referenceCursor*this.trace.sampleRate/this.sr+1e-8);
  if(sample>=this.trace.totalSamples){this.meter.referenceDone=true;return[0,0];}
  this.publish(sample);const clock=sample/this.trace.sampleRate;
  while(this.reactionCursor<this.trace.reactions.length){const e=this.trace.reactions[this.reactionCursor];if(e.availableSample>sample||e.time>clock)break;this.react(e);this.reactionCursor++;this.referenceEvents.push({kind:'reaction',index:e.index,sample,seed:e.seed});}
  while(this.releaseCursor<this.trace.releases.length){const e=this.trace.releases[this.releaseCursor];if(e.availableSample>sample||e.time>clock)break;this.bovs.push({age:Math.max(0,clock-e.time),strength:e.strength,phase:0,low:0});this.releaseCursor++;this.bovCount++;this.referenceEvents.push({kind:'release',index:e.index,sample});this.emitEvent('Unity BOV '+e.index);}
  const out=super.renderStereo();this.referenceCursor++;return out;
 }
}
// Driver-only replay: deliberately never reads captured engine frames, chemical
// reactions, BOV releases, brakes, clutch or launch-control commands.
function validateInputs(recording){
 if(!recording||typeof recording.id!=='string'||!Array.isArray(recording.ticks)||!recording.ticks.length)throw Error('Expected a Unity *.race.json driver recording.');
 for(const tick of recording.ticks){const i=tick.input;if(!i||!Number.isFinite(i.throttle)||i.throttle<0||i.throttle>1||typeof i.shiftUp!=='boolean'||typeof i.shiftDown!=='boolean'||i.shiftUp&&i.shiftDown)throw Error('Invalid throttle/shift recording.');}
 return recording;
}
class InputEngine extends DSP.EngineDSP{
 constructor(sr,settings,recording){super(sr,settings);this.recording=validateInputs(recording);this.inputCursor=0;this.inputTick=-2;this.inputEvents=[];this.inputMode='Idle';}
 setInput(o){if(o.reset){const next=new InputEngine(this.sr,this.p,this.recording);next.inputMode=this.inputMode;Object.assign(this,next);}if(o.mode!==undefined)this.inputMode=o.mode==='Race'?'Race':'Idle';}
 renderStereo(){
  const sample=Math.floor(this.inputCursor*48000/this.sr+1e-8),total=(this.recording.ticks.length+241)*400;
  if(sample>=total){this.meter.referenceDone=true;return[0,0];}
  const tick=Math.min(this.recording.ticks.length-1,Math.floor(sample/400)-1);
  if(tick!==this.inputTick){
   // Consume every tick, including when a lower device rate crosses a boundary.
   for(let i=Math.max(0,this.inputTick+1);i<=tick;i++){
    const input=this.recording.ticks[i].input;
    super.setInput({throttle:input.throttle,shift:input.shiftUp,shiftDown:input.shiftDown});
    if(input.shiftUp||input.shiftDown)this.inputEvents.push({tick:i,sample:(i+1)*400,direction:input.shiftUp?1:-1});
   }
   this.inputTick=tick;
  }
  this.run=this.inputMode==='Race';const out=super.renderStereo();this.inputCursor++;
  Object.assign(this.meter,{reference:true,referenceSeconds:sample/48000,referenceDuration:total/48000,referenceStage:'Unity throttle / shifts; browser '+this.inputMode,driverReplay:true});return out;
 }
}
function createEngine(sr,settings,trace,mode='unity',options={}){
 return mode==='inputs'?new InputEngine(sr,settings,trace):mode==='unity'?new ReferenceEngine(sr,settings,trace,options):new DSP.EngineDSP(sr,settings);
}
return{validate,validateInputs,settingsFromTrace,ReferenceEngine,InputEngine,createEngine};
}
