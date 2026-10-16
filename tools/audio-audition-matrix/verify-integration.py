"""Compare the production DSP to the frozen approved voice and its published raw samples."""
from pathlib import Path
import argparse, hashlib, importlib.util, os, subprocess, sys

root=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--published-raw',type=Path,default=root/'reports/audio-matrix-20261015/raw')
args=parser.parse_args()
folder=root/'reports/audio-integration-20261015/parity-harness'
subprocess.run([sys.executable,str(Path(__file__).parent/'prepare.py'),'--output',str(folder)],check=True)
spec=importlib.util.spec_from_file_location('audition',Path(__file__).parent/'prepare.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
current=(root/'Game/Assets/DragLab/Scripts/Domain/EngineSound.cs').read_text(encoding='utf-8-sig')
standalone=(folder/'CurrentDsp.cs').read_text(encoding='utf-8-sig')
standalone=standalone.replace(module.extract(standalone,'    public sealed class EngineSoundSynth'),module.extract(current,'    public sealed class EngineSoundSynth'))
(folder/'CurrentDsp.cs').write_text(standalone,encoding='utf-8')
reference=(folder/'AuditionSynth.cs').read_text(encoding='utf-8-sig').encode('utf-8')
assert hashlib.sha256(reference).hexdigest()=='c1f694918d2aea27d2ad13f2cd2bebf43e47bd25fb84b1fc1ecec02c8fc1eeef','Approved voice reference changed; obtain a new listening approval before updating this gate.'
env=os.environ.copy();env['DOTNET_CLI_HOME']=str(folder/'.dotnet');env['DOTNET_CLI_TELEMETRY_OPTOUT']='1'
command=['C:/Program Files/dotnet/dotnet.exe','run','--configuration','Release','--project',str(folder/'Preview.csproj'),'--',str(folder),'--parity']
if args.published_raw.is_dir():command.append(str(args.published_raw.resolve()))
else:print('Published raw cache is absent; checking the hash-pinned approved source without an external fixture.',flush=True)
subprocess.run(command,env=env,check=True)
