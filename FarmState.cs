using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace MountainTide
{
    public sealed class Crop
    {
        public readonly string Name;
        public readonly int Seconds, Yield, SeedCost, CashPrice, StarPrice;
        public Crop(string name, int seconds, int yield, int seed, int cash, int stars)
        { Name=name; Seconds=seconds; Yield=yield; SeedCost=seed; CashPrice=cash; StarPrice=stars; }
        public static readonly Crop[] All = {
            new Crop("小麦",48,4,6,5,2), new Crop("萝卜",120,8,26,9,4), new Crop("玉米",210,12,45,12,6),
            new Crop("青菜",60,6,12,6,3), new Crop("南瓜",300,18,80,16,8)
        };
    }
    public sealed partial class Plot
    {
        public bool Opened;
        public int CropId = -1;
        public long Planted;
        public bool Watered;
        public bool Premium, AutoFed;
        [System.ComponentModel.DefaultValue(0)] public int LegacyYieldBonus;
    }
    public sealed partial class FarmState
    {
        public const int Scarecrow=0, Cultivator=1, Drone=2, PestMachine=3, WaterDrone=4, FertilizerDrone=5, SeederDrone=6;
        public const int FertilizerItem=5;
        public const int MaxDrones=CellCount;
        public static readonly int[] CropOrder={0,3,1,2,4};
        public static readonly string[] Machines = {"驱鸟无人机","种子培育仪","采收无人机","治虫无人机","浇水无人机","施肥无人机","播种无人机"};
        public static readonly string[] Effects = {"飞往有鸟害的田块，驱走鸟群。","每台可独立将最多十袋同种普通种子培育为优种。","慢速飞往成熟作物，逐块收割。","飞到虫害田块清除害虫。","飞到出现水滴的作物旁浇水。","每茬自动施一次肥，每块消耗一袋库存化肥。","到种子仓领种，按投放先后飞往空田逐袋播种。"};
        public static readonly int[] Costs = {80,55,100,140,110,150,200};
        const int CurrentSaveVersion=4;
        public int SaveVersion;
        public int HarvestCount;
        public bool LegacySeedCatalog;
        public int LegacyCropMask;
        // Versions 1 and 2 could unlock seed purchases and hold items toward an unfinished order.
        [XmlElement("GreenhouseStage")] public int LegacyOrderStage;
        [XmlArray("GreenhouseDelivered")] public int[] LegacyOrderDeposits;
        public bool ShouldSerializeLegacyOrderStage(){return false;}
        public bool ShouldSerializeLegacyOrderDeposits(){return false;}
        public int Cash=40, Fertilizer=2;
        [XmlElement("Tide")] // Read the balance from existing farm saves.
        public int StarCredits;
        public int[] Seeds={5,0,0,0,0}, Stock=new int[5];
        public bool[] MachinesOwned;
        public bool ShouldSerializeMachinesOwned(){return false;}
        public const int FreePlotCount=5, Columns=9, Rows=4, CellCount=Columns*Rows;
        public Plot[] Plots;
        public int OpenCount {
            get {
                int count=0;
                foreach(Plot plot in Plots)if(plot.Opened)count++;
                return count;
            }
        }
        static int PlotPrice(int number){return Math.Max(0,number-FreePlotCount)*500;}
        public int NextPlotCost {get{return PlotPrice(OpenCount+1);}}
        public long Beginning, TimeBonus;
        public bool HasHarvested, HasTraveled;
        public static long UtcSeconds { get { return DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond; } }
        public long Now { get { return UtcSeconds; } }
        public long Day { get { return 1 + Math.Max(0, Now + TimeBonus - Beginning)/90; } }
        public void Rest(){TimeBonus+=90;}
        public static FarmState New(long now)
        {
            FarmState s=new FarmState();s.Beginning=now;s.SaveVersion=CurrentSaveVersion;
            s.Plots=Grassland();
            return s;
        }
        static Plot[] Grassland(){Plot[] cells=new Plot[CellCount];for(int i=0;i<cells.Length;i++)cells[i]=new Plot();return cells;}
        public bool Till(int i){
            if(i<0||i>=Plots.Length||Plots[i].Opened)return false;
            int cost=NextPlotCost;
            if(Cash<cost)return false;
            Cash-=cost;
            Plots[i].Opened=true;
            return true;
        }
        public bool Uproot(int i){
            if(!Plots[i].Opened||Plots[i].CropId<0||Plots[i].CareExecuting)return false;
            ClearPlant(i);return true;
        }
        void ClearPlant(int i){
            Plot p=Plots[i];p.CropId=-1;p.Planted=0;p.Watered=false;
            p.Premium=false;p.AutoFed=false;p.LegacyYieldBonus=0;ResetCare(i);
        }
        public void ImportOldPlots(long now){
            // Transfer the old fixed beds before giving this farm a free choice of five sites.
            for(int i=0;i<Plots.Length;i++)ReturnCrop(i,now);
            Plots=Grassland();
        }
        void ReturnCrop(int i,long now){
            int id=Plots[i].CropId;
            if(id<0)return;
            Plot plot=Plots[i];
            int seconds=SaveVersion==0?OldSeconds[id]:GrowthSeconds(plot);
            if(plot.Planted+seconds>now){if(plot.Premium)PremiumSeeds[id]++;else Seeds[id]++;return;}
            int yield=SaveVersion==0?OldYield[id]+plot.LegacyYieldBonus:BaseYield(plot);
            Stock[id]+=SaveVersion==0?yield:GetCareYield(i,now,yield);
        }
        internal static bool IsWideField(int count){return count==40||count==44;}
        static int OldPlotIndex(int oldColumns,int row,int column){
            // 44-cell farms stored six even columns, then five odd ones. 40-cell farms were row-major.
            int slot=oldColumns==11?column/2+(column%2==0?0:6):column;
            return row*oldColumns+slot;
        }
        internal void ImportWideField(long now){
            int oldColumns=Plots.Length/Rows;
            Plot[] field=Grassland();
            for(int row=0;row<Rows;row++)
                for(int col=0;col<Columns;col++)
                    field[row*Columns+col]=Plots[OldPlotIndex(oldColumns,row,col)];

            // Give every row first choice of its own free cells before allocating across rows.
            List<int> needsSpace=new List<int>();
            for(int row=0;row<Rows;row++){
                int start=row*Columns;
                int dest=start+Columns-1;
                for(int col=Columns;col<oldColumns;col++){
                    Plot plot=Plots[OldPlotIndex(oldColumns,row,col)];
                    if(!plot.Opened)continue;
                    while(dest>=start&&field[dest].Opened)dest--;
                    if(dest>=start)field[dest--]=plot;
                    else needsSpace.Add(row*oldColumns+col);
                }
            }
            foreach(int oldCell in needsSpace){
                int row=oldCell/oldColumns,col=oldCell%oldColumns;
                int dest=-1,bestDistance=int.MaxValue;
                for(int i=0;i<field.Length;i++){
                    if(field[i].Opened)continue;
                    int dx=col-i%Columns,dy=row-i/Columns;
                    int distance=dx*dx+dy*dy;
                    if(distance<bestDistance){bestDistance=distance;dest=i;}
                }
                int source=OldPlotIndex(oldColumns,row,col);
                if(dest>=0)field[dest]=Plots[source];
                else ReturnCrop(source,now);
            }
            for(int number=OpenCount;number>CellCount;number--)Cash+=PlotPrice(number);
            Plots=field;
        }
        public long Left(int i,long now)
        {
            Plot p=Plots[i];
            // Keep this crop available to its caretaker until the closing pose. Its planting clock still runs.
            return p.CropId<0?0:Math.Max(p.CareExecuting?1:0,p.Planted+GrowthSeconds(p)-now);
        }
        public bool Sow(int i,int id,long now)
        {
            return Sow(i,id,now,false);
        }
        public bool Sow(int i,int id,long now,bool premium){
            lock(tradeLock){
                if(!CanPlant(i)||id<0||id>=Crop.All.Length)return false;
                int[] bags=premium?PremiumSeeds:Seeds;
                if(bags[id]<1)return false;
                bags[id]--;
                Plant(i,id,now,premium);
                foreach(var flight in SeederFlights)
                    if(flight.TargetPlot==i&&flight.Cargo.Count>0)MarkSeederReturning(flight);
                return true;
            }
        }
        bool CanPlant(int i){return i>=0&&i<Plots.Length&&Plots[i].Opened&&Plots[i].CropId<0;}
        void Plant(int i,int id,long now,bool premium){
            Plot plot=Plots[i];plot.CropId=id;plot.Planted=now;plot.Watered=false;
            plot.Premium=premium;plot.AutoFed=false;plot.LegacyYieldBonus=0;ResetCare(i);
        }
        public const int WaterBonusSeconds=5;
        public bool CanWater(int i,long now){
            if(i<0||i>=Plots.Length)return false;
            Plot p=Plots[i];
            return p.CropId>=0&&!p.Watered&&p.CareAlert==CareKind.Water&&Left(i,now)>0;
        }
        public bool Water(int i,long now)
        {
            if(!CanWater(i,now)||Plots[i].CareExecuting)return false;
            ApplyWater(i);
            return true;
        }
        void ApplyWater(int i){
            Plot p=Plots[i];
            p.Watered=true;
            p.Planted-=WaterBonusSeconds;
            ClearWaterAlert(i);
        }
        public bool Feed(int i,long now)
        {
            if(Fertilizer<1 || Plots[i].CropId<0 || Plots[i].CareExecuting || Left(i,now)==0)return false;
            Fertilizer--;Plots[i].Planted-=20;return true;
        }
        public int Harvest(int i,long now)
        {
            int id=Plots[i].CropId;if(id<0 || Left(i,now)>0)return 0;
            int qty=GetCareYield(i,now,BaseYield(Plots[i]));Stock[id]+=qty;
            ClearPlant(i);
            HasHarvested=true;
            if(HarvestCount<int.MaxValue)HarvestCount++;
            return qty;
        }
        static readonly int[] HarvestUnlocks={0,5,10,1,20};
        public bool CropUnlocked(int id){
            if(id<0||id>=Crop.All.Length)return false;
            return HarvestCount>=HarvestUnlocks[id]||(LegacyCropMask&(1<<id))!=0||LegacySeedCatalog&&(id==1||id==2);
        }
        public string CropUnlockHint(int id){
            if(CropUnlocked(id))return "已解锁";
            if(id==3)return "首次采收后解锁";
            return "累计采收 "+HarvestUnlocks[id]+" 茬后解锁";
        }
        public const int MaxTradeQuantity=200, FertilizerPrice=4;
        readonly object tradeLock=new object();
        public static int TradePrice(bool buying,bool sea,int item){
            if(buying&&item==FertilizerItem)return FertilizerPrice;
            if(item<0||item>=Crop.All.Length)return 0;
            if(buying)return Crop.All[item].SeedCost;
            return sea?Crop.All[item].StarPrice:Crop.All[item].CashPrice;
        }
        public int TradeHeld(bool buying,int item){
            if(buying&&item==FertilizerItem)return Fertilizer;
            if(item<0||item>=Crop.All.Length)return 0;
            return buying?Seeds[item]:Stock[item];
        }
        public int TradeLimit(bool buying,bool sea,int item){
            int held=TradeHeld(buying,item);
            if(buying){
                if(item!=FertilizerItem&&!CropUnlocked(item))return 0;
                if(held<0||Cash<=0)return 0;
                int affordable=Cash/TradePrice(true,sea,item);
                return Math.Min(MaxTradeQuantity,Math.Min(affordable,int.MaxValue-held));
            }
            if(held<=0)return 0;
            int balance=sea?StarCredits:Cash;
            if(balance<0)return 0;
            int payable=(int.MaxValue-balance)/TradePrice(false,sea,item);
            return Math.Min(MaxTradeQuantity,Math.Min(held,payable));
        }
        public bool TryTrade(bool buying,bool sea,int item,int quantity){
            lock(tradeLock){
                if(quantity<=0||quantity>TradeLimit(buying,sea,item))return false;
                int total=quantity*TradePrice(buying,sea,item);
                if(buying){
                    Cash-=total;
                    if(item==FertilizerItem)Fertilizer+=quantity;else Seeds[item]+=quantity;
                }else{
                    Stock[item]-=quantity;
                    if(sea)StarCredits+=total;else Cash+=total;
                }
                return true;
            }
        }
        public bool Buy(int id){return TryTrade(true,false,id,1);}
        public int Sell(bool sea,int id,int amount){
            return TryTrade(false,sea,id,amount)?amount*TradePrice(false,sea,id):0;
        }
        public bool NeedsSeed()
        {
            if(Cash>=Crop.All[0].SeedCost)return false;
            foreach(int n in Seeds)if(n>0)return false;
            foreach(int n in PremiumSeeds)if(n>0)return false;
            if(WarehouseSeedCount>0||SeederCargoCount>0)return false;
            foreach(int n in Stock)if(n>0)return false;
            foreach(Plot p in Plots)if(p.CropId>=0)return false;
            return true;
        }
        static readonly int[] OldSeconds={32,48,64}, OldYield={4,5,6};
        bool ImportOrderProgress(){
            if(LegacyOrderStage<0||LegacyOrderStage>4)return false;
            int count=Crop.All.Length;
            if(LegacyOrderStage<3&&LegacyOrderDeposits!=null){
                if(LegacyOrderDeposits.Length!=count*2)return false;
                for(int i=0;i<LegacyOrderDeposits.Length;i++){
                    int amount=LegacyOrderDeposits[i];
                    int[] bag=i<count?Stock:Seeds;
                    if(amount<0||(long)bag[i%count]+amount>int.MaxValue)return false;
                    bag[i%count]+=amount;
                }
            }
            if(LegacyOrderStage>=1)LegacyCropMask|=(1<<3)|(1<<1)|(1<<2);
            if(LegacyOrderStage>=2)LegacyCropMask|=1<<4;
            LegacyOrderDeposits=null;LegacyOrderStage=0;
            return true;
        }
        internal bool PrepareSave(bool fixedBeds){
            if(SaveVersion<0||SaveVersion>CurrentSaveVersion||Beginning<=0||Plots==null)return false;
            bool original=SaveVersion==0;
            bool oldMachines=SaveVersion<3;
            if(fixedBeds?Plots.Length!=9:Plots.Length!=CellCount&&!IsWideField(Plots.Length))return false;
            int crops=original?3:Crop.All.Length;
            if(Seeds==null||Seeds.Length!=crops||Stock==null||Stock.Length!=crops)return false;
            if(Array.Exists(Plots,p=>p==null||p.CropId< -1||p.CropId>=crops||!fixedBeds&&!p.Opened&&p.CropId>=0))return false;
            if(oldMachines){
                if(MachinesOwned==null||MachinesOwned.Length!=(original?3:4)||legacyDroneCount<0||legacyDroneCount>MaxDrones)return false;
                if(!original&&!ImportOrderProgress())return false;
                for(int id=0;id<crops;id++)if(Seeds[id]>0||Stock[id]>0)LegacyCropMask|=1<<id;
                foreach(Plot p in Plots)if(p.CropId>=0){
                    LegacyCropMask|=1<<p.CropId;
                    if(MachinesOwned[Cultivator])p.LegacyYieldBonus=2;
                }
                ImportMachines(original);
            }else if(!ValidAutomation())return false;
            if(SaveVersion<4)StartSeedWarehouse();
            else if(!ValidSeedWarehouse())return false;
            long now=Now+(original?TimeBonus:0);
            if(fixedBeds)ImportOldPlots(now);
            else if(IsWideField(Plots.Length))ImportWideField(now);
            if(original){
                // Preserve the remaining time for each existing crop on the new real-time clock.
                foreach(Plot p in Plots)if(p.CropId>=0)
                    p.Planted+=OldSeconds[p.CropId]-Crop.All[p.CropId].Seconds-TimeBonus;
                LegacySeedCatalog=true;
                HarvestCount=HasHarvested?1:0;
                Array.Resize(ref Seeds,Crop.All.Length);
                Array.Resize(ref Stock,Crop.All.Length);
            }else if(SaveVersion==1){
                // The first five-crop build used a 32-second wheat cycle.
                foreach(Plot p in Plots)if(p.CropId==0)p.Planted+=32-Crop.All[0].Seconds;
            }
            SaveVersion=CurrentSaveVersion;
            return true;
        }
    }
    internal sealed class SaveFile
    {
        private readonly string file;
        private readonly XmlSerializer serializer=new XmlSerializer(typeof(FarmState));
        readonly string legacy;
        bool loadedBackup;
        public string DirectoryPath {get{return Path.GetDirectoryName(file);}}
        public SaveFile(string path,string legacyPath=null){file=path;legacy=legacyPath;}
        public FarmState Load()
        {
            string[] candidates=legacy==null?new[]{file,file+".bak"}:new[]{file,file+".bak",legacy,legacy+".bak"};
            foreach(string path in candidates){
                if(!File.Exists(path))continue;
                try{using(FileStream f=File.OpenRead(path)){
                    FarmState s=(FarmState)serializer.Deserialize(f);
                    bool old=path==legacy||legacy!=null&&path==legacy+".bak";
                    if(!s.PrepareSave(old))continue;
                    loadedBackup=path==file+".bak";
                    return s;
                }}catch(InvalidOperationException){}catch(IOException){}
            }
            loadedBackup=false;
            return FarmState.New(FarmState.UtcSeconds);
        }
        public void Write(FarmState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string tmp=file+".tmp";
            using(FileStream f=File.Create(tmp)){serializer.Serialize(f,state);f.Flush(true);}
            // A bad primary must not displace the good backup that supplied this session.
            if(File.Exists(file))File.Replace(tmp,file,loadedBackup?null:file+".bak",true);else File.Move(tmp,file);
            loadedBackup=false;
        }
    }
}
