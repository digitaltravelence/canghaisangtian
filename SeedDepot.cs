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
        static readonly PointF SeedDepotDock=new PointF(970,548);
        static readonly PointF SeedDepotDoor=new PointF(964,577);
        const int DepotPageSize=4;
        static readonly Rectangle DepotFrame = Rectangle.FromLTRB(838,469,1240,626);
        static readonly RectangleF DepotLabel = new RectangleF(864,574,80,28);
        static readonly RectangleF DepotBody = new RectangleF(841,475,146,96);
        static readonly Rectangle JawBounds = Rectangle.FromLTRB(938,525,970,542);
        Bitmap depotArt, depotCradle, depotAssembly, depotFrontJaw;
        int depotAssemblyCount;
        int depotCrop, depotQuantity=1, depotPage;
        bool depotPremium;
        string depotNotice="";
        double depotNoticeUntil=-1,depotLedgerAt=-1;
        readonly List<DepotBatch> depotLedger=new List<DepotBatch>();
        long depotStored,depotAirborne;

        sealed class DepotBatch
        {
            public int Crop,Stored,Airborne;
            public bool Premium;
        }

        static Bitmap ReadDepotArt(string name)
        {
            using(Stream source=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using(Bitmap original=new Bitmap(source))
                return original.Clone(new Rectangle(0,0,original.Width,original.Height),PixelFormat.Format32bppPArgb);
        }
        Bitmap DepotImage()
        {
            if(depotArt==null)depotArt=ReadDepotArt("seed-depot.png");
            return depotArt;
        }
        void DisposeSeedDepotArt()
        {
            if(depotArt!=null){depotArt.Dispose();depotArt=null;}
            if(depotCradle!=null){depotCradle.Dispose();depotCradle=null;}
            if(depotAssembly!=null){depotAssembly.Dispose();depotAssembly=null;}
            if(depotFrontJaw!=null){depotFrontJaw.Dispose();depotFrontJaw=null;}
        }
        void DrawSeedDepot(Graphics g)
        {
            if(!InField || !state.SeedWarehouseUnlocked)return;
            int count=state.MachineCount(FarmState.SeederDrone);
            if(depotAssembly==null || depotAssemblyCount!=count)BuildDepotAssembly(count);
            Sprite(g,depotAssembly,DepotFrame.X,DepotFrame.Y,DepotFrame.Width,DepotFrame.Height);
            if(panel!="")return;
            DrawBuildingSign(g,"种子仓",904,574,FarmSignFace);
            HitBox(841,475,146,96,"seed-depot-field");
            HitBox(DepotLabel.X,DepotLabel.Y,DepotLabel.Width,DepotLabel.Height,"seed-depot-field");
            HitBox(943,530,48,32,"seed-depot-field");
            int expansion=count-1,rows=(expansion+8)/9;
            for(int row=0;row<rows;row++)
            {
                PointF end=SeedRackPoint(Math.Min(expansion-1,row*9+8));
                HitBox(980,end.Y-25,end.X+17-980,39,"seed-depot-field");
            }
            if(rows>1)HitBox(978,540,22,SeedRackPoint((rows-1)*9).Y-530,"seed-depot-field");
        }
        bool SeedDepotContains(PointF point)
        {
            if(DepotLabel.Contains(point))return true;
            if(depotAssembly==null || !DepotFrame.Contains((int)point.X,(int)point.Y))return false;
            int x=(int)((point.X-DepotFrame.X)*2),y=(int)((point.Y-DepotFrame.Y)*2);
            return x>=0 && y>=0 && (depotAssembly.GetPixel(x,y).A>=150 || DockedSeederContains(point));
        }
        static RectangleF CradleBounds(PointF at,bool transfer)
        {
            float width=transfer?25:31, height=width*.55f;
            return new RectangleF(at.X-width/2,at.Y-height*.53f,width,height);
        }
        void BuildDepotAssembly(int count)
        {
            if(depotCradle==null)depotCradle=ReadDepotArt("seed-cradle.png");
            if(depotFrontJaw==null)BuildDepotJaw();
            if(depotAssembly!=null)depotAssembly.Dispose();
            depotAssembly=new Bitmap(DepotFrame.Width*2,DepotFrame.Height*2,PixelFormat.Format32bppPArgb);
            depotAssemblyCount=count;
            using(Graphics g=Graphics.FromImage(depotAssembly))
            {
                g.ScaleTransform(2,2);
                g.TranslateTransform(-DepotFrame.X,-DepotFrame.Y);
                g.SmoothingMode=SmoothingMode.AntiAlias;
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                g.FillEllipse(B(Color.FromArgb(50,51,52,29)),842,548,148,23);
                int expansion=count-1,rows=(expansion+8)/9;
                if(rows>1)DepotRail(g,978,539,22,SeedRackPoint((rows-1)*9).Y-527);
                for(int row=0;row<rows;row++)
                {
                    PointF first=SeedRackPoint(row*9);
                    PointF end=SeedRackPoint(Math.Min(expansion-1,row*9+8));
                    float x=row==0?910:980;
                    // The deeper bed overlaps the following row; the whole rack shares one chassis.
                    DepotRail(g,x,first.Y-9,end.X+16-x,row==rows-1?20:25);
                    for(float panelX=Math.Max(954,x+4);panelX<end.X-6;panelX+=26)
                    {
                        g.FillRectangle(B(Col("576e30")),panelX,first.Y+5,8,1.6f);
                        g.FillRectangle(B(Col("e2be6e")),panelX+8,first.Y+5,2,1.6f);
                    }
                }
                // The main machine docks inside the open gripper above the independent loading outlet.
                g.FillEllipse(B(Col("84642d")),945,516,23,21);
                g.FillEllipse(B(Col("302e20")),947,518,18,16);
                g.FillEllipse(B(Col("85d4ac")),956,522,4,2);
                g.DrawImage(DepotImage(),DepotBody);
                g.DrawImage(depotCradle,CradleBounds(SeedDepotDock,true));
                for(int i=1;i<count;i++)g.DrawImage(depotCradle,CradleBounds(SeedDockPoint(i),false));
            }
        }
        void BuildDepotJaw()
        {
            // The nearer jaw is cut from the same painted body and can pass in front of the airframe.
            depotFrontJaw=new Bitmap(JawBounds.Width*2,JawBounds.Height*2,PixelFormat.Format32bppPArgb);
            using(Graphics g=Graphics.FromImage(depotFrontJaw))
            using(GraphicsPath edge=new GraphicsPath())
            {
                g.ScaleTransform(2,2); g.TranslateTransform(-JawBounds.X,-JawBounds.Y);
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                edge.AddPolygon(new[]{new PointF(940,527),new PointF(944,526),new PointF(949,531),
                    new PointF(954,531),new PointF(959,528),new PointF(965,527),new PointF(968,528),
                    new PointF(966,533),new PointF(963,537),new PointF(956,539),new PointF(949,539),
                    new PointF(943,536),new PointF(940,533)});
                g.SetClip(edge);
                g.DrawImage(DepotImage(),DepotBody);
            }
        }
        void DepotRail(Graphics g,float x,float y,float width,float height)
        {
            // A bevelled common housing joins every socket back to the hopper's brass outlet.
            Plate(g,x+1,y+3,width,height,Color.FromArgb(75,46,50,26),Color.FromArgb(56,46,50,26),3);
            Plate(g,x,y,width,height,Col("56452b"),Col("493722"),3);
            Plate(g,x+.8f,y,width-1.6f,height-3,Col("748b32"),Col("b8a048"),2.7f);
            g.FillRectangle(B(Col("bbc56a")),x+3,y+1,width-6,1.3f);
            g.FillRectangle(B(Col("efb752")),x+3,y+height-3,width-6,1.2f);
        }
        void DrawCradleLip(Graphics g,PointF at,bool transfer,bool main,float contact)
        {
            if(main)
            {
                Sprite(g,depotFrontJaw,JawBounds.X,JawBounds.Y,JawBounds.Width,JawBounds.Height);
                return;
            }
            RectangleF slot=CradleBounds(at,transfer);
            const float front=.59f;
            RectangleF visible=new RectangleF(slot.X,slot.Y+slot.Height*front,slot.Width,slot.Height*(1-front));
            RectangleF original=new RectangleF(0,depotCradle.Height*front,depotCradle.Width,depotCradle.Height*(1-front));
            g.DrawImage(depotCradle,visible,original,GraphicsUnit.Pixel);
            // The two spring contacts settle inward around the nose as its nozzle seats.
            float reach=slot.Width*(.31f-.12f*contact),y=at.Y-.5f;
            for(int side=-1;side<=1;side+=2)
            {
                float x=at.X+side*reach;
                g.FillEllipse(B(Col("704c25")),x-2.2f,y,4.4f,3);
                g.FillEllipse(B(Col("fcc868")),x-1.6f,y-.4f,3.2f,1.8f);
            }
        }

        int DepotBackpack(int id){return depotPremium?state.PremiumSeeds[id]:state.Seeds[id];}
        static void CountDepotLot(SortedDictionary<long,DepotBatch> rows,WarehouseLot lot,bool airborne)
        {
            DepotBatch row;
            if(!rows.TryGetValue(lot.BatchOrder,out row)){
                row=new DepotBatch{Crop=lot.CropId,Premium=lot.Premium};
                rows.Add(lot.BatchOrder,row);
            }
            if(airborne)row.Airborne+=lot.Quantity;else row.Stored+=lot.Quantity;
        }
        void RefreshDepotLedger()
        {
            if(T<depotLedgerAt)return;
            depotLedgerAt=T+.25;
            var rows=new SortedDictionary<long,DepotBatch>();
            foreach(WarehouseLot lot in state.SeedWarehouse)CountDepotLot(rows,lot,false);
            foreach(SeedFlight flight in state.SeederFlights)
                foreach(WarehouseLot lot in flight.Cargo)CountDepotLot(rows,lot,true);
            depotLedger.Clear();depotStored=0;depotAirborne=0;
            foreach(DepotBatch batch in rows.Values){
                depotLedger.Add(batch);
                depotStored+=batch.Stored;depotAirborne+=batch.Airborne;
            }
            depotPage=Math.Min(depotPage,DepotLastPage);
        }
        int DepotLastPage {get{return Math.Max(0,(depotLedger.Count-1)/DepotPageSize);}}
        void ChangeDepotPage(int step)
        {
            RefreshDepotLedger();
            depotPage=(int)Math.Min(DepotLastPage,Math.Max(0L,(long)depotPage+step));
        }
        bool ScrollSeedDepot(PointF p,int delta)
        {
            if(panel!="seed-depot" || !new RectangleF(615,170,432,426).Contains(p))return false;
            if(delta!=0)ChangeDepotPage(delta<0?1:-1);
            return true;
        }
        void DrawSeedDepotPanel(Graphics g)
        {
            RefreshDepotLedger();
            Plate(g,208,77,869,568,Color.FromArgb(141,43,44,30),Color.FromArgb(100,43,44,30),14);
            Plate(g,204,71,870,570,Col("faf2dc"),Col("8e7750"),14);
            Plate(g,205,72,868,84,Col("52664a"),Col("52664a"),13);
            g.FillRectangle(B(Col("52664a")),205,135,868,21);
            g.FillRectangle(B(Col("d4ba7d")),205,155,868,3);
            Plate(g,228,87,60,56,Col("f5e6c3"),Col("cfbb8e"),9);
            FitSprite(g,DepotImage(),232,90,52,48);
            TextAt(g,"田边种子仓",27,cream,301,81,345,43,true);
            TextAt(g,"种子按投放顺序出仓，每块空田用一袋。",14,Col("e1e7ce"),303,122,440,24);
            ModalButton(g,830,97,153,state.AutoSowEnabled?"自动播种  开":"自动播种  暂停","depot-toggle",true,false);
            ModalButton(g,1006,97,40,"×","close",true,false);
            Plate(g,226,172,375,422,Col("f4ebd2"),Col("cfc19d"),10);
            Plate(g,615,172,436,422,Col("f9f3e0"),Col("cfc19d"),10);
            DrawDepotBackpack(g);
            DrawDepotLedger(g);
            string footer=T<depotNoticeUntil?depotNotice:
                state.AutoSowEnabled?"已启用自动播种，空田和仓内种子齐备后，播种无人机将到仓领取。":
                "自动播种已暂停，尚未播下的种子会由无人机送回种子仓。";
            TextAt(g,footer,14,Col("56694e"),232,600,805,33,T<depotNoticeUntil);
        }
        static string DepotShortCount(int n)
        {
            if(n>=100000000)return (n/100000000.0).ToString("0.#")+"亿";
            if(n>=100000)return (n/10000.0).ToString("0.#")+"万";
            return n.ToString();
        }
        void DrawDepotBackpack(Graphics g)
        {
            TextAt(g,"从背包投种",21,ink,243,184,245,35,true);
            ModalButton(g,244,229,163,"普通种子","depot-normal",true,!depotPremium);
            ModalButton(g,416,229,165,"优质种子","depot-premium",true,depotPremium);
            for(int slot=0;slot<FarmState.CropOrder.Length;slot++){
                int id=FarmState.CropOrder[slot],x=244+slot%3*114,y=277+slot/3*66;
                string action="depot-crop-"+id;bool chosen=id==depotCrop;
                Plate(g,x,y,107,58,chosen?Col("e3e9c9"):Col("fbf5e1"),
                    chosen?Col("63835d"):hover==action?Col("a69870"):Col("cfc3a3"),7);
                FitSprite(g,depotPremium?premiumPackets[id]:art.SeedPackets[id],x+5,y+7,34,42);
                TextAt(g,Crop.All[id].Name,15,ink,x+41,y+4,64,24,true);
                TextAt(g,"× "+DepotShortCount(DepotBackpack(id)),12,Col("64715c"),x+41,y+29,65,21);
                HitBox(x,y,107,58,action);
            }
            g.FillRectangle(B(Col("d1c6a8")),244,415,338,1);
            int held=DepotBackpack(depotCrop);
            depotQuantity=Math.Max(1,Math.Min(depotQuantity,held));
            string name=Crop.All[depotCrop].Name+(depotPremium?"优种":"普通种");
            TextAt(g,name+"  ·  背包 "+held+" 袋",15,ink,244,421,341,28,true);
            ModalButton(g,244,460,41,"−","depot-less",depotQuantity>1,false);
            TextAt(g,held==0?"0 袋":depotQuantity+" 袋",depotQuantity>999999?14:19,ink,288,455,111,46,true,true);
            ModalButton(g,400,460,42,"+","depot-more",depotQuantity<held,false);
            ModalButton(g,449,460,60,"+10","depot-ten",depotQuantity<held,false);
            ModalButton(g,516,460,66,"全部","depot-all",depotQuantity<held,false);
            ModalButton(g,244,532,338,"按此数量投仓","depot-send",held>=depotQuantity);
        }
        void DrawDepotLedger(Graphics g)
        {
            TextAt(g,"投放队列",21,ink,633,184,299,35,true);
            TextAt(g,"在仓 "+depotStored+" 袋     ·     在途 "+depotAirborne+" 袋",16,Col("536843"),634,221,394,31,true);
            g.FillRectangle(B(Col("d5c9ab")),633,259,398,1);
            TextAt(g,"先后",13,Col("6c745f"),635,265,48,23);
            TextAt(g,"种子批次",13,Col("6c745f"),688,265,123,23);
            TextAt(g,"在仓 / 袋",13,Col("6c745f"),829,265,90,23,false,true);
            TextAt(g,"在途 / 袋",13,Col("6c745f"),928,265,90,23,false,true);
            if(depotLedger.Count==0){
                FitSprite(g,art.SeedPackets[0],793,323,60,67);
                TextAt(g,"尚无待播种的批次",17,Col("59704f"),643,399,380,30,true,true);
                TextAt(g,"从左侧选择种子并投仓。",14,Col("707961"),643,431,380,26,false,true);
            }
            for(int slot=0;slot<DepotPageSize;slot++){
                int index=depotPage*DepotPageSize+slot;
                if(index>=depotLedger.Count)break;
                DepotBatch b=depotLedger[index];int y=293+slot*52;
                Plate(g,633,y,399,46,index%2==0?Col("f2e9ce"):Col("f7efda"),Col("e2d6b6"),6);
                TextAt(g,(index+1).ToString(),14,Col("707962"),636,y,39,46,true,true);
                FitSprite(g,b.Premium?premiumPackets[b.Crop]:art.SeedPackets[b.Crop],678,y+4,29,38);
                TextAt(g,Crop.All[b.Crop].Name,16,ink,713,y+2,109,24,true);
                TextAt(g,b.Premium?"优质种子":"普通种子",12,b.Premium?Col("8a612e"):Col("647359"),714,y+24,105,17);
                TextAt(g,b.Stored.ToString(),b.Stored>999999?13:16,ink,829,y,90,46,true,true);
                TextAt(g,b.Airborne.ToString(),b.Airborne>999999?13:16,b.Airborne>0?Col("48716d"):Col("777b68"),928,y,90,46,true,true);
            }
            ModalButton(g,633,532,91,"上一页","depot-prev",depotPage>0,false);
            TextAt(g,(depotPage+1)+" / "+(DepotLastPage+1)+" 页",15,ink,734,532,194,36,false,true);
            ModalButton(g,939,532,93,"下一页","depot-next",depotPage<DepotLastPage,false);
        }
        bool HandleSeedDepotAction(string action)
        {
            if(action=="seed-depot-field" || action=="open-seed-depot"){
                if(!state.SeedWarehouseUnlocked)return true;
                if(action=="seed-depot-field" && (!InField || panel!=""))return true;
                depotPage=0;depotLedgerAt=-1;depotNoticeUntil=-1;
                CloseDrawer();tool="hand";
                if(action=="seed-depot-field")WalkTo(SeedDepotDoor,"seed-depot");
                else {StopWalking();panel="seed-depot";}
                return true;
            }
            if(panel!="seed-depot" || !action.StartsWith("depot-"))return false;
            if(!state.SeedWarehouseUnlocked)return true;
            if(action.StartsWith("depot-crop-")){
                int id;
                if(int.TryParse(action.Substring(11),out id) && id>=0 && id<Crop.All.Length){
                    depotCrop=id;depotQuantity=1;
                }
                return true;
            }
            int available=DepotBackpack(depotCrop);
            switch(action){
                case "depot-normal":case "depot-premium":
                    depotPremium=action=="depot-premium";depotQuantity=1;break;
                case "depot-less":depotQuantity=Math.Max(1,depotQuantity-1);break;
                case "depot-more":case "depot-ten":
                    depotQuantity=(int)Math.Max(1,Math.Min((long)available,(long)depotQuantity+(action=="depot-ten"?10:1)));break;
                case "depot-all":depotQuantity=Math.Max(1,available);break;
                case "depot-send":
                    if(state.DepositSeeds(depotCrop,depotPremium,depotQuantity)){
                        depotNotice="已投仓  "+Crop.All[depotCrop].Name+(depotPremium?"优种":"普通种")+" × "+depotQuantity+" 袋";
                        depotNoticeUntil=T+4;depotQuantity=1;
                        depotLedgerAt=-1;RefreshDepotLedger();depotPage=DepotLastPage;Store();
                    }
                    break;
                case "depot-toggle":state.AutoSowEnabled=!state.AutoSowEnabled;depotNoticeUntil=-1;depotLedgerAt=-1;Store();break;
                case "depot-prev":ChangeDepotPage(-1);break;
                case "depot-next":ChangeDepotPage(1);break;
            }
            return true;
        }
    }
}
