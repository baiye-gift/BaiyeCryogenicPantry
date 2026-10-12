"""Mechanically import generated sprites and export editable SCML/KAnim.
No artwork is repainted. Source alpha is preserved; particles are simple runtime effects.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import argparse, math, os, subprocess, xml.etree.ElementTree as E

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'art/source'
SHEET=Image.open(SOURCE/'future-pantry-parts-oni-v3.png').convert('RGBA')
RECTS=[(55,0,400,480),(403,80,742,474),(742,175,1037,470),(1040,230,1415,445),
       (125,510,346,746),(485,473,644,766),(765,567,1014,706),(1135,520,1335,752),
       (122,771,308,1086),(459,771,646,1086),(796,771,982,1086),(1135,771,1323,1086)]

def crop(index,size):
    im=SHEET.crop(RECTS[index]);box=im.getchannel('A').getbbox()
    if not box: raise ValueError('Empty component')
    return im.crop(box).resize(size,Image.Resampling.LANCZOS)

def part(name,x,y,s=100/256,**kw):
    return (name,dict(x=x,y=y,scale_x=s,scale_y=s,**kw))

class Animation:
    def __init__(self,name):
        self.name=name;self.source=SOURCE/name;self.source.mkdir(parents=True,exist_ok=True)
        self.dest=ROOT/'anim/assets'/name;self.dest.mkdir(parents=True,exist_ok=True)
        self.sprites={};self.clips={}
    def sprite(self,name,im):
        im.save(self.source/(name+'.png'));self.sprites[name]=im
    def clip(self,name,frames):self.clips[name]=frames
    def compile(self,exe):
        root=E.Element('spriter_data',scml_version='1.0',generator='BaiyeCryogenicPantry',generator_version='0.1.0')
        folder=E.SubElement(root,'folder',id='0');ids={}
        for n,(name,im) in enumerate(self.sprites.items()):
            ids[name]=n;E.SubElement(folder,'file',id=str(n),name=name+'.png',width=str(im.width),height=str(im.height),pivot_x='.5',pivot_y='.5')
        entity=E.SubElement(root,'entity',id='0',name=self.name)
        for n,(name,frames) in enumerate(self.clips.items()):
            anim=E.SubElement(entity,'animation',id=str(n),name=name,length=str(len(frames)*50),interval='50',looping='true')
            main=E.SubElement(anim,'mainline');timelines={}
            for f,parts in enumerate(frames):
                key=E.SubElement(main,'key',id=str(f),time=str(f*50))
                for z,(sprite,attrs) in enumerate(parts):
                    if sprite not in timelines:timelines[sprite]=E.SubElement(anim,'timeline',id=str(len(timelines)),name=sprite)
                    tl=timelines[sprite];kid=str(len(tl.findall('key')))
                    tk=E.SubElement(tl,'key',id=kid,time=str(f*50),curve_type='instant')
                    E.SubElement(tk,'object',folder='0',file=str(ids[sprite]),**{k:str(round(v,6)) for k,v in attrs.items()})
                    E.SubElement(key,'object_ref',id=str(z),timeline=tl.attrib['id'],key=kid,z_index=str(z))
        E.indent(root);path=self.source/(self.name+'.scml');E.ElementTree(root).write(path,encoding='utf-8',xml_declaration=True)
        env=os.environ.copy();env['DOTNET_ROLL_FORWARD']='Major'
        subprocess.run([exe,'kanim',str(path),'-o',str(self.dest)],env=env,check=True)
    def render(self,parts,width=200,height=300,pixels=2):
        out=Image.new('RGBA',(round(width*pixels),round(height*pixels)))
        for name,a in parts:
            im=self.sprites[name];im=im.resize((max(1,round(im.width*a.get('scale_x',1)*pixels)),max(1,round(im.height*a.get('scale_y',1)*pixels))),Image.Resampling.LANCZOS)
            im=im.rotate(a.get('angle',0),expand=True,resample=Image.Resampling.BICUBIC)
            x=round((a['x']+width/2)*pixels-im.width/2);y=round((height-a['y'])*pixels-im.height/2)
            out.alpha_composite(im,(x,y))
        return out

def build(exe):
    a=Animation('baiye_cryogenic_pantry')
    for name,index,size in [('body',0,(512,768)),('door',1,(367,550)),('rotor',2,(118,118)),('gas',3,(150,68)),('impeller',4,(40,40)),('tube',5,(68,151)),('status',6,(58,20)),('nozzle',7,(45,64))]:a.sprite(name+'_0',crop(index,size))
    # Simple procedural particles belong to the simulation-effects layer, not the imported art.
    dot=Image.new('RGBA',(10,10));ImageDraw.Draw(dot).ellipse((1,1,8,8),outline=(225,255,255,240),width=2)
    for n in range(3):a.sprite('bubble'+str(n)+'_0',dot)
    def parts(frame,state):
        t=frame/40
        rotor=(t*360 if state in ('cooling','processing') else 0)
        door_slide=5*math.sin(math.pi*t) if state=='door' else 0
        p=[part('body_0',0,150),part('door_0',-6+door_slide,171),part('rotor_0',19,42,angle=rotor),part('gas_0',-43,269)]
        if state!='off':p.append(part('status_0',39,271))
        if state=='processing':
            p += [part('tube_0',76,114),part('nozzle_0',76,78+math.sin(t*4*math.pi)),part('impeller_0',75,52,angle=-t*720)]
            for n in range(3):p.append(part('bubble'+str(n)+'_0',76+(-1 if n%2 else 1)*3,101+((t+n/3)%1)*24,s=.34))
        else:p+=[part('tube_0',76,114),part('nozzle_0',76,78),part('impeller_0',75,52)]
        return p
    for state in ('off','idle','preserved','cooling','processing','broken','door'):
        frames=[parts(n,state) for n in range(40 if state in ('cooling','processing','door') else 1)]
        a.clip(state,frames)
    for state in ('place','on'):a.clip(state,[parts(0,'idle')])
    preview=a.render(parts(0,'preserved'));preview.save(ROOT/'art/pantry-preview.png')
    icon=a.render(parts(0,'idle'),pixels=.42);a.sprite('ui_0',icon);a.clip('ui',[[part('ui_0',0,0,s=1)]])
    a.compile(exe)
    for state in ('processing','cooling'):
        images=[a.render(parts(n,state),pixels=1.4) for n in range(40)]
        images[0].save(ROOT/'art'/(state+'-preview.gif'),save_all=True,append_images=images[1:],duration=50,loop=0,disposal=2)
    b=Animation('baiye_pantry_drinks')
    for n,id in enumerate(('BaiyeChilledSoda','BaiyeBerrySoda','BaiyePepperSoda','BaiyeEnergySoda')):
        name='drink'+str(n)+'_0';b.sprite(name,crop(8+n,(64,112)));b.clip(id,[[part(name,0,0,s=.32)]])
    b.clip('object',b.clips['BaiyeChilledSoda']);b.clip('ui',b.clips['BaiyeChilledSoda']);b.compile(exe)
    print('PASS: original art imported, 10 building clips and 6 drink clips exported; previews are compositing, not Unity screenshots.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--kanimal',required=True);build(p.parse_args().kanimal)
