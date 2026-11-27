using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MountainTide
{
    internal sealed partial class Game
    {
        // The wrists are in world pixels relative to the feet, with +X facing the crop.
        // The last column is the angle of the can spout or the catching net.
        struct CarePoseKey
        {
            internal readonly float Time, Lean, Height, FootY, ToolAngle;
            internal readonly PointF Near, Far;
            internal CarePoseKey(float t,float lean,float height,float footY,float nx,float ny,float fx,float fy,float angle)
            {
                Time=t;Lean=lean;Height=height;FootY=footY;
                Near=new PointF(nx,ny);Far=new PointF(fx,fy);ToolAngle=angle;
            }
        }
        static readonly CarePoseKey IdleCarePose=new CarePoseKey(0,0,1,0,-9,-31,18,-31,0);
        static readonly CarePoseKey[] PourPoses={
            IdleCarePose,
            new CarePoseKey(.13f,-3,.97f,0,0,-35,14,-37,-18),
            new CarePoseKey(.29f,3,.98f,0,12,-48,23,-41,12),
            new CarePoseKey(.44f,7,.94f,0,15,-50,26,-42,42),
            new CarePoseKey(.66f,9,.92f,0,18,-48,27,-40,48),
            new CarePoseKey(.77f,6,.95f,0,15,-48,25,-41,34),
            new CarePoseKey(.88f,2,.98f,0,5,-40,20,-35,-5),
            new CarePoseKey(1,0,1,0,-9,-31,18,-31,0)
        };
        static readonly CarePoseKey[] CatchPoses={
            IdleCarePose,
            new CarePoseKey(.17f,-6,.96f,0,-3,-46,19,-39,-24),
            new CarePoseKey(.31f,-7,.94f,0,7,-62,24,-44,-40),
            new CarePoseKey(.44f,6,.92f,-2,17,-59,30,-43,-16),
            new CarePoseKey(.58f,14,.81f,0,21,-38,34,-32,18),
            new CarePoseKey(.66f,12,.83f,0,21,-37,33,-32,17),
            new CarePoseKey(.79f,5,.96f,-1,15,-49,25,-41,-32),
            new CarePoseKey(.88f,1,.98f,0,12,-51,23,-42,-45),
            new CarePoseKey(1,0,1,0,-9,-31,18,-31,-40)
        };
        static readonly CarePoseKey[] ShooPoses={
            IdleCarePose,
            new CarePoseKey(.14f,-3,.88f,1,-15,-33,20,-37,0),
            new CarePoseKey(.29f,4,1.01f,-4,2,-75,34,-61,0),
            new CarePoseKey(.41f,7,.95f,0,16,-60,40,-71,0),
            new CarePoseKey(.54f,-3,.98f,-3,-18,-72,24,-68,0),
            new CarePoseKey(.66f,6,1.02f,-5,10,-78,42,-66,0),
            new CarePoseKey(.76f,9,.91f,0,20,-56,36,-49,0),
            new CarePoseKey(.86f,3,.97f,0,0,-55,27,-47,0),
            new CarePoseKey(1,0,1,0,-9,-31,18,-31,0)
        };
        static float CareStep(double value,double from,double to)
        {
            float t=(float)Math.Min(1,Math.Max(0,(value-from)/(to-from)));
            return t*t*(3-2*t);
        }
        static float CareSpline(float previous,float a,float b,float next,float t)
        {
            return a+.5f*t*((b-previous)+t*((2*previous-5*a+4*b-next)+t*(3*(a-b)+next-previous)));
        }
        static CarePoseKey SampleCarePose(CarePoseKey[] keys,float u)
        {
            int i=0;while(i<keys.Length-2 && u>keys[i+1].Time)i++;
            CarePoseKey a=keys[i],b=keys[i+1],p=keys[Math.Max(0,i-1)],c=keys[Math.Min(keys.Length-1,i+2)];
            float t=Math.Max(0,Math.Min(1,(u-a.Time)/(b.Time-a.Time)));
            return new CarePoseKey(u,CareSpline(p.Lean,a.Lean,b.Lean,c.Lean,t),
                CareSpline(p.Height,a.Height,b.Height,c.Height,t),CareSpline(p.FootY,a.FootY,b.FootY,c.FootY,t),
                CareSpline(p.Near.X,a.Near.X,b.Near.X,c.Near.X,t),CareSpline(p.Near.Y,a.Near.Y,b.Near.Y,c.Near.Y,t),
                CareSpline(p.Far.X,a.Far.X,b.Far.X,c.Far.X,t),CareSpline(p.Far.Y,a.Far.Y,b.Far.Y,c.Far.Y,t),
                CareSpline(p.ToolAngle,a.ToolAngle,b.ToolAngle,c.ToolAngle,t));
        }
        static PointF CareLerp(PointF a,PointF b,float t){return new PointF(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);}
        static PointF CareJoint(CarePoseKey pose,float x,float y)
        {
            double angle=pose.Lean*Math.PI/180;
            float cosine=(float)Math.Cos(angle),sine=(float)Math.Sin(angle);
            return new PointF(x*cosine-y*pose.Height*sine,pose.FootY+x*sine+y*pose.Height*cosine);
        }
        static PointF ToolPoint(PointF grip,float angle,float length,float x,float y)
        {
            double r=angle*Math.PI/180;
            return new PointF(grip.X+length*(float)(x*Math.Cos(r)-y*Math.Sin(r)),grip.Y+length*(float)(x*Math.Sin(r)+y*Math.Cos(r)));
        }
        static void CareLine(Graphics g,Color colour,float width,PointF a,PointF b)
        {
            using(Pen pen=new Pen(colour,width)){
                pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,a,b);
            }
        }
        static readonly PointF[] NearArmCut={new PointF(51,85),new PointF(57,86),new PointF(62,94),
            new PointF(62,107),new PointF(58,113),new PointF(54,117),new PointF(54,121),new PointF(57,130),
            new PointF(51,137),new PointF(42,138),new PointF(32,137),new PointF(32,107),new PointF(38,94)};
        static readonly PointF[] FarArmCut={new PointF(86,99),new PointF(93,98),new PointF(102,108),
            new PointF(102,137),new PointF(88,137),new PointF(87,131),new PointF(87,112)};
        void DrawCareTorso(Graphics g,CarePoseKey pose)
        {
            GraphicsState body=g.Save();
            try{
                g.TranslateTransform(0,pose.FootY);g.RotateTransform(pose.Lean);g.ScaleTransform(1,pose.Height);
                Bitmap frame=art.FarmerFrames[0,0];
                float scale=106f/frame.Height;
                g.TranslateTransform(-frame.Width*scale/2,-106);g.ScaleTransform(scale,scale);
                // The front sprite already contains its idle arms. Keep the original hat,
                // face, scarf and boots intact and expose the overalls behind the lifted sleeve.
                PointF[] exposed={new PointF(52,92),new PointF(65,96),new PointF(63,128),new PointF(47,142),
                    new PointF(42,141),new PointF(45,126),new PointF(48,114)};
                g.FillPolygon(B(Col("283a37")),exposed);
                g.FillPolygon(B(Col("497075")),new[]{new PointF(55,100),new PointF(63,101),
                    new PointF(58,130),new PointF(45,138),new PointF(49,119)});
                using(GraphicsPath cut=new GraphicsPath()){
                    cut.AddPolygon(NearArmCut);cut.AddPolygon(FarArmCut);g.SetClip(cut,CombineMode.Exclude);
                    Sprite(g,frame,0,0,frame.Width,frame.Height);
                }
            }finally{g.Restore(body);}
        }
        static readonly PointF[] WorkSleeve={new PointF(52,86),new PointF(57,87),new PointF(61,93),
            new PointF(61,104),new PointF(58,110),new PointF(56,115),new PointF(52,118),
            new PointF(47,116),new PointF(40,115),new PointF(36,113),new PointF(37,106),
            new PointF(42,101),new PointF(44,92)};
        static readonly PointF[] WorkHand={new PointF(40,113),new PointF(49,113),new PointF(54,116),
            new PointF(53,121),new PointF(56,126),new PointF(56,131),new PointF(51,136),
            new PointF(47,138),new PointF(40,136),new PointF(37,132),new PointF(38,124)};
        void DrawCareLimbPiece(Graphics g,PointF from,PointF to,PointF srcFrom,PointF srcTo,PointF[] mask,float thickness)
        {
            float dx=to.X-from.X,dy=to.Y-from.Y,sx=srcTo.X-srcFrom.X,sy=srcTo.Y-srcFrom.Y;
            float stretch=(float)Math.Sqrt((dx*dx+dy*dy)/(sx*sx+sy*sy));
            GraphicsState saved=g.Save();
            try{
                g.TranslateTransform(from.X,from.Y);g.RotateTransform((float)(Math.Atan2(dy,dx)*180/Math.PI));
                g.ScaleTransform(Math.Max(.04f,stretch),thickness);
                g.RotateTransform((float)(-Math.Atan2(sy,sx)*180/Math.PI));g.TranslateTransform(-srcFrom.X,-srcFrom.Y);
                using(GraphicsPath clip=new GraphicsPath()){
                    clip.AddPolygon(mask);g.SetClip(clip,CombineMode.Intersect);
                    Bitmap frame=art.FarmerFrames[0,0];Sprite(g,frame,0,0,frame.Width,frame.Height);
                }
            }finally{g.Restore(saved);}
        }
        void DrawCareArm(Graphics g,PointF shoulder,PointF hand,bool near,bool waving)
        {
            float dx=hand.X-shoulder.X,dy=hand.Y-shoulder.Y;
            float dist=(float)Math.Sqrt(dx*dx+dy*dy),inv=1/Math.Max(.01f,dist);
            float bend=(near?.65f:-.55f)*(float)Math.Sqrt(Math.Max(0,13.4f*13.4f-dist*dist*.25f));
            PointF elbow=new PointF((shoulder.X+hand.X)/2-dy*inv*bend,(shoulder.Y+hand.Y)/2+dx*inv*bend);
            float width=near?.58f:.48f;
            // Articulate the painted sleeve, rolled cuff and fist from the character's
            // own front frame so shading and ink still match when either arm rotates.
            DrawCareLimbPiece(g,elbow,hand,new PointF(46,114),new PointF(46,130),WorkHand,width);
            DrawCareLimbPiece(g,shoulder,elbow,new PointF(53,89),new PointF(46,114),WorkSleeve,width);
            if(waving){
                for(int finger=-1;finger<=1;finger++){
                    PointF basePoint=new PointF(hand.X+finger*1.3f,hand.Y-1);
                    PointF tip=new PointF(hand.X+finger*2.2f,hand.Y-4.4f+Math.Abs(finger));
                    CareLine(g,Col("704a33"),2.05f,basePoint,tip);
                    CareLine(g,Col("edb080"),1.3f,basePoint,tip);
                }
            }
        }
        bool DrawCareFarmer(Graphics g)
        {
            if(!InField || manualCarePlot<0 || careWorkTime<0)return false;
            float u=(float)Math.Max(0,Math.Min(1,careWorkTime/CareWorkDuration));
            bool water=manualCareKind==CareKind.Water,pest=manualCareKind==CareKind.Pest;
            CarePoseKey pose=SampleCarePose(water?PourPoses:pest?CatchPoses:ShooPoses,u);
            float dir=flip?-1:1;
            PointF bed=Bed(manualCarePlot);
            PointF target=new PointF((bed.X-player.X)*dir,bed.Y-player.Y);
            float size=CareStep(u,0,.13)*(1-CareStep(u,.88,1));
            GraphicsState actor=g.Save();
            try{
                g.TranslateTransform(player.X,player.Y);g.ScaleTransform(dir,1);
                PointF farShoulder=CareJoint(pose,16,-47),nearShoulder=CareJoint(pose,-4,-50);
                DrawCareArm(g,farShoulder,pose.Far,false,!water&&!pest);
                DrawCareTorso(g,pose);
                if(water)DrawPouringCan(g,pose,u,size,target);
                if(pest)DrawCatchingNet(g,pose,u,size,target);
                DrawCareArm(g,nearShoulder,pose.Near,true,!water&&!pest);
                if(!water&&!pest)DrawShooTraces(g,u,pose);
            }finally {g.Restore(actor);}
            return true;
        }
        void DrawPouringCan(Graphics g,CarePoseKey pose,float u,float size,PointF target)
        {
            if(size<=.005)return;
            GraphicsState can=g.Save();
            try{
                // The existing art's spout points left; the handle sits near (81%,33%).
                g.TranslateTransform(pose.Near.X,pose.Near.Y);g.RotateTransform(pose.ToolAngle);
                g.ScaleTransform(-size,size);
                Sprite(g,art.WateringCan,-29,-10.5f,36,32);
            }finally{g.Restore(can);}
            const float travel=.15f;
            for(int drop=0;drop<27;drop++){
                float emitted=.275f+drop*.019f,age=(u-emitted)/travel;
                if(age<0||age>1.65f)continue;
                CarePoseKey from=SampleCarePose(PourPoses,emitted);
                PointF start=ToolPoint(from.Near,from.ToolAngle,1,26,0);
                PointF land=new PointF(target.X+8+(float)Math.Sin(drop*2.35)*7,target.Y+5+(float)Math.Cos(drop*1.7)*2);
                if(age<=1){
                    PointF pos=CareLerp(start,land,age);
                    pos.X+=(float)Math.Sin(age*Math.PI)*5;pos.Y-=6*age*(1-age);
                    float strength=CareStep(age,0,.14)*(1-CareStep(age,.87,1));
                    if(strength<=.05)continue;
                    CareLine(g,Col("3d899b"),1.5f+strength,new PointF(pos.X,pos.Y-2),pos);
                    CareLine(g,Col("bceaf0"),strength,new PointF(pos.X-.3f,pos.Y-2),new PointF(pos.X-.3f,pos.Y));
                }else if(drop%3==0){
                    float t=(age-1)/.65f,r=1+5*t;
                    using(Pen ring=new Pen(Color.FromArgb((int)(130*(1-t)),174,222,219),1))
                        g.DrawEllipse(ring,land.X-r,land.Y-r*.34f,r*2,r*.68f);
                }
            }
        }
        void DrawCatchingNet(Graphics g,CarePoseKey pose,float u,float size,PointF target)
        {
            PointF hoop=ToolPoint(pose.Near,pose.ToolAngle,size,21,0);
            if(size>.02){
                CareLine(g,Col("624b30"),3.5f,pose.Near,hoop);CareLine(g,Col("bd9658"),1.6f,pose.Near,hoop);
                GraphicsState net=g.Save();
                try{
                    g.TranslateTransform(hoop.X,hoop.Y);g.RotateTransform(pose.ToolAngle+16);
                    g.ScaleTransform(size,size);
                    using(GraphicsPath bag=new GraphicsPath()){
                        bag.AddBezier(-2,-7,4,-9,17,-6,18,0);bag.AddBezier(18,0,17,6,8,9,-2,7);bag.CloseFigure();
                        g.FillPath(B(Color.FromArgb(155,210,225,191)),bag);
                        using(Pen hem=new Pen(Col("89967e"),1.1f))g.DrawPath(hem,bag);
                        GraphicsState mesh=g.Save();g.SetClip(bag,CombineMode.Intersect);
                        using(Pen stringPen=new Pen(Color.FromArgb(147,108,135,111),.78f)){
                            for(int line=-18;line<24;line+=4){
                                g.DrawLine(stringPen,line,-10,line+16,10);g.DrawLine(stringPen,line+16,-10,line,10);
                            }
                        }
                        g.Restore(mesh);
                    }
                    using(Pen rim=new Pen(Col("73572c"),2.5f))g.DrawEllipse(rim,-5.5f,-8.5f,8,17);
                    using(Pen rim=new Pen(Col("e9d29a"),1.25f))g.DrawEllipse(rim,-5.5f,-8.5f,8,17);
                }finally{g.Restore(net);}
            }
            PointF leaf=new PointF(target.X+8,target.Y-15);
            PointF inside=ToolPoint(hoop,pose.ToolAngle+16,size,11,1);
            float caught=CareStep(u,.47,.59);
            PointF bug=CareLerp(leaf,inside,caught);
            bug.X+=(1-caught)*(float)Math.Sin(u*18)*3;bug.Y-=(1-caught)*(float)Math.Abs(Math.Sin(u*15))*2;
            float bugSize=1-CareStep(u,.86,.97);
            if(bugSize>0){
                GraphicsState body=g.Save();g.TranslateTransform(bug.X,bug.Y);g.ScaleTransform(bugSize,bugSize);
                for(int i=-1;i<=1;i++){
                    CareLine(g,Col("48522e"),1,new PointF(i*2,-2),new PointF(i*2-1,-4));
                    CareLine(g,Col("48522e"),1,new PointF(i*2,2),new PointF(i*2+1,4));
                }
                g.FillEllipse(B(Col("46562e")),-4.6f,-3.1f,8.6f,6.2f);g.FillEllipse(B(Col("adcc56")),-3.9f,-2.6f,6,4.9f);
                g.FillEllipse(B(Col("43592d")),2,-2.3f,3.8f,4.5f);
                g.Restore(body);
            }
        }
        void DrawShooTraces(Graphics g,float u,CarePoseKey pose)
        {
            float wave=CareStep(u,.2,.29)*(1-CareStep(u,.72,.84));
            if(wave<.02f)return;
            PointF palm=pose.Near;
            using(Pen motion=new Pen(Color.FromArgb((int)(155*wave),250,232,168),1.55f)){
                motion.StartCap=motion.EndCap=LineCap.Round;
                g.DrawArc(motion,palm.X-9,palm.Y-9,16,20,187,69);
                g.DrawArc(motion,palm.X-13,palm.Y-12,22,25,198,53);
            }
        }
        void DrawCareWorkLabel(Graphics g,PointF bed,CareKind kind,double progress)
        {
            float transition=CareStep(progress,.055,.19)*(1-CareStep(progress,.87,.99));
            if(transition<.015f)return;
            float x=bed.X+(player.X<bed.X?9:-72),y=bed.Y+17;
            GraphicsState label=g.Save();
            g.TranslateTransform(x+31.5f,y+11);g.ScaleTransform(transition,transition);
            g.TranslateTransform(-x-31.5f,-y-11);
            Color edge=kind==CareKind.Water?Col("83c0c5"):Col("d1bf84");
            Plate(g,x,y,63,22,Color.FromArgb(230,50,65,46),edge,6);
            TextAt(g,kind==CareKind.Water?"浇水中":kind==CareKind.Pest?"捉虫中":"驱鸟中",12,cream,x,y-1,63,18,true,true);
            g.FillRectangle(B(Col("89937a")),x+7,y+17,49,2);
            g.FillRectangle(B(edge),x+7,y+17,49*(float)Math.Min(1,progress),2);
            g.Restore(label);
        }
        void DrawCareFlock(Graphics g,int plot,double progress,double clock)
        {
            PointF bed=Bed(plot);
            for(int k=0;k<2;k++){
                double phase=clock*2.4+plot*.71+k*Math.PI;
                float alert=CareStep(progress,.26,.56);
                float flight=CareStep(progress,.53+k*.06,.96+k*.025);
                float scale=(k==0?1.08f:.9f)*(1-CareStep(progress,.76+k*.03,.99));
                if(scale<.018f)continue;
                float outward=k==0?1:-1;
                float x=bed.X+(float)Math.Cos(phase)*(35-k*5)+outward*(alert*5+flight*(66+k*9));
                float y=bed.Y-24-k*4+(float)Math.Sin(phase)*5-alert*9-flight*(44+k*12);
                bool right=flight>.06f?k==0:Math.Sin(phase)<0;
                DrawFlyingBird(g,x,y,right,scale,(float)Math.Sin(phase*4+alert*3+flight*12));
            }
        }
    }
}
