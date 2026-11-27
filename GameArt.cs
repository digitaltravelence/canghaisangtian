using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MountainTide
{
    internal sealed class GameArt : IDisposable
    {
        public readonly Bitmap Soil, Spade, WateringCan;
        public readonly Bitmap[,] Plants=new Bitmap[3,3],FarmerFrames=new Bitmap[2,3];
        public readonly Bitmap[] Tools=new Bitmap[8];
        public readonly Bitmap[] SeedPackets=new Bitmap[3];
        public GameArt()
        {
            Soil=ReadSheet("soil.png",new[]{0,887},new[]{new[]{0,1774}},420)[0];
            Bitmap[] plants=ReadSheet("plants.png",new[]{0,376,762,1254},new[]{new[]{0,346,779,1254},new[]{0,350,771,1254},new[]{0,343,759,1254}},150);
            Bitmap[] farmer=ReadSheet("farmer.png",new[]{0,505,1024},new[]{new[]{0,505,1012,1536},new[]{0,505,1012,1536}},180);
            for(int i=0;i<plants.Length;i++)Plants[i/3,i%3]=plants[i];
            for(int i=0;i<SeedPackets.Length;i++)SeedPackets[i]=MakePacket(i);
            for(int i=0;i<farmer.Length;i++)FarmerFrames[i/3,i%3]=farmer[i];
            Tools=ReadSheet("items.png",new[]{0,442,887},new[]{new[]{0,430,866,1308,1774},new[]{0,414,848,1256,1774}},100);
            Spade=ReadIcon("spade.png");
            WateringCan=ReadIcon("watering-can.png");
        }
        static Bitmap ReadIcon(string name)
        {
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using(Bitmap image=new Bitmap(stream))
                return image.Clone(new Rectangle(0,0,image.Width,image.Height),PixelFormat.Format32bppPArgb);
        }
        static Bitmap[] ReadSheet(string name,int[] rows,int[][] columns,int maxSide)
        {
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using(Bitmap sheet=new Bitmap(stream)){
                Rectangle all=new Rectangle(0,0,sheet.Width,sheet.Height);
                BitmapData data=sheet.LockBits(all,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                byte[] pixels=new byte[data.Stride*data.Height];
                try{Marshal.Copy(data.Scan0,pixels,0,pixels.Length);}finally{sheet.UnlockBits(data);}
                int cols=columns[0].Length-1;Bitmap[] result=new Bitmap[(rows.Length-1)*cols];
                for(int r=0;r<rows.Length-1;r++)for(int c=0;c<cols;c++){
                    int x0=columns[r][c],x1=columns[r][c+1],y0=rows[r],y1=rows[r+1];
                    int left=x1,top=y1,right=x0,bottom=y0;
                    for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){
                        if(pixels[y*data.Stride+x*4+3]<90)continue;
                        left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                    }
                    Rectangle src=Rectangle.FromLTRB(Math.Max(x0,left-2),Math.Max(y0,top-2),Math.Min(x1,right+3),Math.Min(y1,bottom+3));
                    float scale=Math.Min(1,maxSide/(float)Math.Max(src.Width,src.Height));
                    Bitmap sprite=new Bitmap(Math.Max(1,(int)Math.Round(src.Width*scale)),Math.Max(1,(int)Math.Round(src.Height*scale)),PixelFormat.Format32bppPArgb);
                    using(Graphics g=Graphics.FromImage(sprite)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.Half;
                        g.DrawImage(sheet,new Rectangle(0,0,sprite.Width,sprite.Height),src,GraphicsUnit.Pixel);}
                    result[r*cols+c]=sprite;
                }
                return result;
            }
        }
        Bitmap MakePacket(int crop){
            Bitmap packet=new Bitmap(80,80,PixelFormat.Format32bppPArgb);
            Color band=new[]{Color.FromArgb(205,150,53),Color.FromArgb(189,87,83),Color.FromArgb(109,147,67)}[crop];
            using(Graphics g=Graphics.FromImage(packet))
            using(Pen edge=new Pen(Color.FromArgb(102,72,39),2.7f))
            using(Brush paper=new SolidBrush(Color.FromArgb(255,233,184)))
            using(Brush trim=new SolidBrush(band)){
                g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                g.TranslateTransform(42,37);g.RotateTransform(11);
                PointF[] shape={new PointF(-24,-31),new PointF(23,-31),new PointF(22,31),new PointF(-23,31)};
                g.FillPolygon(paper,shape);g.DrawPolygon(edge,shape);
                g.FillRectangle(trim,-22,-29,43,8);g.FillRectangle(trim,-21,20,41,7);
                Bitmap plant=Plants[crop,2];float f=Math.Min(39f/plant.Width,38f/plant.Height);
                float w=plant.Width*f,h=plant.Height*f;g.DrawImage(plant,-w/2,-19+(38-h)/2,w,h);g.ResetTransform();
                g.FillEllipse(trim,7,63,12,8);g.DrawEllipse(edge,7,63,12,8);
            }
            return packet;
        }
        public void Dispose(){Soil.Dispose();Spade.Dispose();WateringCan.Dispose();foreach(Bitmap b in SeedPackets)b.Dispose();foreach(Bitmap b in Tools)b.Dispose();foreach(Bitmap b in Plants)b.Dispose();foreach(Bitmap b in FarmerFrames)b.Dispose();}
    }
}
