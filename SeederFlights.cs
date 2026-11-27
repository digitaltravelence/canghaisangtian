using System;
using System.Drawing;

namespace MountainTide
{
    internal sealed partial class Game
    {
        const float SeedParkHeight = 11;
        const double SeedDockSeconds = .65, SeedTransferSeconds = .95;
        FieldDrone seedBerth;

        static PointF SeedDockPoint(int unit)
        {
            return unit == 0 ? new PointF(957,533) : SeedRackPoint(unit-1);
        }

        static PointF SeedRackPoint(int slot)
        {
            int row = slot / 9;
            return new PointF(1007 + (slot % 9) * 26 + (row % 2) * 5, 542 + row * 23);
        }

        static float SeedDistance(PointF a, PointF b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        static float SeederAirFraction(FieldDrone d)
        {
            return Math.Max(0, Math.Min(1, (d.Height - SeedParkHeight) / (d.WorkHeight - SeedParkHeight)));
        }

        static bool SeederResting(FieldDrone d)
        {
            return d.Phase == FlightPhase.Parked || (d.Phase == FlightPhase.Return &&
                d.Ground == d.Home && d.Height <= SeedParkHeight);
        }

        void RestoreSeeder(FieldDrone d)
        {
            SeedFlight flight = state.GetSeederFlight(d.Number);
            d.Ground = d.Home = SeedDockPoint(d.Number);
            d.Phase = FlightPhase.Parked;
            d.Height = SeedParkHeight;
            if (flight == null) return;
            if (flight.HasPosition)
            {
                d.Ground = new PointF(flight.GroundX, flight.GroundY);
                d.Height = flight.Height;
            }
            else if (flight.Count > 0) d.Ground = SeedDepotDock;
            if (flight.Count > 0)
            {
                d.Plot = flight.TargetPlot;
                d.SeedReturning = flight.Returning;
                d.Phase = flight.Returning ? FlightPhase.Return : d.Plot < 0 ? FlightPhase.Staged : FlightPhase.Approach;
            }
            else if (Math.Abs(d.Height-SeedParkHeight)<=1 && SeedDistance(d.Ground,SeedRackPoint(d.Number))<=1)
            {
                // Older saves parked every aircraft on the separate rack. Let it slide into its new berth.
                d.Phase = FlightPhase.ToStand;
            }
            else if (SeedDistance(d.Ground, d.Home) > 1 || Math.Abs(d.Height - SeedParkHeight) > 1)
            {
                d.SeedReturning = true;
                d.Phase = FlightPhase.Return;
            }
        }

        static void SeedPhase(FieldDrone d, FlightPhase phase)
        {
            d.Phase = phase; d.Age = 0;
        }

        int EmptySeedPlots(int manual)
        {
            int count = 0;
            for (int i = 0; i < state.Plots.Length; i++)
                if (i != manual && state.Plots[i].Opened && state.Plots[i].CropId < 0) count++;
            return count;
        }

        // Allocate empty plots among waiting aircraft when each actually reaches the hatch.
        int SeedLoadShare(int manual)
        {
            if (!state.AutoSowEnabled || state.WarehouseSeedCount == 0) return 0;
            int open = EmptySeedPlots(manual), waiting = 0;
            foreach (FieldDrone other in fleet)
            {
                if (other.Model != FarmState.SeederDrone) continue;
                SeedFlight load = state.GetSeederFlight(other.Number);
                if (load.Count > 0)
                {
                    if (!load.Returning) open -= load.Count;
                }
                else if (other.Phase == FlightPhase.Parked || other == seedBerth) waiting++;
            }
            return open <= 0 ? 0 : (open - 1) / Math.Max(1, waiting) + 1;
        }

        bool HasWorkingSeedLeader()
        {
            foreach (FieldDrone other in fleet)
            {
                if (other.Model != FarmState.SeederDrone) continue;
                SeedFlight load = state.GetSeederFlight(other.Number);
                if (load.Count > 0 && !load.Returning && state.IsNextSeeder(other.Number)) return true;
            }
            return false;
        }

        int ClosestSeedPlot(FieldDrone d, SeedFlight flight, int manual)
        {
            // A later carrier may fly ahead, but cannot reserve the last plot needed by an older shipment.
            int remaining = EmptySeedPlots(manual);
            if (!state.IsNextSeeder(d.Number))
            {
                long order = flight.Cargo[0].FirstOrder;
                foreach (FieldDrone other in fleet)
                {
                    if (other.Model != FarmState.SeederDrone || other == d) continue;
                    SeedFlight load = state.GetSeederFlight(other.Number);
                    if (load.Count > 0 && load.Cargo[0].FirstOrder < order) remaining -= load.Count;
                }
            }
            if (remaining <= 0) return -1;

            int best = -1;
            float closest = float.MaxValue;
            for (int i = 0; i < state.Plots.Length; i++)
            {
                if (i == manual || !state.Plots[i].Opened || state.Plots[i].CropId >= 0) continue;
                if (flightClaims[i] != null && flightClaims[i] != d) continue;
                float distance = SeedDistance(d.Ground, Bed(i));
                if (distance < closest) { closest = distance; best = i; }
            }
            return best;
        }

        void SeedTarget(FieldDrone d, SeedFlight flight, int plot)
        {
            ReleaseFlight(d);
            d.Plot = flight.TargetPlot = plot;
            if (plot >= 0) flightClaims[plot] = d;
        }

        void StartSeedReturn(FieldDrone d, SeedFlight flight)
        {
            SeedTarget(d, flight, -1);
            d.SeedReturning = true;
            state.BeginSeederReturn(d.Number);
            d.Result = "";
            if (d.Phase != FlightPhase.ToDepot && d.Phase != FlightPhase.Docking && d.Phase != FlightPhase.Transfer)
                SeedPhase(d, FlightPhase.Return);
        }

        bool HasReturningSeeder()
        {
            foreach (FieldDrone other in fleet)
            {
                if (other.Model != FarmState.SeederDrone) continue;
                SeedFlight load = state.GetSeederFlight(other.Number);
                if (load.Returning || other.Phase == FlightPhase.Return || (other.SeedReturning &&
                    (other.Phase == FlightPhase.ToDepot || other.Phase == FlightPhase.Docking ||
                     other.Phase == FlightPhase.Transfer))) return true;
            }
            return false;
        }

        static void AscendSeeder(FieldDrone d, double dt)
        {
            d.Height = Math.Min(CruiseHeight, d.Height + (float)(dt * 72));
        }

        bool UpdateSeeder(FieldDrone d, double dt, long now, int manual)
        {
            SeedFlight flight = state.GetSeederFlight(d.Number);
            bool changed = false;
            if (seedBerth == d && d.Phase != FlightPhase.ToDepot && d.Phase != FlightPhase.Docking &&
                d.Phase != FlightPhase.Transfer && SeedDistance(d.Ground, SeedDepotDock) > 28 * 28)
                seedBerth = null;

            if (flight.Count > 0)
            {
                bool atTarget = d.Plot >= 0 && (!state.Plots[d.Plot].Opened || state.Plots[d.Plot].CropId >= 0);
                bool departing = d.Phase != FlightPhase.Return && d.Phase != FlightPhase.ToDepot &&
                    d.Phase != FlightPhase.Docking && d.Phase != FlightPhase.Transfer;
                if (departing && (flight.Returning || !state.AutoSowEnabled || atTarget ||
                    EmptySeedPlots(-1) == 0 || !HasWorkingSeedLeader()))
                {
                    // A manually filled target cancels this whole sortie, even when another bed is free.
                    StartSeedReturn(d, flight);
                    changed = true;
                }
            }

            switch (d.Phase)
            {
                case FlightPhase.Parked:
                    d.SearchIn = Math.Max(0, d.SearchIn - dt);
                    if (d.SearchIn <= 0 && seedBerth == null && !HasReturningSeeder() && SeedLoadShare(manual) > 0)
                    {
                        seedBerth = d;
                        SeedPhase(d, FlightPhase.ToDepot);
                    }
                    break;

                case FlightPhase.Return:
                    if (seedBerth == null || seedBerth == d)
                    {
                        seedBerth = d;
                        SeedPhase(d, FlightPhase.ToDepot);
                    }
                    else if (FlyToward(d, d.Home, dt, d.Airspeed))
                    {
                        d.Height = Math.Max(SeedParkHeight, d.Height - (float)(dt * 55));
                        d.Bank *= (float)Math.Max(0, 1 - dt * 5);
                    }
                    else AscendSeeder(d, dt);
                    break;

                case FlightPhase.ToDepot:
                    // A short hop from the adjacent parking stand needs less lift than a cross-field return.
                    d.Height = Math.Min(CruiseHeight, d.Height + (float)(45 * dt));
                    if (FlyToward(d, SeedDepotDock, dt, d.Airspeed))
                    {
                        d.LiftFrom = d.Height;
                        SeedPhase(d, FlightPhase.Docking);
                    }
                    break;

                case FlightPhase.Docking:
                    d.Bank *= (float)Math.Max(0, 1 - dt * 5);
                    d.Height = d.LiftFrom + (SeedParkHeight - d.LiftFrom) * SmoothFlight(d.Age / SeedDockSeconds);
                    if (d.Age >= SeedDockSeconds) SeedPhase(d, FlightPhase.Transfer);
                    break;

                case FlightPhase.Transfer:
                    if (d.Age < SeedTransferSeconds) break;
                    if (d.SeedReturning || flight.Returning)
                    {
                        if (flight.Returning && state.UnloadSeeder(d.Number) > 0) changed = true;
                        SeedPhase(d, FlightPhase.ToStand);
                    }
                    else if (flight.Count == 0 && state.LoadSeeder(d.Number, SeedLoadShare(manual)) > 0)
                    {
                        SeedPhase(d, FlightPhase.Staged);
                        changed = true;
                    }
                    else SeedPhase(d, FlightPhase.ToStand);
                    break;

                case FlightPhase.Staged:
                case FlightPhase.Approach:
                {
                    if (flight.Count == 0) { StartSeedReturn(d, flight); break; }
                    bool farmerChoosing = d.Plot >= 0 && d.Plot == manual;
                    int next = farmerChoosing ? d.Plot : ClosestSeedPlot(d, flight, manual);
                    if (d.Plot != next) { SeedTarget(d, flight, next); changed = true; }
                    AscendSeeder(d, dt);
                    if (next < 0)
                    {
                        SeedPhase(d, FlightPhase.Staged);
                        FlyToward(d, d.Home, dt, d.Airspeed);
                    }
                    else
                    {
                        if (d.Phase != FlightPhase.Approach) SeedPhase(d, FlightPhase.Approach);
                        if (FlyToward(d, Bed(next), dt, d.Airspeed))
                        {
                            d.LiftFrom = d.Height;
                            SeedPhase(d, FlightPhase.Lower);
                        }
                    }
                    break;
                }

                case FlightPhase.Lower:
                case FlightPhase.AwaitOrder:
                case FlightPhase.Work:
                    if (d.Plot == manual && d.Phase != FlightPhase.Lower)
                    {
                        // Keep the reservation visible to hand sowing until the farmer finishes choosing.
                        if (d.Phase == FlightPhase.Work) SeedPhase(d, FlightPhase.AwaitOrder);
                        break;
                    }
                    if (d.Plot != manual && !state.IsNextSeeder(d.Number) && ClosestSeedPlot(d, flight, manual) < 0)
                    {
                        SeedTarget(d, flight, -1);
                        SeedPhase(d, FlightPhase.Staged);
                        changed = true;
                    }
                    else if (d.Phase == FlightPhase.Lower)
                    {
                        d.Bank *= (float)Math.Max(0, 1 - dt * 4);
                        d.Height = d.LiftFrom + (d.WorkHeight - d.LiftFrom) * SmoothFlight(d.Age / LowerSeconds);
                        if (d.Age >= LowerSeconds) SeedPhase(d, FlightPhase.AwaitOrder);
                    }
                    else if (d.Phase == FlightPhase.AwaitOrder)
                    {
                        if (state.CanPlantSeeder(d.Number, d.Plot)) StartDroneWork(d);
                    }
                    else if (d.Age >= d.WorkSeconds)
                    {
                        int crop = flight.Cargo[0].CropId;
                        if (state.PlantSeeder(d.Number, d.Plot, now))
                        {
                            SeedTarget(d, flight, -1);
                            state.RecordMachineWork(d.Model);
                            LiftDrone(d, "已播" + Crop.All[crop].Name);
                            changed = true;
                        }
                        else if (!state.IsNextSeeder(d.Number)) SeedPhase(d, FlightPhase.AwaitOrder);
                        else { StartSeedReturn(d, flight); changed = true; }
                    }
                    break;

                case FlightPhase.Lift:
                    d.Height = d.LiftFrom + (CruiseHeight - d.LiftFrom) * SmoothFlight(d.Age / LiftSeconds);
                    if (d.Age >= LiftSeconds)
                    {
                        d.Result = "";
                        if (flight.Count > 0) SeedPhase(d, FlightPhase.Staged);
                        else StartSeedReturn(d, flight);
                    }
                    break;

                case FlightPhase.ToStand:
                    d.Height = Math.Min(32, d.Height + (float)(dt * 45));
                    if (FlyToward(d, d.Home, dt, d.Airspeed))
                    {
                        d.LiftFrom = d.Height;
                        SeedPhase(d, FlightPhase.Landing);
                    }
                    break;

                case FlightPhase.Landing:
                    d.Bank *= (float)Math.Max(0, 1 - dt * 5);
                    d.Height = d.LiftFrom + (SeedParkHeight - d.LiftFrom) * SmoothFlight(d.Age / SeedDockSeconds);
                    if (d.Age >= SeedDockSeconds)
                    {
                        if (seedBerth == d) seedBerth = null;
                        d.SeedReturning = false;
                        d.SearchIn = .55;
                        SeedPhase(d, FlightPhase.Parked);
                    }
                    break;
            }

            flight.GroundX = d.Ground.X; flight.GroundY = d.Ground.Y;
            flight.Height = d.Height; flight.HasPosition = true;
            return changed;
        }

        static float SeedDockContact(FieldDrone d, out PointF at, out bool transfer)
        {
            float toHome = SeedDistance(d.Ground,d.Home);
            float toHatch = SeedDistance(d.Ground,SeedDepotDock);
            transfer = toHatch < toHome;
            at = transfer ? SeedDepotDock : d.Home;
            float distance = transfer ? toHatch : toHome;
            return SmoothFlight(1-distance/100) * SmoothFlight(1-(d.Height-SeedParkHeight)/11);
        }

        static float SeedBodyScale(float lift,bool main,float contact)
        {
            return .58f+.42f*lift+(main?.29f*contact:0);
        }

        static float SeedSeatOffset(bool main,float contact)
        {
            return contact*(main?-4.5f:3.8f);
        }

        bool DockedSeederContains(PointF point)
        {
            Bitmap body=art.MachineIcon(FarmState.SeederDrone);
            foreach(FieldDrone d in fleet)
            {
                if(d.Model!=FarmState.SeederDrone || !SeederResting(d))continue;
                float dx=point.X-d.Ground.X;
                bool main=d.Number==0;
                float dy=point.Y-(d.Ground.Y-d.Height+SeedSeatOffset(main,1));
                float width=d.Width*SeedBodyScale(0,main,1),height=width*body.Height/body.Width;
                if(Math.Abs(dx)>width || Math.Abs(dy)>height)continue;
                double angle=d.Bank*Math.PI/180;
                double u=(dx*Math.Cos(angle)+dy*Math.Sin(angle))/width+.5;
                double v=(-dx*Math.Sin(angle)+dy*Math.Cos(angle))/height+.5;
                if(u>=0&&u<1&&v>=0&&v<1&&body.GetPixel((int)(u*body.Width),(int)(v*body.Height)).A>80)return true;
            }
            return false;
        }

        void DrawSeederIdleLight(Graphics g, FieldDrone d, float h)
        {
            // Vary the radius of a fixed-colour indicator so a resting machine never drifts.
            float radius = 2.5f + .9f * (float)Math.Sin(fleetTime * 2.15 + d.Number);
            float x = d.Width * .16f, y = h * .10f;
            g.FillEllipse(B(Color.FromArgb(173, 118, 174, 100)), x-radius, y-radius, radius*2, radius*2);
            g.FillEllipse(B(Color.FromArgb(246, 250, 195)), x-1.3f, y-1.3f, 2.6f, 2.6f);
        }

        void DrawSeedFreight(Graphics g, FieldDrone d, float h)
        {
            SeedFlight flight = state.GetSeederFlight(d.Number);
            bool docking = d.Phase == FlightPhase.Transfer;
            if (flight.Count == 0 && (!docking || d.SeedReturning || SeedLoadShare(ManualFieldTarget()) == 0)) return;
            WarehouseLot lot = flight.Count > 0 ? flight.Cargo[0] : state.SeedWarehouse[0];
            float x = 0, y = h * .29f;
            if (docking)
            {
                float p = SmoothFlight(d.Age / SeedTransferSeconds);
                if (flight.Returning) p = 1 - p;
                x -= 24 * (1 - p); y -= 4 * (float)Math.Sin(Math.PI * p);
            }
            Bitmap packet = lot.Premium ? premiumPackets[lot.CropId] : art.SeedPackets[lot.CropId];
            Sprite(g, packet, x - 7, y - 3, 14, 14);
        }
    }
}

