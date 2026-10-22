// Uses the existing workbench controls, audio graph and WAV writer.
const UnityReference=createUnityReferenceReplay(DSP);
let unityTrace=null,unityInputs=null,unityReferenceUrl=null,browserSound=null;
const referenceActive=()=>($('referenceMode').value==='unity'&&!!unityTrace)||($('referenceMode').value==='inputs'&&!!unityInputs);
const replayMode=()=>referenceActive()?$('referenceMode').value:'free';
const replayPayload=()=>replayMode()==='unity'?unityTrace:replayMode()==='inputs'?unityInputs:null;
const replayOptions=()=>({preserveSound:$('soundSource').value!=='captured'});
function makeAuditionEngine(sr,settings){const e=UnityReference.createEngine(sr,settings,replayPayload(),replayMode(),replayOptions());if(e.recording)e.setInput({mode:$('raceMode').value});return e;}
function referenceControls(){
 const active=referenceActive(),full=replayMode()==='unity';
 for(const id of ['throttle','shift','shiftDown','launch','brake','releaseThrottle','pop','bov','forceTireSlip','tireTestSlip','tireTestSpeed','tireTestAxle','autoRun','renderTire'])$(id).disabled=active;
 $('raceMode').disabled=full;
 const fixed=[...groups['Engine geometry'].map(x=>x[0]),...groups['Race & controls'].map(x=>x[0]),'induction','boostBar','turboThresholdRpm','turboFullBoostRpm','spoolSeconds','turboCoastSeconds','popProbability','overrunPopRate'];
 for(const key of fixed){const field=$('field-'+key),number=document.querySelector(`[data-number="${key}"]`);if(field)field.disabled=full;if(number)number.disabled=full;}
 $('preset').disabled=$('resetPreset').disabled=$('importJson').disabled=full;
 $('referenceMode').disabled=!unityTrace&&!unityInputs;$('referenceRestart').disabled=!active;
 $('modeUnity').disabled=!unityTrace;$('modeInputs').disabled=!unityInputs;$('soundSource').disabled=!unityTrace;
 $('referenceInfo').textContent=full?`${unityTrace.title} · ${(unityTrace.totalSamples/unityTrace.sampleRate).toFixed(2)} s · ${unityTrace.reactions.length} reaction packets · ${unityTrace.releases.length} BOV releases · source ${unityTrace.sourceCommit}. Recorded simulation/events; selected sound tuning. Browser waveform parity with Unity is not claimed.`:replayMode()==='inputs'?`${unityInputs.title} · Unity throttle and shift commands only. Browser calculates RPM, boost, cuts, tires and events. Choose Neutral / dyno or Race drive; brake, clutch and launch inputs are excluded. This is a diagnostic browser model, not a Unity physics replay.`:'Free browser simulation. Manual controls and the browser sequence use the selected sound tuning.';
 $('referenceProgress').textContent='Replay has not started.';if(typeof diagnosticControls==='function')diagnosticControls();
}
function refreshReference(){refreshControls();partEditor();refreshStages();referenceControls();postSettings(true);}
function loadUnityTrace(trace){
 UnityReference.validate(trace);stopAudio();unityTrace=trace;
 if($('soundSource').value==='captured'){if(!browserSound)browserSound=JSON.parse(JSON.stringify(params));params=UnityReference.settingsFromTrace(params,trace);}
 $('referenceMode').value='unity';refreshReference();logEvent('Loaded Unity reference: '+trace.id);return trace;
}
function loadUnityInputs(recording){UnityReference.validateInputs(recording);stopAudio();unityInputs=recording;$('referenceMode').value='inputs';refreshReference();logEvent('Loaded Unity driver inputs: '+recording.id);return recording;}
async function restartReferenceAudio(){
 if(!referenceActive())return;postInput({reset:true});await startAudio();
}
function attachUnityReferenceWav(blob,name){
 if(unityReferenceUrl)URL.revokeObjectURL(unityReferenceUrl);unityReferenceUrl=URL.createObjectURL(blob);$('unityWav').src=unityReferenceUrl;$('unityWavLabel').textContent=name;
}
async function renderUnityReference(compare=false){
 if(renderBusy)return;if(!referenceActive())return renderWav(compare);
 if(compare&&(!slotA||!slotB)){alert('Capture A and B settings first.');return;}
 renderBusy=true;const mode=replayMode(),payload=replayPayload(),options=replayOptions(),drive=$('raceMode').value;
 const rate=48000,settings=compare?[slotA,slotB]:[params],count=mode==='unity'?payload.totalSamples:(payload.ticks.length+241)*400,total=count*settings.length+(compare?rate:0),L=new Float32Array(total),R=new Float32Array(total);
 $('renderSingle').disabled=$('renderAB').disabled=true;
 try{
  for(let k=0;k<settings.length;k++){
   const engine=UnityReference.createEngine(rate,settings[k],payload,mode,options),offset=k*(count+rate);if(engine.recording)engine.setInput({mode:drive});
   for(let at=0;at<count;at+=4096){const end=Math.min(count,at+4096);engine.render(L.subarray(offset+at,offset+end),R.subarray(offset+at,offset+end));
    if((at/4096|0)%8===0){$('renderStatus').textContent='Rendering selected replay through browser DSP…';$('progressFill').style.width=((offset+end)/total*100).toFixed(1)+'%';await new Promise(resolve=>setTimeout(resolve,0));}}
  }
  const blob=wav16(L,R,rate);if(loadedRecordingUrl)URL.revokeObjectURL(loadedRecordingUrl);loadedRecordingUrl=URL.createObjectURL(blob);
  const a=document.createElement('a');a.href=loadedRecordingUrl;a.download=payload.id+'.'+mode+(compare?'.browser-AB.wav':'.browser.wav');a.click();
  $('preview').src=loadedRecordingUrl;$('preview').style.display='block';$('progressFill').style.width='100%';$('renderStatus').textContent=`Complete · ${total/rate} s · ${mode==='unity'?'Unity simulation and events':'Unity throttle/shifts, browser simulation'}; browser DSP. No per-file normalization.`;
 }catch(error){$('renderStatus').textContent='Reference render failed: '+error.message;}
 finally{renderBusy=false;$('renderSingle').disabled=$('renderAB').disabled=false;}
}
function bindUnityReference(){
 $('referenceTrace').onchange=async event=>{const file=event.target.files?.[0];if(!file)return;try{loadUnityTrace(JSON.parse(await file.text()));}catch(error){alert('Cannot load Unity trace: '+error.message);}event.target.value='';};
 $('referenceInputs').onchange=async event=>{const file=event.target.files?.[0];if(!file)return;try{loadUnityInputs(JSON.parse(await file.text()));}catch(error){alert('Cannot load driver inputs: '+error.message);}event.target.value='';};
 $('soundSource').onchange=()=>{stopAudio();if($('soundSource').value==='captured'){browserSound=JSON.parse(JSON.stringify(params));params=UnityReference.settingsFromTrace(params,unityTrace);}else if(browserSound)params=JSON.parse(JSON.stringify(browserSound));refreshReference();};
 $('referenceWav').onchange=event=>{const file=event.target.files?.[0];if(file)attachUnityReferenceWav(file,file.name);};
 $('referenceMode').onchange=()=>{stopAudio();referenceControls();postSettings(true);};
 $('referenceRestart').onclick=()=>restartReferenceAudio().catch(error=>alert(error.message));
 $('renderSingle').onclick=()=>referenceActive()?renderUnityReference(false):renderWav(false);
 $('renderAB').onclick=()=>referenceActive()?renderUnityReference(true):renderWav(true);
 $('restart').onclick=()=>{if(referenceActive())postInput({reset:true});else{holdThrottle=false;setThrottle(0);postInput({reset:true});}};
 $('unityWav').onplay=()=>{stopAudio();$('preview').pause();$('diagnosticAudio').pause();}; // Never overlay reference PCM and generated audio accidentally.
 referenceControls();
}
