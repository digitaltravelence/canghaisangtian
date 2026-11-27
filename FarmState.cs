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
            new Crop("小麦",32,4,6,5,8), new Crop("萝卜",48,5,10,7,11), new Crop("玉米",64,6,15,9,15)
        };
    }
    public sealed class Plot
    {
        public bool Opened;
        public int CropId = -1;
        public long Planted;
        public bool Watered;
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
            if(i<0||i>=Plots.Length||Plots[i].Opened)return false;
            int cost=NextPlotCost;
            if(Cash<cost)return false;
            Cash-=cost;
            Plots[i].Opened=true;
            return true;
        }
        public bool Uproot(int i){
            if(!Plots[i].Opened||Plots[i].CropId<0)return false;
            Plots[i].CropId=-1;Plots[i].Planted=0;Plots[i].Watered=false;return true;
        }
        public void ImportOldPlots(long now){
            // Transfer the old fixed beds before giving this farm a free choice of five sites.
            for(int i=0;i<Plots.Length;i++)ReturnCrop(i,now);
            Plots=Grassland();
        }
        void ReturnCrop(int i,long now){
            int id=Plots[i].CropId;
            if(id<0)return;
            if(Left(i,now)==0)Stock[id]+=Crop.All[id].Yield+(MachinesOwned[1]?2:0);
            else Seeds[id]++;
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
            Plot p=Plots[i];return p.CropId<0?0:Math.Max(0,p.Planted+Crop.All[p.CropId].Seconds-now);
        }
        public bool Sow(int i,int id,long now)
        {
            if(!Plots[i].Opened || Plots[i].CropId>=0 || Seeds[id]<1)return false;
            Seeds[id]--;Plots[i].CropId=id;Plots[i].Planted=now;Plots[i].Watered=false;return true;
        }
        public const int WaterBonusSeconds=5;
        public bool Water(int i,long now)
        {
            Plot p=Plots[i];
            if(p.CropId<0 || p.Watered || Left(i,now)==0)return false;
            p.Watered=true;
            p.Planted-=WaterBonusSeconds;
            return true;
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
            Plots[i].CropId=-1;Plots[i].Watered=false;HasHarvested=true;
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
                    if(s.Plots==null||s.Seeds==null||s.Seeds.Length!=3||s.Stock==null||s.Stock.Length!=3||s.MachinesOwned==null||s.MachinesOwned.Length!=3||s.Beginning<=0)continue;
                    int length=s.Plots.Length;
                    if(old?length!=9:length!=FarmState.CellCount&&!FarmState.IsWideField(length))continue;
                    if(Array.Exists(s.Plots,p=>p==null||p.CropId< -1||p.CropId>2))continue;
                    if(old)s.ImportOldPlots(s.Now);
                    else {
                        if(Array.Exists(s.Plots,p=>!p.Opened&&p.CropId>=0))continue;
                        if(FarmState.IsWideField(length))s.ImportWideField(s.Now);
                    }
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
            if(File.Exists(file))File.Replace(tmp,file,loadedBackup?null:file+".bak");else File.Move(tmp,file);
            loadedBackup=false;
        }
    }
}
