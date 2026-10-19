"""Standalone offline raster/layer review. Embeds assets; no server or CORS needed."""
from pathlib import Path
import base64
import json

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'art/roster-cel-v1'


def main():
    sources=json.loads((ART/'sources.json').read_text(encoding='utf-8-sig'))
    textures={};models=[]
    for row in sources:
        folder=ART/row['id']/'layers';manifest=json.loads((folder/'car-sprite.json').read_text(encoding='utf-8-sig'))
        files={layer['file'] for layer in manifest['layers']}
        images={}
        for file in files:
            key=file if file in ('wheel.png','rotor.png','caliper.png') else row['id']+'/'+file
            textures.setdefault(key,'data:image/png;base64,'+base64.b64encode((folder/file).read_bytes()).decode('ascii'))
            images[file]=key
        models.append({'id':row['id'],'name':row['model'],'year':row['year'],'manifest':manifest,'images':images})
    template='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>DragLab car sprites</title>
<style>*{box-sizing:border-box}body{margin:0;background:#141a21;color:#e6ebef;font:15px system-ui}header{position:sticky;top:0;z-index:2;padding:14px 22px;background:#202731;display:flex;flex-wrap:wrap;gap:18px;align-items:center;border-bottom:2px solid #d84860}label{display:flex;align-items:center;gap:7px}input,select,button{font:inherit;background:#303b48;color:inherit;border:1px solid #6e7c89;border-radius:4px;padding:5px}input[type=color]{width:50px;height:34px;padding:2px}input[type=range]{width:120px}main{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px;padding:20px;max-width:1600px;margin:auto}article{border:1px solid #4b5966;border-radius:10px;overflow:hidden;background:#1b232d}h2{font-size:16px;font-weight:600;margin:14px 18px 0}canvas{width:100%;height:auto;display:block}small{font-size:12px;color:#a7b4c0;font-weight:400;margin-left:8px}#status{margin-left:auto;color:#a7b4c0}@media(max-width:700px){main{grid-template-columns:1fr;padding:10px}header{gap:10px}}</style>
<header><label>Paint <input id="paint" type="color" value="#ce4564"></label><label>View <select id="mode"><option value="body">Body</option><option value="complete">With separate wheels</option><option value="paint">Paint layer</option><option value="fixtures">Fixtures</option></select></label><label>Background <select id="background"><option value="#1b232d">Dark</option><option value="#ffffff">White</option><option value="#808080">Gray</option><option value="#000000">Black</option></select></label><label>Wheel angle <input id="angle" type="range" min="0" max="360" value="0"></label><input id="search" type="search" placeholder="Find model" aria-label="Find model"><span id="status">Loading…</span></header><main id="grid"></main>
<script>const data=__DATA__;const loaded={},cards=[];
function image(src){return new Promise((ok,bad)=>{const i=new Image;i.onload=()=>ok(i);i.onerror=bad;i.src=src})}
function draw(card){const m=card.model.manifest,c=card.canvas,g=c.getContext('2d'),color=document.querySelector('#paint').value,mode=document.querySelector('#mode').value,angle=Number(document.querySelector('#angle').value)*Math.PI/180;c.width=m.canvas.width;c.height=m.canvas.height;g.fillStyle=document.querySelector('#background').value;g.fillRect(0,0,c.width,c.height);
for(const layer of [...m.layers].sort((a,b)=>a.z-b.z)){if(mode==='body'&&!['body-paint','body-fixtures'].includes(layer.id))continue;if(mode==='paint'&&layer.id!=='body-paint')continue;if(mode==='fixtures'&&layer.id!=='body-fixtures')continue;let im=loaded[card.model.images[layer.file]];
if(layer.id==='body-paint'){const t=document.createElement('canvas');t.width=im.width;t.height=im.height;const x=t.getContext('2d');x.drawImage(im,0,0);x.globalCompositeOperation='multiply';x.fillStyle=color;x.fillRect(0,0,t.width,t.height);x.globalCompositeOperation='destination-in';x.drawImage(im,0,0);im=t}
g.save();const p=layer.placement;if(p.mode==='anchor'){const a=m.anchors[p.anchor];g.translate(a.x+p.offset[0],a.y+p.offset[1]);if(layer.rotateWithWheel)g.rotate(angle);g.scale(p.scale,p.scale);g.drawImage(im,-im.width/2,-im.height/2)}else g.drawImage(im,p.x||0,p.y||0);g.restore()}}
function refresh(){const q=document.querySelector('#search').value.toLowerCase();let n=0;for(const card of cards){const visible=card.model.name.toLowerCase().includes(q);card.article.hidden=!visible;if(visible){n++;draw(card)}}document.querySelector('#status').textContent=n+' / '+cards.length}
(async()=>{try{await Promise.all(Object.entries(data.textures).map(async([key,src])=>loaded[key]=await image(src)));for(const model of data.models){const article=document.createElement('article'),title=document.createElement('h2'),canvas=document.createElement('canvas');title.textContent=model.name;const year=document.createElement('small');year.textContent=model.year+' design';title.append(year);article.append(title,canvas);document.querySelector('#grid').append(article);cards.push({model,article,canvas})}refresh();for(const input of document.querySelectorAll('input,select'))input.addEventListener('input',refresh);window.rosterReview={count:cards.length,ready:true}}catch(e){document.querySelector('#status').textContent='Image load failed';window.rosterReview={ready:false,error:String(e)};throw e}})();</script></html>'''
    out=ROOT/'reports/roster-cel-v1';out.mkdir(parents=True,exist_ok=True)
    (out/'index.html').write_text(template.replace('__DATA__',json.dumps({'textures':textures,'models':models},separators=(',',':'))),encoding='utf-8')
    print('Offline review:',out/'index.html')


if __name__=='__main__':main()
