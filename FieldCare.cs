using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MountainTide
{
    public enum CareKind { None, Water, Pest, Bird }

    public sealed partial class Plot
    {
        [DefaultValue(CareKind.None)] public CareKind CareAlert;
        [DefaultValue(0)] public int CareTried;
        [DefaultValue(false)] public bool CareLesson, FinalCareWarning;
        [DefaultValue(false)] public bool SevereCareRisk, TerminalCareRisk;
        [DefaultValue(0)] public int CareThreat, CareLoss;
        [DefaultValue(CareKind.None)] public CareKind LossCause;
        [DefaultValue(-1)] public int FrozenYield = -1;
        [DefaultValue(0)] public int FrozenBaseYield;
        // Only time spent looking at the interactive farm counts toward a warning.
        internal double AlertSeconds;
        // A walking farmer or flying machine owns the job only for this play session.
        internal int CareOwner;
        internal bool CareExecuting;
    }

    public sealed partial class FarmState
    {
        public int CareLessons;
        public bool HasChasedBird, HasClearedPests;
        internal const int WarningSeconds = 10;
        bool careWasVisible;
        long lastCareView;
        double nextCareDelay;

        public void ResetCare(int i)
        {
            Plot p = Plots[i];
            ClearAlert(p);
            p.CareTried = p.CareLoss = 0;
            p.LossCause = CareKind.None;
            p.FrozenYield = -1;
            p.FrozenBaseYield = 0;
        }
        static void ClearAlert(Plot p)
        {
            p.CareAlert = CareKind.None;
            p.CareLesson = p.FinalCareWarning = p.SevereCareRisk = p.TerminalCareRisk = false;
            p.AlertSeconds = 0;
            p.CareThreat = 0;
            p.CareOwner = 0;
            p.CareExecuting = false;
        }
        public void ClearWaterAlert(int i)
        {
            if (Plots[i].CareAlert == CareKind.Water) ClearAlert(Plots[i]);
        }
        public int GetCareYield(int i, long now, int baseYield)
        {
            Plot p = Plots[i];
            if (p.CropId < 0 || Left(i, now) > 0) return 0;
            if (p.FrozenYield < 0) {
                bool attended = careWasVisible && now >= lastCareView && now-lastCareView <= 1;
                if (attended && p.CareThreat > 0 &&
                    (p.CareAlert == CareKind.Pest || p.CareAlert == CareKind.Bird)) {
                    p.CareLoss = p.CareThreat;
                    p.LossCause = p.CareAlert;
                }
                // Round the surviving crop to the nearest whole item; only a total loss yields zero.
                p.FrozenBaseYield = baseYield;
                p.FrozenYield = p.CareLoss >= 100 ? 0 : Math.Max(1, (baseYield * (100-p.CareLoss)+50)/100);
            }
            ClearAlert(p);
            return p.FrozenYield;
        }
        public string HarvestCareNote(int i, long now)
        {
            Plot p = Plots[i];
            if (p.CropId < 0 || Left(i, now) > 0) return "";
            GetCareYield(i,now,BaseYield(p));
            if (p.CareLoss == 0) return "";
            string cause = p.LossCause == CareKind.Bird ? "鸟害" : "虫害";
            return cause + (p.CareLoss == 100 ? "绝收" : "减产 "+p.CareLoss+"%") +
                "（原产 "+p.FrozenBaseYield+" 份）";
        }
        static int CareBit(CareKind kind) { return 1 << ((int)kind-1); }
        static uint CareRoll(Plot p, int i, CareKind kind)
        {
            // A particular crop and phase have one roll, including across a save/load.
            unchecked {
                uint x = (uint)p.Planted ^ ((uint)(i+1)*0x9e3779b9U) ^ ((uint)kind*0x85ebca6bU);
                x ^= x >> 16; x *= 0x85ebca6bU; x ^= x >> 13; x *= 0xc2b2ae35U;
                return x ^ (x >> 16);
            }
        }
        CareKind Opportunity(Plot p, long left)
        {
            if (left < WarningSeconds+2) return CareKind.None;
            double grown = 1 - left/(double)GrowthSeconds(p);
            if (grown >= .34 && grown < .57 && HarvestCount >= 5) return CareKind.Pest;
            if (grown >= .60 && DroneCount > 0 && (p.CropId == 0 || p.CropId == 2)) return CareKind.Bird;
            return CareKind.None;
        }
        static string CarePrompt(CareKind kind, bool lesson)
        {
            if (kind==CareKind.Water) return "水滴表示作物需要水，点气泡或拿水壶点作物，走近浇水。";
            string hint=kind==CareKind.Pest ? "叶片上有虫，点虫子或作物，走近捉虫。" : "飞鸟在谷物上盘旋，点气泡或作物，走近驱赶。";
            return lesson ? hint+" 首次练习不会损失收成。" : hint;
        }
        bool TryRaise(int i, long left, bool watching, out string message)
        {
            message = "";
            Plot p = Plots[i];
            CareKind kind = Opportunity(p, left);
            if (kind == CareKind.None) return false;
            if (!watching && MachineCount(kind==CareKind.Pest ? PestMachine : Scarecrow)==0) return false;
            int bit = CareBit(kind);
            if ((p.CareTried & bit) != 0) return false;
            bool unseen = (CareLessons & bit) == 0;
            bool lesson = watching && unseen;
            uint roll = CareRoll(p,i,kind);
            uint chance = kind == CareKind.Pest ? 22U : 24U;
            if (!lesson && roll%100 >= chance) {
                // The same offline roll stays stable, but the player can still see the first
                // practice warning upon returning to this crop during the same growth phase.
                if (!unseen) p.CareTried |= bit;
                return false;
            }
            // An unclaimed water request can give way to a more urgent pest or bird.
            // After treatment the crop can ask for its water again.
            ClearAlert(p);
            p.CareTried |= bit;
            if (lesson) CareLessons |= bit;
            p.CareAlert = kind;
            p.CareLesson = lesson;
            p.AlertSeconds = 0;
            p.SevereCareRisk = !lesson && kind != CareKind.Water && ((roll/100)%5 == 0 || p.CareLoss > 0);
            p.TerminalCareRisk = p.SevereCareRisk && DroneCount > 0 && OpenCount >= 9 &&
                                 (roll/500)%8 == 0 && left >= 2*WarningSeconds+2;
            if (watching) message=CarePrompt(kind,lesson);
            return true;
        }
        static string AdvanceWarning(Plot p, long left)
        {
            CareKind kind = p.CareAlert;
            string cause = kind == CareKind.Pest ? "虫害" : "鸟害";
            if (p.FinalCareWarning) {
                p.CareThreat = 100;
                return cause+"威胁整茬收成，成熟前赶到并处理仍可救回。";
            }
            p.CareThreat = p.SevereCareRisk ? 50 : 25;
            if (p.TerminalCareRisk && left >= WarningSeconds+2) {
                p.FinalCareWarning = true;
                p.AlertSeconds = 0;
                return "第二次警报！"+cause+"正在加重，抓紧去田边处理。";
            }
            return "若留到成熟，"+cause+"会使这块田减产 "+p.CareThreat+"%。";
        }
        public bool AdvanceFieldCare(long now, double visibleSeconds, bool watching, out string message)
        {
            bool changed = false;
            int active = 0;
            message = "";
            bool continuous = watching && careWasVisible && now >= lastCareView && now-lastCareView <= 1;
            double elapsed = Math.Max(0,Math.Min(.25,visibleSeconds));
            double dt = watching ? elapsed : 0;
            nextCareDelay = Math.Max(0,nextCareDelay-elapsed);
            for (int i=0; i<Plots.Length; i++) {
                Plot p=Plots[i];
                if (p.CropId < 0) continue;
                if (p.CareExecuting) {
                    if (p.CareAlert==CareKind.Bird || p.CareAlert==CareKind.Pest) active++;
                    continue;
                }
                // No new losses in an unobserved interval. On return every still-growing
                // plant gets a fresh window, including when a save contained its warning.
                if (!continuous && p.CareAlert != CareKind.None) {
                    changed |= p.CareThreat != 0 || p.FinalCareWarning;
                    p.CareThreat = 0;
                    p.FinalCareWarning = false;
                    p.AlertSeconds = 0;
                }
                long left=Left(i,now);
                if (left == 0) {
                    if (p.FrozenYield < 0 || p.CareAlert != CareKind.None) {
                        GetCareYield(i,now,BaseYield(p));
                        changed = true;
                    }
                    continue;
                }
                if (p.CareAlert == CareKind.None) continue;
                int bit=CareBit(p.CareAlert);
                if (watching && p.CareOwner<=0 && (CareLessons&bit)==0) {
                    p.CareLesson=true;
                    CareLessons|=bit;
                    if (message.Length==0) message=CarePrompt(p.CareAlert,true);
                    changed=true;
                }
                if (p.CareAlert == CareKind.Water) continue;
                bool warningRunning = p.CareThreat == 0 || (p.FinalCareWarning && p.CareThreat < 100);
                if (!p.CareLesson && warningRunning) {
                    p.AlertSeconds += dt;
                    if (p.AlertSeconds >= WarningSeconds) {
                        message = AdvanceWarning(p,left);
                        changed = true;
                    }
                }
                active++;
            }
            careWasVisible = watching;
            lastCareView = now;
            bool mayWater=watching || MachineCount(WaterDrone)>0;
            if (!mayWater && MachineCount(PestMachine)==0 && MachineCount(Scarecrow)==0) return changed;
            for (int i=0; i<Plots.Length; i++) {
                Plot p=Plots[i];
                if (p.CropId < 0 || p.CareOwner != 0 || p.CareLoss >= 100 ||
                    (p.CareAlert != CareKind.None && p.CareAlert != CareKind.Water)) continue;
                long left=Left(i,now);
                if (left<=0) continue;
                string raised;
                if (active<2 && nextCareDelay<=0 && TryRaise(i,left,watching,out raised)) {
                    if (message.Length==0) message=raised;
                    nextCareDelay=4;
                    active++;
                    changed=true;
                    continue;
                }
                // Every crop gets one real watering opportunity, staggered between 12–24%
                // of its own growth duration. Requests remain valid while a drone is on its way.
                double threshold=.12+((i+3*p.CropId)%7)*.02;
                if (!mayWater || p.CareAlert!=CareKind.None || p.Watered || left<WaterBonusSeconds+2 ||
                    1-left/(double)GrowthSeconds(p)<threshold) continue;
                int waterBit=CareBit(CareKind.Water);
                p.CareAlert=CareKind.Water;
                p.CareLesson=watching && (CareLessons&waterBit)==0;
                if (p.CareLesson) {
                    CareLessons|=waterBit;
                    if (message.Length==0) message=CarePrompt(CareKind.Water,true);
                }
                changed=true;
            }
            return changed;
        }
        bool LiveCare(int i, CareKind kind, long now)
        {
            return i>=0 && i<Plots.Length && kind!=CareKind.None && Plots[i].CropId>=0 &&
                   Plots[i].CareAlert==kind && Left(i,now)>0 && (kind!=CareKind.Water || CanWater(i,now));
        }
        public bool CareAvailable(int i,CareKind kind,long now)
        {
            return LiveCare(i,kind,now) && Plots[i].CareOwner==0;
        }
        public bool CareReservedBy(int i,CareKind kind,int owner,long now)
        {
            return owner!=0 && LiveCare(i,kind,now) && Plots[i].CareOwner==owner;
        }
        public bool TryReserveCare(int i,CareKind kind,int owner,long now)
        {
            if(owner==0 || !LiveCare(i,kind,now)) return false;
            Plot p=Plots[i];
            if(p.CareOwner!=0 && p.CareOwner!=owner) return false;
            p.CareOwner=owner;
            return true;
        }
        internal bool StartCareWork(int i,CareKind kind,int owner,long now)
        {
            if(!CareReservedBy(i,kind,owner,now))return false;
            Plots[i].CareExecuting=true;
            return true;
        }
        public void ReleaseCare(int i,int owner)
        {
            if(i>=0 && i<Plots.Length && Plots[i].CareOwner==owner && !Plots[i].CareExecuting) Plots[i].CareOwner=0;
        }
        public bool FinishCare(int i,CareKind kind,int owner,long now)
        {
            if(!CareReservedBy(i,kind,owner,now)) return false;
            if(kind==CareKind.Water){ApplyWater(i);return true;}
            Plot p=Plots[i];
            if (owner<0 && kind==CareKind.Pest) HasClearedPests=true;
            if (owner<0 && kind==CareKind.Bird) HasChasedBird=true;
            ClearAlert(p);
            return true;
        }
    }

    internal sealed partial class Game
    {
        const int FarmerCareOwner=-1;
        const string CareArrival="tend-alert";
        int manualCarePlot=-1;
        CareKind manualCareKind;
        double careWorkTime=-1;
        bool CareInputLocked {get{return careWorkTime>=0;}}

        void CancelManualCare()
        {
            if(CareInputLocked)return;
            if(manualCarePlot>=0){
                state.ReleaseCare(manualCarePlot,FarmerCareOwner);
                msgEnd=0;
            }
            manualCarePlot=-1;
            manualCareKind=CareKind.None;
            careWorkTime=-1;
        }
        bool BeginManualCare(int i,CareKind kind)
        {
            if (CareInputLocked || !InField || panel!="" || i<0 || i>=state.Plots.Length || kind==CareKind.None) return false;
            long now=state.Now;
            if (manualCarePlot==i && state.CareReservedBy(i,kind,FarmerCareOwner,now)) return true;
            if (!state.CareAvailable(i,kind,now)) {
                if (state.Plots[i].CareOwner>0 && state.Plots[i].CareAlert==kind)
                    Tell("已有无人机飞往这块田。");
                else if(kind==CareKind.Water) Tell("只有出现水滴的作物才需要浇水。");
                return false;
            }
            PointF bed=Bed(i);
            CloseDrawer();
            // WalkTo cancels any old task. The UI and fleet run on the same timer thread.
            WalkTo(new PointF(bed.X+(player.X<bed.X?-32:32),bed.Y+15),CareArrival);
            if(!state.TryReserveCare(i,kind,FarmerCareOwner,now)){StopWalking();return false;}
            manualCarePlot=i;
            manualCareKind=kind;
            careWorkTime=-1;
            Tell(kind==CareKind.Bird?"赶往田边驱鸟。":kind==CareKind.Pest?"赶往田边除虫。":"赶往田边浇水。");
            return true;
        }
        bool TryHandleCarePlot(int i)
        {
            if(!InField || panel!="" || drawer!="" || tool=="shovel" || i<0 || i>=state.Plots.Length) return false;
            CareKind kind=state.Plots[i].CareAlert;
            if(kind==CareKind.None || state.Left(i,state.Now)==0) return false;
            if(kind==CareKind.Water && tool!="hand" && tool!="harvest" && tool!="water") return false;
            BeginManualCare(i,kind);
            return true;
        }
        bool ArriveFieldCare(string action)
        {
            if(action!=CareArrival) return false;
            if(!state.StartCareWork(manualCarePlot,manualCareKind,FarmerCareOwner,state.Now)){
                CancelManualCare();
                return true;
            }
            flip=Bed(manualCarePlot).X<player.X;
            facingAway=false;
            pickTime=T;
            careWorkTime=0;
            Tell(manualCareKind==CareKind.Bird?"正在挥手赶鸟。":manualCareKind==CareKind.Pest?"正在捉虫。":"正在浇水。");
            return true;
        }
        static double CareDuration(CareKind kind){return kind==CareKind.Water?2.30:kind==CareKind.Pest?2.10:2.40;}
        double CareWorkDuration {get{return CareDuration(manualCareKind);}}
        void UpdateManualCare(long now,double dt,bool watching)
        {
            if(manualCarePlot<0) return;
            if(!CareInputLocked && !state.CareReservedBy(manualCarePlot,manualCareKind,FarmerCareOwner,now)){
                bool late=state.Plots[manualCarePlot].CropId>=0 && state.Left(manualCarePlot,now)==0;
                CancelManualCare();
                if(queuedAction==CareArrival) StopWalking();
                if(late && watching) Tell("这块田在照料完成前已成熟。");
                return;
            }
            if(careWorkTime<0 || !watching) return;
            double before=careWorkTime;
            careWorkTime+=Math.Max(0,dt);
            if(manualCareKind==CareKind.Water && before<CareWorkDuration*.28 && careWorkTime>=CareWorkDuration*.28)audio.Play(FarmSound.Water);
            if(careWorkTime<CareWorkDuration) return;
            int i=manualCarePlot;CareKind kind=manualCareKind;
            if(state.FinishCare(i,kind,FarmerCareOwner,now)){
                Store();
                PointF bed=Bed(i);
                Float(kind==CareKind.Water?"已浇水":kind==CareKind.Pest?"害虫已清除":"鸟群已驱散",new PointF(bed.X,bed.Y+67));
            }
            careWorkTime=-1;
            CancelManualCare();
        }
        void TickFieldCare(double dt)
        {
            bool watching=InField && panel=="" && drawer=="" && doorAt<0 && T-travelAt>=.9 &&
                WindowState!=FormWindowState.Minimized && Visible && Form.ActiveForm==this;
            string message;
            long now=state.Now;
            if (state.AdvanceFieldCare(now,dt,watching,out message)) Store();
            UpdateManualCare(now,dt,watching);
            if (watching && message.Length>0 && !CareInputLocked) Tell(message);
        }
        bool HandleFieldCare(string action)
        {
            if (!action.StartsWith("care-")) return false;
            string[] parts=action.Split('-');
            int i, token;
            if (!InField || panel!="" || drawer!="" || parts.Length!=3 ||
                !int.TryParse(parts[1],out i) || !int.TryParse(parts[2],out token) ||
                token<1 || token>3) return true;
            BeginManualCare(i,(CareKind)token);
            return true;
        }
        void DrawFieldCare(Graphics g)
        {
            if (!InField || panel!="" || drawer!="" || doorAt>=0 || T-travelAt<.9) return;
            long now=state.Now;
            for(int i=0;i<state.Plots.Length;i++){
                if(state.Plots[i].CareAlert!=CareKind.Bird || state.Plots[i].CropId<0 || state.Left(i,now)==0)continue;
                double progress=0;
                if(i==manualCarePlot && manualCareKind==CareKind.Bird && careWorkTime>=0)
                    progress=careWorkTime/CareWorkDuration;
                else {
                    FieldDrone drone=flightClaims[i];
                    if(drone!=null && drone.Care==CareKind.Bird && drone.Phase==FlightPhase.Work)
                        progress=drone.Age/drone.WorkSeconds;
                }
                DrawCareFlock(g,i,progress,paintedTime);
            }
            for (int i=0; i<state.Plots.Length; i++) {
                Plot p=state.Plots[i];
                if (p.CropId<0 || p.CareAlert==CareKind.None || state.Left(i,now)==0) continue;
                PointF ground=Bed(i);
                if (p.CareOwner>0) {
                    GraphicsState mark=g.Save();
                    try {
                        g.TranslateTransform(ground.X+18,ground.Y-13);
                        g.ScaleTransform(.58f,.58f);
                        DrawCareIcon(g,p.CareAlert,0,0);
                    } finally {g.Restore(mark);}
                    continue;
                }
                bool working=i==manualCarePlot && careWorkTime>=0;
                if(!working){DrawAlertBubble(g,i,p,ground,true);continue;}
                double progress=careWorkTime/CareWorkDuration;
                float remain=1-CareStep(progress,0,.16);
                if(remain>.01f){
                    GraphicsState shell=g.Save();
                    g.TranslateTransform(ground.X,ground.Y-38);
                    g.ScaleTransform(remain,remain);
                    g.TranslateTransform(-ground.X,-ground.Y+38);
                    DrawAlertBubble(g,i,p,ground,false);
                    g.Restore(shell);
                }
                DrawCareWorkLabel(g,ground,p.CareAlert,progress);
            }
        }
        void DrawAlertBubble(Graphics g,int i,Plot p,PointF ground,bool interactive)
        {
            float markerX=ground.X;
            float x=markerX-39, y=ground.Y-79;
            bool urgent=p.FinalCareWarning, water=p.CareAlert==CareKind.Water;
            Color rim=urgent?Col("9c3b24"):p.SevereCareRisk?Col("af5728"):water?Col("34748a"):Col("82592c");
            Color paper=urgent?Col("ffe2b8"):Col("fff5d5");
            string action="care-"+i+"-"+(int)p.CareAlert;
            if(water && !p.CareLesson){
                Plate(g,markerX-13,y+9,26,28,paper,rim,9);
                DrawCareIcon(g,CareKind.Water,markerX-12,y+10);
                if(interactive)HitBox(markerX-14,y+8,28,31,action);
                return;
            }
            PointF[] tail={new PointF(x+33,y+35),new PointF(x+39,y+44),new PointF(x+45,y+35)};
            using (Pen pen=new Pen(rim,1.8f)) { g.FillPolygon(B(paper),tail);g.DrawPolygon(pen,tail); }
            Plate(g,x,y,78,37,paper,rim,8);
            DrawCareIcon(g,p.CareAlert,x+5,y+5);
            string verb=water?"浇水":p.CareAlert==CareKind.Pest?"除虫":"赶鸟";
            TextAt(g,verb,15,rim,x+30,y+1,43,20,true,true);
            int seconds=Math.Max(1,(int)Math.Ceiling(FarmState.WarningSeconds-p.AlertSeconds));
            string label=p.CareLesson?"练习":water?"按需":p.CareThreat==100?"将绝收":
                urgent?"急警"+seconds+"秒":p.CareThreat>0?"将减 "+p.CareThreat+"%":
                p.SevereCareRisk?"重警"+seconds+"秒":seconds+"秒";
            TextAt(g,label,11,rim,x+23,y+19,53,15,true,true);
            if(interactive)HitBox(x,y,78,42,action);
        }
        void DrawCareIcon(Graphics g,CareKind kind,float x,float y)
        {
            GraphicsState before=g.Save();
            try {
                g.TranslateTransform(x,y);
                using(Pen outline=new Pen(Col("4d5238"),1.7f)) {
                    outline.StartCap=outline.EndCap=LineCap.Round;
                    if(kind==CareKind.Water) {
                        using(GraphicsPath drop=new GraphicsPath()) {
                            drop.AddBezier(12,1,10,7,3,13,3,17);
                            drop.AddBezier(3,17,3,27,20,27,21,17);
                            drop.AddBezier(21,17,21,12,15,7,12,1);
                            g.FillPath(B(Col("60b9d1")),drop);g.DrawPath(outline,drop);
                            using(Pen glint=new Pen(Col("d0f5f6"),2))g.DrawArc(glint,6,12,8,9,85,80);
                        }
                    }else if(kind==CareKind.Pest) {
                        for(int leg=0;leg<3;leg++){
                            float yy=11+leg*4;
                            g.DrawLine(outline,7,yy,3,yy+2);g.DrawLine(outline,16,yy,21,yy+2);
                        }
                        g.FillEllipse(B(Col("9aba49")),6,8,12,15);g.DrawEllipse(outline,6,8,12,15);
                        g.DrawLine(outline,12,9,12,21);g.FillEllipse(B(Col("536037")),8,4,9,7);
                        g.DrawLine(outline,9,6,6,3);g.DrawLine(outline,15,6,18,3);
                    }else {
                        PointF[] wing={new PointF(1,7),new PointF(13,12),new PointF(20,7),new PointF(18,16),new PointF(11,21),new PointF(6,18)};
                        g.FillPolygon(B(Col("a78b66")),wing);g.DrawPolygon(outline,wing);
                        g.FillEllipse(B(Col("667483")),14,5,8,9);g.DrawEllipse(outline,14,5,8,9);
                        g.FillPolygon(B(Col("d79a42")),new[]{new PointF(21,8),new PointF(26,10),new PointF(21,12)});
                        g.FillEllipse(B(Col("362e27")),18,7,2,2);
                    }
                }
            } finally { g.Restore(before); }
        }
    }
}
