// Uses the existing workbench controls, audio graph and WAV writer.
const UnityReference=createUnityReferenceReplay(DSP);
let unityTrace=null,unityReferenceUrl=null;
const referenceActive=()=>!!unityTrace&&$('referenceMode').value==='unity';
function makeAuditionEngine(sr,settings){return referenceActive()?new UnityReference.ReferenceEngine(sr,settings,unityTrace):new DSP.EngineDSP(sr,settings);}
function referenceControls(){
 const active=referenceActive();for(const id of ['throttle','raceMode','shift','launch','brake','releaseThrottle','pop','bov','forceTireSlip','tireTestSlip','tireTestSpeed','tireTestAxle','autoRun','renderTire'])$(id).disabled=active;
 const fixed=[...groups['Engine geometry'].map(x=>x[0]),...groups['Race & controls'].map(x=>x[0]),'induction','boostBar','turboThresholdRpm','turboFullBoostRpm','spoolSeconds','turboCoastSeconds','popProbability','overrunPopRate'];
 for(const key of fixed){const field=$('field-'+key),number=document.querySelector(`[data-number="${key}"]`);if(field)field.disabled=active;if(number)number.disabled=active;}
 $('preset').disabled=$('resetPreset').disabled=$('importJson').disabled=active;
 $('referenceMode').disabled=!unityTrace;$('referenceRestart').disabled=!active;
 $('referenceInfo').textContent=active?`${unityTrace.title} · ${(unityTrace.totalSamples/unityTrace.sampleRate).toFixed(2)} s · ${unityTrace.reactions.length} reaction packets · ${unityTrace.releases.length} BOV releases · source ${unityTrace.sourceCommit}. Audio controls remain editable; waveform parity with Unity is not claimed.`:'Free browser simulation. Import a Unity trace to replace approximate driving and event timing with the recorded game timeline.';
}
function loadUnityTrace(trace){
 UnityReference.validate(trace);stopAudio();unityTrace=trace;params=UnityReference.settingsFromTrace(params,trace);$('referenceMode').value='unity';
 refreshControls();partEditor();refreshStages();referenceControls();postSettings(true);logEvent('Loaded Unity reference: '+trace.id);return trace;
}
async function restartReferenceAudio(){
 if(!referenceActive())return;postInput({reset:true});await startAudio();
}
function attachUnityReferenceWav(blob,name){
 if(unityReferenceUrl)URL.revokeObjectURL(unityReferenceUrl);unityReferenceUrl=URL.createObjectURL(blob);$('unityWav').src=unityReferenceUrl;$('unityWavLabel').textContent=name;
}
async function renderUnityReference(compare=false){
 if(renderBusy)return;if(!referenceActive())return renderWav(compare);
 if(compare&&(!slotA||!slotB)){alert('Capture A and B settings first.');return;}
 renderBusy=true;const rate=48000,settings=compare?[slotA,slotB]:[params],count=unityTrace.totalSamples,total=count*settings.length+(compare?rate:0),L=new Float32Array(total),R=new Float32Array(total);
 $('renderSingle').disabled=$('renderAB').disabled=true;
 try{
  for(let k=0;k<settings.length;k++){
   const engine=new UnityReference.ReferenceEngine(rate,settings[k],unityTrace),offset=k*(count+rate);
   for(let at=0;at<count;at+=4096){const end=Math.min(count,at+4096);engine.render(L.subarray(offset+at,offset+end),R.subarray(offset+at,offset+end));
    if((at/4096|0)%8===0){$('renderStatus').textContent='Rendering recorded Unity timeline through browser DSP…';$('progressFill').style.width=((offset+end)/total*100).toFixed(1)+'%';await new Promise(resolve=>setTimeout(resolve,0));}}
  }
  const blob=wav16(L,R,rate);if(loadedRecordingUrl)URL.revokeObjectURL(loadedRecordingUrl);loadedRecordingUrl=URL.createObjectURL(blob);
  const a=document.createElement('a');a.href=loadedRecordingUrl;a.download=unityTrace.id+(compare?'.browser-reference-AB.wav':'.browser-reference.wav');a.click();
  $('preview').src=loadedRecordingUrl;$('preview').style.display='block';$('progressFill').style.width='100%';$('renderStatus').textContent=`Complete · ${total/rate} s · recorded Unity physics/events; browser DSP transfer functions. No per-file normalization.`;
 }catch(error){$('renderStatus').textContent='Reference render failed: '+error.message;}
 finally{renderBusy=false;$('renderSingle').disabled=$('renderAB').disabled=false;}
}
function bindUnityReference(){
 $('referenceTrace').onchange=async event=>{const file=event.target.files?.[0];if(!file)return;try{loadUnityTrace(JSON.parse(await file.text()));}catch(error){alert('Cannot load Unity trace: '+error.message);}event.target.value='';};
 $('referenceWav').onchange=event=>{const file=event.target.files?.[0];if(file)attachUnityReferenceWav(file,file.name);};
 $('referenceMode').onchange=()=>{stopAudio();referenceControls();postSettings(true);};
 $('referenceRestart').onclick=()=>restartReferenceAudio().catch(error=>alert(error.message));
 $('renderSingle').onclick=()=>referenceActive()?renderUnityReference(false):renderWav(false);
 $('renderAB').onclick=()=>referenceActive()?renderUnityReference(true):renderWav(true);
 $('restart').onclick=()=>{if(referenceActive())postInput({reset:true});else{holdThrottle=false;setThrottle(0);postInput({reset:true});}};
 $('unityWav').onplay=()=>stopAudio(); // Never overlay reference PCM and generated audio accidentally.
 referenceControls();
}
