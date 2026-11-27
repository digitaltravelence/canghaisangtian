using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MountainTide
{
    internal sealed partial class Game
    {
        enum FlightPhase { Patrol, Approach, Lower, Work, Lift, Return,
            Parked, ToDepot, Docking, Transfer, Staged, AwaitOrder, ToStand, Landing }

        sealed class FieldDrone
        {
            internal readonly int Model, Number, Owner;
            internal readonly CareKind Care;
            internal readonly float Width, WorkHeight, Airspeed;
            internal readonly double WorkSeconds;
            internal FlightPhase Phase;
            internal PointF Ground, Home;
            internal float Height = CruiseHeight, LiftFrom, Bank;
            internal double Age, SearchIn;
            internal int Plot = -1, Crop;
            internal bool SeedReturning;
            internal long Planted;
            internal string Result = "";

            internal FieldDrone(int model, int number, PointF home)
            {
                Model = model; Number = number; Owner = 1 + model * FarmState.MaxDrones + number;
                Ground = Home = home;
                Width = 47; WorkHeight = 44; Airspeed = 112; WorkSeconds = 1.7;
                switch (model)
                {
                    case FarmState.Drone: Width = 51; Airspeed = 107; WorkSeconds = 2.65; break;
                    case FarmState.PestMachine: Care = CareKind.Pest; Width = 44; Airspeed = 155; break;
                    case FarmState.Scarecrow: Care = CareKind.Bird; Width = 48; WorkHeight = 55; Airspeed = 155; break;
                    case FarmState.WaterDrone: Care = CareKind.Water; Width = 50; WorkHeight = 46; Airspeed = 140; break;
                    case FarmState.FertilizerDrone: Width = 46; WorkHeight = 34; WorkSeconds = 1.95; break;
                    case FarmState.SeederDrone: Width = 42; WorkHeight = 29; WorkSeconds = 1.8; break;
                }
                if(Care!=CareKind.None) WorkSeconds=CareDuration(Care);
                SearchIn = .08 + (Owner % 6) * .047;
            }
        }

        const float CruiseHeight = 79;
        const double LowerSeconds = .9, LiftSeconds = 1.08;
        // Collect urgent field alerts before sending the less time-sensitive machines.
        static readonly int[] FlightModels = { FarmState.PestMachine, FarmState.Scarecrow, FarmState.WaterDrone,
            FarmState.Drone, FarmState.FertilizerDrone, FarmState.SeederDrone };
        readonly List<FieldDrone> fleet = new List<FieldDrone>();
        readonly int[] fleetCounts = new int[FarmState.Machines.Length];
        readonly FieldDrone[] flightClaims = new FieldDrone[FarmState.CellCount];
        double fleetTime, lastWorkSound = -10;

        PointF PatrolPoint(int model, int number)
        {
            double lane = number * 2.39996 + (model - FarmState.Drone) * .94;
            double t = fleetTime * .078 + lane;
            return FieldPoint((float)(FieldColumns * (.5 + .36 * Math.Sin(t))),
                (float)(FieldRows * (.5 + .32 * Math.Sin(t * .82 + lane * .54))));
        }

        void SyncFleet()
        {
            foreach (int model in FlightModels)
            {
                int want = state.MachineCount(model), previous = fleetCounts[model];
                if (want == previous) continue;
                if (want < previous)
                {
                    for (int i = fleet.Count - 1; i >= 0; i--)
                    {
                        FieldDrone d = fleet[i];
                        if (d.Model == model && d.Number >= want) { ReleaseFlight(d); fleet.RemoveAt(i); }
                    }
                }
                for (int number = previous; number < want; number++)
                {
                    FieldDrone d = new FieldDrone(model, number, PatrolPoint(model, number));
                    if (model == FarmState.SeederDrone) RestoreSeeder(d);
                    fleet.Add(d);
                }
                fleetCounts[model] = want;
            }
        }

        static float SmoothFlight(double fraction)
        {
            double p = Math.Max(0, Math.Min(1, fraction));
            return (float)(p * p * (3 - 2 * p));
        }

        static bool FlyToward(FieldDrone d, PointF to, double dt, float topSpeed)
        {
            float dx = to.X-d.Ground.X, dy = to.Y-d.Ground.Y;
            float dist = (float)Math.Sqrt(dx*dx+dy*dy);
            float step = Math.Min(dist, Math.Min(topSpeed, 20+dist*1.25f)*(float)dt);
            float vx = dist > .01f ? dx/dist : 0;
            d.Bank += (vx*Math.Min(8,dist*.17f)-d.Bank)*(float)Math.Min(1,dt*4);
            if (step >= dist) { d.Ground = to; return true; }
            d.Ground = new PointF(d.Ground.X+vx*step, d.Ground.Y+dy/dist*step);
            return false;
        }

        int ManualFieldTarget()
        {
            if (!InField) return -1;
            string action = heldCrop != "" ? heldCrop : walking ? queuedAction : "";
            int plot;
            if (action.StartsWith("plot") && int.TryParse(action.Substring(4), out plot)) return plot;
            return drawer == "seeds" && activePlot >= 0 ? activePlot : -1;
        }

        bool Eligible(FieldDrone d, int i, long now, bool inFlight)
        {
            Plot p = state.Plots[i];
            if (!p.Opened) return false;
            if (d.Care != CareKind.None)
                return inFlight ? state.CareReservedBy(i,d.Care,d.Owner,now) : state.CareAvailable(i,d.Care,now);
            if (d.Model == FarmState.FertilizerDrone)
                return p.CareAlert == CareKind.None && state.CanAutoFeed(i,now);
            return p.CropId >= 0 && state.Left(i,now) == 0;
        }

        void ReleaseFlight(FieldDrone d)
        {
            if (d.Plot < 0) return;
            if (d.Care != CareKind.None) state.ReleaseCare(d.Plot,d.Owner);
            if (flightClaims[d.Plot] == d) flightClaims[d.Plot] = null;
            d.Plot = -1;
        }

        void LiftDrone(FieldDrone d, string result)
        {
            ReleaseFlight(d);
            d.Phase=FlightPhase.Lift; d.LiftFrom=d.Height; d.Age=0; d.Result=result;
        }

        void FindFlightJob(FieldDrone d, long now, int manual)
        {
            if (d.Model==FarmState.FertilizerDrone)
            {
                long supplies=state.Fertilizer;
                foreach (FieldDrone other in fleet)
                    if (other.Model==d.Model && other.Plot>=0) supplies--;
                if (supplies<=0) return;
            }
            int best = -1;
            float nearest = float.MaxValue;
            for (int i=0; i<state.Plots.Length; i++)
            {
                if (flightClaims[i]!=null || i==manual || !Eligible(d,i,now,false)) continue;
                PointF bed = Bed(i);
                float dx=bed.X-d.Ground.X,dy=bed.Y-d.Ground.Y, dist=dx*dx+dy*dy;
                if (dist < nearest) { nearest=dist; best=i; }
            }
            if (best < 0 || d.Care!=CareKind.None && !state.TryReserveCare(best,d.Care,d.Owner,now)) return;
            Plot p = state.Plots[best];
            d.Plot=best; d.Crop=p.CropId; d.Planted=p.Planted;
            d.Home=d.Ground; d.Phase=FlightPhase.Approach; d.Age=0;
            flightClaims[best]=d;
        }

        bool SettleFlight(FieldDrone d, long now)
        {
            int i=d.Plot;
            string result="";
            // Inventory and crop state belong to the same transaction as manual work.
            // Recheck at touchdown: another drone can have used the last bag.
            if (Eligible(d,i,now,true))
            {
                if (d.Care!=CareKind.None)
                {
                    if (state.FinishCare(i,d.Care,d.Owner,now))
                        result=d.Care==CareKind.Pest ? "已除虫" : d.Care==CareKind.Bird ? "已驱鸟" : "已浇水";
                }
                else if (d.Model==FarmState.FertilizerDrone)
                {
                    if (state.AutoFeed(i,now)) result="已施肥";
                }
                else
                {
                    string note=state.HarvestCareNote(i,now);
                    int qty=state.Harvest(i,now);
                    result=qty>0 ? "+"+qty+" "+Crop.All[d.Crop].Name : "绝收";
                    if (InField && panel=="" && !string.IsNullOrEmpty(note)) Tell("无人机收获 "+qty+" 份 · "+note);
                }
            }
            if (result!="") state.RecordMachineWork(d.Model);
            LiftDrone(d,result);
            return result!="";
        }

        void StartDroneWork(FieldDrone d)
        {
            d.Phase=FlightPhase.Work; d.Age=0;
            if (d.Model==FarmState.Drone) PlayDroneSound(FarmSound.Reap);
            else if (d.Model==FarmState.SeederDrone) PlayDroneSound(FarmSound.Sow);
            else if (d.Model==FarmState.FertilizerDrone) PlayDroneSound(FarmSound.Fertilize);
        }
        void PlayDroneSound(FarmSound sound)
        {
            if (!InField || panel!="" || fleetTime-lastWorkSound<.34) return;
            audio.Play(sound); lastWorkSound=fleetTime;
        }

        void UpdateHarvestFleet(double dt, long now)
        {
            if (dt<=0) return;
            fleetTime+=dt; SyncFleet();
            int manual=ManualFieldTarget();
            Array.Clear(flightClaims,0,flightClaims.Length);
            foreach (FieldDrone d in fleet)
            {
                if (d.Plot<0) continue;
                if (d.Model==FarmState.SeederDrone)
                {
                    flightClaims[d.Plot]=d;
                    continue;
                }
                Plot p=state.Plots[d.Plot];
                bool same=p.CropId==d.Crop && p.Planted==d.Planted;
                // Care reservations are first-come, including those held by the farmer.
                if (!same || d.Care==CareKind.None && d.Plot==manual || !Eligible(d,d.Plot,now,true))
                    LiftDrone(d,"");
                else flightClaims[d.Plot]=d;
            }
            bool changed=false;
            foreach (FieldDrone d in fleet)
            {
                d.Age+=dt;
                if (d.Model==FarmState.SeederDrone)
                {
                    if (UpdateSeeder(d,dt,now,manual)) changed=true;
                    continue;
                }
                switch (d.Phase)
                {
                    case FlightPhase.Patrol:
                        FlyToward(d,PatrolPoint(d.Model,d.Number),dt,25);
                        d.SearchIn-=dt;
                        if (d.SearchIn<=0) { d.SearchIn=.36+(d.Owner%5)*.045; FindFlightJob(d,now,manual); }
                        break;
                    case FlightPhase.Approach:
                        if (FlyToward(d,Bed(d.Plot),dt,d.Airspeed)) { d.Phase=FlightPhase.Lower; d.Age=0; }
                        break;
                    case FlightPhase.Lower:
                        d.Bank*=(float)Math.Max(0,1-dt*4);
                        d.Height=CruiseHeight+(d.WorkHeight-CruiseHeight)*SmoothFlight(d.Age/LowerSeconds);
                        if (d.Age>=LowerSeconds) {
                            if(d.Care==CareKind.None || state.StartCareWork(d.Plot,d.Care,d.Owner,now))StartDroneWork(d);
                            else LiftDrone(d,"");
                        }
                        break;
                    case FlightPhase.Work:
                        if (d.Care==CareKind.Water && d.Age>=d.WorkSeconds*.16 && d.Age-dt<d.WorkSeconds*.16)
                            PlayDroneSound(FarmSound.Water);
                        if (d.Age>=d.WorkSeconds && SettleFlight(d,now)) changed=true;
                        break;
                    case FlightPhase.Lift:
                        d.Height=d.LiftFrom+(CruiseHeight-d.LiftFrom)*SmoothFlight(d.Age/LiftSeconds);
                        if (d.Age>=LiftSeconds) { d.Phase=FlightPhase.Return; d.Age=0; d.Result=""; }
                        break;
                    case FlightPhase.Return:
                        if (FlyToward(d,d.Home,dt,d.Airspeed)) { d.Phase=FlightPhase.Patrol; d.Age=0; d.SearchIn=.12; }
                        break;
                }
            }
            if (changed) Store();
        }

        float HarvestPlantBend(int plot, int tuft)
        {
            FieldDrone d=flightClaims[plot];
            if (d==null || (d.Phase!=FlightPhase.Work && d.Phase!=FlightPhase.Lower)) return 0;
            double power=d.Model==FarmState.Drone ? 1 : .38;
            if (d.Phase==FlightPhase.Lower) power*=.42*SmoothFlight(d.Age/LowerSeconds);
            return (float)(power*(3*Math.Sin(fleetTime*13+tuft*1.7)+(1-tuft)*2));
        }

        void DrawHarvestShadows(Graphics g)
        {
            foreach (FieldDrone d in fleet)
            {
                if (d.Model==FarmState.SeederDrone)
                {
                    float radius=9+7*SeederAirFraction(d);
                    g.FillEllipse(B(Color.FromArgb(48,58,51,29)),d.Ground.X-radius,d.Ground.Y-3,radius*2,6);
                    continue;
                }
                float proximity=(CruiseHeight-d.Height)/(CruiseHeight-d.WorkHeight);
                float rx=d.Width*.37f+proximity*5,ry=6+proximity*2;
                g.FillEllipse(B(Color.FromArgb(31,59,59,36)),d.Ground.X-rx-3,d.Ground.Y-ry,rx*2+6,ry*2);
                g.FillEllipse(B(Color.FromArgb(45,58,51,29)),d.Ground.X-rx,d.Ground.Y-ry+2,rx*2,ry*2-3);
            }
        }

        void DrawHarvestFleet(Graphics g)
        {
            foreach (FieldDrone d in fleet)
            {
                float bob=(float)Math.Sin(fleetTime*3.3+d.Owner*2.1)*(d.Phase==FlightPhase.Work?.65f:1.55f);
                bool seeder=d.Model==FarmState.SeederDrone;
                float lift=seeder?SeederAirFraction(d):1;
                bob*=lift;
                PointF socket=PointF.Empty; bool atHatch=false;
                float contact=seeder?SeedDockContact(d,out socket,out atHatch):0;
                bool mainSocket=seeder && d.Number==0 && !atHatch;
                float x=d.Ground.X,y=d.Ground.Y-d.Height+bob+SeedSeatOffset(mainSocket,contact);
                Bitmap icon=art.MachineIcon(d.Model);
                float h=d.Width*icon.Height/icon.Width;
                GraphicsState transform=g.Save();
                g.TranslateTransform(x,y); g.RotateTransform(d.Bank);
                if (seeder)
                {
                    float size=SeedBodyScale(lift,mainSocket,contact);
                    g.ScaleTransform(size,size);
                }
                if (d.Phase==FlightPhase.Work) DrawDroneWork(g,d,h);
                Sprite(g,icon,-d.Width/2,-h/2,d.Width,h);
                if (!seeder || (!SeederResting(d) && d.Phase!=FlightPhase.Transfer)) AnimateRotors(g,d,h);
                if (seeder) DrawSeedFreight(g,d,h);
                if (seeder && SeederResting(d)) DrawSeederIdleLight(g,d,h);
                g.Restore(transform);
                if (contact>0) DrawCradleLip(g,socket,atHatch,mainSocket,contact);
                if (d.Result!="" && d.Phase==FlightPhase.Lift)
                {
                    float w=g.MeasureString(d.Result,F(12,true)).Width+12;
                    Plate(g,x-w/2,y-h/2-20,w,17,Dark,Rim,6);
                    TextAt(g,d.Result,12,gold,x-w/2,y-h/2-21,w,19,true,true);
                }
            }
        }

        void AnimateRotors(Graphics g, FieldDrone d, float height)
        {
            // The individually painted disks define their hubs as fractions of each tight sprite.
            PointF left=RotorLeft[d.Model],right=RotorRight[d.Model];
            double spin=fleetTime*29+d.Owner;
            DrawPropeller(g,(left.X-.5f)*d.Width,(left.Y-.5f)*height,d.Width*.104f,spin);
            DrawPropeller(g,(right.X-.5f)*d.Width,(right.Y-.5f)*height,d.Width*.104f,-spin+1);
        }
        static readonly PointF[] RotorLeft = {
            new PointF(.241f,.087f), PointF.Empty, new PointF(.230f,.082f), new PointF(.235f,.085f),
            new PointF(.225f,.068f), new PointF(.226f,.072f), new PointF(.228f,.077f)
        };
        static readonly PointF[] RotorRight = {
            new PointF(.794f,.094f), PointF.Empty, new PointF(.790f,.082f), new PointF(.778f,.087f),
            new PointF(.779f,.068f), new PointF(.787f,.072f), new PointF(.796f,.084f)
        };

        void DrawPropeller(Graphics g,float x,float y,float radius,double spin)
        {
            float dx=(float)Math.Cos(spin)*radius,dy=(float)Math.Sin(spin)*radius*.32f;
            using (Pen gleam=new Pen(Color.FromArgb(200,229,245,213),1.25f))
                g.DrawLine(gleam,x-dx,y-dy,x+dx,y+dy);
        }

        void DrawDroneWork(Graphics g,FieldDrone d,float h)
        {
            float nozzle=h*.36f;
            if (d.Model==FarmState.Drone)
            {
                float sweep=(float)Math.Sin(d.Age*8.2+d.Owner);
                using (Pen steel=new Pen(Color.FromArgb(247,223,234,218),2))
                {
                    g.DrawArc(steel,-16+sweep*4,nozzle,18,9,24,130);
                    g.DrawArc(steel,-2-sweep*4,nozzle,18,9,26,130);
                }
                for (int n=0;n<6;n++)
                {
                    double p=(d.Age*1.4+n*.17)%1;
                    float x=(float)((1-p)*Math.Sin(n*3.8+d.Owner)*23),y=(float)(nozzle+17-p*17);
                    g.FillEllipse(B(n%2==0?gold:Color.PaleGoldenrod),x-1.4f,y-1,3.2f,2);
                }
            }
            else if(d.Care!=CareKind.None)DrawDroneTreatment(g,d,nozzle);
            else
            {
                bool sowing=d.Model==FarmState.SeederDrone;
                for (int n=0;n<(sowing?3:8);n++)
                {
                    double p=(d.Age*(sowing?1.35:1.8)+n*(sowing?.34:.13))%1;
                    float x=(float)(Math.Sin(n*2.3+d.Age*3)*p*(sowing?5:16)),y=nozzle+(float)(p*p)*22;
                    g.FillEllipse(B(sowing?Color.FromArgb(111,71,35):Color.FromArgb(226,173,89)),x-1.8f,y-1.3f,3.6f,2.8f);
                    g.FillEllipse(B(Color.FromArgb(251,216,143)),x-1,y-1,.95f,1.2f);
                }
            }
        }
        void DrawDroneTreatment(Graphics g,FieldDrone d,float nozzle)
        {
            double u=Math.Min(1,d.Age/d.WorkSeconds);
            float power=SmoothFlight(u/.22)*SmoothFlight((1-u)/.20);
            Color colour=d.Care==CareKind.Water?Col("b9eff4"):d.Care==CareKind.Pest?Col("bde5aa"):Col("ffe3a0");
            // A small indicator warms up before the nozzle, collector or chime starts.
            using(Brush light=new SolidBrush(Color.FromArgb((int)(185*power),colour)))
                g.FillEllipse(light,-2.6f,nozzle-2,5.2f,3.8f);
            if(d.Care==CareKind.Bird){
                for(int k=0;k<3;k++){
                    double age=(u-.14-k*.18)/.42;
                    if(age<=0||age>=1)continue;
                    float r=5+28*SmoothFlight(age);
                    using(Pen wave=new Pen(Color.FromArgb((int)(190*Math.Sin(Math.PI*age)),colour),1.5f)){
                        wave.StartCap=wave.EndCap=LineCap.Round;
                        g.DrawArc(wave,-r,nozzle-r*.22f,r*2,r*.95f,24,132);
                    }
                }
                return;
            }
            bool water=d.Care==CareKind.Water;
            int drops=water?28:15;
            for(int k=0;k<drops;k++){
                double age=(u-(.16+.48*k/(drops-1)))/.31;
                if(age<=0||age>=1)continue;
                float t=(float)age,spread=(float)Math.Sin(k*2.4);
                float x=water?spread*(2+13*t):spread*(1-t)*20;
                float y=water?nozzle+25*t*t:nozzle+24*(1-SmoothFlight(t));
                int alpha=(int)(210*Math.Sin(Math.PI*age));
                if(water){
                    using(Pen drop=new Pen(Color.FromArgb(alpha,colour),1.7f)){
                        drop.StartCap=drop.EndCap=LineCap.Round;
                        g.DrawLine(drop,x,y,x+spread*.85f,y+1+3*t);
                    }
                }else{
                    x+=(float)Math.Sin(t*Math.PI*2+k)*3*(1-t);
                    using(Brush mote=new SolidBrush(Color.FromArgb(alpha,colour)))
                        g.FillEllipse(mote,x-1.7f,y-1.2f,3.4f,2.4f);
                }
            }
        }
    }
}
