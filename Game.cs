using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace MountainTide
{
    internal sealed partial class Game : Form
    {
        const int W=1280,H=720;
        readonly FarmState state;
        readonly SaveFile save;
        readonly GameArt art=new GameArt();
        readonly GamePointer palm=new GamePointer(false),pluck=new GamePointer(true);
        readonly GamePointer shovelPointer;
        readonly GamePointer[] seedPointers=new GamePointer[3];
        readonly Bitmap farm,port;
        readonly Timer timer=new Timer();
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();
        readonly List<Hit> hits=new List<Hit>();
        readonly Dictionary<int,SolidBrush> brushes=new Dictionary<int,SolidBrush>();
        readonly Color cream=Color.FromArgb(255,243,210),gold=Color.FromArgb(255,214,129),ink=Color.FromArgb(56,62,44);
        internal bool Sea {get;private set;}
        string panel="",tool="hand",message="",floatText="",hover="";
        int selectedSeed=0,activePlot=-1,activeSale=0,saleQuantity=1;
        double last,travelAt=-5,msgEnd,floatEnd;
        long lastSecond=-1;
        PointF player=new PointF(890,480),destination=new PointF(890,480),mouse=new PointF(-20,-20),floatPoint;
        float scale=1,offsetX,offsetY;
        const float WalkSpeed=328f;
        bool flip=true,facingAway,travelSwitched,fullscreen,saveFailed,walking;
        double stride,ringStart=-10,ringArrival=-10,pickTime=-10;
        string queuedAction="",heldCrop="";
        int queuedCrop=-1;
        long queuedPlanted;
        Rectangle windowed; FormWindowState previousState;
        struct Hit{public RectangleF Area;public string Action;public bool Enabled;}
        public Game(FarmState data,SaveFile file)
        {
            state=data;save=file;
            using(Bitmap cursorImage=new Bitmap(48,48)){
                using(Graphics g=Graphics.FromImage(cursorImage)){g.SmoothingMode=SmoothingMode.AntiAlias;DrawSpade(g,0,0,48);}
                shovelPointer=new GamePointer(cursorImage,6,43);
            }
            for(int i=0;i<seedPointers.Length;i++)
                using(Bitmap packet=new Bitmap(art.SeedPackets[i],new Size(43,43)))seedPointers[i]=new GamePointer(packet,6,36);
            farm=Map("farm.png");port=Map("port.png");
            Text="沧海桑田";DoubleBuffered=true;KeyPreview=true;BackColor=Color.FromArgb(28,42,37);
            Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Rectangle work=Screen.PrimaryScreen.WorkingArea;
            int width=Math.Min(1280,Math.Min(work.Width-42,(work.Height-76)*16/9));ClientSize=new Size(width,width*9/16);
            MinimumSize=new Size(740,450);StartPosition=FormStartPosition.CenterScreen;
            SetStyle(ControlStyles.ResizeRedraw|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint,true);
            if(state.Automate(state.Now)>0)Store();timer.Interval=40;timer.Tick+=Tick;timer.Start();
        }
        static Bitmap Map(string name)
        {
            using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using(Image im=Image.FromStream(s)){
                Bitmap b=new Bitmap(W,H,PixelFormat.Format32bppPArgb);
                using(Graphics g=Graphics.FromImage(b)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(im,0,0,W,H);}
                return b;
            }
        }
        double T{get{return clock.Elapsed.TotalSeconds;}}
        void Store(){try{save.Write(state);saveFailed=false;}catch(IOException){saveFailed=true;}catch(UnauthorizedAccessException){saveFailed=true;}}
        void Tick(object sender,EventArgs e)
        {
            double t=T,dt=Math.Min(.16,t-last);last=t;
            if(t-travelAt<.9 && t-travelAt>=.4 && !travelSwitched)SwitchWorld();
            if(t-travelAt>=.9&&!travelSwitched&&travelAt>=0)SwitchWorld();
            float dx=destination.X-player.X,dy=destination.Y-player.Y;
            double distance=Math.Sqrt(dx*dx+dy*dy);
            if(walking){
                double step=Math.Min(distance,dt*WalkSpeed);
                if(distance>.001){player.X+=(float)(dx*step/distance);player.Y+=(float)(dy*step/distance);stride+=step/124;}
                if(distance-step<.01){player=destination;walking=false;ringArrival=t;OnArrival();}
            }
            long now=state.Now;
            if(now!=lastSecond){lastSecond=now;int n=state.Automate(now);if(n>0){Store();Tell("无人机已收获 "+n+" 份作物");}}
            if(WindowState!=FormWindowState.Minimized){UpdatePointer();Invalidate();}
        }
        void SwitchWorld()
        {
            Sea=!Sea;travelSwitched=true;state.HasTraveled=true;Store();
            destination=player=Sea?new PointF(626,455):new PointF(890,480);tool="hand";hover="";mouse=new PointF(-99,-99);
            flip=!Sea;facingAway=false;
            Tell(Sea?"百年以后，昔日的山谷成了大海。":"潮声远去，你回到了自己的农场。");
        }
        internal void Travel(){if(T-travelAt<.9)return;StopWalking();CloseDrawer();panel="";hover="";floatEnd=0;msgEnd=0;travelAt=T;travelSwitched=false;}
        void StopWalking(){walking=false;queuedAction="";heldCrop="";Capture=false;destination=player;ringStart=-10;ringArrival=-10;}
        void WalkTo(PointF point,string action)
        {
            panel="";destination=point;queuedAction=action;walking=true;ringStart=T;ringArrival=-10;
            float dx=point.X-player.X,dy=point.Y-player.Y;
            if(Math.Abs(dx)>3)flip=dx<0;
            facingAway=dy< -Math.Abs(dx)*.3f;
            if(action.StartsWith("plot")){
                Plot p=state.Plots[int.Parse(action.Substring(4))];queuedCrop=p.CropId;queuedPlanted=p.Planted;
            }
        }
        void OnArrival()
        {
            string action=queuedAction;queuedAction="";
            if(action=="")return;
            if(action.StartsWith("plot")){
                int i=int.Parse(action.Substring(4));Plot p=state.Plots[i];
                if(p.CropId!=queuedCrop||p.Planted!=queuedPlanted){tool="hand";Tell("这格作物已变化，请重新选择。");return;}
                Tend(i);return;
            }
            panel=action;activePlot=-1;
        }
        void Tell(string s){message=s;msgEnd=T+3.4;}
        void Float(string s,PointF p){floatText=s;floatPoint=p;floatEnd=T+1.4;}
        Font F(int size,bool bold)
        {
            string k=size+(bold?"b":"n");Font f;if(!fonts.TryGetValue(k,out f)){f=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);fonts[k]=f;}return f;
        }
        SolidBrush B(Color c){SolidBrush b;if(!brushes.TryGetValue(c.ToArgb(),out b)){b=new SolidBrush(c);brushes[c.ToArgb()]=b;}return b;}
        static Color Col(string c){return ColorTranslator.FromHtml("#"+c);}
        static GraphicsPath Round(float x,float y,float w,float h,float radius)
        {
            float d=radius*2;GraphicsPath p=new GraphicsPath();p.AddArc(x,y,d,d,180,90);p.AddArc(x+w-d,y,d,d,270,90);p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();return p;
        }
        void Plate(Graphics g,float x,float y,float w,float h,Color bg,Color edge,float radius=7)
        {
            using(GraphicsPath p=Round(x,y,w,h,radius))using(Pen pen=new Pen(edge,1.5f)){g.FillPath(B(bg),p);g.DrawPath(pen,p);}
        }
        void TextAt(Graphics g,string s,int size,Color color,float x,float y,float w,float h,bool bold=false,bool center=false)
        {
            using(StringFormat fmt=new StringFormat()){
                fmt.Alignment=center?StringAlignment.Center:StringAlignment.Near;fmt.LineAlignment=StringAlignment.Center;fmt.FormatFlags=StringFormatFlags.NoWrap;
                g.DrawString(s,F(size,bold),B(color),new RectangleF(x,y,w,h),fmt);
            }
        }
        void HitBox(float x,float y,float w,float h,string action,bool enabled=true){hits.Add(new Hit{Area=new RectangleF(x,y,w,h),Action=action,Enabled=enabled});}
        void Sprite(Graphics g,Image bm,float x,float y,float w,float h)
        {
            g.InterpolationMode=InterpolationMode.Bilinear;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(bm,new RectangleF(x,y,w,h));g.PixelOffsetMode=PixelOffsetMode.Default;
        }
        // Each pair of staggered rows fits six wide diamonds and five offset ones.
        const int HalfBedWidth=68,HalfBedHeight=25;
        internal static PointF Bed(int i){
            int pair=i/11,slot=i%11;bool odd=slot>=6;
            return new PointF(214+(odd?HalfBedWidth:0)+(odd?slot-6:slot)*HalfBedWidth*2,
                              359+(pair*2+(odd?1:0))*HalfBedHeight);
        }
        PointF[] Corners(PointF p){return new[]{new PointF(p.X,p.Y-HalfBedHeight),new PointF(p.X+HalfBedWidth,p.Y),new PointF(p.X,p.Y+HalfBedHeight),new PointF(p.X-HalfBedWidth,p.Y)};}
        void DrawSoil(Graphics g,int i)
        {
            PointF p=Bed(i);
            if(state.Plots[i].Opened)Sprite(g,art.Soil,p.X-HalfBedWidth+2,p.Y-HalfBedHeight+1,HalfBedWidth*2-4,HalfBedHeight*2-2);
            bool aiming=drawer==""&&(tool=="seed"||tool=="shovel")&&hover=="plot"+i;
            if(panel!=""||!aiming&&(activePlot<0||drawer==""))return;
            PointF[] edge=Corners(p);bool selected=aiming||i==activePlot;
            if(selected)g.FillPolygon(B(Color.FromArgb(68,255,212,81)),edge);
            using(Pen pen=new Pen(selected?Col("ffdf63"):Color.FromArgb(107,66,95,47),selected?3.5f:1.1f))g.DrawPolygon(pen,edge);
        }
        int Stage(int i){long left=state.Left(i,state.Now);return left==0?2:left<Crop.All[state.Plots[i].CropId].Seconds*.7?1:0;}
        RectangleF PlantBounds(int i,int stage,int tuft){
            PointF p=Bed(i);Bitmap image=art.Plants[state.Plots[i].CropId,stage];
            float h=stage==2?50:stage==1?36:20;
            float w=Math.Min(55,h*image.Width/image.Height);
            float x=p.X+(tuft==0?-23:tuft==1?23:0)+(float)Math.Sin(T*1.8+i+tuft)*.8f;
            float y=p.Y+(tuft==2?13:-1);return new RectangleF(x-w/2,y-h,w,h);
        }
        void DrawPlants(Graphics g,int i){
            int id=state.Plots[i].CropId;if(id<0)return;int stage=Stage(i);
            for(int tuft=0;tuft<3;tuft++){RectangleF r=PlantBounds(i,stage,tuft);Sprite(g,art.Plants[id,stage],r.X,r.Y,r.Width,r.Height);}
            PointF p=Bed(i);if(stage==2)Spark(g,p.X+24,p.Y-47+(float)Math.Sin(T*2+i)*3,3.8f,gold);
        }
        void Spark(Graphics g,float x,float y,float r,Color color)
        {
            g.FillPolygon(B(color),new[]{new PointF(x,y-r*1.6f),new PointF(x+r*.32f,y-r*.25f),new PointF(x+r,y),new PointF(x+r*.32f,y+r*.25f),new PointF(x,y+r*1.6f),new PointF(x-r*.32f,y+r*.25f),new PointF(x-r,y),new PointF(x-r*.32f,y-r*.25f)});
        }
        void DrawFarmer(Graphics g){
            float x=player.X,y=player.Y;double beat=stride*Math.PI*2;
            float bob=walking?(float)Math.Abs(Math.Sin(beat))*3.7f:(float)Math.Sin(T*2.2)*.65f;
            int stepFrame=(int)(stride*4)%4;
            int pose=!walking?0:stepFrame==0?1:stepFrame==2?2:0;
            float lean=(float)Math.Sin(Math.PI*Math.Min(1,Math.Max(0,(T-pickTime)/.38)));
            Bitmap frame=art.FarmerFrames[facingAway?1:0,pose];float height=106-lean*8,width=height*frame.Width/frame.Height;
            g.FillEllipse(B(Color.FromArgb(68,74,61,29)),x-22,y-6,44,12);
            GraphicsState sc=g.Save();g.TranslateTransform(x,y-bob);if(flip)g.ScaleTransform(-1,1);
            if(walking)g.RotateTransform((float)Math.Sin(beat)*2);else if(lean>0)g.RotateTransform(-lean*8);
            Sprite(g,frame,-width/2,-height,width,height);g.Restore(sc);
        }
        void DrawMarker(Graphics g){
            double t=T-ringStart,fade=walking?1:1-(T-ringArrival)/.38;
            if(ringStart<0||fade<=0)return;
            for(int i=0;i<2;i++){double phase=(t*1.3+i*.5)%1;float r=(float)(9+20*phase);
                using(Pen rim=new Pen(Color.FromArgb((int)(165*fade*(1-phase)),133,80,13),4))
                using(Pen light=new Pen(Color.FromArgb((int)(255*fade*(1-phase)),255,219,92),2.5f)){
                    g.DrawEllipse(rim,destination.X-r,destination.Y-r*.45f,r*2,r*.9f);g.DrawEllipse(light,destination.X-r,destination.Y-r*.45f,r*2,r*.9f);
                }}
        }
        void Sign(Graphics g,float cx,float cy,string name,string action)
        {
            float w=name.Length*18+37,h=34;bool over=hover==action&&panel=="";
            Plate(g,cx-w/2+2,cy+3,w,h,Color.FromArgb(115,30,32,23),Color.FromArgb(50,30,32,23),6);
            g.FillPolygon(B(Col("745634")),new[]{new PointF(cx-6,cy+h-1),new PointF(cx+6,cy+h-1),new PointF(cx,cy+h+6)});
            Plate(g,cx-w/2,cy,w,h,over?Col("ffedba"):Col("f4d795"),Col("6f5338"),5);
            g.FillEllipse(B(Col("997143")),cx-w/2+8,cy+15,4,4);g.FillEllipse(B(Col("997143")),cx+w/2-12,cy+15,4,4);
            TextAt(g,name,16,Col("493e2c"),cx-w/2,cy,w,h,true,true);HitBox(cx-w/2,cy,w,h+6,action);
        }
        void DrawWorld(Graphics g)
        {
            g.InterpolationMode=InterpolationMode.Bilinear;g.DrawImage(Sea?port:farm,0,0,W,H);
            if(!Sea){
                for(int i=0;i<state.Plots.Length;i++)DrawSoil(g,i);
                DrawMarker(g);bool farmerDrawn=false;
                for(int i=0;i<state.Plots.Length;i++){
                    if(!farmerDrawn&&player.Y<Bed(i).Y+15){DrawFarmer(g);farmerDrawn=true;}DrawPlants(g,i);
                }
                if(!farmerDrawn)DrawFarmer(g);
                if(state.MachinesOwned[0])Sprite(g,art.Tools[7],844,378,48,48);
                if(state.MachinesOwned[1])Sprite(g,art.Tools[2],827,412,36,36);
                if(state.MachinesOwned[2]){float x=565+(float)Math.Sin(T*.42)*145,y=351+(float)Math.Cos(T*.6)*32;Sprite(g,art.Tools[7],x,y,38,38);using(Pen pen=new Pen(cream,2)){g.DrawLine(pen,x-4,y+10,x+12,y+10);g.DrawLine(pen,x+24,y+10,x+42,y+10);}}
                HitBox(99,64,304,239,"home");HitBox(489,78,275,227,"supply");HitBox(869,62,384,245,"grain");
                Sign(g,242,249,"我的农舍","home");Sign(g,637,245,"青芽种子铺","supply");Sign(g,1041,249,"山村粮站","grain");
            }else{
                DrawMarker(g);DrawFarmer(g);HitBox(98,90,420,224,"harbor");HitBox(852,57,402,254,"tech");
                Sign(g,307,248,"码头收购摊","harbor");Sign(g,1046,239,"科技工坊","tech");
                for(int i=0;i<12;i++){
                    float x=893+(i*79)%370,y=558+(i*53)%145,sz=(float)Math.Sin(T*2+i*1.7);
                    if(sz>.1)Spark(g,x,y,sz*3,Color.FromArgb(204,239,255,224));
                }
            }
            if(floatEnd>T){double elapsed=1.4-(floatEnd-T);float y=floatPoint.Y-52-(float)elapsed*21;
                Plate(g,floatPoint.X-81,y,162,32,Color.FromArgb(222,43,65,43),gold);
                TextAt(g,floatText,15,gold,floatPoint.X-81,y,162,32,true,true);}
        }
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;g.Clear(BackColor);
            scale=Math.Min(ClientSize.Width/(float)W,ClientSize.Height/(float)H);
            offsetX=(ClientSize.Width-W*scale)/2;offsetY=(ClientSize.Height-H*scale)/2;
            g.TranslateTransform(offsetX,offsetY);g.ScaleTransform(scale,scale);g.SetClip(new Rectangle(0,0,W,H));
            g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            hits.Clear();DrawWorld(g);DrawHud(g);
            if(panel!=""){hits.Clear();DrawPanel(g);}
            if(T-travelAt<.9){double p=(T-travelAt)/.9;int a=(int)(250*(1-Math.Min(1,Math.Abs(p-.46)/.54)));
                using(Brush b=new SolidBrush(Color.FromArgb(Math.Max(0,a),247,232,179)))g.FillRectangle(b,0,0,W,H);
                using(Pen pen=new Pen(Color.FromArgb(Math.Max(0,a),255,250,214),4))g.DrawEllipse(pen,640-(float)p*760,360-(float)p*760,(float)p*1520,(float)p*1520);}
        }
        protected override void Dispose(bool disposing){if(disposing){timer.Stop();timer.Dispose();Cursor=Cursors.Default;palm.Dispose();pluck.Dispose();shovelPointer.Dispose();foreach(GamePointer p in seedPointers)p.Dispose();farm.Dispose();port.Dispose();art.Dispose();foreach(Font f in fonts.Values)f.Dispose();foreach(Brush b in brushes.Values)b.Dispose();}base.Dispose(disposing);}
        protected override void OnFormClosing(FormClosingEventArgs e){Store();base.OnFormClosing(e);}
        string FindAction(PointF p)
        {
            for(int i=hits.Count-1;i>=0;i--)if(hits[i].Area.Contains(p))return hits[i].Enabled?hits[i].Action:"";
            if(panel==""&&!Sea){
                int ground=-1;
                for(int i=0;i<state.Plots.Length;i++){PointF b=Bed(i);if(Math.Abs(p.X-b.X)/HalfBedWidth+Math.Abs(p.Y-b.Y)/HalfBedHeight<=1){ground=i;break;}}
                if(drawer==""&&(tool=="seed"||tool=="shovel"))return ground>=0?"plot"+ground:"";
                if(ground>=0&&drawer!="")return "plot"+ground;
                // Match the frontmost painted leaf, then check the ground underneath it.
                for(int i=state.Plots.Length-1;i>=0;i--){
                    int id=state.Plots[i].CropId;if(id<0)continue;
                    int stage=Stage(i);Bitmap img=art.Plants[id,stage];
                    for(int k=2;k>=0;k--){RectangleF b=PlantBounds(i,stage,k);if(!b.Contains(p))continue;
                        int px=Math.Min(img.Width-1,(int)((p.X-b.X)*img.Width/b.Width)),py=Math.Min(img.Height-1,(int)((p.Y-b.Y)*img.Height/b.Height));
                        if(img.GetPixel(px,py).A>60)return "plot"+i;}
                }
                if(ground>=0)return "plot"+ground;
            }
            return "";
        }
        PointF WorldPoint(MouseEventArgs e){return new PointF((e.X-offsetX)/scale,(e.Y-offsetY)/scale);}
        bool Ripe(string action){if(!action.StartsWith("plot")||Sea||panel!=""||drawer!=""||tool!="hand")return false;int i=int.Parse(action.Substring(4));return state.Plots[i].CropId>=0&&state.Left(i,state.Now)==0;}
        void UpdatePointer(){
            hover=mouse.X<0||T-travelAt<.9?"":FindAction(mouse);
            Cursor desired=heldCrop!=""&&hover==heldCrop?pluck.Value:Ripe(hover)?palm.Value:hover==""?Cursors.Default:Cursors.Hand;
            if(!Sea&&panel==""&&drawer==""&&T-travelAt>=.9){
                if(tool=="shovel")desired=shovelPointer.Value;
                else if(tool=="seed")desired=seedPointers[selectedSeed].Value;
            }
            if(Cursor!=desired)Cursor=desired;
        }
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);mouse=WorldPoint(e);UpdatePointer();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);mouse=new PointF(-500,-500);hover="";Cursor=Cursors.Default;}
        protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)heldCrop="";}
        protected override void OnMouseUp(MouseEventArgs e){
            base.OnMouseUp(e);if(e.Button!=MouseButtons.Left||heldCrop=="")return;
            string target=heldCrop;heldCrop="";Capture=false;mouse=WorldPoint(e);
            if(FindAction(mouse)==target&&Ripe(target))Action(target);UpdatePointer();Refresh();
        }
        protected override void OnMouseDown(MouseEventArgs e){
            base.OnMouseDown(e);if(T-travelAt<.9)return;
            if(e.Button==MouseButtons.Right){StopWalking();CloseDrawer();panel="";tool="hand";Refresh();return;}
            if(e.Button!=MouseButtons.Left)return;mouse=WorldPoint(e);string a=FindAction(mouse);
            if(Ripe(a)){StopWalking();heldCrop=a;Capture=true;UpdatePointer();}
            else if(a!="")Action(a);
            else if(panel==""&&Walkable(mouse)){CloseDrawer();WalkTo(mouse,"");}
            else if(panel==""){CloseDrawer();}
            Refresh();
        }
        bool Walkable(PointF p){
            PointF[] path=Sea?new[]{new PointF(227,354),new PointF(566,306),new PointF(932,321),new PointF(1075,373),new PointF(1180,487),new PointF(945,535),new PointF(467,563),new PointF(81,502)}:
                new[]{new PointF(126,342),new PointF(320,305),new PointF(607,302),new PointF(1030,319),new PointF(1143,388),new PointF(1149,548),new PointF(866,649),new PointF(349,657),new PointF(117,568)};
            using(GraphicsPath gp=new GraphicsPath()){gp.AddPolygon(path);return gp.IsVisible(p);}
        }
        protected override void OnKeyDown(KeyEventArgs e){
            base.OnKeyDown(e);if(e.KeyCode==Keys.F11){ToggleFullScreen();return;}
            if(e.KeyCode==Keys.Escape){StopWalking();if(panel!="")panel="";else if(drawer!="")CloseDrawer();else if(fullscreen)ToggleFullScreen();else tool="hand";Refresh();return;}
            if(panel!=""||T-travelAt<.9)return;
            switch(e.KeyCode){
                case Keys.D1:case Keys.NumPad1:Action("hand");break;
                case Keys.D2:case Keys.NumPad2:if(!Sea)Action("seeds");break;
                case Keys.D3:case Keys.NumPad3:if(!Sea)Action("feedtool");break;
                case Keys.B:case Keys.D4:Action("bag");break;
                case Keys.T:case Keys.Space:Travel();break;
                case Keys.F1:Action("help");break;
            }Refresh();
        }
        void ToggleFullScreen(){
            if(!fullscreen){windowed=Bounds;previousState=WindowState;WindowState=FormWindowState.Normal;FormBorderStyle=FormBorderStyle.None;Bounds=Screen.FromControl(this).Bounds;fullscreen=true;}
            else{FormBorderStyle=FormBorderStyle.Sizable;Bounds=windowed;WindowState=previousState;fullscreen=false;}
        }
    }
}
