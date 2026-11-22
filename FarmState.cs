using System;
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
            new Crop("小麦",32,4,6,5,8), new Crop("萝卜",48,5,10,7,11), new Crop("玉米",64,6,15,9,15)
        };
    }
    public sealed class Plot
    {
        public bool Opened;
        public int CropId = -1;
        public long Planted;
    }
    public sealed class FarmState
    {
        public static readonly string[] Machines = {"自动播种器","根系培育仪","采收无人机"};
        public static readonly string[] Effects = {"收割时消耗一袋同种种子，自动补种。","每块田每次收获额外增加两份作物。","作物成熟时自动收割，直接送入背包。"};
        public static readonly int[] Costs = {30,55,85};
        public int Cash=40, Fertilizer=2;
        [XmlElement("Tide")] // Read the balance from existing farm saves.
        public int StarCredits;
        public int[] Seeds={3,2,1}, Stock={0,0,0};
        public bool[] MachinesOwned=new bool[3];
        public const int LandLimit=5, CellCount=44;
        public Plot[] Plots;
        public int OpenCount {get{return Array.FindAll(Plots,p=>p.Opened).Length;}}
        public long Beginning, TimeBonus;
        public bool HasHarvested, HasTraveled;
        public static long UtcSeconds { get { return DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond; } }
        public long Now { get { return UtcSeconds + TimeBonus; } }
        public long Day { get { return 1 + Math.Max(0, Now - Beginning)/90; } }
        public static FarmState New(long now)
        {
            FarmState s=new FarmState();s.Beginning=now;
            s.Plots=Grassland();
            return s;
        }
        static Plot[] Grassland(){Plot[] cells=new Plot[CellCount];for(int i=0;i<cells.Length;i++)cells[i]=new Plot();return cells;}
        public bool Till(int i){
            if(i<0||i>=Plots.Length||Plots[i].Opened||OpenCount>=LandLimit)return false;
            Plots[i].Opened=true;return true;
        }
        public bool Uproot(int i){
            if(!Plots[i].Opened||Plots[i].CropId<0)return false;
            Plots[i].CropId=-1;Plots[i].Planted=0;return true;
        }
        public void ImportOldPlots(long now){
            // Transfer the old fixed beds before giving this farm a free choice of five sites.
            for(int i=0;i<Plots.Length;i++){int id=Plots[i].CropId;if(id<0)continue;
                if(Left(i,now)==0)Stock[id]+=Crop.All[id].Yield+(MachinesOwned[1]?2:0);
                else Seeds[id]++;
            }
            Plots=Grassland();
        }
        public long Left(int i,long now)
        {
            Plot p=Plots[i];return p.CropId<0?0:Math.Max(0,p.Planted+Crop.All[p.CropId].Seconds-now);
        }
        public bool Sow(int i,int id,long now)
        {
            if(!Plots[i].Opened || Plots[i].CropId>=0 || Seeds[id]<1)return false;
            Seeds[id]--;Plots[i].CropId=id;Plots[i].Planted=now;return true;
        }
        public bool Feed(int i,long now)
        {
            if(Fertilizer<1 || Plots[i].CropId<0 || Left(i,now)==0)return false;
            Fertilizer--;Plots[i].Planted-=20;return true;
        }
        public int Harvest(int i,long now)
        {
            int id=Plots[i].CropId;if(id<0 || Left(i,now)>0)return 0;
            int qty=Crop.All[id].Yield+(MachinesOwned[1]?2:0);Stock[id]+=qty;
            Plots[i].CropId=-1;HasHarvested=true;
            if(MachinesOwned[0])Sow(i,id,now);return qty;
        }
        public int Automate(long now)
        {
            if(!MachinesOwned[2])return 0;
            int total=0;
            while(true){
                int first=-1;long next=long.MaxValue;
                for(int i=0;i<Plots.Length;i++){
                    Plot p=Plots[i];if(p.CropId<0)continue;
                    long ready=p.Planted+Crop.All[p.CropId].Seconds;
                    if(ready<=now && ready<next){first=i;next=ready;}
                }
                if(first<0)return total;
                total+=Harvest(first,next);
            }
        }
        public bool Buy(int id)
        {
            int cost=id==3?4:Crop.All[id].SeedCost;if(Cash<cost)return false;
            Cash-=cost;if(id==3)Fertilizer++;else Seeds[id]++;return true;
        }
        public int Sell(bool sea,int id,int amount)
        {
            if(amount<=0||Stock[id]<amount)return 0;
            int value=amount*(sea?Crop.All[id].StarPrice:Crop.All[id].CashPrice);
            Stock[id]-=amount;if(sea)StarCredits+=value;else Cash+=value;return value;
        }
        public bool Equip(int id)
        {
            if(MachinesOwned[id]||StarCredits<Costs[id])return false;
            StarCredits-=Costs[id];MachinesOwned[id]=true;return true;
        }
        public bool NeedsSeed()
        {
            if(Cash>=Crop.All[0].SeedCost)return false;
            foreach(int n in Seeds)if(n>0)return false;
            foreach(int n in Stock)if(n>0)return false;
            foreach(Plot p in Plots)if(p.CropId>=0)return false;
            return true;
        }
    }
    internal sealed class SaveFile
    {
        private readonly string file;
        private readonly XmlSerializer serializer=new XmlSerializer(typeof(FarmState));
        readonly string legacy;
        public SaveFile(string path,string legacyPath=null){file=path;legacy=legacyPath;}
        public FarmState Load()
        {
            string[] candidates=legacy==null?new[]{file,file+".bak"}:new[]{file,file+".bak",legacy,legacy+".bak"};
            foreach(string path in candidates){
                if(!File.Exists(path))continue;
                try{using(FileStream f=File.OpenRead(path)){
                    FarmState s=(FarmState)serializer.Deserialize(f);
                    bool old=path==legacy||legacy!=null&&path==legacy+".bak";
                    if(s.Plots==null||s.Plots.Length!=(old?9:FarmState.CellCount)||s.Seeds==null||s.Seeds.Length!=3||s.Stock==null||s.Stock.Length!=3||s.MachinesOwned==null||s.MachinesOwned.Length!=3||s.Beginning<=0)continue;
                    if(Array.Exists(s.Plots,p=>p==null||p.CropId< -1||p.CropId>2))continue;
                    if(old)s.ImportOldPlots(s.Now);
                    else if(s.OpenCount>FarmState.LandLimit||Array.Exists(s.Plots,p=>!p.Opened&&p.CropId>=0))continue;
                    return s;
                }}catch(InvalidOperationException){}catch(IOException){}
            }
            return FarmState.New(FarmState.UtcSeconds);
        }
        public void Write(FarmState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string tmp=file+".tmp";
            using(FileStream f=File.Create(tmp)){serializer.Serialize(f,state);f.Flush(true);}
            if(File.Exists(file))File.Replace(tmp,file,file+".bak");else File.Move(tmp,file);
        }
    }
}
