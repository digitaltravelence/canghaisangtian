using System;
using System.Drawing;

namespace MountainTide
{
    internal sealed partial class Game
    {
        static readonly int[] WorkshopOrder={FarmState.Drone,FarmState.WaterDrone,FarmState.PestMachine,FarmState.Scarecrow,FarmState.FertilizerDrone,FarmState.SeederDrone,FarmState.Cultivator};
        Image DeviceIcon(int id){return art.MachineIcon(id);}
        void FitSprite(Graphics g,Image image,float x,float y,float w,float h)
        {
            float scale=Math.Min(w/image.Width,h/image.Height);
            float width=image.Width*scale,height=image.Height*scale;
            Sprite(g,image,x+(w-width)/2,y+(h-height)/2,width,height);
        }
        void DrawProgress(Graphics g)
        {
            string goal=state.DroneCount==0?"首架无人机  "+Math.Min(100,state.StarCredits)+" / 100 星元":state.AutomationComplete?"全田自动化已达成":"开垦 "+state.OpenCount+"/36   ·   自动作业 "+state.CompletedAutomationKinds+"/6";
            Plate(g,23,93,240,31,Dark,Rim,8);
            TextAt(g,goal,14,gold,33,93,220,31,true,true);
            HitBox(23,93,240,31,"open-automation");
            DrawAutomationRibbon(g);
        }
    }
}
