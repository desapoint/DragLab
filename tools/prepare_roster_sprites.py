"""Prepare raster masters without redrawing their car bodies.

Pillow/numpy only. --measure records proposed native arch fits for inspection;
the normal pass consumes that reviewed plan and emits layered game assets/QA.
Authored source bytes, previous sprite packs and game physics are untouched.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import re
import shutil
import uuid
import numpy as np
from PIL import Image, ImageDraw, ImageChops, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "art/roster-cel-v1"
DEST = ROOT / "Game/Assets/DragLab/Resources/Sprites/Vehicles/roster"


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path, value): path.write_text(json.dumps(value, indent=2), encoding="utf-8")


def runs(row):
    edges = np.flatnonzero(np.diff(np.r_[False, row, False].astype(np.int8)))
    return list(zip(edges[::2], edges[1::2]))


def inspect_arch(im, front):
    a = np.array(im.getchannel("A")) > 127
    yy, xx = np.where(a); left, right, top, bottom = int(xx.min()), int(xx.max()), int(yy.min()), int(yy.max())
    width = right-left; points=[]
    for y in range(round(top+(bottom-top)*.45), bottom-5):
        solid=np.flatnonzero(a[y]);
        if len(solid)<2: continue
        candidates=[(lo,hi) for lo,hi in runs(~a[y]) if lo>solid[0] and hi<solid[-1] and hi-lo>width*.025
                    and lo>left+width*(.55 if front else .025) and hi<left+width*(.975 if front else .45)
                    and ((.57 if front else .05)<((lo+hi)*.5-left)/width<(.95 if front else .44))]
        if candidates:
            lo,hi=max(candidates,key=lambda p:p[1]-p[0]); points.append((y,int(lo),int(hi)))
    if len(points)<20: raise ValueError("No measurable empty arch")
    widest=max(p[2]-p[1] for p in points)
    broad=[p for p in points if p[2]-p[1]>=widest*.97]
    x=float(np.median([(p[1]+p[2])*.5 for p in broad])); r=widest*.5
    return {"x":round(x,3),"y":round(points[0][0]+r,3),"archRadius":round(r,3),
            "top":points[0][0],"bottom":bottom,"contour":[[p[1],p[0]] for p in points]+[[p[2],p[0]] for p in reversed(points)]}


def measure():
    assets={}
    for f in sorted((ROOT/"Game/Assets/DragLab/Resources/Data/Vehicles").glob("*.asset")):
        text=f.read_text(encoding="utf-8-sig"); get=lambda name: re.search(r"^    "+name+r": (.+)$",text,re.M)[1]
        family=get("modelFamily").lower()
        if family not in assets: assets[family]={"wheelbaseM":float(get("wheelbaseM")),"wheelRadiusM":float(get("wheelRadiusM")),"asset":f.relative_to(ROOT).as_posix()}
    plan={"schemaVersion":1,"bodyAlphaFloor":2,"models":[]}
    for folder in sorted(p for p in ART.iterdir() if p.is_dir() and (p/"master.png").exists()):
        image=Image.open(folder/"master.png").convert("RGBA")
        plan["models"].append({"id":folder.name,"sourceSha256":sha(folder/"master.png"),"nativeSize":list(image.size),
            "reference":assets[folder.name],"rearWheel":inspect_arch(image,False),"frontWheel":inspect_arch(image,True),"fixtureExclusions":[]})
    write(ART/"preparation-plan.json",plan)
    overview(plan, "geometry", True)
    print("Measured",len(plan["models"]),"raster bodies; inspect geometry.png before preparation.")


def meta(path):
    output=Path(str(path)+".meta")
    if not output.exists():
        guid=uuid.uuid5(uuid.NAMESPACE_URL,"draglab/"+path.relative_to(ROOT).as_posix()).hex
        output.write_text(f"fileFormatVersion: 2\nguid: {guid}\n"+("folderAsset: yes\n" if path.is_dir() else ""),encoding="utf-8")


def prepare(model):
    folder=ART/model["id"]; assert sha(folder/"master.png")==model["sourceSha256"]
    raw=Image.open(folder/"master.png").convert("RGBA"); pixels=np.array(raw); alpha=pixels[:,:,3].copy(); alpha[alpha<2]=0
    # Generated interiors contain 251-254 alpha noise. Restore intended opaque
    # surfaces, keeping the actual translucent silhouette/antialiasing below 250.
    alpha[alpha>=250]=255
    rgb=pixels[:,:,:3].astype(np.int16); r,g,b=np.moveaxis(rgb,2,0)
    paint=(g-r>np.maximum(15,g*.22))&(b-r>np.maximum(10,b*.2))&(r<np.minimum(g,b)*.72)&(alpha>0)
    # Explicit inspected exclusions can protect a painted-looking fixture (e.g. mirror casing).
    exclusions=Image.new("L",raw.size); draw=ImageDraw.Draw(exclusions)
    for polygon in model["fixtureExclusions"]: draw.polygon([tuple(p) for p in polygon],fill=255)
    paint &= np.array(exclusions)==0
    fixtures=np.zeros_like(pixels); fixtures[:,:,:3]=pixels[:,:,:3]; fixtures[:,:,3]=np.where(paint,0,alpha)
    neutral=np.max(rgb,axis=2).astype(np.uint8)
    base=np.zeros_like(pixels); base[:,:,:3]=np.where(paint[:,:,None],neutral[:,:,None],44)
    # Continuous hidden foundation; don't double alpha at the exposed fixture silhouette.
    base[:,:,3]=np.where((~paint)&(alpha<255),0,alpha)
    visible=np.zeros_like(pixels); visible[:,:,:3]=255;visible[:,:,3]=np.where(paint,alpha,0)
    rear,front=model["rearWheel"],model["frontWheel"]
    scale=model["reference"]["wheelbaseM"]*200/(front["x"]-rear["x"])
    radius=min(rear["archRadius"],front["archRadius"])*.94
    # Keep the approved natural wheel-to-body proportions; this visual radius does not set physics.
    center_y=(rear["y"]+front["y"])*.5
    width=max(1024,math.ceil(raw.width*scale+40));height=max(384,math.ceil(center_y*scale+radius*scale+40))
    offset_x=round(width*.5-(rear["x"]+front["x"])*.5*scale)
    offset_y=round(height-24-(center_y+radius)*scale)
    def normalized(image):
        out=Image.new("RGBA",(width,height)); resized=image.resize((round(raw.width*scale),round(raw.height*scale)),Image.Resampling.LANCZOS)
        out.alpha_composite(resized,(offset_x,offset_y));return out
    out=folder/"layers";out.mkdir(exist_ok=True)
    images={"body-paint":normalized(Image.fromarray(base)),"body-fixtures":normalized(Image.fromarray(fixtures)),"paint-mask":normalized(Image.fromarray(visible))}
    backing=Image.new("RGBA",raw.size)
    backing_draw=ImageDraw.Draw(backing)
    for arch in (rear,front): backing_draw.polygon([tuple(p) for p in arch["contour"]],fill=(9,12,16,255))
    ba=np.array(backing); ba[:,:,3]=np.where(alpha==0,ba[:,:,3],0);images["car-underlay"]=normalized(Image.fromarray(ba))
    shadow=Image.new("RGBA",(width,height));sd=ImageDraw.Draw(shadow)
    sd.ellipse((offset_x+30,height-30,width-offset_x-30,height-14),fill=(0,0,0,65));images["shadow"]=shadow
    for name,image in images.items(): image.save(out/(name+".png"))
    for name in ("wheel","rotor","caliper"): shutil.copyfile(ROOT/"art/vehicles/hatch"/(name+".png"),out/(name+".png"))
    anchors={}
    for label,arch in (("rearWheel",rear),("frontWheel",front)):
        anchors[label]={"x":round(arch["x"]*scale+offset_x,3),"y":round(center_y*scale+offset_y,3),"radius":round(radius*scale,3)}
    layers=[{"id":name,"file":name+".png","z":z,"placement":{"mode":"canvas","x":0,"y":0}} for name,z in (("shadow",0),("car-underlay",50))]
    for axle in ("rear","front"):
        for part,z in (("rotor",100),("caliper",200),("wheel",300)):
            layers.append({"id":axle+"-"+part,"file":part+".png","z":z,"rotateWithWheel":part!="caliper","placement":{"mode":"anchor","anchor":axle+"Wheel","offset":[0,0],"pivotNormalized":[.5,.5],"scale":round(radius*scale/76*(.88 if axle=="rear" and part!="wheel" else 1),6)}})
    layers.extend({"id":name,"file":name+".png","z":z,"placement":{"mode":"canvas","x":0,"y":0}} for name,z in (("body-paint",400),("body-fixtures",430)))
    manifest={"schemaVersion":1,"revision":1,"vehicle":{"id":model["id"],"make":"DragLab","model":model["id"].title(),"facing":"right","trim":"Fictional raster body"},"canvas":{"width":width,"height":height},"anchors":anchors,"layers":layers,"masks":{"paint":"paint-mask.png"},"sources":[{"file":"../master.png","sha256":model["sourceSha256"]}],"notes":["Protected fixtures are source pixels above a neutral paint foundation.","Existing independent wheel/rotor/caliper masters reused; new shop wheel artwork is separate.","Visual axle spacing follows authored game wheelbase; art does not set physics."]}
    # Runtime manifests stay self-contained; source provenance lives beside the layer pack.
    manifest["sources"]=[]
    write(out/"car-sprite.json",manifest)
    provenance={"sourceSha256":model["sourceSha256"],"scale":scale,"offset":[offset_x,offset_y],"alphaFloor":2,"opaqueAlphaCeiling":250,"paintClassification":{"gMinusR":"max(15,0.22g)","bMinusR":"max(10,0.20b)","redBelowMinGB":.72},"paintPixels":int(paint.sum()),"fixturePixels":int(((~paint)&(alpha>0)).sum())}
    write(folder/"preparation.json",provenance)
    dst=DEST/model["id"];dst.mkdir(parents=True,exist_ok=True);meta(DEST);meta(dst)
    for file in out.iterdir(): shutil.copyfile(file,dst/file.name);meta(dst/file.name)
    return validate(model,images)


def recolor(folder, color):
    base=Image.open(folder/"layers/body-paint.png").convert("RGBA"); colored=ImageChops.multiply(base,Image.new("RGBA",base.size,color))
    colored.alpha_composite(Image.open(folder/"layers/body-fixtures.png"));return colored


def validate(model,images):
    folder=ART/model["id"];base=images["body-paint"];fixture=np.array(images["body-fixtures"])
    protected=fixture[:,:,3]==255; visible=np.array(images["paint-mask"])[:,:,3]>250
    colors=("#f54b64","#edc857","#385bea","#eeeeee","#16171a")
    for color in colors:
        output=np.array(recolor(folder,color)); assert np.array_equal(output[protected,:3],fixture[protected,:3]),model["id"]+" fixture repaint leak"
    red=np.array(recolor(folder,"#f54b64")); blue=np.array(recolor(folder,"#385bea"))
    assert np.all(np.any(red[visible,:3]!=blue[visible,:3],axis=1)),model["id"]+" incomplete paint coverage"
    assert int(visible.sum())>10000 and int(protected.sum())>10000
    m=json.loads((folder/"layers/car-sprite.json").read_text(encoding="utf-8"))
    for arch in m["anchors"].values():
        x,y=round(arch["x"]),round(arch["y"]); assert base.getpixel((x,y))[3]==0 and images["body-fixtures"].getpixel((x,y))[3]==0,model["id"]+" baked wheel"
    return {"id":model["id"],"sourceSha256":model["sourceSha256"],"paintPixels":int(visible.sum()),"protectedPixels":int(protected.sum()),"repaints":len(colors),"fixtureDifference":0,"runtimeCanvas":[base.width,base.height],"wheelbasePx":round(m["anchors"]["frontWheel"]["x"]-m["anchors"]["rearWheel"]["x"],3)}


def overview(plan,name,geometry=False):
    out=ART/"review";out.mkdir(exist_ok=True);columns=3;cw,ch=620,265
    sheet=Image.new("RGB",(columns*cw,math.ceil(len(plan["models"])/columns)*ch),"#1b222b"); draw=ImageDraw.Draw(sheet)
    for i,model in enumerate(plan["models"]):
        folder=ART/model["id"]; image=Image.open(folder/"master.png").convert("RGBA") if geometry else recolor(folder,"#d94462")
        if geometry:
            d=ImageDraw.Draw(image)
            for arch in (model["rearWheel"],model["frontWheel"]):
                x,y,r=arch["x"],arch["y"],arch["archRadius"]*.94;d.ellipse((x-r,y-r,x+r,y+r),outline="#ffce51",width=5);d.line((x-18,y,x+18,y),fill="white",width=3)
        image.thumbnail((cw-20,ch-35),Image.Resampling.LANCZOS);x=i%columns*cw+(cw-image.width)//2;y=i//columns*ch+10
        sheet.paste(image,(x,y),image);draw.text((i%columns*cw+18,i//columns*ch+ch-20),model["id"].upper(),fill="white")
    sheet.save(out/(name+".png"))


def main():
    parser=argparse.ArgumentParser();parser.add_argument("--measure",action="store_true");args=parser.parse_args()
    if args.measure: measure();return
    plan=json.loads((ART/"preparation-plan.json").read_text(encoding="utf-8-sig"));results=[prepare(m) for m in plan["models"]]
    write(ART/"verification.json",{"models":results,"fixtureDifference":0,"sourceCount":len(results)})
    overview(plan,"red-repaint")
    print("Prepared",len(results),"layered raster bodies; five fixture-invariant repaints and empty wheel centers checked.")


if __name__=="__main__":main()
