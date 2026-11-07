import sys, glob, numpy as np
from PIL import Image
from scipy import ndimage
FILL=np.array([215,153,72],float)
def shade(path, size=128, fit=118, lo=0.70, hi=1.06):
    im=Image.open(path).convert('RGBA'); a=np.array(im).astype(float)/255
    m=a[...,3]>0.5
    ys,xs=np.nonzero(m); y0,y1,x0,x1=ys.min(),ys.max()+1,xs.min(),xs.max()+1
    w,h=x1-x0,y1-y0; s=fit/max(w,h); ox=(size-w*s)/2; oy=(size-h*s)/2
    # same sampling as C#: bilinear of the source at each icon pixel (PIL y is top-down; same mapping either way)
    yy,xx=np.mgrid[0:size,0:size]
    sx=x0+(xx+0.5-ox)/s-0.5; sy=y0+(yy+0.5-oy)/s-0.5
    def bil(ch):
        return ndimage.map_coordinates(ch,[sy,sx],order=1,mode='constant',cval=0)
    A=bil(a[...,3]); 
    lum=a[...,0]*.3+a[...,1]*.59+a[...,2]*.11
    # premultiplied so the edge doesn't darken
    LA=bil(lum*a[...,3]); L=np.where(A>1e-3,LA/np.maximum(A,1e-3),0)
    inside=A>0.5
    ref=np.percentile(L[inside],95)
    k=np.clip(L/ref,0,1)
    col=FILL[None,None,:]*(lo+(hi-lo)*k)[...,None]
    out=np.zeros((size,size,4))
    out[...,:3]=np.clip(col,0,255); out[...,3]=A*255
    # soft shadow around: alpha grown and blurred, under the figure
    sh=ndimage.gaussian_filter(np.clip(ndimage.grey_dilation(A,size=(5,5)),0,1),1.6)*150/255
    base=np.zeros((size,size,4)); base[...,:3]=[25,18,12]; base[...,3]=sh*255
    # composite out over base
    fa=out[...,3:4]/255; ba=base[...,3:4]/255
    oa=fa+ba*(1-fa)
    oc=(out[...,:3]*fa+base[...,:3]*ba*(1-fa))/np.maximum(oa,1e-6)
    res=np.dstack([oc,oa*255]).clip(0,255).astype(np.uint8)
    return Image.fromarray(res), np.percentile(L[inside],[5,50,95])
if __name__=='__main__':
    d='icons-h/PotatoWright-test-icons/'
    fs=sorted(glob.glob(d+'*Pose*_render.png'))
    W=8;S=128; rows=(len(fs)+W-1)//W
    sheet=Image.new('RGBA',(W*S,rows*S),(60,48,38,255))
    for i,f in enumerate(fs):
        im,p=shade(f); 
        if i<3: print(f.split('/')[-1],p)
        sheet.alpha_composite(im,((i%W)*S,(i//W)*S))
    sheet.save('shade_sheet.png')
    big=Image.new('RGBA',(3*384,384),(60,48,38,255))
    for i,n in enumerate(['Male_Pose_Warrioridle','Female_Pose_Wave','Male_Pose_Sitting']):
        im,_=shade(d+'PotatoWright_Mannequin'+n+'_render.png'); big.alpha_composite(im.resize((384,384),Image.NEAREST),(i*384,0))
    big.save('shade_big.png')
