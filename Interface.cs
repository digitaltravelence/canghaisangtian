using System;
using System.Drawing;
using System.Windows.Forms;

namespace MountainTide
{
    internal sealed partial class Game
    {
        Color Dark {get{return Color.FromArgb(234,40,55,42);}}
        Color Rim {get{return Col("b79b69");}}
        void DrawHud(Graphics g)
        {
            Plate(g,22,19,243,67,Dark,Rim,10);
            Sprite(g,Sea?art.Tools[6]:art.Plants[0,2],30,25,43,52);
            TextAt(g,Sea?"海上避难所":InField?"云岫农场":"山景农舍",24,cream,83,23,170,34,true);
            TextAt(g,(Sea?"2126 · 百年后":"2026 · 现代")+"    第 "+state.Day+" 天",13,Col("d4d4ad"),84,56,176,22);
            Plate(g,829,20,127,41,Dark,Rim,14);Sprite(g,art.Tools[5],840,27,27,27);
            TextAt(g,"现钱 "+state.Cash,16,cream,874,20,84,41,true);
            Plate(g,964,20,127,41,Dark,Rim,14);Sprite(g,art.Tools[6],975,27,27,27);
            TextAt(g,"星元 "+state.StarCredits,16,cream,1007,20,84,41,true);
            Plate(g,1099,20,43,41,Dark,Rim,16);TextAt(g,"?",22,gold,1099,18,43,41,true,true);HitBox(1099,20,43,41,"help");
            if(saveFailed){Plate(g,21,94,233,31,Col("814d3b"),Rim);TextAt(g,"存档写入失败，请检查磁盘",14,cream,26,94,222,31,false,true);}
            if(panel=="")DrawFarmControls(g);
        }
        void ModalButton(Graphics g,int x,int y,int w,string label,string action,bool enabled=true,bool primary=true)
        {
            Color bg=!enabled?Col("d5d1bb"):primary?(hover==action?Col("567b51"):Col("416442")):(hover==action?Col("ece3c4"):Col("e2d9bb"));
            Plate(g,x,y,w,36,bg,enabled?(primary?Col("304f33"):Col("aba184")):Col("bcb9a4"),5);
            TextAt(g,label,14,enabled?(primary?cream:ink):Col("777765"),x,y,w,36,true,true);HitBox(x,y,w,36,action,enabled);
        }
        void Row(Graphics g,int y,Image im,string name,string description)
        {
            g.FillRectangle(B(Col("e5ddc3")),380,y+56,520,1);
            Sprite(g,im,386,y+4,40,44);TextAt(g,name,17,ink,442,y,430,28,true);
            TextAt(g,description,14,Col("62705b"),442,y+25,452,23);
        }
        void DrawPanel(Graphics g)
        {
            g.FillRectangle(B(Color.FromArgb(104,20,29,23)),0,0,W,H);
            if(panel=="settings"){DrawSoundSettings(g);return;}
            const int x=351,y=140,w=578,h=427;
            Plate(g,x+4,y+6,w,h,Color.FromArgb(135,33,38,28),Color.FromArgb(130,33,38,28),13);
            Plate(g,x,y,w,h,Col("faf2da"),Col("8b7851"),12);
            string name="",intro="";
            switch(panel){
                case "grain":name="山村粮站";intro="掌柜把秤砣摆上桌。今天收什么粮？";break;
                case "supply":name="青芽种子铺";intro="一袋种子种一块田。化肥能让作物提前 20 秒成熟。";break;
                case "harbor":name="码头收购摊";intro="“真是从土里长出来的？这批粮，我们收了。”";break;
                case "tech":name="科技工坊";intro="选好设备后带回山村，设备会自动在田间工作。";break;
                case "sale":name=Sea?"码头交货":"粮站过秤";intro="可以分批卖，余下的收成会留在背包里。";break;


                case "bed":name="床边休息";intro="窗外就是你的地。睡一觉，作物也会长大。";break;
                case "help":name="田间小记";intro="翻地时，我捡到一只旧怀表。拧动表冠，山谷里竟有了潮声。";break;
            }
            TextAt(g,name,26,ink,382,156,449,40,true);TextAt(g,intro,15,Col("62705b"),384,204,520,30);
            ModalButton(g,868,164,38,"×","close",true,false);
            if(panel=="grain"||panel=="harbor"){
                for(int i=0;i<3;i++){int top=251+i*65;Crop c=Crop.All[i];int price=Sea?c.StarPrice:c.CashPrice;
                    Row(g,top,art.Plants[i,2],c.Name,price+(Sea?" 星元":" 现钱")+" / 份     背包 "+state.Stock[i]+" 份");
                    ModalButton(g,793,top+5,103,"出售","sale"+i,state.Stock[i]>0);}
                TextAt(g,Sea?"换到的星元可以在栈桥另一头的工坊使用。":"本次收购结算现钱。种子和化肥在绿棚顶的种子铺出售。",14,Col("66765e"),385,489,505,38);
            }else if(panel=="supply"){
                for(int i=0;i<4;i++){int top=239+i*58;int cost=i==3?4:Crop.All[i].SeedCost;string item=i==3?"化肥":Crop.All[i].Name+"种子";
                    Row(g,top,i==3?art.Tools[2]:art.Plants[i,2],item,"余 "+(i==3?state.Fertilizer:state.Seeds[i])+" 袋"+(i<3?"   ·   "+Crop.All[i].Seconds+" 秒成熟":""));
                    ModalButton(g,793,top+4,104,cost+" 现钱","buy"+i,state.Cash>=cost);}
                TextAt(g,"持有现钱  "+state.Cash,16,ink,385,489,250,38,true);
                if(state.NeedsSeed())ModalButton(g,688,496,208,"领取一袋应急麦种","relief");
            }else if(panel=="tech"){
                for(int i=0;i<3;i++){int top=249+i*71;Row(g,top,art.Tools[7],FarmState.Machines[i],FarmState.Effects[i]);
                    ModalButton(g,789,top,108,state.MachinesOwned[i]?"已装备":FarmState.Costs[i]+" 星元","equip"+i,!state.MachinesOwned[i]&&state.StarCredits>=FarmState.Costs[i]);}
                TextAt(g,"持有星元  "+state.StarCredits,16,ink,385,491,345,35,true);
            }else if(panel=="sale"){
                Crop c=Crop.All[activeSale];int price=Sea?c.StarPrice:c.CashPrice;
                Row(g,253,art.Plants[activeSale,2],c.Name,"背包 "+state.Stock[activeSale]+" 份 · 每份 "+price+(Sea?" 星元":" 现钱"));
                ModalButton(g,411,345,56,"−","minus",saleQuantity>1,false);TextAt(g,saleQuantity+" 份",22,ink,470,339,100,49,true,true);
                ModalButton(g,570,345,56,"+","plus",saleQuantity<state.Stock[activeSale],false);
                ModalButton(g,644,345,78,"全部","all",true,false);ModalButton(g,737,345,78,"一份","one",true,false);
                TextAt(g,"本次可得   "+(price*saleQuantity)+(Sea?" 星元":" 现钱"),21,ink,385,421,480,43,true,true);
                ModalButton(g,714,493,183,"确认出售","confirmSale");
            }else if(panel=="bed"){
                Sprite(g,art.FarmerFrames[0,0],415,266,74,103);TextAt(g,"回家歇歇脚",23,ink,534,267,300,45,true);
                TextAt(g,"休息一天会让农场时间前进 90 秒。",16,ink,534,318,370,39);
                TextAt(g,"作物平时也会生长，关掉游戏后照常计时。",15,Col("64715d"),390,414,500,42,false,true);
                ModalButton(g,715,494,181,"休息到明天","sleep");
            }else{
                string[] lines={"用铲子开田，前五块免费。第六块 500 现钱，此后每块多 500。","打开种袋，选种子，再逐格点击空田播种。右键放下。","水壶（W）每茬可浇一次，提前 "+FarmState.WaterBonusSeconds+" 秒成熟。","到山村粮站卖收成，到青芽种子铺买种子和化肥。","拨动怀表去百年后的避难所，码头收粮，工坊卖设备。","B 背包，T 穿越，Esc 收起，F11 全屏。进度自动保存。"};
                for(int i=0;i<lines.Length;i++)TextAt(g,lines[i],15,ink,387,252+i*40,512,38);
                ModalButton(g,740,494,157,"回到游戏","close");
            }
        }
        internal void Action(string a)
        {
            if(a=="settings"){
                if(panel=="settings")CloseSoundSettings();
                else {settingsReturnPanel=panel;StopWalking();panel="settings";}
                return;
            }
            if(panel=="settings"){SoundSettingsAction(a);return;}
            if(a=="close"){panel="";CloseDrawer();return;}
            if(a=="watch"){Travel();return;}
            if((a=="leaveHouse"||a=="bed")&&scene==Scene.House){
                CloseDrawer();WalkTo(a=="bed"?BesideBed:InsideDoor,a);return;
            }
            if(DrawerAction(a))return;
            if(a=="help"){StopWalking();CloseDrawer();panel=a;return;}
            if(a=="home"||a=="grain"||a=="harbor"||a=="tech"||a=="supply"){
                if(scene==Scene.House||Sea!=(a=="tech"||a=="harbor"))return;
                PointF door=Sea?(a=="tech"?new PointF(1016,347):new PointF(342,344)):
                    a=="home"?HomeEntry:a=="supply"?SeedEntry:GrainEntry;
                CloseDrawer();tool="hand";WalkTo(door,a);return;
            }
            if(a.StartsWith("sale")&&(panel=="grain"||panel=="harbor")){
                activeSale=int.Parse(a.Substring(4));saleQuantity=state.Stock[activeSale];if(saleQuantity>0)panel="sale";return;
            }
            if(panel=="sale"){
                if(a=="minus")saleQuantity=Math.Max(1,saleQuantity-1);
                if(a=="plus")saleQuantity=Math.Min(state.Stock[activeSale],saleQuantity+1);
                if(a=="one")saleQuantity=1;if(a=="all")saleQuantity=state.Stock[activeSale];
                if(a=="confirmSale"){int earned=state.Sell(Sea,activeSale,saleQuantity);Store();Tell("售出 "+saleQuantity+" 份"+Crop.All[activeSale].Name+"，获得 "+earned+(Sea?" 星元。":" 现钱。"));panel="";}return;
            }
            if(a.StartsWith("buy")&&!Sea&&panel=="supply"){
                int id=int.Parse(a.Substring(3));if(state.Buy(id))Store();return;
            }
            if(a=="relief"&&!Sea&&panel=="supply"&&state.NeedsSeed()){state.Seeds[0]++;Store();return;}
            if(a.StartsWith("equip")&&Sea&&panel=="tech"){
                int id=int.Parse(a.Substring(5));if(state.Equip(id)){Store();Tell("已购入"+FarmState.Machines[id]+"。");}return;
            }
            if(a=="sleep"&&scene==Scene.House&&panel=="bed"){state.TimeBonus+=90;state.Automate(state.Now);Store();panel="";Tell("第 "+state.Day+" 天清晨，去看看田里的收成吧。");}
        }
        void Tend(int i)
        {
            if(Sea)return;
            Plot p=state.Plots[i];PointF b=Bed(i);flip=b.X<player.X;facingAway=false;
            string work=tool;tool="hand";pickTime=T;
            if(work=="hand"&&p.CropId>=0&&state.Left(i,state.Now)==0){
                int crop=p.CropId;
                int n=state.Harvest(i,state.Now);
                if(n>0){
                    Store();audio.Play(crop==0?FarmSound.Reap:FarmSound.Pick);
                    Float("+"+n+" "+Crop.All[crop].Name,b);Tell("收成已装进背包。");
                }
                return;
            }
            if(work=="water"){
                tool="water";
                if(state.Water(i,state.Now)){
                    Store();audio.Play(FarmSound.Water);Float("已浇水",b);
                }else Tell("这株作物现在无需浇水。");
                return;
            }
            if(work=="feed"&&state.Feed(i,state.Now)){
                Store();audio.Play(FarmSound.Fertilize);Float("生长加快",b);return;
            }
            SelectCell(i);
        }
    }
}
