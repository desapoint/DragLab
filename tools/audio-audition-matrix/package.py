"""Validate, loudness-match, deduplicate and package the offline listening library."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor, as_completed
import argparse, csv, hashlib, io, json, subprocess, wave, zipfile
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
FFMPEG=Path('C:/tools/FFMPEG/ffmpeg.exe')
RATE=48000

def run(args,**kwargs):
    return subprocess.run([str(FFMPEG),'-hide_banner','-nostats',*args],capture_output=True,check=True,**kwargs)

def loudness(path):
    result=run(['-f','f32le','-ar',str(RATE),'-ac','1','-i',str(path),'-af','loudnorm=I=-20:TP=-3:LRA=11:print_format=json','-f','null','-'])
    log=result.stderr.decode('utf-8',errors='replace')
    return json.JSONDecoder().raw_decode(log[log.rfind('{'):])[0]

def encode(out,identity,row):
    media=out/'audio'/(identity+'.flac');meta=out/'audio'/(identity+'.json')
    if media.exists() and meta.exists():
        cached=json.loads(meta.read_text(encoding='utf-8-sig'))
        assert hashlib.sha256(media.read_bytes()).hexdigest()==cached['sha256']
        return cached
    parts=[];measured=[]
    for side in ['before','after']:
        path=out/'raw'/(row['id']+'-'+side+'.f32')
        x=np.fromfile(path,dtype='<f4').astype(float)
        assert len(x)==10*RATE and np.isfinite(x).all() and abs(x).max()<=1
        m=loudness(path)
        gain=min(-20-float(m['input_i']),-3-float(m['input_tp']))
        x*=10**(gain/20)
        fade=int(RATE*.025);x[:fade]*=np.linspace(0,1,fade);x[-fade:]*=np.linspace(1,0,fade)
        assert abs(x).max()<.72
        measured.append({'side':side,'source_lufs':float(m['input_i']),'gain_db':gain,'presented_lufs':float(m['input_i'])+gain,'peak':float(abs(x).max())})
        parts.append(x)
    combined=np.concatenate([parts[0],np.zeros(int(RATE*.7)),parts[1]])
    data=io.BytesIO()
    with wave.open(data,'wb') as writer:
        writer.setnchannels(1);writer.setsampwidth(2);writer.setframerate(RATE);writer.writeframes((combined*32767).astype('<i2').tobytes())
    temp=media.with_suffix('.partial')
    run(['-y','-i','pipe:0','-c:a','flac','-compression_level','5','-f','flac',str(temp)],input=data.getvalue())
    temp.replace(media)
    # Decode every finished asset and compare every sample, not just the container header.
    decoded=run(['-i',str(media),'-f','s16le','-acodec','pcm_s16le','pipe:1']).stdout
    assert decoded==(combined*32767).astype('<i2').tobytes()
    result={'file':'audio/'+media.name,'samples':len(combined),'seconds':len(combined)/RATE,'measurements':measured,'sha256':hashlib.sha256(media.read_bytes()).hexdigest(),'lossless_roundtrip':True}
    meta.write_text(json.dumps(result,indent=2),encoding='utf-8')
    return result

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,default=ROOT/'reports/audio-matrix-20261015');parser.add_argument('--limit',type=int);args=parser.parse_args()
    out=args.output.resolve();assert out.is_relative_to(ROOT/'reports')
    cases=json.loads((out/'cases.json').read_text(encoding='utf-8-sig'))
    if args.limit:cases=cases[:args.limit]
    (out/'audio').mkdir(exist_ok=True)
    unique={};identities={}
    for row in cases:
        digest=hashlib.sha256(b'audition-matrix-normalize-v1')
        for side in ['before','after']:
            data=(out/'raw'/(row['id']+'-'+side+'.f32')).read_bytes()
            assert len(data)==10*RATE*4
            digest.update(data)
        identity=digest.hexdigest()[:24];identities[row['id']]=identity;unique.setdefault(identity,row)
    metadata={}
    with ThreadPoolExecutor(max_workers=4) as pool:
        jobs={pool.submit(encode,out,identity,row):identity for identity,row in unique.items()}
        for i,future in enumerate(as_completed(jobs),1):
            identity=jobs[future];metadata[identity]=future.result()
            if i%100==0:print(f'Packaged {i}/{len(jobs)} unique A/B files',flush=True)
    rows=[]
    for case in cases:
        row={k:v for k,v in case.items() if k!='profile'}
        media=metadata[identities[row['id']]];row.update(file=media['file'],audioHash=media['sha256'])
        row['levelDifferenceLufs']=round(media['measurements'][1]['presented_lufs']-media['measurements'][0]['presented_lufs'],3)
        rows.append(row)
    (out/'manifest.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
    with (out/'coverage-matrix.csv').open('w',encoding='utf-8',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(rows[0]),lineterminator='\n');writer.writeheader();writer.writerows(rows)
    template=(Path(__file__).parent/'viewer.html').read_text(encoding='utf-8-sig')
    (out/'index.html').write_text(template.replace('__MATRIX_JSON__',json.dumps(rows,ensure_ascii=False).replace('<','\\u003c')),encoding='utf-8')
    (out/'highlights').mkdir(exist_ok=True)
    for row in rows:
        if row['factoryReference']:
            run(['-y','-i',str(out/row['file']),'-c:a','pcm_s16le',str(out/'highlights'/(row['engineKey']+'-AB.wav'))])
    hashes=json.loads((out/'source-hashes.json').read_text(encoding='utf-8-sig'))
    for path,expected in hashes.items():assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==expected,path
    summary={'cases':len(rows),'unique_audio_pairs':len(metadata),'authored_engine_definitions':len({r['engineKey'] for r in rows if 'laboratory' not in r['group'].lower()}),'actual_cylinder_counts':sorted({r['cylinders'] for r in rows if 'laboratory' not in r['group'].lower()}),'groups':{g:sum(r['group']==g for r in rows) for g in sorted({r['group'] for r in rows})},'max_level_difference_lufs':max(abs(r['levelDifferenceLufs']) for r in rows),'finite_bounded_pcm':True,'lossless_roundtrip_all_unique_files':True,'source_hashes_unchanged':True,'game_integration':False,'audio_bytes':sum((out/m['file']).stat().st_size for m in metadata.values())}
    (out/'verification.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
    with zipfile.ZipFile(out/'listening-matrix.zip','w',compression=zipfile.ZIP_STORED) as archive:
        for name in ['index.html','manifest.json','coverage-matrix.csv','verification.json','source-hashes.json']:
            archive.write(out/name,name)
        for m in sorted(metadata.values(),key=lambda x:x['file']):archive.write(out/m['file'],m['file'])
    print(json.dumps(summary,indent=2),flush=True)

if __name__=='__main__':main()
