using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace MountainTide
{
    internal sealed partial class Game
    {
        sealed class WindPoses : IDisposable
        {
            readonly Bitmap atlas;
            readonly int x,y,width,height;
            public WindPoses(string asset,int x,int y){
                this.x=x;this.y=y;
                using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(asset))
                using(Bitmap image=new Bitmap(stream))
                    atlas=image.Clone(new Rectangle(0,0,image.Width,image.Height),PixelFormat.Format32bppPArgb);
                width=atlas.Width/8;height=atlas.Height/5;
            }
            void Section(Graphics g,int top,int bottom,double force){
                int pose=(int)Math.Round(39*Math.Max(0,Math.Min(1,force)));
                if(pose==0||bottom<=top)return;
                g.DrawImage(atlas,new Rectangle(x,y+top,width,bottom-top),
                    pose%8*width,pose/8*height+top,width,bottom-top,GraphicsUnit.Pixel);
            }
            public void Draw(Graphics g,double force){Section(g,0,height,force);}
            public void Draw(Graphics g,int pinY,double upper,double lower){
                int split=pinY-y;
                Section(g,0,split,upper);Section(g,split,height,lower);
            }
            public void Dispose(){atlas.Dispose();}
        }

        static double Ease(double u){u=Math.Max(0,Math.Min(1,u));return u*u*(3-2*u);}
        static double Breeze(double t){
            // Pressure builds, lingers, then drains away. Both sides feel the same incoming gust.
            double p=t+1.20+.60*Math.Sin(t*.44)+.22*Math.Sin(t*.81);
            double crest=.5-.5*Math.Cos(p*.93);
            return .08+.88*Ease(crest)*(.87+.13*Math.Sin(t*.43+.8));
        }
        void DrawRoomBreeze(Graphics g,double t){
            windowFlowers.Draw(g,Breeze(t-.13));
            leftCurtain.Draw(g,244,Breeze(t),Breeze(t-.40));
            rightCurtain.Draw(g,239,Breeze(t-.16),Breeze(t-.67));
            double beat=(t+1.6)%8.9;
            double twitch=beat<1.1?Math.Pow(Math.Sin(Math.PI*beat/1.1),2)*(.65+.35*Math.Cos(beat*13)):0;
            shelfVine.Draw(g,148,twitch,Breeze(t-.68));
        }

        static Region ReadFlightWindow(){
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("flight-window.png"))
            using(Bitmap mask=new Bitmap(stream))
            using(GraphicsPath shape=new GraphicsPath(FillMode.Winding)){
                var spans=new List<Rectangle>();
                for(int y=0;y<mask.Height;y++){
                    int x=0;
                    while(x<mask.Width){
                        while(x<mask.Width&&mask.GetPixel(x,y).R<128)x++;
                        int start=x;
                        while(x<mask.Width&&mask.GetPixel(x,y).R>=128)x++;
                        if(x>start)spans.Add(new Rectangle(start,y,x-start,1));
                    }
                }
                shape.AddRectangles(spans.ToArray());return new Region(shape);
            }
        }
        const float BirdSpeed=113;
        void DrawWindowFlock(Graphics g,double t){
            GraphicsState saved=g.Save();
            using(GraphicsPath opening=new GraphicsPath()){
                opening.AddPolygon(new[]{new Point(110,139),new Point(329,156),new Point(332,179),
                    new Point(340,202),new Point(347,220),new Point(84,220),new Point(99,181)});
                g.SetClip(opening,CombineMode.Intersect);
            }
            g.TranslateTransform(-100,153);g.ScaleTransform(.62f,.62f);
            DrawFlock(g,t);g.Restore(saved);
        }
        void DrawFlock(Graphics g,double t){
            double timeline=t+2.6;
            int cycle=(int)(timeline/25);
            double pass=timeline%25;
            if(pass>15)return;
            bool right=(cycle%2)==0;
            int count=4+(cycle*7+1)%4;
            GraphicsState saved=g.Save();g.SetClip(flightWindow,CombineMode.Intersect);
            for(int i=0;i<count;i++){
                int row=(i+1)/2;int side=i%2==0?-1:1;
                double distance=pass*BirdSpeed-row*35-(i%2)*14;
                float x=(float)(right?distance-115:1395-distance);
                if(x<0||x>1280)continue;
                double rhythm=t*5.85+i*1.23;
                double flap=(.58+.42*Math.Cos(t*1.32+i*.37))*Math.Sin(rhythm);
                float y=(float)(54+Math.Sin(distance*.006+cycle*.8)*10+side*row*6+1.1*Math.Cos(rhythm));
                float size=.76f+.13f*(i%3);
                DrawFlyingBird(g,x,y,right,size,(float)flap);
            }
            g.Restore(saved);
        }
        void DrawFlyingBird(Graphics g,float x,float y,bool right,float size,float flap){
            GraphicsState prior=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(right?size:-size,size);
            // Two articulated wings share the shoulder; the head and tail hold a steady heading.
            float lift=flap*5;
            using(GraphicsPath wing=new GraphicsPath()){
                wing.StartFigure();wing.AddBezier(1,0,0,-3,-1,-5-lift,0,-6-lift);
                wing.AddBezier(0,-6-lift,3,-8-lift,4,-9-lift,6,-9-lift);
                wing.AddBezier(6,-9-lift,3,-4-lift,4,-1,2,1);wing.CloseFigure();
                g.FillPath(B(Color.FromArgb(169,181,181)),wing);
            }
            using(GraphicsPath bird=new GraphicsPath()){
                bird.AddBezier(-8,1,-5,1,-3,1,0,0);bird.AddBezier(0,0,2,0,4,-1,5,-2);
                bird.AddBezier(5,-2,7,-3,8,-1,8,0);bird.AddBezier(8,0,4,3,1,4,-3,3);
                bird.AddLine(-3,3,-8,4);bird.CloseFigure();g.FillPath(B(Color.FromArgb(115,117,113)),bird);
                g.TranslateTransform(0,-.6f);g.FillPath(B(Color.FromArgb(251,242,214)),bird);
            }
            using(GraphicsPath near=new GraphicsPath()){
                near.AddBezier(2,1,0,-1,-3,-3-lift,-5,-4-lift);
                near.AddBezier(-5,-4-lift,-7,-6-lift,-10,-6-lift*1.35f,-12,-7-lift*1.35f);
                near.AddBezier(-12,-7-lift*1.35f,-10,-3-lift,-7,-1-lift,-6,-1-lift);
                near.AddBezier(-6,-1-lift,-4,1,-1,3,2,1);near.CloseFigure();
                g.FillPath(B(Color.FromArgb(249,241,221)),near);
                using(Pen edge=new Pen(Color.FromArgb(166,110,109,103),.64f))g.DrawPath(edge,near);
            }
            g.FillPolygon(B(Color.FromArgb(169,114,43)),new[]{new PointF(7.5f,-1.3f),new PointF(10.6f,-.15f),new PointF(7.8f,.1f)});
            g.Restore(prior);
        }

        static readonly PointF[][] Blossoms={
            new[]{new PointF(46,593),new PointF(91,567),new PointF(120,610)},
            new[]{new PointF(1216,428),new PointF(1249,404),new PointF(1249,427)}
        };
        const double ButterflyFlight=5.4,ButterflyRest=3.8;
        PointF ButterflyPosition(int patch,double t,out bool perched,out float heading){
            // Each hop starts and ends exactly on a moving flower centre.
            double period=ButterflyFlight+ButterflyRest;
            double clock=t+(patch==0?2.1:6.7);
            int hop=(int)(clock/period);double age=clock%period;
            PointF[] flowers=Blossoms[patch];
            PointF a=farmMotion.PaintedPosition(flowers[hop%flowers.Length]);
            PointF b=farmMotion.PaintedPosition(flowers[(hop+1)%flowers.Length]);
            perched=age>=ButterflyFlight;
            heading=b.X>=a.X?1:-1;
            double u=perched?1:Ease(age/ButterflyFlight);
            double arc=Math.Sin(Math.PI*u);
            return new PointF((float)(a.X+(b.X-a.X)*u+2.2*arc*Math.Sin(2*Math.PI*u)),
                              (float)(a.Y+(b.Y-a.Y)*u-13*arc-2.2*arc*Math.Sin(4*Math.PI*u))-.8f);
        }
        void DrawButterflies(Graphics g,double t){
            for(int i=0;i<Blossoms.Length;i++){
                bool resting;float heading;
                PointF p=ButterflyPosition(i,t,out resting,out heading);
                float wing=resting?(float)(.23+.13*(1+Math.Cos(t*1.65+i))):
                    (float)(.28+.72*Math.Abs(Math.Sin(t*9.4+i)));
                GraphicsState save=g.Save();g.TranslateTransform(p.X,p.Y);
                g.ScaleTransform(heading,1);g.RotateTransform(resting?-23:-13);
                Color main=i==0?Color.FromArgb(109,176,202):Color.FromArgb(224,153,61);
                using(Pen rim=new Pen(Color.FromArgb(94,70,55),.56f)){
                    for(int side=-1;side<=1;side+=2){
                        using(GraphicsPath wings=new GraphicsPath()){
                            float span=side*(i==0?3.65f:3.15f)*wing;
                            wings.AddBezier(0,0,span*.52f,-2.3f,span,-3.7f,span,-2.1f);
                            wings.AddBezier(span,-2.1f,span,-.6f,span*.7f,-.15f,span*.57f,.1f);
                            wings.AddBezier(span*.57f,.1f,span*.84f,1.7f,span*.36f,2,0,.7f);
                            wings.CloseFigure();g.FillPath(B(main),wings);g.DrawPath(rim,wings);
                        }
                    }
                    g.DrawLine(rim,0,-1.55f,0,1.5f);
                }
                g.Restore(save);
            }
        }
    }
}
