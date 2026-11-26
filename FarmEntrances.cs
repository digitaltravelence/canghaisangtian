using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MountainTide
{
    internal sealed partial class Game
    {
        // All contours follow the painted artwork in the same 1280 x 720 space as the farm.
        sealed class FarmEntrances : IDisposable
        {
            readonly Region supply = new Region();
            readonly Region grain = new Region();
            readonly Region door = new Region();
            static readonly Rectangle DoorBounds = Rectangle.FromLTRB(241, 146, 279, 205);
            readonly Bitmap[] doorFrames = new Bitmap[23];

            public FarmEntrances(Bitmap farm, Bitmap house)
            {
                supply.MakeEmpty();
                Shape(supply, true,
                    482,155, 482,148, 484,145, 488,145, 491,148, 491,151,
                    601,155, 602,150, 605,148, 608,149, 610,153, 610,155,
                    613,156, 614,160, 613,163, 624,174, 630,178, 630,184,
                    628,191, 626,192, 621,192, 621,235, 625,236, 626,240,
                    626,242, 623,243, 623,265, 614,266, 614,265, 585,266,
                    584,263, 581,263, 581,266, 556,266, 547,265, 520,265,
                    518,266, 510,265, 508,266, 502,265, 501,263, 496,264,
                    495,266, 478,266, 477,265, 467,265, 466,263, 467,260,
                    468,260, 468,240, 465,239, 465,234, 469,233, 469,212,
                    477,210, 479,202, 479,191, 474,187, 475,182, 478,178,
                    478,171, 474,177, 472,176, 477,168, 478,161, 478,156);
                // Hanging flowers, their wooden bracket and the trailing leaves on the left.
                Shape(supply, true,
                    480,162, 484,160, 481,169, 478,174, 478,179, 492,179,
                    497,180, 497,186, 480,186, 479,189, 482,188, 485,190,
                    488,192, 490,196, 492,198, 492,204, 491,207, 491,210,
                    489,213, 487,210, 485,212, 485,218, 483,217, 482,213,
                    480,211, 477,215, 470,216, 465,214, 462,211, 461,213,
                    462,218, 460,222, 459,224, 457,222, 456,218, 454,217,
                    454,211, 451,207, 449,206, 449,203, 452,201, 452,196,
                    454,194, 456,194, 457,191, 461,190, 462,188, 465,188,
                    464,185, 465,182, 469,178, 469,172, 471,168, 473,167,
                    474,170, 476,168);
                // Daylight through the open right side of the awning.
                Shape(supply, false, 599,196, 609,206, 610,208, 610,219,
                    606,218, 606,215, 608,211, 608,208, 605,208, 601,213,
                    599,211, 598,209, 598,200);
                Shape(supply, false, 608,191, 610,191, 610,194);
                Shape(supply, false, 608,223, 610,223, 610,234, 607,234, 608,229);

                grain.MakeEmpty();
                Shape(grain, true,
                    772,190, 793,178, 819,163, 844,149, 862,138, 864,139,
                    880,148, 905,160, 925,171, 945,183, 960,192, 963,194, 961,196,
                    959,199, 950,198, 949,200, 949,270, 945,272, 941,272,
                    940,275, 928,275, 915,275, 891,275, 865,274, 839,273,
                    810,273, 800,273, 786,272, 786,266, 787,262, 789,261,
                    789,198, 786,198, 784,197, 775,197);

                door.MakeEmpty();
                Shape(door, true, 243,195, 243,170, 244,163, 247,156,
                    251,152, 256,149, 261,147, 264,148, 269,151, 273,156,
                    276,161, 277,167, 277,202, 248,203, 248,199, 246,196);
                BuildDoorFrames(farm, house);
            }

            static void Shape(Region region, bool include, params int[] xy)
            {
                Point[] vertices = new Point[xy.Length / 2];
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = new Point(xy[2*i], xy[2*i+1]);
                using (GraphicsPath path = new GraphicsPath()) {
                    path.AddPolygon(vertices);
                    if (include) region.Union(path);
                    else region.Exclude(path);
                }
            }

            public string At(PointF p)
            {
                if (DoorBounds.Contains(Point.Truncate(p)) && door.IsVisible(p)) return "home";
                if (p.Y < 137 || p.Y > 275) return "";
                if (p.X >= 448 && p.X <= 631 && supply.IsVisible(p)) return "supply";
                if (p.X >= 771 && p.X <= 964 && grain.IsVisible(p)) return "grain";
                return "";
            }

            // Project the two original wooden leaves around their hinges. The opening shows
            // the actual interior, with a darker lintel and more light towards the floor.
            void BuildDoorFrames(Bitmap farm, Bitmap house)
            {
                int w = DoorBounds.Width, h = DoorBounds.Height;
                using (Bitmap wood = farm.Clone(DoorBounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (Bitmap view = new Bitmap(w,h)) {
                    using (Graphics g = Graphics.FromImage(view)) {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(house, new Rectangle(0,0,w,h), new Rectangle(395,111,300,478), GraphicsUnit.Pixel);
                    }
                    for (int frame = 0; frame < doorFrames.Length; frame++) {
                        Bitmap image = doorFrames[frame] = new Bitmap(w,h);
                        double u = frame / (double)(doorFrames.Length-1);
                        double angle = 1.45 * u*u*(3-2*u);
                        double c = Math.Cos(angle), s = Math.Sin(angle);
                        for (int y=0; y<h; y++) for (int x=0; x<w; x++) {
                            float wx = DoorBounds.X+x+.5f, wy = DoorBounds.Y+y+.5f;
                            if (!door.IsVisible(wx,wy)) continue;
                            double hinge = wx<262.5 ? 243 : 277;
                            double width = Math.Abs(262.5-hinge), dir = wx<262.5 ? 1 : -1;
                            double offset = (wx-hinge)*dir/width;
                            double divisor = c-.26*s*offset;
                            double across = divisor>0 ? offset/divisor : 2;
                            Color pixel = view.GetPixel(x,y);
                            double light = .36+.30*y/h;
                            if (across>=0 && across<=1) {
                                double depth = 1+.26*s*across;
                                int sx = (int)Math.Floor(hinge+dir*across*width)-DoorBounds.X;
                                int sy = (int)Math.Floor(173+(wy-173)*depth)-DoorBounds.Y;
                                if (sx>=0 && sx<w && sy>=0 && sy<h && door.IsVisible(DoorBounds.X+sx+.5f,DoorBounds.Y+sy+.5f)) {
                                    pixel = wood.GetPixel(sx,sy);
                                    light = 1-s*(dir>0?.30:.43);
                                }
                            }
                            image.SetPixel(x,y,Color.FromArgb((int)(pixel.R*light),(int)(pixel.G*light),(int)(pixel.B*light)));
                        }
                    }
                }
            }

            public void DrawDoor(Graphics g, double elapsed)
            {
                int i=(int)Math.Round(Math.Min(1,Math.Max(0,elapsed)/.78)*(doorFrames.Length-1));
                if (i>0) g.DrawImage(doorFrames[i], DoorBounds, 0,0,DoorBounds.Width,DoorBounds.Height,GraphicsUnit.Pixel);
            }

            public void Dispose()
            {
                supply.Dispose(); grain.Dispose(); door.Dispose();
                foreach (Bitmap frame in doorFrames) frame.Dispose();
            }
        }
    }
}
