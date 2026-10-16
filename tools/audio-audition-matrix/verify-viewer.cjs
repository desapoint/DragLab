// Pure in-memory viewer-logic checks. No browser, network, audio device or UI automation.
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const assert=require('node:assert/strict');
const folder=path.resolve(process.argv[2]||'reports/audio-matrix-20261015');
const html=fs.readFileSync(path.join(folder,'index.html'),'utf8');
const manifest=JSON.parse(fs.readFileSync(path.join(folder,'manifest.json'),'utf8'));
const core=manifest.filter(r=>r.type!=='effect');
const effects=manifest.filter(r=>r.type==='effect');
const script=html.match(/<script>([\s\S]*?)<\/script>/)[1];
class Element{
  constructor(tag){this.tag=tag;this.children=[];this.value='';this.textContent='';this.checked=false;this.disabled=false;this.dataset={};this.readyState=1;this.currentTime=0;this.paused=true;}
  append(...children){this.children.push(...children)}
  add(value){this.children.push(value)}
  replaceChildren(...children){this.children=[...children]}
  setAttribute(name,value){this[name]=value}
  pause(){this.paused=true}
  play(){this.paused=false;return Promise.resolve()}
  load(){}
}
const elements=new Map();
const get=id=>{if(!elements.has(id))elements.set(id,new Element(id));return elements.get(id)};
const context=vm.createContext({document:{getElementById:get,createElement:tag=>new Element(tag)},Option:function(text,value){this.text=text;this.value=value}});
vm.runInContext(script,context,{timeout:5000});
let checks=0;
function check(title,test){test();checks++;console.log('PASS: '+title)}
function count(){return Number(get('count').textContent.split(' matching')[0].replace(/\D/g,''))}
function select(label,value){const wrapper=get('filters').children.find(x=>x.textContent===label);const field=wrapper.children[0];field.value=value;field.onchange()}
async function main(){
 check('complete engine manifest and hidden laboratory default',()=>{assert.equal(core.length,1326);assert.equal(count(),1272)});
 check('bounded row creation and factory references first',()=>{assert.equal(get('rows').children.length,25);assert.match(get('rows').children[0].children[0].children[0].textContent,/Factory configuration/)});
 check('factory-only gives all 13 actual configurations',()=>{get('factory').checked=true;get('factory').onchange();assert.equal(count(),13)});
 check('reset returns to all real variants',()=>{get('reset').onclick();assert.equal(count(),1272);assert.equal(get('factory').checked,false)});
 check('cylinder filtering matches manifest',()=>{select('Cylinders','8');assert.equal(count(),core.filter(r=>r.cylinders===8&&!r.group.includes('laboratory')).length)});
 check('fuel filter excludes petrol',()=>{get('reset').onclick();select('Fuel','Diesel');assert.equal(count(),core.filter(r=>r.fuelKind==='Diesel'&&!r.group.includes('laboratory')).length)});
 check('laboratory coverage includes 16-cylinder references',()=>{get('reset').onclick();get('lab').checked=true;get('lab').onchange();select('Cylinders','16');assert.ok(count()>0)});
 check('release-valve filtering',()=>{get('reset').onclick();select('Release valve','Flutter');assert.equal(count(),core.filter(r=>r.valve==='Flutter'&&!r.group.includes('laboratory')).length)});
 check('empty search handles paging safely',()=>{get('search').value='no such engine 999999';get('search').oninput();assert.equal(count(),0);assert.equal(get('rows').children.length,0);assert.equal(get('prev').disabled,true);assert.equal(get('next').disabled,true)});
 get('reset').onclick();
 await get('rows').children[0].children[5].children[1].onclick();
 check('After button selects second segment',()=>{assert.equal(get('audio').currentTime,10.7);assert.equal(get('audio').paused,false);assert.ok(get('audio').src.endsWith('.flac'))});
 await get('before').onclick();get('audio').currentTime=10;get('audio').ontimeupdate();
 check('Before segment stops before the after half',()=>assert.equal(get('audio').paused,true));
 await get('both').onclick();
 check('full A/B starts at zero',()=>{assert.equal(get('audio').currentTime,0);assert.equal(get('audio').paused,false)});
 if(effects.length){
  check('Effects section isolates all 124 physical captures',()=>{get('effects').onclick();assert.equal(count(),124);assert.equal(effects.length,124);assert.equal(get('factory').disabled,true)});
  check('hard limiter effect filter',()=>{select('Effect','Hard rev limiter');assert.equal(count(),22)});
  check('diesel uses only fuel-cut limiter examples',()=>{select('Fuel','Diesel');assert.equal(count(),2);assert.ok(effects.filter(r=>r.fuelKind==='Diesel').every(r=>r.cut==='Fuel'))});
  await get('rows').children[0].children[5].children[1].onclick();
  check('effect After starts at 12.7 seconds',()=>assert.equal(get('audio').currentTime,12.7));
  await get('before').onclick();get('audio').currentTime=11;get('audio').ontimeupdate();
  check('effect Before retains its full 12-second segment',()=>assert.equal(get('audio').paused,false));
  get('audio').currentTime=12;get('audio').ontimeupdate();
  check('effect Before stops at the right boundary',()=>assert.equal(get('audio').paused,true));
  check('anti-lag requires eligible petrol turbo hardware',()=>{get('reset').onclick();select('Effect','Anti-lag on lift');assert.equal(count(),3);assert.ok(effects.filter(r=>r.effect==='Anti-lag on lift').every(r=>r.fuelKind==='Petrol'&&r.antiLagTicks>0))});
  check('backfire examples contain actual simulated pops',()=>{get('reset').onclick();select('Effect','Backfire / overrun');assert.equal(count(),2);assert.ok(effects.filter(r=>r.effect==='Backfire / overrun').every(r=>r.pops>0))});
  check('returning to engine section resets filters',()=>{get('engines').onclick();assert.equal(count(),1272);assert.equal(get('factory').disabled,false)});
 }
 console.log(`PASS: ${checks} viewer logic checks. Mock media only; no browser playback claim.`);
}
main().catch(error=>{console.error(error);process.exitCode=1});
