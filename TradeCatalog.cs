using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MountainTide
{
    internal sealed partial class Game
    {
        static readonly RectangleF CatalogFrame = new RectangleF(236,99,808,520);
        static readonly RectangleF CatalogView = new RectangleF(264,241,728,287);
        static readonly RectangleF CatalogRail = new RectangleF(995,241,25,287);
        const int CatalogCardWidth=357, CatalogCardHeight=122, CatalogColumnStep=369, CatalogRowStep=133;
        float catalogOffset, catalogStartOffset, catalogGripOffset;
        PointF catalogPress;
        int catalogPressedItem=-1;
        bool catalogHeld, catalogDragged, catalogScrolling, catalogOnRail;

        bool InTradeCatalog { get { return panel=="supply" || panel=="grain" || panel=="harbor"; } }
        int CatalogCount { get { return FarmState.CropOrder.Length+(panel=="supply"?1:0); } }
        float CatalogHeight { get { return (CatalogCount-1)/2*CatalogRowStep+CatalogCardHeight+4; } }
        float CatalogLimit { get { return Math.Max(0,CatalogHeight-CatalogView.Height); } }

        RectangleF CatalogCard(int slot)
        {
            return new RectangleF(CatalogView.Left+1+slot%2*CatalogColumnStep,
                CatalogView.Top+2+slot/2*CatalogRowStep-catalogOffset,CatalogCardWidth,CatalogCardHeight);
        }
        int CatalogItem(int slot)
        {
            return slot<FarmState.CropOrder.Length ? FarmState.CropOrder[slot] : FarmState.FertilizerItem;
        }
        bool CatalogUnlocked(int id)
        {
            return panel!="supply" || id==FarmState.FertilizerItem || state.CropUnlocked(id);
        }
        int CatalogItemAt(PointF point)
        {
            if (!CatalogView.Contains(point)) return -1;
            for (int slot=0;slot<CatalogCount;slot++) {
                if (!CatalogCard(slot).Contains(point)) continue;
                int id=CatalogItem(slot);
                return CatalogUnlocked(id) ? id : -1;
            }
            return -1;
        }
        RectangleF CatalogThumb()
        {
            float height=CatalogRail.Height*Math.Min(1,CatalogView.Height/CatalogHeight);
            float travel=CatalogRail.Height-height;
            float top=CatalogRail.Top+(CatalogLimit>0?travel*catalogOffset/CatalogLimit:0);
            return new RectangleF(CatalogRail.Left+8,top,9,height);
        }
        void SetCatalogOffset(float next)
        {
            catalogOffset=Math.Max(0,Math.Min(CatalogLimit,next));
        }
        void SlideCatalogThumb(float y)
        {
            float travel=CatalogRail.Height-CatalogThumb().Height;
            if (travel>0) SetCatalogOffset((y-CatalogRail.Top-catalogGripOffset)*CatalogLimit/travel);
        }
        void CancelCatalogDrag()
        {
            if (!catalogHeld) return;
            catalogHeld=false;catalogDragged=false;catalogScrolling=false;catalogOnRail=false;catalogPressedItem=-1;
            if (Capture) Capture=false;
        }
        void ResetTradeCatalog()
        {
            CancelCatalogDrag();
            catalogOffset=0;
        }
        bool CatalogPointerDown(PointF point)
        {
            if (!InTradeCatalog || (!CatalogView.Contains(point) && !CatalogRail.Contains(point))) return false;
            CancelCatalogDrag();
            catalogPress=point;catalogStartOffset=catalogOffset;catalogPressedItem=CatalogItemAt(point);
            catalogHeld=true;catalogOnRail=CatalogRail.Contains(point);
            if (catalogOnRail) {
                RectangleF thumb=CatalogThumb();
                catalogGripOffset=point.Y>=thumb.Top && point.Y<=thumb.Bottom ? point.Y-thumb.Top : thumb.Height/2;
                SlideCatalogThumb(point.Y);
            }
            Capture=true;
            Invalidate();
            return true;
        }
        bool CatalogPointerMove(PointF point)
        {
            if (!catalogHeld) return false;
            if (!InTradeCatalog) { CancelCatalogDrag();return true; }
            if (catalogOnRail) SlideCatalogThumb(point.Y);
            else {
                float dx=point.X-catalogPress.X,dy=point.Y-catalogPress.Y;
                if (dx*dx+dy*dy>=36) catalogDragged=true;
                if (Math.Abs(dy)>=6) catalogScrolling=true;
                if (catalogScrolling) SetCatalogOffset(catalogStartOffset-dy);
            }
            Invalidate();
            return true;
        }
        bool CatalogPointerUp(PointF point)
        {
            if (!catalogHeld) return false;
            CatalogPointerMove(point);
            bool select=catalogHeld && !catalogDragged && !catalogOnRail && catalogPressedItem>=0 &&
                catalogPressedItem==CatalogItemAt(point);
            int id=catalogPressedItem;
            CancelCatalogDrag();
            if (select) Action("trade-item-"+id);
            Invalidate();
            return true;
        }
        bool ScrollTradeCatalog(PointF point,int delta)
        {
            if (!InTradeCatalog || !CatalogFrame.Contains(point)) return false;
            if (!catalogOnRail && delta!=0) {
                SetCatalogOffset(catalogOffset-delta*(72f/120));
                if (catalogHeld) {catalogDragged=true;catalogStartOffset=catalogOffset;catalogPress=point;}
                Invalidate();
            }
            return true;
        }

        void DrawTradeCatalog(Graphics g)
        {
            bool supply=panel=="supply",sea=panel=="harbor";
            Color accent=sea?Col("365f68"):supply?Col("465e40"):Col("705536");
            Color trim=sea?Col("b4c8b6"):supply?Col("c5d2a8"):Col("e7c99a");
            Plate(g,240,105,808,520,Color.FromArgb(130,35,39,30),Color.FromArgb(100,35,39,30),15);
            Plate(g,236,99,808,520,Col("faf2dc"),Col("907952"),14);
            Plate(g,237,100,806,94,accent,accent,13);
            g.FillRectangle(B(accent),237,174,806,19);
            g.FillRectangle(B(Col("cfb476")),237,192,806,3);
            Plate(g,263,119,55,54,Col("f7eccf"),Col("d1be93"),12);
            FitSprite(g,supply?art.SeedPackets[0]:sea?art.Tools[6]:art.Plants[0,2],270,123,41,45);
            TextAt(g,supply?"青芽种子铺":sea?"码头收购摊":"山村粮站",28,cream,331,111,358,44,true);
            TextAt(g,supply?"普通种子与田间补给":sea?"未来码头以星元收购农作物":"山村粮站以现钱收购农作物",
                14,trim,333,153,412,24);
            Plate(g,748,126,201,39,Color.FromArgb(38,14,24,17),trim,15);
            FitSprite(g,art.Tools[sea?6:5],758,134,24,22);
            TextAt(g,(sea?"星元   ":"现钱   ")+(sea?state.StarCredits:state.Cash),16,cream,790,126,151,39,true);
            ModalButton(g,979,126,38,"×","close",true,false);

            TextAt(g,supply?"店内商品":"收购价目",19,ink,265,200,220,37,true);
            TextAt(g,"共 "+CatalogCount+" 种",14,Col("70745d"),914,203,80,32,false,true);
            GraphicsState content=g.Save();
            try {
                g.SetClip(CatalogView,CombineMode.Intersect);
                for (int slot=0;slot<CatalogCount;slot++) {
                    RectangleF bounds=CatalogCard(slot);
                    RectangleF visible=RectangleF.Intersect(bounds,CatalogView);
                    if (visible.IsEmpty) continue;
                    int id=CatalogItem(slot);
                    DrawCatalogCard(g,bounds,id,supply,sea,accent);
                    HitBox(visible.X,visible.Y,visible.Width,visible.Height,"trade-item-"+id,CatalogUnlocked(id));
                }
            } finally {g.Restore(content);}
            RectangleF thumb=CatalogThumb();
            Plate(g,thumb.X+2,CatalogRail.Top,5,CatalogRail.Height,Col("e4d8b9"),Col("ded1af"),2);
            Plate(g,thumb.X,thumb.Y,thumb.Width,thumb.Height,catalogOnRail?accent:Col("a99a73"),Col("94845e"),4);

            g.FillRectangle(B(Col("d3c6a6")),265,547,749,1);
            string notice=TradeCatalogNotice;
            bool relief=supply && state.NeedsSeed();
            TextAt(g,notice.Length>0?notice:"点选商品查看详情，滚轮或上下拖动可翻阅。",
                15,notice.Length>0?accent:Col("65715a"),267,560,relief?550:732,38,notice.Length>0);
            if (relief) ModalButton(g,837,561,178,"领一袋救急麦种","relief");
        }
        void DrawCatalogCard(Graphics g,RectangleF bounds,int id,bool supply,bool sea,Color accent)
        {
            float x=bounds.X,y=bounds.Y;
            bool fertilizer=id==FarmState.FertilizerItem,unlocked=CatalogUnlocked(id);
            bool pointed=unlocked && !catalogDragged && hover=="trade-item-"+id;
            Color paper=!unlocked?Col("ece9d9"):pointed?Col("fffbec"):Col("fcf5df");
            Color edge=pointed?accent:!unlocked?Col("d3ceb9"):Col("d2c095");
            Color body=unlocked?ink:Col("64695c"),muted=unlocked?Col("646f59"):Col("777a6e");
            Plate(g,x,y,bounds.Width,bounds.Height,paper,edge,9);
            Plate(g,x+13,y+13,64,70,unlocked?Col("efe8ca"):Col("e4e3d4"),unlocked?Col("d9c99f"):Col("d0cfbe"),9);
            Image artImage=fertilizer?art.Tools[2]:supply?art.SeedPackets[id]:art.Plants[id,2];
            FitSprite(g,artImage,x+19,y+18,52,60);
            string name=fertilizer?"化肥":Crop.All[id].Name+(supply?"种子":"");
            TextAt(g,name,20,body,x+89,y+9,228,34,true);
            string detail=fertilizer?"施用后提前 20 秒成熟":Crop.All[id].Seconds+" 秒成熟";
            TextAt(g,detail,14,muted,x+90,y+43,245,24);
            string unit=supply?"袋":"份";
            int held=state.TradeHeld(supply,id);
            string stock=(supply?"持有 ":"背包 ")+held+" "+unit;
            TextAt(g,stock,14,muted,x+90,y+67,248,22);
            g.FillRectangle(B(edge),x+14,y+91,bounds.Width-28,1);
            int price=FarmState.TradePrice(supply,sea,id);
            TextAt(g,"每"+unit+" "+price+(sea?" 星元":" 现钱"),15,unlocked?accent:body,x+15,y+93,146,26,true);
            if (!unlocked) {
                string requirement=state.CropUnlockHint(id);
                TextAt(g,requirement,13,muted,x+147,y+94,195,23,false,true);
            } else {
                using(Pen pen=new Pen(pointed?accent:Col("9a9c82"),1.6f)) {
                    pen.StartCap=pen.EndCap=LineCap.Round;
                    g.DrawLines(pen,new[]{new PointF(x+333,y+21),new PointF(x+338,y+26),new PointF(x+333,y+31)});
                }
            }
        }
    }
}
