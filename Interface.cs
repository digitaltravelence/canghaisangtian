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
            if(panel==""&&!saveFailed)DrawProgress(g);
            if(panel=="")DrawFarmControls(g);
        }
        void ModalButton(Graphics g,int x,int y,int w,string label,string action,bool enabled=true,bool primary=true)
        {
            Color bg=!enabled?Col("d5d1bb"):primary?(hover==action?Col("567b51"):Col("416442")):(hover==action?Col("ece3c4"):Col("e2d9bb"));
            Plate(g,x,y,w,36,bg,enabled?(primary?Col("304f33"):Col("aba184")):Col("bcb9a4"),5);
            TextAt(g,label,14,enabled?(primary?cream:ink):Col("777765"),x,y,w,36,true,true);HitBox(x,y,w,36,action,enabled);
        }
        void DrawPanel(Graphics g)
        {
            g.FillRectangle(B(Color.FromArgb(104,20,29,23)),0,0,W,H);
            if(panel=="settings"){DrawSoundSettings(g);return;}
            if(panel=="seed-depot"){DrawSeedDepotPanel(g);return;}
            if(InTradeCatalog){
                DrawTradeCatalog(g);
                if(tradeItem>=0){hits.Clear();DrawTradeQuantity(g);}
                return;
            }
            if(panel=="tech"||panel=="automation"||panel=="cultivation"){DrawEquipmentPanel(g);return;}
            const int x=351,y=140,w=578,h=427;
            Plate(g,x+4,y+6,w,h,Color.FromArgb(135,33,38,28),Color.FromArgb(130,33,38,28),13);
            Plate(g,x,y,w,h,Col("faf2da"),Col("8b7851"),12);
            string name="",intro="";
            switch(panel){
                case "bed":name="床边休息";intro="窗外就是你的地。休息后，日历会翻到下一天。";break;
                case "help":name="田间小记";intro="翻地时，我捡到一只旧怀表。拧动表冠，山谷里竟有了潮声。";break;
            }
            TextAt(g,name,26,ink,382,156,449,40,true);TextAt(g,intro,15,Col("62705b"),384,204,520,30);
            ModalButton(g,868,164,38,"×","close",true,false);
            if(panel=="bed"){
                Sprite(g,art.FarmerFrames[0,0],415,266,74,103);TextAt(g,"回家歇歇脚",23,ink,534,267,300,45,true);
                TextAt(g,"睡到次日清晨。作物按实际时间生长。",16,ink,524,318,370,39);
                TextAt(g,"作物平时也会生长，关掉游戏后照常计时。",15,Col("64715d"),390,414,500,42,false,true);
                ModalButton(g,715,494,181,"休息到明天","sleep");
            }else{
                string[] lines={"铲子开田，前五块免费。选麦种，按住划过空田可逐格播种。","水滴出现才可浇水。点击虫鸟或受害作物，走近完成治理。","粮站换现钱，码头换星元。点货品，拖动数量滑块，再确认。","未来工坊出售无人机和种子培育仪。首架采收机 100 星元。","开满 36 块田，让六类无人机各完成一次作业，达成全田自动化。","B 背包，T 穿越，W 水壶，Esc 收起，F11 全屏。进度自动保存。"};
                for(int i=0;i<lines.Length;i++)TextAt(g,lines[i],15,ink,387,252+i*40,512,38);
                ModalButton(g,740,494,157,"回到游戏","close");
            }
        }
        internal void Action(string a)
        {
            if(CareInputLocked)return;
            if(a=="settings"){
                if(panel=="settings")CloseSoundSettings();
                else {settingsReturnPanel=panel;StopWalking();panel="settings";}
                return;
            }
            if(panel=="settings"){SoundSettingsAction(a);return;}
            if(HandleTradeAction(a))return;
            if(HandleFieldCare(a))return;
            if(HandleSeedDepotAction(a))return;
            if(HandleEquipmentAction(a))return;
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
            if(a=="relief"&&!Sea&&panel=="supply"&&state.NeedsSeed()){state.Seeds[0]++;Store();return;}
            if(a.StartsWith("equip")&&Sea&&panel=="tech"){
                int id=int.Parse(a.Substring(5));
                bool firstSeeder=id==FarmState.SeederDrone&&!state.SeedWarehouseUnlocked;
                if(state.Equip(id)){
                    if(firstSeeder)seedDepotAnnounceUntil=T+7;
                    Store();Tell(firstSeeder?"种子仓已解锁，放入种子后无人机就会出发。":"已购入"+FarmState.Machines[id]+"。");
                }
                return;
            }
            if(a=="sleep"&&scene==Scene.House&&panel=="bed"){state.Rest();Store();panel="";Tell("第 "+state.Day+" 天清晨，去看看田里的收成吧。");}
        }
        void Tend(int i)
        {
            if(Sea)return;
            Plot p=state.Plots[i];PointF b=Bed(i);flip=b.X<player.X;facingAway=false;
            pickTime=T;
            if(tool=="harvest"&&IsMature(i)){
                bool firstHarvest=!state.HasHarvested;int crop=p.CropId;
                string care=state.HarvestCareNote(i,state.Now);
                int n=state.Harvest(i,state.Now);
                Store();audio.Play(crop==0?FarmSound.Reap:FarmSound.Pick);
                Float("+"+n+" "+Crop.All[crop].Name,b);
                Tell(care.Length>0?"实收 "+n+" 份 · "+care:firstHarvest?"第一茬入袋了！粮站卖粮换现钱，拨动怀表去码头卖粮换星元。":"收成已装进背包。");
                return;
            }
            if(tool=="feed"){
                if(!state.Feed(i,state.Now)){Tell("请对生长中的作物使用背包里的化肥。");return;}
                Store();audio.Play(FarmSound.Fertilize);Float("生长加快",b);return;
            }
            SelectCell(i);
        }
    }
}
