"""Verify actual SCML, PNG and exported native KAnim; does not run Unity."""
from pathlib import Path
from PIL import Image, ImageChops
import argparse, re, struct, xml.etree.ElementTree as E

ROOT=Path(__file__).resolve().parents[1]
arguments=argparse.ArgumentParser()
arguments.add_argument('--roundtrip',type=Path,help='Directory of native KAnim reimported by kanimal, with one subdirectory per animation set')
options=arguments.parse_args()
passed=0
def check(condition,message):
    global passed
    assert condition,message
    passed+=1

class Reader:
    def __init__(self,path):self.data=path.read_bytes();self.offset=4
    def read(self,fmt):
        result=struct.unpack_from('<'+fmt,self.data,self.offset);self.offset+=struct.calcsize('<'+fmt);return result
    def string(self):
        n=self.read('I')[0];s=self.data[self.offset:self.offset+n].decode('utf-8');self.offset+=n;return s

def exported_animation(path):
    r=Reader(path);check(r.data[:4]==b'ANIM','animation magic')
    version,elements,frames,count=r.read('4I');check(version==5,'animation version')
    clips={};actual_elements=0;actual_frames=0
    for _ in range(count):
        name=r.string();_,rate,n=r.read('IfI');parts=[]
        for _ in range(n):
            bounds=r.read('4f');ne=r.read('I')[0];actual_frames+=1;actual_elements+=ne
            parts.append([(*r.read('4I'),r.read('11f')) for _ in range(ne)])
        clips[name]=(rate,parts)
    maximum=r.read('I')[0];hashes={}
    for _ in range(r.read('I')[0]):
        h=r.read('I')[0];hashes[h]=r.string()
    check(r.offset==len(r.data),'unconsumed animation data')
    # Native KGlobalAnimParser ignores the two legacy header counts; kanimal
    # leaves them zero. The trailing visible-element maximum is authoritative.
    check(actual_frames>0 and maximum==max(len(f) for _,fs in clips.values() for f in fs),'native visible element maximum')
    return clips,hashes

def exported_build(path,atlas,source_count):
    r=Reader(path);check(r.data[:4]==b'BILD','build magic')
    version,ns,nf=r.read('3I');check(version==10,'build version');r.string();actual=0;symbols={}
    for _ in range(ns):
        header=r.read('5I');frames=[]
        for _ in range(header[4]):
            vals=r.read('3I8f');actual+=1;frames.append(vals)
            check(vals[1]>0 and vals[2]==0 and all(0<=v<=1 for v in vals[-4:]),'atlas UV/index/duration out of range')
        check(header[0] not in symbols,'duplicate native symbol');symbols[header[0]]=frames
    for _ in range(r.read('I')[0]):r.read('I');r.string()
    # Native parser ignores the legacy build frame-total header.
    check(r.offset==len(r.data) and actual==source_count,'native build/source frame totals')
    return symbols

def native_geometry(element,build):
    symbol,frame,_,_,f=element
    check(symbol in build,'native animation references missing symbol')
    matches=[b for b in build[symbol] if b[0]<=frame<b[0]+b[1]]
    check(len(matches)==1,'native animation references missing/overlapping symbol frame')
    b=matches[0];cx,cy,w,h=b[3:7]
    corners=[(.005*(f[4]*x+f[6]*y+f[8]),-.005*(f[5]*x+f[7]*y+f[9]))
             for x in (cx-w/2,cx+w/2) for y in (cy-h/2,cy+h/2)]
    return b,(min(x for x,y in corners),min(y for x,y in corners),max(x for x,y in corners),max(y for x,y in corners))

for name,building in [('baiye_cryogenic_pantry',True),('baiye_pantry_drinks',False)]:
    src=ROOT/'art/source'/name;dest=ROOT/'anim/assets'/name
    tree=E.parse(src/(name+'.scml')).getroot();files={f.attrib['id']:f.attrib for f in tree.findall('folder/file')}
    clips={a.attrib['name']:a for a in tree.findall('entity/animation')}
    native,hashes=exported_animation(dest/(name+'_anim.bytes'))
    atlas=Image.open(dest/(name+'.png'));check(atlas.mode=='RGBA' and max(atlas.size)<=2048,'bounded RGBA atlas')
    build=exported_build(dest/(name+'_build.bytes'),atlas,len(files))
    check(clips.keys()==native.keys(),'native/source clips agree')
    for f in files.values():
        im=Image.open(src/f['name']);check(im.mode=='RGBA' and im.size==(int(f['width']),int(f['height'])),'sprite dimensions/alpha')
        if options.roundtrip:
            path=options.roundtrip/name/f['name'];check(path.is_file(),'reimported sprite missing: '+f['name'])
            restored=Image.open(path).convert('RGBA');check(restored.size==im.size,'native sprite dimensions disagree: '+f['name'])
            delta=ImageChops.difference(im,restored)
            check(delta.getchannel('A').getextrema()==(0,0),'native sprite alpha differs from current source: '+f['name'])
            visible=im.getchannel('A').point(lambda alpha:255 if alpha else 0)
            delta=Image.composite(delta,Image.new('RGBA',delta.size),visible)
            check(max(high for low,high in delta.getextrema()[:3])<=1,'native visible RGB differs from current source: '+f['name'])
    for clip,(rate,frames) in native.items():
        for frame in frames:
            for element in frame:
                _,bounds=native_geometry(element,build)
                if building and clip!='ui':
                    x0,y0,x1,y1=bounds
                    check(-1.01<=x0<=x1<=1.01 and -.01<=y0<=y1<=3.01,'building animation exceeds 2x3: '+clip+' '+hashes.get(element[0],''))
    if building:
        check({'off','idle','preserved','cooling','processing','broken','door','ui','place','on'}<=native.keys(),'required lifecycle clips')
        for clip in ('cooling','processing','door'):check(len(native[clip][1])==40,'mechanical clip frame count')
        check(native['cooling'][1][0]!=native['cooling'][1][10],'cooling moves parts')
        check(native['processing'][1][0]!=native['processing'][1][10],'processing moves parts')
        check(native['door'][1][0]!=native['door'][1][10],'door moves parts')
        check(not any(hashes.get(e[0],'').startswith('status') for e in native['off'][1][0]),'powered status absent when off')
        def named(prefix,clip,frame=0):
            elements=[e for e in native[clip][1][frame] if hashes.get(e[0],'').startswith(prefix)]
            check(len(elements)==1,'exactly one '+prefix+' in '+clip)
            return elements[0]
        # Inspect individual native transforms rather than accepting any moving layer.
        for prefix,clip in [('rotor','cooling'),('impeller','processing'),('nozzle','processing'),('door','door')]:
            check(named(prefix,clip,0)[4]!=named(prefix,clip,5)[4],prefix+' has independent mechanical motion')
        for n in range(3):
            prefix='bubble'+str(n)
            first=named(prefix,'processing',0);later=named(prefix,'processing',5)
            _,b0=native_geometry(first,build);_,b1=native_geometry(later,build)
            check(b1[1]>b0[1],prefix+' rises inside the liquid tube')
            for clip in ('off','idle','preserved','cooling'):
                check(not any(hashes.get(e[0],'').startswith(prefix) for e in native[clip][1][0]),prefix+' absent outside processing')
    else:
        check({'BaiyeChilledSoda','BaiyeBerrySoda','BaiyePepperSoda','BaiyeEnergySoda'}<=native.keys(),'four separate bottle clips')
print(f'PASS {passed} source/native KAnim, symbol references, geometry, alpha and motion assertions; no Unity rendering.')
