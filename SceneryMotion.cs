using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MountainTide
{
    internal sealed partial class Game
    {
        void DrawHearthAir(Graphics g,double time)
        {
            // Steam starts at the copper kettle's spout and thins as it rises.
            for(int i=0;i<3;i++){
                double age=(time*.41+i/3.0)%1;
                float x=631+(float)(4*age*Math.Sin(time*1.6+i*2.1)),y=244-(float)age*29;
                float spread=(float)(1+3*age);
                int alpha=(int)(87*Math.Sin(age*Math.PI));
                using(Pen steam=new Pen(Color.FromArgb(alpha,255,242,216),1.3f+(float)age)){
                    steam.StartCap=LineCap.Round;steam.EndCap=LineCap.Round;
                    g.DrawBezier(steam,x,y,x-spread,y-3,x+spread,y-5,x+spread,y-8);
                }
            }
            // Motes only appear in the light coming through the left-hand window.
            for(int i=0;i<14;i++){
                double age=(time*(.054+.006*(i%3))+i*.38197)%1;
                float x=(float)(156+(i*63)%112+age*45+5*Math.Sin(time*.6+i));
                float y=(float)(245+(i*37)%158+age*30+3*Math.Sin(time*.82+i));
                int alpha=(int)(74*Math.Sin(age*Math.PI));
                float size=i%3==0?2.6f:1.5f;
                g.FillEllipse(B(Color.FromArgb(alpha,255,247,208)),x,y,size,size);
            }
        }
    }

    // The masks live in map coordinates. Only the paint under a moving material is
    // resampled; characters, doors, shop outlines and interface use the original geometry.
    internal sealed class SceneryMotion
    {
        enum Material { Still, Cloud, Leaves, Lawn, Curtain, Fire, Glow, Sea, Flag, Boat, Falls }
        const int Step=5, Unit=256;
        readonly Bitmap map;
        readonly int width,height,columns;
        readonly int[] original,pixels;
        readonly Node[] nodes;
        readonly int[] movingNodes;
        readonly Cell[] cells;
        readonly Wave[] clocks=new Wave[11];
        double lastFrame=-1;

        struct Node {
            public Material Kind;
            public float SinA,CosA,SinB,CosB,Depth;
            public int X,Y,Light;
        }
        struct Cell {
            public int Node,Pixel,X,Y;
            public bool Warp,Shade;
        }
        struct Wave { public float SinA,CosA,SinB,CosB; }
        // Seconds are shared across scene changes. Layers use different periods and spatial phases.
        static readonly double[] Speed={0,.32,1.23,.48,1.08,5.3,3.8,1.65,2.7,1.12,2.8};

        internal SceneryMotion(Bitmap map,string maskName)
        {
            this.map=map;width=map.Width;height=map.Height;columns=width/Step+1;
            original=new int[width*height];pixels=new int[original.Length];
            Rectangle bounds=new Rectangle(0,0,width,height);
            BitmapData data=map.LockBits(bounds,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            try { Marshal.Copy(data.Scan0,original,0,original.Length); }
            finally { map.UnlockBits(data); }
            Array.Copy(original,pixels,original.Length);
            nodes=new Node[columns*(height/Step+1)];
            var active=new List<int>();
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(maskName))
            using(Bitmap mask=new Bitmap(stream)){
                if(mask.Width!=columns||mask.Height!=height/Step+1)throw new InvalidDataException(maskName);
                for(int y=0;y<mask.Height;y++)for(int x=0;x<columns;x++){
                    Color color=mask.GetPixel(x,y);
                    if(color.G==0)continue;
                    int i=y*columns+x;
                    nodes[i]=MakeNode((Material)color.R,x*Step,y*Step,color.G/255f);
                    active.Add(i);
                }
            }
            movingNodes=active.ToArray();
            var visible=new List<Cell>();
            for(int y=0;y<height;y+=Step)for(int x=0;x<width;x+=Step){
                int i=y/Step*columns+x/Step;
                Cell cell=new Cell{Node=i,Pixel=y*width+x,X=x,Y=y};
                AddMaterial(ref cell,nodes[i].Kind);AddMaterial(ref cell,nodes[i+1].Kind);
                AddMaterial(ref cell,nodes[i+columns].Kind);AddMaterial(ref cell,nodes[i+columns+1].Kind);
                if(cell.Warp||cell.Shade)visible.Add(cell);
            }
            cells=visible.ToArray();
        }

        static void AddMaterial(ref Cell cell,Material kind)
        {
            if(kind==Material.Still)return;
            if(kind!=Material.Lawn&&kind!=Material.Glow)cell.Warp=true;
            if(kind==Material.Lawn||kind==Material.Glow||kind==Material.Fire||kind==Material.Sea)cell.Shade=true;
        }

        static Node MakeNode(Material kind,int x,int y,float weight)
        {
            double a,b;
            switch(kind){
                case Material.Cloud: a=-x*.002+y*.003;b=-x*.004-y*.009;break;
                case Material.Lawn: a=-x*.009+y*.012;b=-x*.016-y*.006;break;
                case Material.Leaves: a=-x*.013+y*.021;b=x*.026+y*.035;break;
                case Material.Curtain: a=y*.035-x*.018;b=y*.053+x*.033;break;
                case Material.Fire: a=y*.12+x*.1;b=-x*.22+y*.08;break;
                case Material.Glow: a=.4;b=1;break;
                case Material.Sea: a=y*.105-x*.024;b=y*.065+x*.048;break;
                case Material.Flag: a=-x*.093+y*.025;b=-x*.053;break;
                case Material.Boat: a=0;b=1.1;break;
                default: a=-y*.16;b=-y*.09+x*.05;break;
            }
            Node n=new Node{Kind=kind,SinA=(float)Math.Sin(a)*weight,CosA=(float)Math.Cos(a)*weight,
                SinB=(float)Math.Sin(b)*weight,CosB=(float)Math.Cos(b)*weight};
            if(kind==Material.Boat)n.Depth=(x-1170)/90f;
            if(kind==Material.Sea)n.Depth=.40f+.70f*y/720;
            if(kind==Material.Flag)n.Depth=Math.Max(0,Math.Min(1,(x-296)/44f));
            return n;
        }

        void MoveNodes(double t)
        {
            for(int i=1;i<clocks.Length;i++){
                double a=t*Speed[i],b=a*1.67+.8;
                clocks[i]=new Wave{SinA=(float)Math.Sin(a),CosA=(float)Math.Cos(a),
                    SinB=(float)Math.Sin(b),CosB=(float)Math.Cos(b)};
            }
            foreach(int i in movingNodes){
                Node n=nodes[i];Wave w=clocks[(int)n.Kind];
                float a=w.SinA*n.CosA+w.CosA*n.SinA;
                float b=w.SinB*n.CosB+w.CosB*n.SinB;
                float x=0,y=0,light=0;
                switch(n.Kind){
                    case Material.Cloud: x=13*a+3*b;y=.9f*b;break;
                    case Material.Leaves: x=3.8f*a+1.3f*b;y=1.7f*a-.65f*b;break;
                    case Material.Lawn: light=12*a+4*b;break;
                    case Material.Curtain: x=3.1f*a+1.1f*b;y=1.3f*b;break;
                    case Material.Fire: x=1.6f*a+b;y=3.4f*a+1.7f*b;light=38*a+14*b;break;
                    case Material.Glow: light=11*a+5*b;break;
                    case Material.Sea: x=(3.6f*a+1.45f*b)*n.Depth;y=(1.85f*b+.7f*a)*n.Depth;light=7*a+3*b;break;
                    case Material.Flag: x=1.65f*b*n.Depth;y=(4.2f*a+1.4f*b)*n.Depth;break;
                    case Material.Boat: x=1.35f*b;y=3.1f*a+1.65f*n.Depth*b;break;
                    case Material.Falls: x=.6f*b;y=2.8f*a+1.1f*b;break;
                }
                nodes[i].X=(int)(x*Unit);nodes[i].Y=(int)(y*Unit);nodes[i].Light=(int)(light*Unit);
            }
        }

        // Four-bit fractional positions keep the two-channel packed blends in integer range.
        static int Blend(int a,int b,int f)
        {
            int rb=(((a&0xff00ff)*(16-f)+(b&0xff00ff)*f)>>4)&0xff00ff;
            int green=(((a&0xff00)*(16-f)+(b&0xff00)*f)>>4)&0xff00;
            return rb|green;
        }
        static int Shade(int pixel,int gain)
        {
            int r=Math.Min(255,((pixel>>16)&255)*gain>>8);
            int g=Math.Min(255,((pixel>>8)&255)*gain>>8);
            int b=Math.Min(255,(pixel&255)*gain>>8);
            return r<<16|g<<8|b;
        }
        void RenderCell(Cell cell)
        {
            int i=cell.Node;
            Node a=nodes[i],b=nodes[i+1],c=nodes[i+columns],d=nodes[i+columns+1];
            for(int row=0;row<Step;row++){
                int lx=a.X+(c.X-a.X)*row/Step,rx=b.X+(d.X-b.X)*row/Step;
                int ly=a.Y+(c.Y-a.Y)*row/Step,ry=b.Y+(d.Y-b.Y)*row/Step;
                int light=a.Light+(c.Light-a.Light)*row/Step,endLight=b.Light+(d.Light-b.Light)*row/Step;
                int dl=(endLight-light)/Step;
                int start=cell.Pixel+row*width;
                int sx=cell.X*Unit+lx,sy=(cell.Y+row)*Unit+ly;
                int dx=Unit+(rx-lx)/Step,dy=(ry-ly)/Step;
                for(int j=0;j<Step;j++,sx+=dx,sy+=dy,light+=dl){
                    int color;
                    if(cell.Warp){
                        int u=Math.Max(0,Math.Min((width-1)*Unit,sx));
                        int v=Math.Max(0,Math.Min((height-1)*Unit,sy));
                        int x=u>>8,y=v>>8,p=y*width+x;
                        int q=x<width-1?p+1:p,down=y<height-1?width:0;
                        int fx=(u>>4)&15,fy=(v>>4)&15;
                        color=Blend(Blend(original[p],original[q],fx),Blend(original[p+down],original[q+down],fx),fy);
                    }else color=original[start+j];
                    if(cell.Shade)color=Shade(color,256+(light>>8));
                    pixels[start+j]=unchecked((int)0xff000000)|color;
                }
            }
        }
        internal void Update(double time)
        {
            if(time>=lastFrame&&time-lastFrame<1.0/30)return;
            MoveNodes(time);
            foreach(Cell cell in cells)RenderCell(cell);
            Rectangle all=new Rectangle(0,0,width,height);
            BitmapData bits=map.LockBits(all,ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
            try { Marshal.Copy(pixels,0,bits.Scan0,pixels.Length); }
            finally { map.UnlockBits(bits); }
            lastFrame=time;
        }
        // RenderCell samples source = screen + displacement. Invert that map for the
        // butterfly's feet so they stay on the same painted petal when it sways.
        internal PointF PaintedPosition(PointF petal)
        {
            PointF screen=petal;
            for(int j=0;j<3;j++){
                int x=(int)screen.X/Step,y=(int)screen.Y/Step,i=y*columns+x;
                float u=screen.X/Step-x,v=screen.Y/Step-y;
                Node a=nodes[i],b=nodes[i+1],c=nodes[i+columns],d=nodes[i+columns+1];
                float dx=(a.X*(1-u)+b.X*u)*(1-v)+(c.X*(1-u)+d.X*u)*v;
                float dy=(a.Y*(1-u)+b.Y*u)*(1-v)+(c.Y*(1-u)+d.Y*u)*v;
                screen=new PointF(petal.X-dx/Unit,petal.Y-dy/Unit);
            }
            return screen;
        }
    }

    internal sealed class PaintedPart : IDisposable
    {
        readonly Bitmap sprite;
        internal PaintedPart(Bitmap background,string resource,int x,int y)
        {
            using(Stream source=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            using(Bitmap atlas=new Bitmap(source)){
                int w=atlas.Width/2,h=atlas.Height;
                using(Graphics g=Graphics.FromImage(background)){
                    g.DrawImage(atlas,new Rectangle(x,y,w,h),0,0,w,h,GraphicsUnit.Pixel);
                }
                sprite=atlas.Clone(new Rectangle(w,0,w,h),PixelFormat.Format32bppPArgb);
            }
        }
        internal void Draw(Graphics g,float x,float y,float angle,float scaleX,float scaleY,float pivotX,float pivotY)
        {
            GraphicsState old=g.Save();
            g.TranslateTransform(x,y);g.RotateTransform(angle);g.ScaleTransform(scaleX,scaleY);
            g.DrawImage(sprite,new RectangleF(-pivotX,-pivotY,sprite.Width,sprite.Height),
                new RectangleF(0,0,sprite.Width,sprite.Height),GraphicsUnit.Pixel);
            g.Restore(old);
        }
        public void Dispose(){sprite.Dispose();}
    }
}
