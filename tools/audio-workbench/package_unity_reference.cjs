'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const folder=path.resolve(process.argv[2]||path.join(__dirname,'../../reports/unity-audio-reference-20261021'));
const traces=fs.readdirSync(folder).filter(n=>n.endsWith('.audio-trace.json')).sort().map(n=>JSON.parse(fs.readFileSync(path.join(folder,n),'utf8')));
assert(traces.length);const source=fs.readFileSync(path.join(__dirname,'DragLab_Audio_Workbench_DrivetrainFix.html'),'utf8');
function safe(x){return JSON.stringify(x).replace(/</g,'\\u003c');}
for(const t of traces){
 const boot=`\n<script>loadUnityTrace(${safe(t)});$('unityWav').src=${safe(t.id+'.unity.wav')};$('unityWavLabel').textContent=${safe(t.title+' — Unity engine + tires (24-bit)')};
const bundleChoice=document.createElement('select');bundleChoice.className='field-value';bundleChoice.style.width='100%';
for(const item of ${safe(traces.map(x=>({id:x.id,title:x.title})) )}){const option=document.createElement('option');option.value=item.id+'.html';option.textContent=item.title;option.selected=item.id===${safe(t.id)};bundleChoice.append(option);}bundleChoice.onchange=()=>{location.href=bundleChoice.value;};
$('referenceInfo').before(bundleChoice);
const links=document.createElement('p');links.className='hint';
for(const [label,suffix]of [['Trace JSON','.audio-trace.json'],['Unity engine','.unity-engine.wav'],['Unity tires','.unity-tires.wav'],['Unity dry source','.unity-source.wav'],['Browser replay','.browser.wav'],['Unity / browser A/B','.unity-browser-AB.wav'],['Signal comparison','.comparison.png']]){const a=document.createElement('a');a.href=${safe(t.id)}+suffix;a.textContent=label;a.style.marginRight='12px';a.style.color='var(--cyan)';links.append(a);}
$('referenceInfo').after(links);</script>\n`;
 const html=source.replace('</body>',boot+'</body>');fs.writeFileSync(path.join(folder,t.id+'.html'),html,'utf8');
 if(t.id===traces[0].id)fs.writeFileSync(path.join(folder,'index.html'),html,'utf8');
}
fs.copyFileSync(path.join(__dirname,'DragLab_Audio_Workbench_DrivetrainFix.html'),path.join(folder,'workbench.html'));
fs.writeFileSync(path.join(folder,'README.md'),`# Unity reference review\n\nOpen index.html. A recorded Tempest is preloaded; choose another configuration above its reference description. Start audio renders the recorded Unity timeline through editable browser DSP. The separate WAV player is the authoritative Unity output. Restart recorded sequence rewinds browser playback. Rendering A/B keeps the same imported timeline. Free simulation restores manual controls.\n\nAll audio is unnormalized 48 kHz stereo; native reference WAVs are 24-bit. Each trace has hardware, listener, per-wheel contact, all numeric sound frames and lossless seeded reaction/release events. Each combined A/B WAV plays Unity first, one second silence, then the browser. Reference timing is exact; browser acoustic parity is NOT claimed. No game source, save or installed build was changed.\n\nCase files: ${traces.map(t=>t.id).join(', ')}.\n\nSource tools and detailed schema are in tools/audio-workbench and docs/UNITY_AUDIO_REFERENCE.md.\n`,'utf8');
console.log('Packaged '+traces.length+' ready-to-open standalone reference pages: '+path.join(folder,'index.html'));
