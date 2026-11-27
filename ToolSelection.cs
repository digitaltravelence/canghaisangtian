using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MountainTide
{
    internal sealed partial class Game
    {
        GamePointer waterPointer, fertilizerPointer;
        readonly GamePointer[] premiumPointers=new GamePointer[Crop.All.Length];
        readonly Bitmap[] premiumPackets=new Bitmap[Crop.All.Length];
        bool selectedPremium, browsePremium;
        int SelectedSeedCount {get{return (selectedPremium?state.PremiumSeeds:state.Seeds)[selectedSeed];}}
        PointF touchPoint;
        bool touching;
        double touchUntil=-1;

        static GamePointer MakeToolPointer(Image source,int hotX,int hotY)
        {
            using(Bitmap icon=new Bitmap(46,46))
            using(Graphics g=Graphics.FromImage(icon)){
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                float f=Math.Min(44f/source.Width,44f/source.Height);
                float w=source.Width*f,h=source.Height*f;
                g.DrawImage(source,(46-w)/2,(46-h)/2,w,h);
                return new GamePointer(icon,hotX,hotY);
            }
        }
        void CreateToolPointers()
        {
            waterPointer=MakeToolPointer(art.WateringCan,3,20);
            fertilizerPointer=MakeToolPointer(art.Tools[2],7,38);
            for(int id=0;id<Crop.All.Length;id++){
                var packet=new Bitmap(art.SeedPackets[id]);
                using(Graphics g=Graphics.FromImage(packet)){
                    g.SmoothingMode=SmoothingMode.AntiAlias;
                    using(var rim=new Pen(Col("855827"),2)){
                        g.FillEllipse(B(Col("f8ce69")),44,43,27,27);g.DrawEllipse(rim,44,43,27,27);
                    }
                    Spark(g,57.5f,56.5f,7.5f,Col("94651f"));
                }
                premiumPackets[id]=packet;
                premiumPointers[id]=MakeToolPointer(packet,6,36);
            }
        }
        void DisposeToolPointers()
        {
            harvestHand.Dispose();
            waterPointer.Dispose();fertilizerPointer.Dispose();
            foreach(GamePointer p in premiumPointers)p.Dispose();
            foreach(Bitmap b in premiumPackets)b.Dispose();
        }
        Cursor SelectedToolPointer()
        {
            switch(tool){
                case "harvest":return harvestHand.Value;
                case "shovel":return shovelPointer.Value;
                case "water":return waterPointer.Value;
                case "feed":return fertilizerPointer.Value;
                case "seed":return (selectedPremium?premiumPointers:seedPointers)[selectedSeed].Value;
                default:return null;
            }
        }
        Image SelectedToolImage()
        {
            switch(tool){
                case "harvest":return harvestHand.Artwork;
                case "shovel":return art.Spade;
                case "water":return art.WateringCan;
                case "feed":return art.Tools[2];
                case "seed":return selectedPremium?premiumPackets[selectedSeed]:art.SeedPackets[selectedSeed];
                default:return null;
            }
        }
        [DllImport("user32.dll")] static extern IntPtr GetMessageExtraInfo();
        [DllImport("user32.dll")] static extern bool GetPointerType(uint pointerId,out uint pointerType);
        void TrackContact(PointF p,bool down)
        {
            long info=GetMessageExtraInfo().ToInt64();
            // Windows promotes a finger contact to a compatibility mouse message.
            if((info&0xffffff80L)==0xff515780L){
                if(down)touching=true;
                touchPoint=p;touchUntil=T+.85;
            }else if(down&&T>touchUntil){touchUntil=-1;}
        }
        protected override void WndProc(ref Message m)
        {
            if(IsHandleCreated&&clock!=null&&(m.Msg==0x0245||m.Msg==0x0246||m.Msg==0x0247)){
                uint kind;
                if(GetPointerType((uint)(m.WParam.ToInt64()&0xffff),out kind)&&kind==2){
                    long packed=m.LParam.ToInt64();
                    Point pt=PointToClient(new Point((short)(packed&0xffff),(short)((packed>>16)&0xffff)));
                    touchPoint=new PointF((pt.X-offsetX)/scale,(pt.Y-offsetY)/scale);
                    touching=m.Msg!=0x0247;
                    touchUntil=T+.85;
                }
            }
            if(m.Msg==0x001f||m.Msg==0x024c)touching=false;
            base.WndProc(ref m);
        }
        void DrawTouchTool(Graphics g)
        {
            if(CareInputLocked||!InField||drawer!=""||panel!=""||(!touching&&T>touchUntil))return;
            Image img=heldCrop!=""&&hover==heldCrop?pluck.Artwork:SelectedToolImage();
            if(img!=null)FitSprite(g,img,Math.Min(W-46,Math.Max(0,touchPoint.X+11)),Math.Max(0,touchPoint.Y-48),42,42);
        }
    }
}
