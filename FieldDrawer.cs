using System;
using System.Drawing;

namespace MountainTide
{
    internal sealed partial class Game
    {
        string drawer="";
        int clearCrop=-1;
        long clearPlanted;

        void CloseDrawer(){drawer="";activePlot=-1;clearCrop=-1;}
        void OpenDrawer(string page,int cell){
            StopWalking();
            if(tool!="harvest")tool="hand";
            drawer=page;activePlot=cell;clearCrop=-1;
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
                if(state.NextPlotCost==0)TillPlot(i);
                else OpenDrawer("expand",i);
                return;
            }
            if(p.CropId>=0){
                OpenDrawer("tools",i);clearCrop=p.CropId;clearPlanted=p.Planted;return;
            }
            Tell("这块田已经开垦，可以选种子播种。");
        }
        void TillPlot(int i){
            int cost=state.NextPlotCost;
            if(!state.Till(i))return;
            audio.Play(FarmSound.Till);
            EquipTool("shovel");
            FinishFieldClick(i,cost==0?"荒地已开垦":"已开垦 · −"+cost+" 现钱");
        }
        void FinishFieldClick(int i,string result){
            // Commit before walking: another click may redirect the farmer immediately.
            PointF b=Bed(i);Store();Float(result,b);WalkTo(new PointF(b.X+(player.X<b.X?-48:48),b.Y+15),"");
        }
        void EquipTool(string name){
            StopWalking();CloseDrawer();tool=name;msgEnd=0;UpdatePointer();
        }
        void SowAlong(PointF end)
        {
            float dx=end.X-previousSeedPoint.X,dy=end.Y-previousSeedPoint.Y;
            int steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(dx),Math.Abs(dy))/10f));
            int lastCell=-1;
            for(int j=1;j<=steps&&SelectedSeedCount>0;j++){
                PointF point=new PointF(previousSeedPoint.X+dx*j/steps,previousSeedPoint.Y+dy*j/steps);
                int cell=GroundAt(point);
                if(cell<0||cell==lastCell)continue;
                if(!state.Plots[cell].Opened||state.Plots[cell].CropId>=0)continue;
                string target="plot"+cell;
                if(FindAction(point)!=target)continue;
                lastCell=cell;
                DrawerAction(target);
            }
            previousSeedPoint=end;
        }
        bool DrawerAction(string a){
            switch(a){
                case "drawer-bg":return true;
                case "fold":CloseDrawer();return true;
                case "hand":case "feedtool":case "tools":OpenDrawer("tools",activePlot);return true;
                case "seed-grade":browsePremium=!browsePremium;return true;
                case "seeds":OpenDrawer("seeds",activePlot);return true;
                case "bag":case "stock":OpenDrawer("stock",activePlot);return true;
                case "machines":OpenDrawer("machines",activePlot);return true;
                case "confirm-expand":
                    if(InField&&drawer=="expand"&&activePlot>=0)TillPlot(activePlot);
                    return true;
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
                if(p.CareExecuting){Tell("无人机正在照料这块田，完成后就能操作。");return true;}
                if(Ripe(a)){msgEnd=0;WorkAt(i,"harvest");return true;}
                if(TryHandleCarePlot(i))return true;
                if(drawer==""&&tool=="shovel"){UseShovel(i);return true;}
                if(drawer==""&&tool=="seed"){
                    if(state.Sow(i,selectedSeed,state.Now,selectedPremium)){
                        audio.Play(FarmSound.Sow);
                        FinishFieldClick(i,(selectedPremium?"优质":"")+Crop.All[selectedSeed].Name+"已播种");
                    }
                    else Tell(!p.Opened?"请先用铲子开垦这格。":p.CropId>=0?"这格已经种了"+Crop.All[p.CropId].Name+"。":Crop.All[selectedSeed].Name+"种子用完了，可去种子铺补货。");
                    return true;
                }
                if(drawer==""&&tool=="feed"){
                    if(state.Fertilizer<1)Tell("化肥用完了，可以到种子铺补货。");
                    else if(p.CropId<0||state.Left(i,state.Now)==0)Tell("请点一株正在生长的作物施肥。");
                    else WorkAt(i,"feed");
                    return true;
                }
                if(drawer==""&&tool=="water"){
                    if(p.CropId<0)Tell("请点一株生长中的作物浇水。");
                    else Tell("等这块田出现水滴气泡后再浇水。");
                    return true;
                }
                SelectCell(i);PointF b=Bed(i);WalkTo(new PointF(b.X+(player.X<b.X?-52:52),b.Y+16),"");return true;
            }
            if(!a.StartsWith("use-"))return false;
            if(!InField)return true;
            string work=a.Substring(4);
            if(work=="hand")work="harvest";
            if(work.StartsWith("seed")){
                selectedSeed=int.Parse(work.Substring(4));selectedPremium=browsePremium;if(SelectedSeedCount==0)return true;
                work="seed";
            }
            if(work=="water"||work=="feed"||work=="shovel"||work=="seed"||activePlot<0){
                EquipTool(work);
                Tell(work=="shovel"?"点草地开垦，点作物可铲除。":work=="seed"?"点空田或按住拖动播种。":work=="feed"?"点生长中的作物施肥。":work=="water"?"点出现水滴的作物浇水。":"点击成熟作物采收。");
                return true;
            }
            WorkAt(activePlot,work);
            return true;
        }

        void DrawFarmControls(Graphics g){
            if(drawer!=""){DrawDrawer(g);return;}
            string hint="";
            if(heldCrop!=""&&heldCrop==hover)hint="松开采摘 · 移开鼠标可取消";
            else if(tool!="hand"&&tool!="harvest"&&Ripe(hover))hint="作物已成熟 · 点击采收";
            else if(T<msgEnd)hint=message;
            else if(Sea)hint="左侧码头卖粮换星元 · 右侧工坊添置设备";
            else if(scene==Scene.House)hint="点床休息 · 点红门回到农场";
            else if(tool=="shovel"){
                int opened=state.OpenCount;
                string land=opened==FarmState.CellCount?"全部土地已开垦":
                    opened<FarmState.FreePlotCount?"还可免费开垦 "+(FarmState.FreePlotCount-opened)+" 块":"下一块 "+state.NextPlotCost+" 现钱";
                hint="铲子 · "+land+" · 右键放下";
            }
            else if(tool=="seed")hint=(selectedPremium?"优质":"")+Crop.All[selectedSeed].Name+"种子 × "+SelectedSeedCount+" · 点击或按住划过空田播种 · 右键放下";
            else if(tool=="feed")hint="已选化肥 · 点生长中的作物施肥";
            else if(tool=="water")hint="水壶 · 点出现水滴的作物 · 右键放下";
            else if(state.OpenCount==0)hint="拿起铲子，点击草地 · 前 "+FarmState.FreePlotCount+" 块免费开垦";
            else if(hover.StartsWith("plot")){
                int i=int.Parse(hover.Substring(4));Plot p=state.Plots[i];
                hint=!p.Opened?"点击草地选择位置":p.CropId<0?"空田 · 点击选种":(p.Premium?"优质":"")+Crop.All[p.CropId].Name+(state.Left(i,state.Now)==0?"已成熟 · 点击采收":" · "+state.Left(i,state.Now)+" 秒后成熟")+(p.Premium?" · 基础产量 "+state.BaseYield(p)+" 份":"");
            }
            if(hint!=""){
                float w=Math.Min(680,g.MeasureString(hint,F(15,false)).Width+34);
                Plate(g,(W-w)/2,593,w,30,Dark,Rim,14);TextAt(g,hint,15,cream,(W-w)/2,593,w,30,false,true);
            }
            string[] acts=InField?new[]{"use-shovel","seeds","use-water","use-feed","stock","watch"}:new[]{"stock","watch"};
            string[] labels=InField?new[]{"铲子","种袋","水壶","化肥","背包","时空怀表"}:new[]{"背包",Sea?"回到农场":"时空怀表"};
            Image[] icons=InField?new[]{art.Spade,art.Tools[1],art.WateringCan,art.Tools[2],art.Tools[3],art.Tools[4]}:new[]{art.Tools[3],art.Tools[4]};
            int width=acts.Length*78+16,start=(W-width)/2;
            Plate(g,start+2,635,width,72,Col("785839"),Col("795b3b"),10);
            Plate(g,start,630,width,72,Col("8d6944"),Col("4e3b2b"),10);
            for(int i=0;i<acts.Length;i++){
                int x=start+8+i*78;bool on=hover==acts[i]||InField&&(acts[i]=="use-shovel"&&tool=="shovel"||acts[i]=="seeds"&&tool=="seed"||acts[i]=="use-water"&&tool=="water"||acts[i]=="use-feed"&&tool=="feed");
                Plate(g,x,636,72,61,on?Col("ffe6a4"):Col("efcf92"),Col("795733"),6);
                Sprite(g,icons[i],x+18,638,36,36);
                TextAt(g,labels[i],14,ink,x,674,72,20,true,true);HitBox(x,636,72,61,acts[i]);
            }
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
                Image icon=tab=="tools"?art.Spade:art.Tools[tab=="seeds"?1:tab=="stock"?3:7];
                Plate(g,x,top-4,101,35,on?Col("ffebba"):Col("d9ac72"),Col("73502e"),7);
                Sprite(g,icon,x+11,top,23,23);
                TextAt(g,label,16,Col("513e2c"),x+38,top-3,61,31,true);
                HitBox(x,top-4,101,35,tab);x+=105;
            }
            TextAt(g,InField?"已开垦   "+state.OpenCount+" / "+FarmState.CellCount+" 块":"随身背包",15,Col("382d21"),824,top-1,280,28,true);
            TrayButton(g,1140,top,100,25,"收起  ▾","fold");
            Plot cell=activePlot<0?null:state.Plots[activePlot];
            string title=Sea?"海上行囊":cell==null?"随身行囊":!cell.Opened?"未开垦草地":cell.CropId<0?"空田":(cell.Premium?"优质":"")+Crop.All[cell.CropId].Name;
            string sub=cell==null?"选择下列物品":!cell.Opened?(state.NextPlotCost==0?"免费开垦":"开垦需 "+state.NextPlotCost+" 现钱"):cell.CropId<0?"已开垦 · 可以播种":state.Left(activePlot,state.Now)==0?"已成熟 · 可以采收":"生长中 · 剩 "+state.Left(activePlot,state.Now)+" 秒";
            Sprite(g,art.FarmerFrames[0,0],49,top+39,50,77);
            TextAt(g,title,19,ink,110,top+45,160,30,true);TextAt(g,sub,13,Col("796748"),110,top+80,154,27);
            using(Pen sep=new Pen(Col("d5bd8d")))g.DrawLine(sep,270,top+44,270,top+115);
            if(drawer=="expand"){
                int cost=state.NextPlotCost;
                bool canPay=state.Cash>=cost;
                TextAt(g,"开垦第 "+(state.OpenCount+1)+" 块田，需要 "+cost+" 现钱",19,ink,295,top+42,525,34,true);
                string balance="持有 "+state.Cash+" 现钱"+(canPay?" · 开垦后剩余 "+(state.Cash-cost):" · 还差 "+(cost-state.Cash));
                TextAt(g,balance,15,Col("785f45"),295,top+76,525,28);
                TrayButton(g,833,top+60,167,40,canPay?"确认开垦":"现钱不足","confirm-expand",canPay);
                TrayButton(g,1012,top+60,163,40,"取消","keep");return;
            }
            if(clearCrop>=0){
                TextAt(g,"铲除这株"+Crop.All[clearCrop].Name+"？",19,ink,295,top+42,450,34,true);
                TextAt(g,"这茬作物将被移除，原地可以直接改种。",15,Col("785f45"),295,top+76,496,28);
                TrayButton(g,833,top+60,167,40,"确认铲除","remove");TrayButton(g,1012,top+60,163,40,"保留作物","keep");return;
            }
            string detail="",guide="";
            if(drawer=="tools"){
                bool wild=cell!=null&&!cell.Opened,planted=cell!=null&&cell.CropId>=0;
                bool ripe=planted&&state.Left(activePlot,state.Now)==0;
                Card(g,294,art.Spade,"铲子","","use-shovel",true);
                Card(g,426,art.Tools[0],cell!=null&&cell.CropId==0?"收割":"采收","","use-hand",cell==null||ripe);
                Card(g,558,art.Tools[2],"化肥",state.Fertilizer.ToString(),"use-feed",state.Fertilizer>0&&(cell==null||planted&&!ripe));
                Card(g,690,art.WateringCan,"浇水","","use-water",cell==null||state.CanWater(activePlot,state.Now));
                detail=wild?"选择开垦位置":"田间农具";
                guide="拿起铲子，再点要开垦或铲苗的地块。";
            }else if(drawer=="seeds"){
                for(int slot=0;slot<FarmState.CropOrder.Length;slot++){
                    int i=FarmState.CropOrder[slot];
                    int count=(browsePremium?state.PremiumSeeds:state.Seeds)[i];
                    Card(g,286+slot*125,browsePremium?premiumPackets[i]:art.SeedPackets[i],Crop.All[i].Name+"种子",count.ToString(),"use-seed"+i,InField&&count>0);
                }
                detail=browsePremium?"优质种子":"普通种子";guide="";
                TrayButton(g,922,top+82,194,29,browsePremium?"查看普通种子":"查看优质种子","seed-grade");
            }else if(drawer=="stock"){
                for(int slot=0;slot<FarmState.CropOrder.Length;slot++){
                    int i=FarmState.CropOrder[slot];
                    Card(g,286+slot*125,art.Plants[i,2],Crop.All[i].Name,state.Stock[i].ToString(),"drawer-bg",true);
                }
                detail="收成入袋";guide=Sea?"码头用星元收购农作物。":"粮站付现钱，未来的码头付星元。";
            }else{
                DrawEquipmentStrip(g,top);
                return;
            }
            int detailX=drawer=="seeds"||drawer=="stock"?923:828;
            TextAt(g,detail,17,ink,detailX,top+44,1205-detailX,30,true);
            TextAt(g,guide,14,Col("746044"),detailX,top+78,1205-detailX,29);
        }
        void TrayButton(Graphics g,int x,int y,int w,int h,string label,string action,bool enabled=true){
            Color fill=!enabled?Col("e0d6bd"):hover==action?Col("ffdf99"):Col("f1d193");
            Plate(g,x,y,w,h,fill,Col("87653d"),6);
            TextAt(g,label,14,enabled?ink:Col("777765"),x,y,w,h,true,true);HitBox(x,y,w,h,enabled?action:"drawer-bg");
        }
        void Card(Graphics g,int x,Image img,string label,string count,string action,bool enabled){
            int y=605;Color edge=enabled&&hover==action&&action!="drawer-bg"?Col("a96b18"):Col("c39e5c");
            Plate(g,x+2,y+2,113,61,Col("e0cda3"),Col("e0cda3"),8);
            Plate(g,x,y,113,61,enabled?Col("fff9e9"):Col("eee7d6"),edge,8);
            float factor=Math.Min(77f/img.Width,49f/img.Height);
            float w=img.Width*factor,h=img.Height*factor;
            Sprite(g,img,x+(113-w)/2,y+(56-h)/2,w,h);
            if(count!=""){int ww=count.Length>2?52:28;Plate(g,x+111-ww,y+39,ww,20,enabled?Col("805432"):Col("817968"),Col("e7c681"),6);
                TextAt(g,count,12,cream,x+111-ww,y+39,ww,20,true,true);}
            TextAt(g,label,14,enabled?ink:Col("827b6c"),x-5,y+62,123,23,true,true);
            HitBox(x,y,113,87,enabled?action:"drawer-bg");
        }
    }
}
