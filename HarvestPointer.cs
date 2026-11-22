using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MountainTide
{
    // A genuine Windows cursor stays attached to the mouse between animation frames.
    internal sealed class GamePointer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct IconInfo {public int icon,hotX,hotY;public IntPtr mask,color;}
        [DllImport("user32.dll")] static extern bool GetIconInfo(IntPtr icon,out IconInfo info);
        [DllImport("user32.dll")] static extern IntPtr CreateIconIndirect(ref IconInfo info);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] static extern bool DestroyCursor(IntPtr cursor);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr bitmap);
        readonly IntPtr handle;
        public readonly Cursor Value;
        public GamePointer(bool closed)
        {
            using(Bitmap image=new Bitmap(44,44))
            using(Graphics g=Graphics.FromImage(image))
            using(Pen edge=new Pen(Color.FromArgb(78,57,40),2))
            using(GraphicsPath hand=new GraphicsPath()){
                g.SmoothingMode=SmoothingMode.AntiAlias;edge.LineJoin=LineJoin.Round;
                // The pinch forms around the little wheat stem at the cursor hotspot.
                using(Pen stem=new Pen(Color.FromArgb(170,123,37),1.8f))g.DrawLine(stem,7,3,19,24);
                using(Brush ear=new SolidBrush(Color.FromArgb(255,198,65))){
                    for(int i=0;i<3;i++){g.FillEllipse(ear,3+i*3,3+i*4,6,4);g.FillEllipse(ear,8+i*3,i*4,4,6);}
                }
                hand.StartFigure();
                if(closed){
                    hand.AddBezier(33,25,29,22,28,16,23,14);
                    hand.AddBezier(23,14,19,12,16,9,13,9);
                    hand.AddBezier(13,9,9,9,8,12,11,14);
                    hand.AddBezier(11,14,14,16,17,16,18,19);
                    hand.AddBezier(18,19,15,19,13,16,10,16);
                    hand.AddBezier(10,16,7,17,9,20,11,22);
                    hand.AddBezier(11,22,10,25,13,28,16,30);
                }else{
                    hand.AddBezier(33,25,30,20,28,17,25,15);
                    hand.AddLine(25,15,16,8);hand.AddBezier(16,8,12,5,9,8,12,11);
                    hand.AddLine(12,11,20,18);hand.AddLine(20,18,9,12);
                    hand.AddBezier(9,12,5,10,4,15,8,17);hand.AddLine(8,17,16,22);
                    hand.AddLine(16,22,8,19);hand.AddBezier(8,19,5,18,4,22,7,24);
                    hand.AddLine(7,24,16,30);
                }
                hand.AddBezier(16,30,19,32,23,32,27,34);hand.AddLine(27,34,34,26);hand.CloseFigure();
                using(Brush glove=new SolidBrush(Color.Ivory))g.FillPath(glove,hand);
                g.DrawPath(edge,hand);
                using(Pen fold=new Pen(Color.FromArgb(174,160,135),1.4f)){
                    g.DrawArc(fold,17,18,10,12,190,100);g.DrawLine(fold,20,29,25,30);
                }
                Point[] cuff={new Point(24,34),new Point(33,23),new Point(40,28),new Point(31,39)};
                using(Brush b=new SolidBrush(Color.FromArgb(52,153,149)))g.FillPolygon(b,cuff);g.DrawPolygon(edge,cuff);
                handle=Create(image,12,12);
                Value=new Cursor(handle);
            }
        }
        public GamePointer(Bitmap image,int hotX,int hotY){
            handle=Create(image,hotX,hotY);
            Value=new Cursor(handle);
        }
        static IntPtr Create(Bitmap image,int hotX,int hotY){
            IntPtr original=image.GetHicon();IconInfo info;
            if(!GetIconInfo(original,out info)){DestroyIcon(original);throw new InvalidOperationException("无法创建农具光标。");}
            try{
                info.icon=0;info.hotX=hotX;info.hotY=hotY;
                IntPtr cursor=CreateIconIndirect(ref info);
                if(cursor==IntPtr.Zero)throw new InvalidOperationException("无法创建农具光标。");
                return cursor;
            }finally{DeleteObject(info.mask);DeleteObject(info.color);DestroyIcon(original);}
        }
        public void Dispose(){Value.Dispose();DestroyCursor(handle);}
    }
}
