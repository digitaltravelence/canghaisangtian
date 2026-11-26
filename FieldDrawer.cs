using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MountainTide
{
    internal sealed partial class Game
    {
        string drawer="";
        int clearCrop=-1;
        long clearPlanted;

        void CloseDrawer(){drawer="";activePlot=-1;clearCrop=-1;}
        void OpenDrawer(string page,int cell){
            StopWalking();tool="hand";drawer=page;activePlot=cell;clearCrop=-1;
        }
        void SelectCell(int i){
            Plot p=state.Plots[i];
            OpenDrawer(p.Opened&&p.CropId<0?"seeds":"tools",i);
        }
        void WorkAt(int i,string work){
            PointF b=Bed(i);CloseDrawer();tool=work;
            WalkTo(new PointF(b.X+(player.X<b.X?-52:52),b.Y+16),"plot"+i);
        }
        void UseShovel(int i){
            Plot p=state.Plots[i];
            if(!p.Opened){
                if(state.Till(i))FinishFieldClick(i,"荒地已开垦");
                else Tell("五块地已全部开垦。");return;
            }
            if(p.CropId>=0){
                OpenDrawer("tools",i);clearCrop=p.CropId;clearPlanted=p.Planted;return;
            }
            Tell("这块田已经开垦，可以选种子播种。");
        }
        void FinishFieldClick(int i,string result){
            // Commit before walking: another click may redirect the farmer immediately.
            PointF b=Bed(i);Store();Float(result,b);WalkTo(new PointF(b.X+(player.X<b.X?-48:48),b.Y+15),"");
        }
        void EquipTool(string name){
            StopWalking();CloseDrawer();tool=name;msgEnd=0;UpdatePointer();
        }
        bool DrawerAction(string a){
            switch(a){
                case "drawer-bg":return true;
                case "fold":CloseDrawer();tool="hand";return true;
                case "hand":case "feedtool":case "tools":OpenDrawer("tools",activePlot);return true;
                case "seeds":OpenDrawer("seeds",activePlot);return true;
                case "bag":case "stock":OpenDrawer("stock",activePlot);return true;
                case "machines":OpenDrawer("machines",activePlot);return true;
                case "keep":EquipTool("shovel");return true;
                case "remove":
                    if(!Sea&&activePlot>=0&&clearCrop>=0&&state.Plots[activePlot].CropId==clearCrop&&state.Plots[activePlot].Planted==clearPlanted){
                        int i=activePlot;EquipTool("shovel");if(state.Uproot(i))FinishFieldClick(i,"作物已铲除");
                    }
                    else {clearCrop=-1;Tell("这格作物已变化，请重新选择。");}return true;
            }
            if(a.StartsWith("plot")){
                if(!InField)return true;
                int i=int.Parse(a.Substring(4));Plot p=state.Plots[i];
                if(drawer==""&&tool=="shovel"){UseShovel(i);return true;}
                if(drawer==""&&tool=="seed"){
                    if(state.Sow(i,selectedSeed,state.Now))FinishFieldClick(i,Crop.All[selectedSeed].Name+"已播种");
                    else Tell(!p.Opened?"请先用铲子开垦这格。":p.CropId>=0?"这格已经种了"+Crop.All[p.CropId].Name+"。":Crop.All[selectedSeed].Name+"种子用完了，可去种子铺补货。");
                    return true;
                }
                if(drawer==""&&tool=="feed"&&p.CropId>=0&&state.Left(i,state.Now)>0&&state.Fertilizer>0){WorkAt(i,"feed");return true;}
                if(drawer==""&&tool=="hand"&&p.CropId>=0&&state.Left(i,state.Now)==0){WorkAt(i,"hand");return true;}
                SelectCell(i);PointF b=Bed(i);WalkTo(new PointF(b.X+(player.X<b.X?-52:52),b.Y+16),"");return true;
            }
            if(!a.StartsWith("use-"))return false;
            if(!InField)return true;
            string work=a.Substring(4);
            if(work.StartsWith("seed")){
                selectedSeed=int.Parse(work.Substring(4));if(state.Seeds[selectedSeed]==0)return true;
                work="seed";
            }
            if(work=="shovel"||work=="seed"||activePlot<0){
                EquipTool(work);
                Tell(work=="shovel"?"铲子已选好，点草地开垦，点作物可铲除。":work=="seed"?"点击已开垦的空田播种。":work=="feed"?"点击生长中的作物施肥。":"点击成熟作物采收。");return true;
            }
            WorkAt(activePlot,work);
            return true;
        }

        void DrawFarmControls(Graphics g){
            if(drawer!=""){DrawDrawer(g);return;}
            string hint="";
            if(heldCrop!=""&&heldCrop==hover)hint="松开采摘 · 移开鼠标可取消";
            else if(T<msgEnd)hint=message;
            else if(Sea)hint="码头收作物，工坊卖设备";
            else if(scene==Scene.House)hint="点床休息 · 点红门回到农场";
            else if(tool=="shovel")hint="铲子 · "+FarmState.CellCount+" 格草地可选，剩余 "+(FarmState.LandLimit-state.OpenCount)+" 个开垦名额 · 右键放下";
            else if(tool=="seed")hint=Crop.All[selectedSeed].Name+"种子 × "+state.Seeds[selectedSeed]+" · 点空田播种 · 右键放下";
            else if(tool=="feed")hint="已选化肥 · 点生长中的作物施肥";
            else if(state.OpenCount==0)hint="从 "+FarmState.CellCount+" 格草地中自选 "+FarmState.LandLimit+" 格 · 拿起铲子，点地开垦";
            else if(hover.StartsWith("plot")){
                int i=int.Parse(hover.Substring(4));Plot p=state.Plots[i];
                hint=!p.Opened?"点击草地选择位置":p.CropId<0?"空田 · 点击选种":Crop.All[p.CropId].Name+(state.Left(i,state.Now)==0?"成熟了 · 点击采收":" · "+state.Left(i,state.Now)+" 秒后成熟");
            }
            if(hint!=""){
                float w=Math.Min(680,g.MeasureString(hint,F(15,false)).Width+34);
                Plate(g,(W-w)/2,593,w,30,Dark,Rim,14);TextAt(g,hint,15,cream,(W-w)/2,593,w,30,false,true);
            }
            string[] acts=InField?new[]{"use-shovel","seeds","feedtool","stock","watch"}:new[]{"stock","watch"};
            string[] labels=InField?new[]{"铲子","种袋","化肥","背包","时空怀表"}:new[]{"背包",Sea?"回到农场":"时空怀表"};
            int[] icons=InField?new[]{-1,1,2,3,4}:new[]{3,4};
            int width=acts.Length*78+16,start=(W-width)/2;
            Plate(g,start+2,635,width,72,Col("785839"),Col("795b3b"),10);
            Plate(g,start,630,width,72,Col("8d6944"),Col("4e3b2b"),10);
            for(int i=0;i<acts.Length;i++){
                int x=start+8+i*78;bool on=hover==acts[i]||!Sea&&(i==0&&tool=="shovel"||i==1&&tool=="seed"||i==2&&tool=="feed");
                Plate(g,x,636,72,61,on?Col("ffe6a4"):Col("efcf92"),Col("795733"),6);
                if(icons[i]<0)DrawSpade(g,x+18,638,36);else Sprite(g,art.Tools[icons[i]],x+18,638,36,36);
                TextAt(g,labels[i],14,ink,x,674,72,20,true,true);HitBox(x,636,72,61,acts[i]);
            }
        }
        void DrawSpade(Graphics g,float x,float y,float size){
            GraphicsState old=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(size/48,size/48);
            using(Pen edge=new Pen(Col("583d29"),5.5f))using(Pen grip=new Pen(Col("d69b50"),3)){
                edge.StartCap=edge.EndCap=LineCap.Round;grip.StartCap=grip.EndCap=LineCap.Round;
                g.DrawLine(edge,17,31,34,11);g.DrawLine(grip,17,31,34,11);
                PointF[] handle={new PointF(30,8),new PointF(36,3),new PointF(43,8),new PointF(36,15),new PointF(30,8)};
                g.DrawLines(edge,handle);g.DrawLines(grip,handle);
            }
            PointF[] blade={new PointF(12,23),new PointF(25,32),new PointF(18,40),new PointF(6,44),new PointF(5,35)};
            g.FillPolygon(B(Col("acc6c3")),blade);using(Pen line=new Pen(Col("4a5551"),2))g.DrawPolygon(line,blade);
            using(Pen shine=new Pen(Col("eaf6e4"),2))g.DrawLine(shine,15,29,8,39);g.Restore(old);
        }
        void DrawDrawer(Graphics g){
            const int top=568;
            HitBox(22,top,1236,144,"drawer-bg");
            Plate(g,25,top+5,1230,138,Col("735136"),Col("4f3929"),12);
            Plate(g,22,top,1236,139,Col("b6834d"),Col("654329"),12);
            Plate(g,32,top+29,1216,102,Col("fff3d6"),Col("906b40"),9);
            string[] tabs=InField?new[]{"tools","seeds","stock","machines"}:new[]{"stock","machines","seeds"};
            int x=39;
            foreach(string tab in tabs){
                bool on=tab==drawer;string label=tab=="tools"?"农具":tab=="seeds"?"种子":tab=="stock"?"农作物":"设备";
                int icon=tab=="seeds"?1:tab=="stock"?3:7;
                Plate(g,x,top-4,101,35,on?Col("ffebba"):Col("d9ac72"),Col("73502e"),7);
                if(tab=="tools")DrawSpade(g,x+12,top-1,24);else Sprite(g,art.Tools[icon],x+11,top,23,23);
                TextAt(g,label,16,Col("513e2c"),x+38,top-3,61,31,true);
                HitBox(x,top-4,101,35,tab);x+=105;
            }
            TextAt(g,InField?"开垦名额   "+(FarmState.LandLimit-state.OpenCount)+" / "+FarmState.LandLimit+" 可用":"随身背包",15,Col("382d21"),824,top-1,280,28,true);
            TrayButton(g,1140,top,100,25,"收起  ▾","fold");
            Plot cell=activePlot<0?null:state.Plots[activePlot];
            string title=Sea?"海上行囊":cell==null?"随身行囊":!cell.Opened?"未开垦草地":cell.CropId<0?"空田":Crop.All[cell.CropId].Name;
            string sub=cell==null?"选择下列物品":!cell.Opened?(state.OpenCount<5?"可在这里开一块田":"开垦名额已用完"):cell.CropId<0?"已开垦 · 可以播种":state.Left(activePlot,state.Now)==0?"已成熟 · 可以采收":"生长中 · 剩 "+state.Left(activePlot,state.Now)+" 秒";
            Sprite(g,art.FarmerFrames[0,0],49,top+39,50,77);
            TextAt(g,title,19,ink,110,top+45,160,30,true);TextAt(g,sub,13,Col("796748"),110,top+80,154,27);
            using(Pen sep=new Pen(Col("d5bd8d")))g.DrawLine(sep,270,top+44,270,top+115);
            if(clearCrop>=0){
                TextAt(g,"铲除这株"+Crop.All[clearCrop].Name+"？",19,ink,295,top+42,450,34,true);
                TextAt(g,"这茬作物将被移除，原地可以直接改种。",15,Col("785f45"),295,top+76,496,28);
                TrayButton(g,833,top+60,167,40,"确认铲除","remove");TrayButton(g,1012,top+60,163,40,"保留作物","keep");return;
            }
            string detail="",guide="";
            if(drawer=="tools"){
                bool wild=cell!=null&&!cell.Opened,planted=cell!=null&&cell.CropId>=0;
                bool ripe=planted&&state.Left(activePlot,state.Now)==0;
                Card(g,294,null,"铲子","","use-shovel",true);
                Card(g,426,art.Tools[0],"采收","","use-hand",cell==null||ripe);
                Card(g,558,art.Tools[2],"化肥",state.Fertilizer.ToString(),"use-feed",state.Fertilizer>0&&(cell==null||planted&&!ripe));
                detail=wild?"选择开垦位置":"田间农具";
                guide="拿起铲子，再点要开垦或铲苗的地块。";
            }else if(drawer=="seeds"){
                for(int i=0;i<3;i++)Card(g,294+i*132,art.SeedPackets[i],Crop.All[i].Name+"种子",state.Seeds[i].ToString(),"use-seed"+i,InField&&state.Seeds[i]>0);
                detail="种袋";guide=InField?"拿起一种种子，再依次点击要播种的空田。":Sea?"种子可以带回山村播种。":"出门到田里播种。";
            }else if(drawer=="stock"){
                for(int i=0;i<3;i++)Card(g,294+i*132,art.Plants[i,2],Crop.All[i].Name,state.Stock[i].ToString(),"drawer-bg",true);
                detail="收成入袋";guide=Sea?"码头用星元收购农作物。":"粮站付现钱，未来的码头付星元。";
            }else{
                for(int i=0;i<3;i++)Card(g,294+i*132,art.Tools[7],FarmState.Machines[i],state.MachinesOwned[i]?"已装备":"未购入","drawer-bg",state.MachinesOwned[i]);
                detail="农田设备";guide="用星元在避难所的科技工坊购置。";
            }
            TextAt(g,detail,17,ink,737,top+44,457,30,true);
            TextAt(g,guide,14,Col("746044"),737,top+78,479,29);
        }
        void TrayButton(Graphics g,int x,int y,int w,int h,string label,string action){
            Plate(g,x,y,w,h,hover==action?Col("ffdf99"):Col("f1d193"),Col("87653d"),6);
            TextAt(g,label,14,ink,x,y,w,h,true,true);HitBox(x,y,w,h,action);
        }
        void Card(Graphics g,int x,Image img,string label,string count,string action,bool enabled){
            int y=605;Color edge=enabled&&hover==action&&action!="drawer-bg"?Col("a96b18"):Col("c39e5c");
            Plate(g,x+2,y+2,113,61,Col("e0cda3"),Col("e0cda3"),8);
            Plate(g,x,y,113,61,enabled?Col("fff9e9"):Col("eee7d6"),edge,8);
            if(img==null)DrawSpade(g,x+34,y+3,53);
            else {float factor=Math.Min(77f/img.Width,49f/img.Height);float w=img.Width*factor,h=img.Height*factor;Sprite(g,img,x+(113-w)/2,y+(56-h)/2,w,h);}
            if(count!=""){int ww=count.Length>2?52:28;Plate(g,x+111-ww,y+39,ww,20,enabled?Col("805432"):Col("817968"),Col("e7c681"),6);
                TextAt(g,count,12,cream,x+111-ww,y+39,ww,20,true,true);}
            TextAt(g,label,14,enabled?ink:Col("827b6c"),x-5,y+62,123,23,true,true);
            HitBox(x,y,113,87,enabled?action:"drawer-bg");
        }
    }
}
