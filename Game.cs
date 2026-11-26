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
        readonly Bitmap farm,port,house;
        readonly FarmEntrances entrances;
        readonly SceneryMotion farmMotion,houseMotion,harborMotion;
        readonly PaintedPart windmill;
        readonly Region flightWindow;
        readonly WindPoses windowFlowers,leftCurtain,rightCurtain,shelfVine;
        readonly Timer timer=new Timer();
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();
        readonly List<Hit> hits=new List<Hit>();
        readonly Dictionary<int,SolidBrush> brushes=new Dictionary<int,SolidBrush>();
        readonly Color cream=Color.FromArgb(255,243,210),gold=Color.FromArgb(255,214,129),ink=Color.FromArgb(56,62,44);
        enum Scene { Farm, House, Harbor }
        Scene scene=Scene.Farm;
        internal bool Sea {get{return scene==Scene.Harbor;}}
        bool InField {get{return scene==Scene.Farm;}}
        string panel="",tool="hand",message="",floatText="",hover="";
        int selectedSeed=0,activePlot=-1,activeSale=0,saleQuantity=1;
        double last,travelAt=-5,msgEnd,floatEnd;
        double paintedTime;
        long lastSecond=-1;
        static readonly PointF FarmStart=new PointF(1040,493);
        static readonly PointF HomeEntry=new PointF(303,236),SeedEntry=new PointF(542,280),GrainEntry=new PointF(865,287);
        static readonly PointF Threshold=new PointF(262,204);
        const double StepInsideAt=.78, EnterHouseAt=1.34, EntranceEnd=1.56;
        double doorAt=-1, thresholdStep;
        static readonly PointF InsideDoor=new PointF(880,402),BesideBed=new PointF(943,481);
        PointF player=FarmStart,destination=FarmStart,mouse=new PointF(-20,-20),floatPoint;
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
            farm=Map("farm.png");port=Map("port.png");house=Map("house.png");
            using(Stream view=Assembly.GetExecutingAssembly().GetManifestResourceStream("window-valley.png"))
            using(Bitmap clearWindow=new Bitmap(view))
            using(Graphics g=Graphics.FromImage(house))g.DrawImageUnscaled(clearWindow,181,188);
            entrances=new FarmEntrances(farm,house);
            DrawGrassPlots(farm);
            using(Stream sky=Assembly.GetExecutingAssembly().GetManifestResourceStream("clear-sky.png"))
            using(Bitmap cleared=new Bitmap(sky))
            using(Graphics g=Graphics.FromImage(farm))g.DrawImageUnscaled(cleared,0,0);
            flightWindow=ReadFlightWindow();
            windowFlowers=new WindPoses("window-flowers.png",63,219);
            leftCurtain=new WindPoses("curtain-left.png",9,89);
            rightCurtain=new WindPoses("curtain-right.png",322,121);
            shelfVine=new WindPoses("trailing-vine.png",392,94);
            windmill=new PaintedPart(port,"mill-blades.png",764,56);
            farmMotion=new SceneryMotion(farm,"farm-motion.png");
            houseMotion=new SceneryMotion(house,"house-motion.png");
            harborMotion=new SceneryMotion(port,"harbor-motion.png");
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
            if(doorAt>=0)AdvanceEntrance(t);
            long now=state.Now;
            if(now!=lastSecond){lastSecond=now;int n=state.Automate(now);if(n>0){Store();Tell("无人机已收获 "+n+" 份作物");}}
            if(WindowState!=FormWindowState.Minimized){UpdatePointer();Invalidate();}
        }
        void SwitchWorld()
        {
            scene=Sea?Scene.Farm:Scene.Harbor;travelSwitched=true;state.HasTraveled=true;Store();
            destination=player=Sea?new PointF(626,455):FarmStart;tool="hand";hover="";mouse=new PointF(-99,-99);
            flip=!Sea;facingAway=false;
            Tell(Sea?"百年以后，昔日的山谷成了大海。":"潮声远去，你回到了自己的农场。");
        }
        internal void Travel(){if(T-travelAt<.9)return;StopWalking();CloseDrawer();panel="";hover="";floatEnd=0;msgEnd=0;travelAt=T;travelSwitched=false;}
        void StopWalking(){walking=false;queuedAction="";heldCrop="";Capture=false;destination=player;ringStart=-10;ringArrival=-10;}
        void AdvanceEntrance(double now)
        {
            double elapsed=now-doorAt;
            if(InField && elapsed>=EnterHouseAt){
                scene=Scene.House;destination=player=InsideDoor;
                facingAway=false;flip=true;thresholdStep=0;hits.Clear();
            }else if(InField && elapsed>StepInsideAt){
                double step=Math.Min(1,(elapsed-StepInsideAt)/.48);
                stride+=(step-thresholdStep)*.64;thresholdStep=step;
                player.X=HomeEntry.X+(Threshold.X-HomeEntry.X)*(float)step;
                player.Y=HomeEntry.Y+(Threshold.Y-HomeEntry.Y)*(float)step;
            }
            if(elapsed>=EntranceEnd)doorAt=-1;
        }
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
            if(action=="home"||action=="leaveHouse"){
                StopWalking();CloseDrawer();tool="hand";panel="";msgEnd=0;floatEnd=0;hover="";
                hits.Clear();
                if(action=="home"){
                    doorAt=T;thresholdStep=0;facingAway=true;flip=true;
                }else{
                    scene=Scene.Farm;destination=player=HomeEntry;facingAway=false;flip=false;
                }
                return;
            }
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
        const int HalfBedWidth=74,HalfBedHeight=28;
        internal static PointF Bed(int i){
            int pair=i/11,slot=i%11;bool odd=slot>=6;
            return new PointF(140+(odd?HalfBedWidth:0)+(odd?slot-6:slot)*HalfBedWidth*2,
                              336+(pair*2+(odd?1:0))*HalfBedHeight);
        }
        static PointF[] Corners(PointF p){return new[]{new PointF(p.X,p.Y-HalfBedHeight),new PointF(p.X+HalfBedWidth,p.Y),new PointF(p.X,p.Y+HalfBedHeight),new PointF(p.X-HalfBedWidth,p.Y)};}
        static void DrawGrassPlots(Bitmap map)
        {
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("turf.png"))
            using(Bitmap seams=new Bitmap(stream))
            using(Graphics g=Graphics.FromImage(map)){
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode=PixelOffsetMode.Half;
                // Two short-grass samples per diagonal. Adjacent cells share one painted edge.
                int width=seams.Width/4;
                const int margin=4;
                var painted=new HashSet<Point>();
                for(int i=0;i<FarmState.CellCount;i++){
                    PointF[] corners=Corners(Bed(i));
                    for(int j=0;j<corners.Length;j++){
                        Point a=Point.Round(corners[j]),b=Point.Round(corners[(j+1)%corners.Length]);
                        Point middle=new Point((a.X+b.X)/2,(a.Y+b.Y)/2);
                        if(!painted.Add(middle))continue;
                        bool slopesDown=(a.X<b.X)==(a.Y<b.Y);
                        int sample=(slopesDown?0:2)+(i+j)%2;
                        Rectangle edge=new Rectangle(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y)-margin,
                            HalfBedWidth,HalfBedHeight+2*margin);
                        g.DrawImage(seams,edge,sample*width,0,width,seams.Height,GraphicsUnit.Pixel);
                    }
                }
            }
        }
        void DrawSoil(Graphics g,int i)
        {
            PointF p=Bed(i);
            if(state.Plots[i].Opened)Sprite(g,art.Soil,p.X-HalfBedWidth+2,p.Y-HalfBedHeight+1,HalfBedWidth*2-4,HalfBedHeight*2-2);
            bool aiming=drawer==""&&(tool=="seed"||tool=="shovel")&&hover=="plot"+i;
            if(panel!=""||!(aiming||(drawer!=""&&i==activePlot)))return;
            PointF[] edge=Corners(p);
            g.FillPolygon(B(Color.FromArgb(68,255,212,81)),edge);
            using(Pen pen=new Pen(Col("ffdf63"),3.5f))g.DrawPolygon(pen,edge);
        }
        int Stage(int i){long left=state.Left(i,state.Now);return left==0?2:left<Crop.All[state.Plots[i].CropId].Seconds*.7?1:0;}
        RectangleF PlantBounds(int i,int stage,int tuft){
            PointF p=Bed(i);Bitmap image=art.Plants[state.Plots[i].CropId,stage];
            float h=stage==2?50:stage==1?36:20;
            float w=Math.Min(55,h*image.Width/image.Height);
            float x=p.X+(tuft==0?-23:tuft==1?23:0);
            float y=p.Y+(tuft==2?13:-1);return new RectangleF(x-w/2,y-h,w,h);
        }
        float PlantBend(int i,int stage,int tuft){
            PointF p=Bed(i);double phase=paintedTime*1.23-p.X*.013+p.Y*.021+tuft*.23;
            return (float)((Math.Sin(phase)+.28*Math.Sin(phase*1.67+tuft))*(1+stage*1.15));
        }
        void DrawPlants(Graphics g,int i){
            int id=state.Plots[i].CropId;if(id<0)return;int stage=Stage(i);
            Bitmap image=art.Plants[id,stage];g.PixelOffsetMode=PixelOffsetMode.Half;
            for(int tuft=0;tuft<3;tuft++){
                RectangleF r=PlantBounds(i,stage,tuft);float bend=PlantBend(i,stage,tuft);
                g.DrawImage(image,new[]{new PointF(r.Left+bend,r.Top),new PointF(r.Right+bend,r.Top),new PointF(r.Left,r.Bottom)},
                    new RectangleF(0,0,image.Width,image.Height),GraphicsUnit.Pixel);
            }
            g.PixelOffsetMode=PixelOffsetMode.Default;
            PointF p=Bed(i);if(stage==2)Spark(g,p.X+24,p.Y-47+(float)Math.Sin(T*2+i)*3,3.8f,gold);
        }
        void Spark(Graphics g,float x,float y,float r,Color color)
        {
            g.FillPolygon(B(color),new[]{new PointF(x,y-r*1.6f),new PointF(x+r*.32f,y-r*.25f),new PointF(x+r,y),new PointF(x+r*.32f,y+r*.25f),new PointF(x,y+r*1.6f),new PointF(x-r*.32f,y+r*.25f),new PointF(x-r,y),new PointF(x-r*.32f,y-r*.25f)});
        }
        struct FarmerPose{
            public Bitmap Image;
            public float Width,Height,Y,Rotation;
        }
        FarmerPose GetFarmerPose(){
            float y=player.Y;double beat=stride*Math.PI*2;
            bool stepping=walking||thresholdStep>0&&thresholdStep<1;
            float bob=stepping?(float)Math.Abs(Math.Sin(beat))*3.7f:(float)Math.Sin(T*2.2)*.65f;
            int stepFrame=(int)(stride*4)%4;
            int pose=!stepping?0:stepFrame==0?1:stepFrame==2?2:0;
            float lean=(float)Math.Sin(Math.PI*Math.Min(1,Math.Max(0,(T-pickTime)/.38)));
            Bitmap frame=art.FarmerFrames[facingAway?1:0,pose];
            float height=scene==Scene.House?162+(y-InsideDoor.Y)*.16f:106-lean*8-(float)thresholdStep*47;
            return new FarmerPose{Image=frame,Width=height*frame.Width/frame.Height,Height=height,Y=y-bob,
                Rotation=stepping?(float)Math.Sin(beat)*2:-lean*8};
        }
        bool FarmerCovers(PointF p){
            if(Math.Abs(p.X-player.X)>64||p.Y<player.Y-114||p.Y>player.Y+5)return false;
            FarmerPose pose=GetFarmerPose();
            double dx=(p.X-player.X)*(flip?-1:1),dy=p.Y-pose.Y,r=pose.Rotation*Math.PI/180;
            double u=(dx*Math.Cos(r)+dy*Math.Sin(r))/pose.Width+.5;
            double v=(-dx*Math.Sin(r)+dy*Math.Cos(r))/pose.Height+1;
            return u>=0&&u<1&&v>=0&&v<1&&pose.Image.GetPixel((int)(u*pose.Image.Width),(int)(v*pose.Image.Height)).A>96;
        }
        void DrawFarmer(Graphics g){
            FarmerPose pose=GetFarmerPose();
            float shadow=1-(float)thresholdStep*.48f;
            g.FillEllipse(B(Color.FromArgb(68,74,61,29)),player.X-22*shadow,player.Y-6*shadow,44*shadow,12*shadow);
            GraphicsState sc=g.Save();g.TranslateTransform(player.X,pose.Y);if(flip)g.ScaleTransform(-1,1);
            g.RotateTransform(pose.Rotation);
            Sprite(g,pose.Image,-pose.Width/2,-pose.Height,pose.Width,pose.Height);g.Restore(sc);
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
        void DrawWorld(Graphics g)
        {
            double sceneTime=paintedTime=T;
            (Sea?harborMotion:InField?farmMotion:houseMotion).Update(sceneTime);
            g.InterpolationMode=InterpolationMode.Bilinear;g.DrawImage(Sea?port:InField?farm:house,0,0,W,H);
            if(scene==Scene.House){
                DrawWindowFlock(g,sceneTime);
                DrawRoomBreeze(g,sceneTime);
                DrawHearthAir(g,sceneTime);
                DrawMarker(g);DrawFarmer(g);
                HitBox(836,154,123,236,"leaveHouse");
                HitBox(802,378,143,32,"leaveHouse");
                HitBox(1013,316,261,143,"bed");
                HitBox(1090,252,182,71,"bed");
            }else if(InField){
                DrawFlock(g,sceneTime);
                DrawButterflies(g,sceneTime);
                if(doorAt>=0)entrances.DrawDoor(g,T-doorAt);
                for(int i=0;i<state.Plots.Length;i++)DrawSoil(g,i);
                DrawMarker(g);bool farmerDrawn=false;
                for(int i=0;i<state.Plots.Length;i++){
                    if(!farmerDrawn&&player.Y<Bed(i).Y+15){DrawFarmer(g);farmerDrawn=true;}DrawPlants(g,i);
                }
                if(!farmerDrawn)DrawFarmer(g);
                if(state.MachinesOwned[0])Sprite(g,art.Tools[7],955,423,48,48);
                if(state.MachinesOwned[1])Sprite(g,art.Tools[2],956,473,36,36);
                if(state.MachinesOwned[2]){float x=565+(float)Math.Sin(T*.42)*145,y=351+(float)Math.Cos(T*.6)*32;Sprite(g,art.Tools[7],x,y,38,38);using(Pen pen=new Pen(cream,2)){g.DrawLine(pen,x-4,y+10,x+12,y+10);g.DrawLine(pen,x+24,y+10,x+42,y+10);}}
            }else{
                windmill.Draw(g,789.5f,85.5f,(float)(sceneTime*29%360),1,1,25.5f,29.5f);
                DrawMarker(g);DrawFarmer(g);HitBox(98,90,420,224,"harbor");HitBox(852,57,402,254,"tech");
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
            if(doorAt>=0){
                double elapsed=T-doorAt;
                double shade=InField?(elapsed-1.10)/(EnterHouseAt-1.10):(EntranceEnd-elapsed)/(EntranceEnd-EnterHouseAt);
                if(shade>0)using(Brush veil=new SolidBrush(Color.FromArgb((int)(255*Math.Min(1,shade)),39,28,19)))g.FillRectangle(veil,0,0,W,H);
            }
            if(T-travelAt<.9){double p=(T-travelAt)/.9;int a=(int)(250*(1-Math.Min(1,Math.Abs(p-.46)/.54)));
                using(Brush b=new SolidBrush(Color.FromArgb(Math.Max(0,a),247,232,179)))g.FillRectangle(b,0,0,W,H);
                using(Pen pen=new Pen(Color.FromArgb(Math.Max(0,a),255,250,214),4))g.DrawEllipse(pen,640-(float)p*760,360-(float)p*760,(float)p*1520,(float)p*1520);}
        }
        protected override void Dispose(bool disposing){if(disposing){timer.Stop();timer.Dispose();Cursor=Cursors.Default;palm.Dispose();pluck.Dispose();shovelPointer.Dispose();foreach(GamePointer p in seedPointers)p.Dispose();entrances.Dispose();flightWindow.Dispose();windmill.Dispose();windowFlowers.Dispose();leftCurtain.Dispose();rightCurtain.Dispose();shelfVine.Dispose();farm.Dispose();port.Dispose();house.Dispose();art.Dispose();foreach(Font f in fonts.Values)f.Dispose();foreach(Brush b in brushes.Values)b.Dispose();}base.Dispose(disposing);}
        protected override void OnFormClosing(FormClosingEventArgs e){Store();base.OnFormClosing(e);}
        string FindAction(PointF p)
        {
            for(int i=hits.Count-1;i>=0;i--)if(hits[i].Area.Contains(p))return hits[i].Enabled?hits[i].Action:"";
            if(panel==""&&InField){
                string entry=entrances.At(p);
                if(entry!="")return FarmerCovers(p)?"":entry;
                int ground=-1;
                for(int i=0;i<state.Plots.Length;i++){PointF b=Bed(i);if(Math.Abs(p.X-b.X)/HalfBedWidth+Math.Abs(p.Y-b.Y)/HalfBedHeight<=1){ground=i;break;}}
                if(drawer==""&&(tool=="seed"||tool=="shovel"))return ground>=0?"plot"+ground:"";
                if(ground>=0&&drawer!="")return "plot"+ground;
                // Match the frontmost painted leaf, then check the ground underneath it.
                for(int i=state.Plots.Length-1;i>=0;i--){
                    int id=state.Plots[i].CropId;if(id<0)continue;
                    int stage=Stage(i);Bitmap img=art.Plants[id,stage];
                    for(int k=2;k>=0;k--){RectangleF b=PlantBounds(i,stage,k);if(p.Y<b.Top||p.Y>=b.Bottom)continue;
                        float localX=p.X-PlantBend(i,stage,k)*(b.Bottom-p.Y)/b.Height;
                        if(localX<b.Left||localX>=b.Right)continue;
                        int px=Math.Min(img.Width-1,(int)((localX-b.X)*img.Width/b.Width)),py=Math.Min(img.Height-1,(int)((p.Y-b.Y)*img.Height/b.Height));
                        if(img.GetPixel(px,py).A>60)return "plot"+i;}
                }
                if(ground>=0)return "plot"+ground;
            }
            return "";
        }
        PointF WorldPoint(MouseEventArgs e){return new PointF((e.X-offsetX)/scale,(e.Y-offsetY)/scale);}
        bool Ripe(string action){if(!action.StartsWith("plot")||Sea||panel!=""||drawer!=""||tool!="hand")return false;int i=int.Parse(action.Substring(4));return state.Plots[i].CropId>=0&&state.Left(i,state.Now)==0;}
        void UpdatePointer(){
            hover=mouse.X<0||T-travelAt<.9||doorAt>=0?"":FindAction(mouse);
            Cursor desired=heldCrop!=""&&hover==heldCrop?pluck.Value:Ripe(hover)?palm.Value:hover==""?Cursors.Default:Cursors.Hand;
            if(InField&&panel==""&&drawer==""&&T-travelAt>=.9){
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
            base.OnMouseDown(e);if(T-travelAt<.9||doorAt>=0)return;
            if(e.Button==MouseButtons.Right){StopWalking();CloseDrawer();panel="";tool="hand";Refresh();return;}
            if(e.Button!=MouseButtons.Left)return;mouse=WorldPoint(e);string a=FindAction(mouse);
            if(Ripe(a)){StopWalking();heldCrop=a;Capture=true;UpdatePointer();}
            else if(a!="")Action(a);
            else if(panel==""&&Walkable(mouse)){CloseDrawer();WalkTo(mouse,"");}
            else if(panel==""){CloseDrawer();if(queuedAction=="home"||queuedAction=="supply"||queuedAction=="grain")StopWalking();}
            Refresh();
        }
        bool Walkable(PointF p){
            PointF[] path=scene==Scene.House?new[]{new PointF(455,451),new PointF(596,408),new PointF(890,393),new PointF(944,414),new PointF(997,565),new PointF(984,686),new PointF(411,686),new PointF(409,551)}:
                Sea?new[]{new PointF(227,354),new PointF(566,306),new PointF(932,321),new PointF(1075,373),new PointF(1180,487),new PointF(945,535),new PointF(467,563),new PointF(81,502)}:
                new[]{new PointF(10,310),new PointF(272,242),new PointF(420,267),new PointF(630,268),new PointF(750,271),new PointF(916,276),new PointF(978,307),new PointF(943,365),new PointF(1022,408),new PointF(1170,454),new PointF(1165,568),new PointF(1002,643),new PointF(350,657),new PointF(40,543)};
            using(GraphicsPath gp=new GraphicsPath()){gp.AddPolygon(path);return gp.IsVisible(p);}
        }
        protected override void OnKeyDown(KeyEventArgs e){
            base.OnKeyDown(e);if(e.KeyCode==Keys.F11){ToggleFullScreen();return;}
            if(doorAt>=0)return;
            if(e.KeyCode==Keys.Escape){StopWalking();if(panel!="")panel="";else if(drawer!="")CloseDrawer();else if(fullscreen)ToggleFullScreen();else tool="hand";Refresh();return;}
            if(panel!=""||T-travelAt<.9)return;
            switch(e.KeyCode){
                case Keys.D1:case Keys.NumPad1:if(InField)Action("hand");break;
                case Keys.D2:case Keys.NumPad2:if(InField)Action("seeds");break;
                case Keys.D3:case Keys.NumPad3:if(InField)Action("feedtool");break;
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
