using System;
using System.Drawing;

namespace MountainTide
{
    internal sealed partial class Game
    {
        int stationIndex, batchCrop, batchSize=1;
        bool completionShown;
        double celebrationUntil=-1;
        static readonly string[] JobNames={"驱鸟","培育","收割","治虫","浇水","施肥","播种"};

        void CheckAutomationMilestone()
        {
            if(completionShown||!state.AutomationComplete)return;
            completionShown=true;celebrationUntil=T+9;
            Tell("36 块农田全部开垦，六种无人作业已全部完成！");
        }
        void DrawAutomationRibbon(Graphics g)
        {
            if(T>celebrationUntil)return;
            Plate(g,385,85,510,50,Col("416446"),Col("e1b660"),12);
            TextAt(g,"全田自动化达成  ·  36 块田，六种无人作业",20,gold,396,85,488,50,true,true);
        }
        void DrawSeedStation(Graphics g)
        {
            int count=state.MachineCount(FarmState.Cultivator);
            if(count==0)return;
            const int x=898,y=385;
            g.FillEllipse(B(Color.FromArgb(64,55,53,27)),x+6,y+51,63,15);
            FitSprite(g,DeviceIcon(FarmState.Cultivator),x,y-10,76,75);
            bool ready=Array.Exists(state.CultivationBatches,b=>b.Quantity>0&&b.ReadyAt<=state.Now);
            Plate(g,x-5,y+61,88,22,Dark,ready?gold:Rim,7);
            TextAt(g,ready?"优种可领取":"培育仪 × "+count,12,ready?gold:cream,x-5,y+61,88,22,true,true);
            HitBox(x-5,y-10,88,94,"cultivator-field");
        }
        void DrawEquipmentStrip(Graphics g,int top)
        {
            for(int slot=0;slot<WorkshopOrder.Length;slot++){
                int id=WorkshopOrder[slot],x=288+slot*89,y=top+40;
                FitSprite(g,DeviceIcon(id),x+16,y,56,47);
                TextAt(g,JobNames[id]+" × "+state.MachineCount(id),13,ink,x,y+46,89,25,true,true);
            }
            TrayButton(g,932,top+42,196,32,"农场自动化","open-automation");
            TrayButton(g,932,top+80,81,32,"种子仓","open-seed-depot",state.SeedWarehouseUnlocked);
            TrayButton(g,1019,top+80,109,32,"种子培育仪","open-cultivation",state.MachineCount(FarmState.Cultivator)>0);
        }
        void DrawEquipmentPanel(Graphics g)
        {
            Plate(g,237,80,814,558,Color.FromArgb(150,34,40,29),Color.FromArgb(110,34,40,29),15);
            Plate(g,232,74,814,558,Col("faf2da"),Col("8b7851"),14);
            string title=panel=="tech"?"科技工坊":panel=="cultivation"?"种子培育仪":"农场自动化";
            TextAt(g,title,27,ink,262,91,280,45,true);
            if(panel!="automation")ModalButton(g,704,96,122,"农场自动化","open-automation",true,false);
            if(panel!="tech"&&Sea)ModalButton(g,839,96,108,"科技工坊","back-workshop",true,false);
            ModalButton(g,981,96,39,"×","close",true,false);
            if(panel=="tech")DrawWorkshop(g);
            else if(panel=="cultivation")DrawCultivation(g);
            else DrawAutomation(g);
        }
        double seedDepotAnnounceUntil=-1;
        void DrawWorkshop(Graphics g)
        {
            TextAt(g,"购买后设备会送到农场。同类设备可以增购，下一台的价格随数量递增。",15,Col("626f54"),264,139,745,30);
            for(int slot=0;slot<WorkshopOrder.Length;slot++){
                int id=WorkshopOrder[slot],x=258+slot%2*385,y=179+slot/2*91;
                Plate(g,x,y,375,83,Col("f6e9c9"),Col("d4bd8a"),8);
                FitSprite(g,DeviceIcon(id),x+8,y+9,54,51);
                TextAt(g,FarmState.Machines[id],17,ink,x+70,y+5,190,26,true);
                TextAt(g,"持有 "+state.MachineCount(id)+(id==FarmState.Cultivator?" 台":" 架"),13,Col("64725c"),x+70,y+31,162,20);
                string hint=state.MachineUnlockHint(id);
                TextAt(g,hint.Length>0?hint:FarmState.Effects[id],13,Col("626b51"),x+12,y+56,350,22);
                bool full=state.MachineCount(id)>=FarmState.MaxDrones;
                ModalButton(g,x+242,y+11,123,full?"已满":state.MachinePrice(id)+" 星元","equip"+id,state.CanEquip(id));
            }
            TextAt(g,"持有 "+state.StarCredits+" 星元",18,ink,265,561,360,45,true);
            if(state.SeedWarehouseUnlocked)ModalButton(g,668,562,126,"打开种子仓","open-seed-depot",true,false);
            if(seedDepotAnnounceUntil>T)TextAt(g,"种子仓已随首架播种机解锁",14,Col("416442"),445,561,219,43,true);
            ModalButton(g,809,562,201,"管理种子培育仪","open-cultivation",state.MachineCount(FarmState.Cultivator)>0,false);
        }
        void DrawAutomation(Graphics g)
        {
            TextAt(g,state.AutomationComplete?"36 块农田已经全部开垦，六种作业都已完成。你可以继续经营。":
                "开垦全部农田，并让六类无人机各完成一次田间作业。",16,ink,264,146,752,34);
            TextAt(g,"开垦 "+state.OpenCount+" / "+FarmState.CellCount+" 块     已完成 "+state.CompletedAutomationKinds+" / 6 种作业",19,ink,264,185,755,36,true);
            for(int slot=0;slot<6;slot++){
                int id=WorkshopOrder[slot],x=262+slot%3*254,y=233+slot/3*66;
                bool done=state.HasMachineWorked(id);
                Plate(g,x,y,243,57,done?Col("dfebce"):Col("f1e6c9"),Col("c6b387"),8);
                FitSprite(g,DeviceIcon(id),x+7,y+4,49,47);
                TextAt(g,JobNames[id],16,ink,x+63,y+2,73,27,true);
                TextAt(g,done?"已完成作业":state.MachineCount(id)>0?"等待首单":"尚未购入",13,Col("657256"),x+63,y+29,162,22);
                TextAt(g,"× "+state.MachineCount(id),15,ink,x+168,y+4,65,36,false,true);
            }
            bool owned=state.SeedWarehouseUnlocked;
            g.FillRectangle(B(Col("d7c9a8")),263,378,750,1);
            TextAt(g,"播种无人机 · 种子仓",20,ink,264,392,500,36,true);
            ModalButton(g,855,390,149,state.AutoSowEnabled&&owned?"自动播种  开":"自动播种  关","sow-switch",owned,state.AutoSowEnabled&&owned);
            TextAt(g,owned?"把背包里的种子依次放入仓内，无人机会按投放顺序前往空田播种。":"在科技工坊购买第一架播种无人机，即可同时解锁农田旁的种子仓。",15,Col("607052"),264,435,743,30);
            Plate(g,264,483,744,58,Col("eae6cc"),Col("c6b387"),8);
            string stock=owned?"仓内 "+state.WarehouseSeedCount+" 袋     机上 "+state.SeederCargoCount+" 袋":"种子仓尚未解锁";
            TextAt(g,stock,17,ink,280,492,452,38,true);
            ModalButton(g,852,494,140,"管理种子仓","open-seed-depot",owned);
            TextAt(g,"化肥库存 "+state.Fertilizer+" 袋  ·  施肥无人机每次作业消耗 1 袋",14,Col("677359"),264,575,515,29);
            ModalButton(g,823,568,186,"管理种子培育仪","open-cultivation",state.MachineCount(FarmState.Cultivator)>0,false);
        }
        void DrawCultivation(Graphics g)
        {
            int count=state.CultivationBatches.Length;
            if(count==0)return;
            stationIndex=Math.Min(stationIndex,count-1);
            SeedBatch batch=state.CultivationBatches[stationIndex];
            TextAt(g,"每台独立培育一种普通种子，每批 1–10 袋。优质种子产量提高 50%，生长期缩短约 20%。",15,Col("607052"),264,141,750,33);
            Plate(g,262,189,225,348,Col("e9e6c7"),Col("c2b381"),12);
            FitSprite(g,DeviceIcon(FarmState.Cultivator),293,212,165,162);
            TextAt(g,"第 "+(stationIndex+1)+" / "+count+" 台",19,ink,271,392,207,39,true,true);
            ModalButton(g,276,462,90,"上一台","station-prev",stationIndex>0,false);
            ModalButton(g,380,462,90,"下一台","station-next",stationIndex+1<count,false);
            if(batch.Quantity>0){
                long left=Math.Max(0,batch.ReadyAt-state.Now);bool ready=left==0;
                TextAt(g,ready?"本批优种可以领取":"正在培育",23,ink,522,211,460,47,true);
                FitSprite(g,premiumPackets[batch.CropId],528,288,102,101);
                TextAt(g,Crop.All[batch.CropId].Name+"   "+batch.Quantity+" 袋",19,ink,648,305,300,39,true);
                TextAt(g,ready?"领取后存入背包的优质种袋":"还需 "+left+" 秒",16,Col("607052"),648,347,340,35);
                int duration=FarmState.CultivationSeconds(batch.Quantity);
                float filled=445*Math.Max(0,Math.Min(1,1-left/(float)duration));
                g.FillRectangle(B(Col("d8d2b5")),527,429,445,9);
                g.FillRectangle(B(Col("73955f")),527,429,filled,9);
                ModalButton(g,782,469,190,"领取优质种子","batch-collect",ready);
            }else{
                TextAt(g,"选择投入的普通种子",20,ink,512,197,493,37,true);
                for(int slot=0;slot<FarmState.CropOrder.Length;slot++){
                    int id=FarmState.CropOrder[slot],x=512+slot*99;
                    Plate(g,x,255,91,91,batchCrop==id?Col("e5eccd"):Col("f3e8cb"),batchCrop==id?Col("719359"):Col("c9b98f"),8);
                    FitSprite(g,art.SeedPackets[id],x+23,260,45,45);
                    TextAt(g,Crop.All[id].Name+" ×"+state.Seeds[id],13,ink,x+2,309,87,27,true,true);
                    HitBox(x,255,91,91,"batch-crop-"+id);
                }
                int limit=Math.Min(FarmState.MaxBatch,state.Seeds[batchCrop]);
                batchSize=Math.Max(1,Math.Min(batchSize,limit));
                TextAt(g,"投入数量",17,ink,516,366,110,34,true);
                ModalButton(g,627,365,46,"−","batch-less",batchSize>1,false);
                TextAt(g,(limit==0?0:batchSize)+" 袋",17,ink,674,365,82,36,true,true);
                ModalButton(g,760,365,46,"+","batch-more",batchSize<limit,false);
                ModalButton(g,822,365,103,"最多","batch-max",limit>0,false);
                TextAt(g,limit==0?"背包里没有这种普通种子。":"本批用时 "+FarmState.CultivationSeconds(batchSize)+" 秒，完成后可领 "+batchSize+" 袋优种。",15,Col("607052"),517,423,480,40);
                ModalButton(g,782,481,190,"投入种子","batch-start",limit>0);
            }
        }
        bool HandleEquipmentAction(string a)
        {
            if(a=="cultivator-field"&&InField&&panel==""&&state.CultivationBatches.Length>0){
                CloseDrawer();tool="hand";WalkTo(new PointF(951,477),"cultivation");return true;
            }
            if(a=="open-automation"||a=="open-cultivation"){
                if(a=="open-cultivation"&&state.CultivationBatches.Length==0)return true;
                StopWalking();CloseDrawer();tool="hand";panel=a.Substring(5);return true;
            }
            if(a=="back-workshop"&&Sea){panel="tech";return true;}
            if(panel=="automation"&&a=="sow-switch"&&state.SeedWarehouseUnlocked){
                state.AutoSowEnabled=!state.AutoSowEnabled;Store();return true;
            }
            if(panel!="cultivation"||state.CultivationBatches.Length==0)return false;
            if(a=="station-prev")stationIndex=Math.Max(0,stationIndex-1);
            else if(a=="station-next")stationIndex=Math.Min(state.CultivationBatches.Length-1,stationIndex+1);
            else if(a.StartsWith("batch-crop-")){batchCrop=int.Parse(a.Substring(11));batchSize=1;}
            else if(a=="batch-max")batchSize=Math.Max(1,Math.Min(FarmState.MaxBatch,state.Seeds[batchCrop]));
            else if(a=="batch-more")batchSize=Math.Min(Math.Min(FarmState.MaxBatch,state.Seeds[batchCrop]),batchSize+1);
            else if(a=="batch-less")batchSize=Math.Max(1,batchSize-1);
            else if(a=="batch-start"){
                if(state.StartCultivation(stationIndex,batchCrop,batchSize,state.Now)){audio.Play(FarmSound.Sow);Store();}
            }else if(a=="batch-collect"){
                if(state.CollectCultivation(stationIndex,state.Now)>0){audio.Play(FarmSound.Pick);Store();}
            }else return false;
            return true;
        }
    }
}
