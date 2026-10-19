"""Verify the current production pressure/induction DSP against its reviewed foundation."""
from pathlib import Path
import argparse,subprocess,sys,uuid

ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--output',type=Path)
args=parser.parse_args()
out=args.output or ROOT/('reports/audio-pressure-20261019-check-'+uuid.uuid4().hex[:8])
subprocess.run([sys.executable,str(Path(__file__).with_name('verify-pressure-live.py')),'--output',str(out)],check=True)
