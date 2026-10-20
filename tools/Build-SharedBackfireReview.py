"""Validate the shared-source matrix WAVs and build a local, single-player review page."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'reports/shared-backfire-audition-20261020'

HTML = r'''<!doctype html>
<html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>DragLab · Shared backfire generator review</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#11161a;color:#e2e5e7;font:16px/1.5 system-ui,sans-serif}header{padding:24px 32px;background:#1d2429;border-bottom:2px solid #e32838}h1{font-size:25px;margin:0 0 8px}p{margin:8px 0;color:#b4c0c8}main{display:grid;grid-template-columns:340px minmax(0,1fr);max-width:1500px;margin:auto}aside{padding:20px;border-right:1px solid #374149}article{padding:26px;min-width:0}input,select,button,a.download{font:inherit;color:inherit;background:#252e35;border:1px solid #64717c;border-radius:5px;padding:8px 12px}button{cursor:pointer}button:hover,a.download:hover{background:#37434b}button.selected{border-color:#ff394d;background:#51202a}input{width:100%;margin:0 0 12px}select{width:100%;margin-bottom:12px}.configs{display:grid;gap:8px;max-height:75vh;overflow:auto}.configs button{text-align:left;font-size:14px}.configs small{display:block;color:#b0c0c9}h2{margin:0 0 6px;font-size:22px}.tag{display:inline-block;background:#252e35;border-radius:5px;padding:4px 9px;margin:4px 6px 4px 0;color:#ccd3d7;font-size:14px}.warning{border-left:3px solid #f5b951;padding:8px 12px;background:#232729}.player{position:sticky;top:0;background:#11161af5;padding:14px 0;z-index:1}audio{width:100%;display:block}.actions{display:flex;flex-wrap:wrap;gap:10px;align-items:center;margin-top:12px}.actions label{display:flex;align-items:center;gap:8px}.actions input{width:auto;margin:0}.download{text-decoration:none}.current{color:#ff8190;font-weight:600;min-height:26px;margin-top:10px}table{width:100%;border-collapse:collapse;margin:16px 0}td,th{text-align:left;padding:10px;border-bottom:1px solid #343d43;font-size:14px}td:first-child{width:85px}tr.now{background:#422129}td button{text-align:left;width:100%;background:transparent;border-color:transparent;padding:2px}.note{color:#aab9c3;font-size:13px}.count{color:#b0c0c9;font-size:13px}details{background:#1c2429;padding:12px;border-radius:5px;margin:18px 0}summary{cursor:pointer}pre{white-space:pre-wrap;font-size:13px}a{color:#ff9ca7}@media(max-width:800px){main{grid-template-columns:1fr}aside{border-right:0;border-bottom:1px solid #374149}.configs{max-height:230px}header,article{padding:18px}td,th{padding:8px 4px}}
</style>
<header><h1>DragLab · One shared backfire generator</h1><p>34 configurations · one 64-second WAV each · same controlled chapter order · engine and induction included · no rival or tires</p><p class="warning">Preview only. These are deliberately authored acoustic bench events, not predictions of normal car behavior. Runtime game audio is unchanged.</p></header>
<main><aside><input id="search" type="search" placeholder="Search engine, exhaust, turbo…" aria-label="Search configurations"><select id="fuel" aria-label="Filter fuel"><option value="">All fuel systems</option><option>Injection</option><option>Carburetor</option><option>Diesel</option></select><div class="count" id="count"></div><div class="configs" id="configs"></div></aside>
<article><h2 id="title"></h2><div id="tags"></div><p id="geometry"></p><div class="player"><audio id="audio" controls preload="metadata"></audio><div class="actions"><button id="previous">Previous config</button><button id="next">Next config</button><label><input id="keep" type="checkbox" checked>Keep position when switching</label><label><input id="all" type="checkbox">Continue through all configs</label><a id="download" class="download" download>Download this WAV</a></div><div class="current" id="current"></div></div>
<p>Click a chapter to jump to it. Fuel-cut and oxygen-starved chapters are intentional no-explosion controls. Diesel petrol-only chapters also remain controls; its delayed-fuel fault is explicitly forced for review.</p>
<table><thead><tr><th>Time</th><th>Chapter / controls</th><th>Events</th></tr></thead><tbody id="chapters"></tbody></table>
<details><summary>Inputs and scope</summary><p>A single pressure/turbulence algorithm takes fuel, oxygen, burn duration, mixing, pressure, gas volume, temperature and source location. It has no event-type sound switch. A prepared bidirectional duct carries the result through the remaining pipes, turbine, catalyst, resonators, silencers and outlet, with reflected waves.</p><p>The engine bed is the existing production synthesizer with its old pop layer disabled on a private copy. RPM, boost, cuts and event packets are controlled laboratory inputs. Geometry, gas conditions and chemistry are preview assumptions, not measured vehicle data or full reactive-flow simulation. New-game physics, wear, saves and audio have not been modified.</p><pre id="inputs"></pre></details>
<details><summary>Events for this configuration</summary><pre id="events"></pre></details>
</article></main>
<script id="data" type="application/json">__DATA__</script>
<script>
const data=JSON.parse(document.getElementById('data').textContent),cases=data.cases;
const $=id=>document.getElementById(id),audio=$('audio');let selected=cases.findIndex(c=>c.id==='30-exhaust-2'),pending=0;
function time(v){return Math.floor(v/60)+':'+Math.floor(v%60).toString().padStart(2,'0')}
function list(){let q=$('search').value.toLowerCase(),f=$('fuel').value,shown=0;$('configs').replaceChildren();cases.forEach((c,i)=>{if(f&&c.fuel!==f)return;if(!(c.title+' '+c.induction+' '+c.exhaust+' '+c.cylinders+' cylinders '+c.valve+' '+c.id).toLowerCase().includes(q))return;shown++;let b=document.createElement('button');b.textContent=c.title;b.className=i===selected?'selected':'';let s=document.createElement('small');s.textContent=c.cylinders+' cyl · '+c.fuel+' · '+c.induction;b.append(s);b.onclick=()=>select(i);$('configs').append(b)});$('count').textContent=shown+' / '+cases.length+' configurations'}
function select(i,reset=false){let wasPlaying=!audio.paused,position=$('keep').checked&&!reset?audio.currentTime:0;selected=(i+cases.length)%cases.length;let c=cases[selected];audio.pause();pending=position||0;audio.src=c.file;$('download').href=c.file;$('title').textContent=c.title;$('tags').replaceChildren();[c.cylinders+' cylinders',c.litres.toFixed(1)+' L',c.fuel,c.induction,c.valve+' valve'].forEach(v=>{let t=document.createElement('span');t.className='tag';t.textContent=v;$('tags').append(t)});$('geometry').textContent='Assumed path: '+c.lengthM.toFixed(2)+' m · diameter ×'+c.diameterScale.toFixed(2)+' · '+c.temperatureK+' K · '+(c.exhaust||'open acoustic path');$('chapters').replaceChildren();c.chapters.forEach((ch,index)=>{let tr=document.createElement('tr'),at=document.createElement('td'),body=document.createElement('td'),counts=document.createElement('td');tr.dataset.index=index;at.textContent=time(ch.start)+'–'+time(ch.end);let b=document.createElement('button');b.textContent=ch.title;b.onclick=()=>{audio.currentTime=ch.start;audio.play().catch(e=>$('current').textContent=e.message)};let note=document.createElement('div');note.className='note';note.textContent=ch.note;body.append(b,note);counts.textContent=ch.accepted+' audible / '+ch.suppressed+' controls';tr.append(at,body,counts);$('chapters').append(tr)});$('inputs').textContent=JSON.stringify({file:c.file,source:c.source,peak:c.peak,rms:c.rms,sha256:c.sha256,geometry:c.geometry,callbackMillisecondsPerSecond:c.callbackMillisecondsPerSecond},null,2);$('events').textContent=JSON.stringify(c.events,null,2);list();audio.onloadedmetadata=()=>{audio.currentTime=Math.min(pending,Math.max(0,audio.duration-.1));update();if(wasPlaying)audio.play().catch(()=>{})};update()}
function update(){let c=cases[selected],index=Math.min(15,Math.floor((audio.currentTime||0)/4));$('current').textContent=time(audio.currentTime||0)+' / 1:04 · '+c.chapters[index].title;document.querySelectorAll('#chapters tr').forEach((r,i)=>r.classList.toggle('now',i===index))}
audio.ontimeupdate=update;audio.onended=()=>{if($('all').checked&&selected<cases.length-1){select(selected+1,true);audio.play().catch(()=>{})}};
$('previous').onclick=()=>select(selected-1);$('next').onclick=()=>select(selected+1);$('search').oninput=list;$('fuel').onchange=list;select(selected,true);
</script></html>'''

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--folder', type=Path, default=FOLDER)
    folder = parser.parse_args().folder
    manifest = json.loads((folder / 'manifest.json').read_text(encoding='utf-8-sig'))
    configs = manifest['cases']
    assert len(configs) == 34 and len({c['id'] for c in configs}) == 34
    assert {c['cylinders'] for c in configs} == {3, 4, 5, 6, 8, 10, 12}
    assert len({c['induction'] for c in configs}) == 6
    assert {c['fuel'] for c in configs} == {'Injection', 'Carburetor', 'Diesel'}
    assert {c['valve'] for c in configs} >= {'Recirculated', 'Hiss', 'Chirp', 'Flutter'}
    lines = []
    hashes = {}
    prefixes = {}
    for c in configs:
        path = folder / c['file']
        assert hashlib.sha256(path.read_bytes()).hexdigest() == c['sha256']
        hashes[c['id']] = c['sha256']
        with wave.open(str(path), 'rb') as audio:
            assert (audio.getframerate(), audio.getnchannels(), audio.getsampwidth(), audio.getnframes()) == (48000, 2, 2, 3072000)
            pcm = np.frombuffer(audio.readframes(audio.getnframes()), dtype='<i2').reshape(-1, 2).astype(float) / 32768
        assert np.max(np.abs(pcm)) < .98 and c['overflows'] == 0
        assert len(c['chapters']) == 16
        prefixes[c['id']] = hashlib.sha256(pcm[:8*48000].tobytes()).hexdigest()
        assert c['chapters'][2]['accepted'] == c['chapters'][14]['accepted'] == 0
        assert all(ch['start'] == i*4 and ch['end'] == i*4+4 for i, ch in enumerate(c['chapters']))
        assert all(ev['accepted'] == (ev['energyJ'] > 0) for ev in c['events'])
        assert c['chapters'][0]['eventRms'] == c['chapters'][1]['eventRms'] == 0
        for ev in c['events']:
            p = ev['input']
            energy = min(p['fuelMg'], p['oxygenMg']/p['oxygenPerFuel'])*1e-6*p['heatingJPerKg']*p['burnEfficiency'] if p['ignited'] else 0
            assert abs(energy - ev['energyJ']) < 1e-6
        matched = [next(ev['input'] for ev in c['events'] if int(ev['time']//4) == chapter).copy() for chapter in (8, 9, 10)]
        for p in matched:
            p.pop('sourceMetres')
        assert matched[0] == matched[1] == matched[2], (c['id'], 'location study changed more than source position')
        for ch in c['chapters']:
            samples = pcm[int(ch['start']*48000):int(ch['end']*48000)]
            assert np.sqrt(np.mean(samples*samples)) > .0001, (c['id'], ch['title'], 'engine went silent')
        if c['fuel'] == 'Diesel':
            assert all(c['chapters'][i]['accepted'] == 0 for i in (3, 4, 6, 7, 8, 9, 10, 11, 13))
        else:
            assert all(c['chapters'][i]['accepted'] > 0 for i in (3, 4, 5, 6, 7, 8, 9, 10, 12, 13))
        lines.append(f"PASS {c['id']}: SHA256, 64 s stereo PCM16, sixteen audible-engine chapters, energy/control/suppression rules, bounded mix, no voice overflow.")
    for identity in ('50-short-pipe', '51-long-pipe', '52-narrow-pipe', '53-wide-pipe', '54-cool-exhaust', '55-hot-exhaust', '57-leaking-silencer'):
        assert prefixes[identity] == prefixes['30-exhaust-2'], (identity, 'engine baseline changed')
        assert hashes[identity] != hashes['30-exhaust-2'], (identity, 'parameter had no effect')
    assert hashes['52-narrow-pipe'] != hashes['53-wide-pipe']
    assert hashes['54-cool-exhaust'] != hashes['55-hot-exhaust']
    serialized = json.dumps(manifest, ensure_ascii=False).replace('<', chr(92)+'u003c')
    ids = re.findall(r'id="([^"]+)"', HTML)
    references = re.findall(r"\$\('([^']+)'\)", HTML)
    assert len(ids) == len(set(ids)) and set(references) <= set(ids)
    assert HTML.count('<audio ') == 1
    (folder / 'index.html').write_text(HTML.replace('__DATA__', serialized), encoding='utf-8')
    report = folder / 'verification.txt'
    original = report.read_text(encoding='utf-8-sig').split('\nPASS independent PCM')[0].rstrip()
    report.write_text(original+'\nPASS independent PCM/manifest checks: 34 files; 7 cylinder counts, 6 induction layouts, 3 fuel systems, 4 release modes, full chapter sequence.\n'+'\n'.join(lines)+'\nPASS static review HTML: unique control IDs/references, one native audio player, 34 configuration records.\n', encoding='utf-8')
    print(f"PASS: {len(configs)} audio files; review page: {(folder / 'index.html').resolve()}")
    print('PCM MiB:', round(sum((folder / c['file']).stat().st_size for c in configs)/1048576, 1))
    print('Maximum mixed peak:', max(c['peak'] for c in configs))
    print('Desktop source ms/audio second:', min(c['callbackMillisecondsPerSecond'] for c in configs), 'to', max(c['callbackMillisecondsPerSecond'] for c in configs))

if __name__ == '__main__':
    main()
