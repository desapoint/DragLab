// Independent numerical comparison against the unmodified supplied Phase 3.5 DSP.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '..');
const folder = path.resolve(process.argv[2] || path.join(root, 'reports/exhaust-waveguide-20261022'));
const source = fs.readFileSync(path.join(root, 'docs/reference/audio-phase35/Engine_Workbench_Phase35_Waveguide_Muffler.html'), 'utf8');
const helpers = source.slice(source.indexOf('class Delay{'), source.indexOf('const partDefaults='));
const acoustic = source.slice(source.indexOf('class AcousticWavePath{'), source.indexOf('class Core{'));
if (!helpers || !acoustic || !acoustic.includes('class ExhaustWaveSystem')) throw new Error('Reference extraction failed');
const context = vm.createContext({});
vm.runInContext(`const TAU=2*Math.PI, clamp=(x,a,b)=>Math.max(a,Math.min(b,x)), lerp=(a,b,f)=>a+(b-a)*f;${helpers}\n${acoustic}\nglobalThis.ExhaustWaveSystem=ExhaustWaveSystem;`, context, { timeout: 5000 });
const manifest = JSON.parse(fs.readFileSync(path.join(folder, 'reference-cases.json'), 'utf8'));
let checks=0, maxError=0;
const lines=[];
for (const entry of manifest.cases) {
  const s=Object.fromEntries(Object.entries(entry.spec).map(([k,v])=>[k,typeof v==='number'?Math.fround(v):v]));
  const p={modelExhaustPropagation:s.pipeMode===1?'sim':'approx',modelMufflerAcoustics:s.mufflerMode===1?'sim':'approx',waveHeaderLengthM:s.headerLengthM,waveCollectorLengthM:s.collectorLengthM,waveTailLengthM:s.tailLengthM,waveHeaderDiameterMm:s.headerDiameterMm,waveCollectorDiameterMm:s.collectorDiameterMm,waveTailDiameterMm:s.tailDiameterMm,waveWallLoss:s.wallLoss,waveReflectionStrength:s.reflectionStrength,waveGasTemperatureK:s.gasTemperatureK,waveOutputGain:s.outputGain,mufflerChamberLengthM:s.chamberLengthM,mufflerExpansionRatio:s.expansionRatio,mufflerPacking:s.packing,mufflerResonatorHz:s.resonatorHz,mufflerResonatorQ:s.resonatorQ,mufflerResonatorDepth:s.resonatorDepth};
  const model=new context.ExhaustWaveSystem(entry.rate,p);
  const native=fs.readFileSync(path.join(folder,entry.file));
  if(native.length!==entry.rate*16)throw new Error('Incomplete native impulse '+entry.file);
  let error=0;
  for(let i=0;i<entry.rate;i++){
    const left=i===0?1:i===137?.12:0,right=i===0?.3:i===137?-.07:0;
    const out=model.process(left,right,i===47?.2:0,i===79?.1:0,entry.position);
    for(let c=0;c<2;c++)error=Math.max(error,Math.abs(out[c]-native.readDoubleLE(i*16+c*8)));
  }
  if(!Number.isFinite(error)||error>2e-10)throw new Error(`${entry.file}: mismatch ${error}`);
  maxError=Math.max(maxError,error);checks++;lines.push(`PASS: ${entry.file} max absolute difference ${error}`);
}
lines.push(`Checks: ${checks}; worst absolute difference: ${maxError}`);
fs.writeFileSync(path.join(folder,'reference-parity.txt'),lines.join('\n')+'\n','utf8');
console.log(lines.join('\n'));
