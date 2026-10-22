// Offline audition inside the existing workbench. Never changes its live preset.
const SignalDiagnostics=createAudioSignalDiagnostics(DSP,UnityReference);
let diagnosticAuditions=[],diagnosticObjectUrls=[],diagnosticBusy=false;
function diagnosticControls(){
 $('diagnosticDrive').disabled=diagnosticBusy;$('diagnosticBuild').disabled=diagnosticBusy||!unityTrace||!unityInputs;
 $('diagnosticPass').disabled=diagnosticBusy||!diagnosticAuditions.length;
 $('diagnosticPrevious').disabled=diagnosticBusy||+$('diagnosticPass').value<=0;
 $('diagnosticDownload').disabled=diagnosticBusy||!diagnosticAuditions.length;
}
function selectDiagnosticPass(){const i=+$('diagnosticPass').value,item=diagnosticAuditions[i];if(!item)return;
 $('diagnosticAudio').pause();$('diagnosticAudio').src=item.url;$('diagnosticNote').textContent=SignalDiagnostics.passes[i].description;
 $('diagnosticDownload').href=item.url;$('diagnosticDownload').download=item.filename;diagnosticControls();
}
function loadDiagnosticAuditions(items,note){
 $('diagnosticAudio').pause();for(const u of diagnosticObjectUrls)URL.revokeObjectURL(u);diagnosticObjectUrls=[];diagnosticAuditions=items;
 const select=$('diagnosticPass');select.innerHTML='';for(let i=0;i<items.length;i++){const o=document.createElement('option');o.value=i;o.textContent=SignalDiagnostics.passes[i].title;select.append(o);}select.value='0';
 $('diagnosticStatus').textContent=note;selectDiagnosticPass();
}
async function buildSignalDiagnostics(){
 if(diagnosticBusy||!unityTrace||!unityInputs)return;
 const settings=JSON.parse(JSON.stringify(params)),trace=unityTrace,inputs=unityInputs,mode=$('diagnosticDrive').value,rate=48000;
 try{SignalDiagnostics.validatePair(trace,inputs);}catch(e){$('diagnosticStatus').textContent=e.message;return;}
 diagnosticBusy=true;diagnosticControls();stopAudio();$('unityWav').pause();$('diagnosticAudio').pause();
 const urls=[];try{
  const source=SignalDiagnostics.beginCapture(settings,trace,inputs,mode),scratchL=new Float32Array(4096),scratchR=new Float32Array(4096),count=trace.totalSamples;
  for(let at=0;at<count;at+=4096){const n=Math.min(4096,count-at);source.render(scratchL.subarray(0,n),scratchR.subarray(0,n));if((at/4096|0)%16===0){$('diagnosticStatus').textContent='Capturing independent browser driving… '+Math.round((at+n)/count*100)+'%';await new Promise(r=>setTimeout(r,0));}}
  const timeline=SignalDiagnostics.finishCapture(source,settings,trace,mode),items=[];
  for(const pass of SignalDiagnostics.passes){
   const engine=SignalDiagnostics.createEngine(settings,trace,timeline,pass.level),l=new Float32Array(count),r=new Float32Array(count);
   for(let at=0;at<count;at+=4096){const end=Math.min(count,at+4096);engine.render(l.subarray(at,end),r.subarray(at,end));if((at/4096|0)%16===0){$('diagnosticStatus').textContent=pass.title+' · '+Math.round(end/count*100)+'%';await new Promise(resolve=>setTimeout(resolve,0));}}
   const blob=wav16(l,r,rate),url=URL.createObjectURL(blob);urls.push(url);items.push({url,blob,filename:trace.id+'.diagnostic-'+pass.id+'.wav'});
  }
  loadDiagnosticAuditions(items,`${trace.title} · fixed ${settings.label} / seed ${settings.seed} · browser ${mode} · ${count/rate}s each. Natural levels. Settings captured at render start; no game changes.`);diagnosticObjectUrls=urls;
 }catch(e){for(const u of urls)URL.revokeObjectURL(u);$('diagnosticStatus').textContent='Diagnostic failed: '+e.message;}
 finally{diagnosticBusy=false;diagnosticControls();}
}
async function playPreviousDiagnostic(){
 const i=+$('diagnosticPass').value;if(i<1||diagnosticBusy)return;const a=diagnosticAuditions[i-1],b=diagnosticAuditions[i];
 if(b.previousUrl){$('diagnosticAudio').src=b.previousUrl;await $('diagnosticAudio').play();return;}
 // Built-in WAV writer uses 48 kHz stereo PCM16, so A/B needs no codec or
 // browser decoding/re-render of the signals. Keep their exact stored samples.
 const files=await Promise.all([a.blob.arrayBuffer(),b.blob.arrayBuffer()]),n=(files[0].byteLength-44)/4,m=(files[1].byteLength-44)/4,l=new Float32Array(n+48000+m),r=new Float32Array(l.length);
 for(const [buffer,offset]of [[files[0],0],[files[1],n+48000]]){const v=new DataView(buffer);for(let k=44,j=offset;k<buffer.byteLength;k+=4,j++){l[j]=v.getInt16(k,true)/32767;r[j]=v.getInt16(k+2,true)/32767;}}
 const url=URL.createObjectURL(wav16(l,r,48000));diagnosticObjectUrls.push(url);$('diagnosticAudio').src=url;await $('diagnosticAudio').play();
}
function bindSignalDiagnostics(){
 $('diagnosticBuild').onclick=buildSignalDiagnostics;$('diagnosticPass').onchange=selectDiagnosticPass;
 $('diagnosticPrevious').onclick=()=>playPreviousDiagnostic().catch(e=>{$('diagnosticStatus').textContent=e.message;});
 $('diagnosticAudio').onplay=()=>{stopAudio();$('unityWav').pause();$('preview').pause();};$('preview').onplay=()=>{stopAudio();$('unityWav').pause();$('diagnosticAudio').pause();};diagnosticControls();
}
