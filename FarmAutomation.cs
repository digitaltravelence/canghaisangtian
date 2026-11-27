using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace MountainTide
{
    public sealed class SeedBatch
    {
        public int CropId=-1;
        public int Quantity;
        public long ReadyAt;
    }

    // A contiguous part of one deposit. Each individual bag keeps its original number.
    public sealed class WarehouseLot
    {
        public long BatchOrder, FirstOrder;
        public int CropId, Quantity;
        public bool Premium;

        internal WarehouseLot TakeFirst(int n){
            var head=new WarehouseLot{BatchOrder=BatchOrder,FirstOrder=FirstOrder,CropId=CropId,Premium=Premium,Quantity=n};
            FirstOrder+=n;
            Quantity-=n;
            return head;
        }
    }

    public sealed class SeedFlight
    {
        public List<WarehouseLot> Cargo=new List<WarehouseLot>();
        public bool Returning, HasPosition;
        public int TargetPlot=-1;
        public float GroundX, GroundY, Height;
        [XmlIgnore] public int Count {get{int n=0;foreach(var lot in Cargo)n+=lot.Quantity;return n;}}
    }

    public sealed partial class FarmState
    {
        public const int MaxBatch=10;
        public static readonly int[] FieldMachines={Drone,Scarecrow,PestMachine,FertilizerDrone,WaterDrone,SeederDrone};
        public int[] MachineCounts=new int[7];
        public int[] PremiumSeeds=new int[5];
        public SeedBatch[] CultivationBatches=new SeedBatch[0];
        public List<WarehouseLot> SeedWarehouse=new List<WarehouseLot>();
        public SeedFlight[] SeederFlights=new SeedFlight[0];
        public long NextSeedOrder=1, LastSownOrder;
        bool autoSowEnabled;
        public bool AutoSowEnabled {
            get{return autoSowEnabled;}
            set{
                lock(tradeLock){
                    autoSowEnabled=value;
                    if(!value&&SeederFlights!=null)
                        foreach(var flight in SeederFlights)
                            if(flight!=null&&flight.Cargo!=null&&flight.Cargo.Count>0)MarkSeederReturning(flight);
                }
            }
        }
        [XmlIgnore] public bool SeedWarehouseUnlocked {get{return MachineCount(SeederDrone)>0;}}
        [XmlIgnore] public long WarehouseSeedCount {get{long n=0;foreach(var lot in SeedWarehouse)n+=lot.Quantity;return n;}}
        [XmlIgnore] public long SeederCargoCount {get{long n=0;foreach(var flight in SeederFlights)n+=flight.Count;return n;}}
        public int AutomationWorkMask;

        int legacyDroneCount;
        public int DroneCount {get{return MachineCount(Drone);} set{legacyDroneCount=value;}}
        public bool ShouldSerializeDroneCount(){return false;}

        public int GrowthSeconds(Plot p){
            int seconds=Crop.All[p.CropId].Seconds;
            return p.Premium?(4*seconds+4)/5:seconds;
        }
        public int BaseYield(Plot p){
            int normal=Crop.All[p.CropId].Yield;
            return (p.Premium?normal*3/2:normal)+p.LegacyYieldBonus;
        }
        public int MachineCount(int id){return id>=0&&id<MachineCounts.Length?MachineCounts[id]:0;}
        public int MachinePrice(int id){return Costs[id]*(MachineCount(id)+1);}
        public string MachineUnlockHint(int id){
            if(MachineCount(id)>0)return "";
            if(id==Scarecrow&&!HasChasedBird)return "先在田里手动赶走一次飞鸟";
            if(id==PestMachine&&!HasClearedPests)return "先在田里手动清除一次虫害";
            if(id==Cultivator&&HarvestCount<5)return "累计采收五茬后可购买";
            if(id>=WaterDrone&&DroneCount==0)return "先购买一架采收无人机";
            if(id==FertilizerDrone&&OpenCount<6)return "开垦六块农田后可购买";
            if(id==SeederDrone&&OpenCount<8)return "开垦八块农田后可购买";
            return "";
        }
        public bool CanEquip(int id){
            return id>=0&&id<Machines.Length&&MachineCount(id)<MaxDrones&&
                MachineUnlockHint(id)==""&&StarCredits>=MachinePrice(id);
        }
        public bool Equip(int id){
            if(!CanEquip(id))return false;
            StarCredits-=MachinePrice(id);
            MachineCounts[id]++;
            if(id==Cultivator){
                Array.Resize(ref CultivationBatches,MachineCounts[id]);
                CultivationBatches[MachineCounts[id]-1]=new SeedBatch();
            }
            if(id==SeederDrone){
                Array.Resize(ref SeederFlights,MachineCounts[id]);
                SeederFlights[MachineCounts[id]-1]=new SeedFlight();
                if(MachineCounts[id]==1)AutoSowEnabled=true;
            }
            return true;
        }

        public static int CultivationSeconds(int quantity){return 30+3*quantity;}
        public bool StartCultivation(int station,int crop,int quantity,long now){
            if(station<0||station>=CultivationBatches.Length||crop<0||crop>=Crop.All.Length||
               quantity<1||quantity>MaxBatch||quantity>Seeds[crop])return false;
            SeedBatch batch=CultivationBatches[station];
            if(batch.Quantity!=0)return false;
            Seeds[crop]-=quantity;
            batch.CropId=crop;batch.Quantity=quantity;batch.ReadyAt=now+CultivationSeconds(quantity);
            return true;
        }
        public int CollectCultivation(int station,long now){
            if(station<0||station>=CultivationBatches.Length)return 0;
            SeedBatch batch=CultivationBatches[station];
            if(batch.Quantity==0||batch.ReadyAt>now)return 0;
            int count=batch.Quantity;
            PremiumSeeds[batch.CropId]+=count;
            CultivationBatches[station]=new SeedBatch();
            return count;
        }
        public bool CanAutoFeed(int i,long now){
            return MachineCount(FertilizerDrone)>0&&Fertilizer>0&&i>=0&&i<Plots.Length&&
                Plots[i].CropId>=0&&!Plots[i].AutoFed&&Left(i,now)>0;
        }
        public bool AutoFeed(int i,long now){
            if(!CanAutoFeed(i,now)||!Feed(i,now))return false;
            Plots[i].AutoFed=true;
            return true;
        }
        public bool DepositSeeds(int crop,bool premium,int quantity){
            lock(tradeLock){
                if(!SeedWarehouseUnlocked||crop<0||crop>=Crop.All.Length||quantity<1||
                   NextSeedOrder>long.MaxValue-quantity)return false;
                int[] bag=premium?PremiumSeeds:Seeds;
                if(bag[crop]<quantity)return false;
                SeedWarehouse.Add(new WarehouseLot{BatchOrder=NextSeedOrder,FirstOrder=NextSeedOrder,
                    CropId=crop,Premium=premium,Quantity=quantity});
                NextSeedOrder+=quantity;
                bag[crop]-=quantity;
                return true;
            }
        }
        public SeedFlight GetSeederFlight(int number){
            return number>=0&&number<SeederFlights.Length?SeederFlights[number]:null;
        }
        public int LoadSeeder(int number,int maxQuantity){
            lock(tradeLock){
                SeedFlight flight=GetSeederFlight(number);
                if(!AutoSowEnabled||flight==null||flight.Returning||flight.Cargo.Count!=0||maxQuantity<=0)return 0;
                int loaded=0;
                while(loaded<maxQuantity&&SeedWarehouse.Count>0){
                    WarehouseLot head=SeedWarehouse[0];
                    int take=Math.Min(head.Quantity,maxQuantity-loaded);
                    flight.Cargo.Add(head.TakeFirst(take));
                    if(head.Quantity==0)SeedWarehouse.RemoveAt(0);
                    loaded+=take;
                }
                return loaded;
            }
        }
        public bool IsNextSeeder(int number){
            SeedFlight flight=GetSeederFlight(number);
            if(!AutoSowEnabled||flight==null||flight.Returning||flight.Cargo.Count==0)return false;
            // Returning an earlier bag cannot advance the sequence. Only a successful landing does.
            return flight.Cargo[0].FirstOrder==LastSownOrder+1;
        }
        public bool CanPlantSeeder(int number,int plot){
            return IsNextSeeder(number)&&CanPlant(plot)&&
                (SeederFlights[number].TargetPlot<0||SeederFlights[number].TargetPlot==plot);
        }
        public bool PlantSeeder(int number,int plot,long now){
            lock(tradeLock){
                if(!CanPlantSeeder(number,plot)||SeederFlights[number].TargetPlot!=plot)return false;
                SeedFlight flight=SeederFlights[number];
                WarehouseLot next=flight.Cargo[0];
                Plant(plot,next.CropId,now,next.Premium);
                LastSownOrder=next.FirstOrder;
                next.Quantity--;next.FirstOrder++;
                if(next.Quantity==0)flight.Cargo.RemoveAt(0);
                flight.TargetPlot=-1;
                return true;
            }
        }
        static void MarkSeederReturning(SeedFlight flight){flight.Returning=true;flight.TargetPlot=-1;}
        public bool BeginSeederReturn(int number){
            lock(tradeLock){
                SeedFlight flight=GetSeederFlight(number);
                if(flight==null||flight.Cargo.Count==0||flight.Returning)return false;
                MarkSeederReturning(flight);
                return true;
            }
        }
        static bool Joinable(WarehouseLot left,WarehouseLot right){
            return left.BatchOrder==right.BatchOrder&&left.FirstOrder+left.Quantity==right.FirstOrder;
        }
        void PutLotBack(WarehouseLot lot){
            int at=SeedWarehouse.FindIndex(other=>other.FirstOrder>lot.FirstOrder);
            if(at<0)at=SeedWarehouse.Count;
            SeedWarehouse.Insert(at,lot);
            if(at>0&&Joinable(SeedWarehouse[at-1],lot)){
                WarehouseLot previous=SeedWarehouse[at-1];
                previous.Quantity+=lot.Quantity;
                SeedWarehouse.RemoveAt(at);
                lot=previous;at--;
            }
            if(at+1<SeedWarehouse.Count&&Joinable(lot,SeedWarehouse[at+1])){
                lot.Quantity+=SeedWarehouse[at+1].Quantity;SeedWarehouse.RemoveAt(at+1);
            }
        }
        public int UnloadSeeder(int number){
            lock(tradeLock){
                SeedFlight flight=GetSeederFlight(number);
                if(flight==null||!flight.Returning)return 0;
                int count=flight.Count;
                foreach(var lot in flight.Cargo)PutLotBack(lot);
                flight.Cargo.Clear();flight.Returning=false;flight.TargetPlot=-1;
                return count;
            }
        }
        public bool HasMachineWorked(int id){
            return id>=0&&id<Machines.Length&&id!=Cultivator&&(AutomationWorkMask&(1<<id))!=0;
        }
        public void RecordMachineWork(int id){
            if(id!=Cultivator&&MachineCount(id)>0)AutomationWorkMask|=1<<id;
        }
        public int CompletedAutomationKinds {
            get{
                int completed=0;
                foreach(int id in FieldMachines)if(HasMachineWorked(id))completed++;
                return completed;
            }
        }
        public bool AutomationComplete {get{return OpenCount==CellCount&&CompletedAutomationKinds==FieldMachines.Length;}}

        void StartSeedWarehouse(){
            SeedWarehouse=new List<WarehouseLot>();NextSeedOrder=1;LastSownOrder=0;
            SeederFlights=new SeedFlight[MachineCount(SeederDrone)];
            for(int n=0;n<SeederFlights.Length;n++)SeederFlights[n]=new SeedFlight();
        }
        bool ValidLots(List<WarehouseLot> lots,List<WarehouseLot> all,out long count){
            count=0;long end=1;
            if(lots==null)return false;
            foreach(var lot in lots){
                if(lot==null||lot.CropId<0||lot.CropId>=Crop.All.Length||lot.Quantity<1||
                   lot.BatchOrder<1||lot.FirstOrder<lot.BatchOrder||lot.FirstOrder<end||
                   lot.FirstOrder>NextSeedOrder-lot.Quantity)return false;
                end=lot.FirstOrder+lot.Quantity;
                if(end-lot.BatchOrder>int.MaxValue)return false;
                count+=lot.Quantity;all.Add(lot);
            }
            return true;
        }
        static bool Finite(float value){return !float.IsInfinity(value)&&!float.IsNaN(value);}
        bool ValidSeedWarehouse(){
            if(LastSownOrder<0||LastSownOrder>=NextSeedOrder||SeederFlights==null||
               SeederFlights.Length!=MachineCount(SeederDrone))return false;
            var all=new List<WarehouseLot>();long count;
            if(!ValidLots(SeedWarehouse,all,out count)||!SeedWarehouseUnlocked&&count!=0)return false;
            foreach(var flight in SeederFlights){
                if(flight==null||!ValidLots(flight.Cargo,all,out count)||count>int.MaxValue||
                   flight.TargetPlot< -1||flight.TargetPlot>=CellCount||!Finite(flight.GroundX)||
                   !Finite(flight.GroundY)||!Finite(flight.Height))return false;
                if(!AutoSowEnabled&&count>0)MarkSeederReturning(flight);
            }
            all.Sort((a,b)=>a.FirstOrder.CompareTo(b.FirstOrder));
            WarehouseLot previous=null;
            long expected=LastSownOrder+1;
            foreach(var lot in all){
                if(lot.FirstOrder!=expected)return false;
                if(previous!=null){
                    if(lot.BatchOrder==previous.BatchOrder){
                        if(lot.CropId!=previous.CropId||lot.Premium!=previous.Premium)return false;
                    }else if(lot.BatchOrder<expected)return false;
                }
                expected+=lot.Quantity;
                previous=lot;
            }
            return expected==NextSeedOrder;
        }
        bool ValidAutomation(){
            if(MachineCounts==null||MachineCounts.Length!=Machines.Length||
               Array.Exists(MachineCounts,n=>n<0||n>MaxDrones))return false;
            int availableWork=0;
            foreach(int id in FieldMachines)if(MachineCount(id)>0)availableWork|=1<<id;
            if((AutomationWorkMask&~availableWork)!=0)return false;
            if(PremiumSeeds==null||PremiumSeeds.Length!=Crop.All.Length||Array.Exists(PremiumSeeds,n=>n<0))return false;
            if(CultivationBatches==null||CultivationBatches.Length!=MachineCount(Cultivator))return false;
            foreach(SeedBatch batch in CultivationBatches){
                if(batch==null||batch.Quantity<0||batch.Quantity>MaxBatch)return false;
                if(batch.Quantity==0){if(batch.CropId!=-1||batch.ReadyAt!=0)return false;}
                else if(batch.CropId<0||batch.CropId>=Crop.All.Length||batch.ReadyAt<=0)return false;
            }
            return true;
        }
        void ImportMachines(bool original){
            MachineCounts=new int[Machines.Length];
            for(int i=0;i<MachinesOwned.Length;i++)if(MachinesOwned[i])MachineCounts[i]=1;
            if(original){
                if(MachinesOwned[0])StarCredits+=30;
                MachineCounts[0]=0;
            }else MachineCounts[Drone]=Math.Max(MachineCounts[Drone],legacyDroneCount);
            CultivationBatches=MachineCounts[Cultivator]>0?new[]{new SeedBatch()}:new SeedBatch[0];
            MachinesOwned=null;
        }
    }
}
