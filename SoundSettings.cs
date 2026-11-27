using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MountainTide
{
    internal sealed partial class Game
    {
        const int VolumeLeft=428, VolumeRight=780;
        string settingsReturnPanel="", draggedVolume;
        bool soundSaveFailed;

        void DrawSettingsButton(Graphics g)
        {
            bool active=hover=="settings"||panel=="settings";
            Plate(g,1151,22,100,41,Col("533f2e"),Col("4b3929"),14);
            Plate(g,1151,19,100,41,active?Col("586646"):Dark,Rim,14);
            DrawGear(g,1173,39.5f,12,gold,Col("635334"));
            TextAt(g,"设置",17,cream,1194,19,50,41,true);
            HitBox(1151,19,100,43,"settings");
        }

        static void DrawGear(Graphics g,float x,float y,float radius,Color face,Color edge)
        {
            PointF[] outline=new PointF[32];
            for(int i=0;i<outline.Length;i++){
                double angle=i*Math.PI/16-Math.PI/2;
                float r=i%4==1||i%4==2?radius:radius*.79f;
                outline[i]=new PointF(x+r*(float)Math.Cos(angle),y+r*(float)Math.Sin(angle));
            }
            using(GraphicsPath path=new GraphicsPath(FillMode.Alternate))
            using(Brush fill=new SolidBrush(face))
            using(Pen rim=new Pen(edge,1.35f)){
                path.AddPolygon(outline);
                float inner=radius*.32f;
                path.AddEllipse(x-inner,y-inner,2*inner,2*inner);
                g.FillPath(fill,path);g.DrawPath(rim,path);
            }
        }

        void DrawSoundSettings(Graphics g)
        {
            Plate(g,380,193,520,330,Color.FromArgb(145,43,31,22),Color.FromArgb(145,43,31,22),18);
            Plate(g,378,187,524,329,Col("b48956"),Col("704f35"),18);
            Plate(g,383,192,514,318,Col("fbf1d8"),Col("e2c99a"),14);
            DrawGear(g,420,228,17,Col("bd934b"),Col("785a35"));
            TextAt(g,"声音设置",27,ink,450,204,280,47,true);
            ModalButton(g,841,208,37,"×","close",true,false);
            DrawVolumeRow(g,263,true);
            DrawVolumeRow(g,374,false);
            if(soundSaveFailed)TextAt(g,"设置已生效，暂时无法保存到磁盘。",13,Col("844b35"),409,478,460,27);
        }

        void DrawVolumeRow(Graphics g,int y,bool music)
        {
            bool enabled=music?audio.MusicEnabled:audio.EffectsEnabled;
            int volume=music?audio.MusicVolume:audio.EffectsVolume;
            string toggle=music?"toggle-music":"toggle-effects";
            string slider=music?"music-volume":"effects-volume";
            Plate(g,402,y,476,98,Col("fff9e9"),Col("d9c7a1"),11);
            TextAt(g,music?"背景音乐":"交互音效",19,ink,425,y+7,250,34,true);

            Color track=enabled?Col("59784c"):Col("a39b85");
            TextAt(g,enabled?"开启":"关闭",14,enabled?Col("435e3a"):Col("726c5b"),742,y+7,48,32,true,true);
            Plate(g,796,y+10,56,26,track,hover==toggle?Col("6f512f"):Col("776f50"),13);
            float thumbX=enabled?825:799;
            g.FillEllipse(B(Col("514c38")),thumbX,y+13,23,22);
            g.FillEllipse(B(Col("fff3cd")),thumbX,y+12,23,21);
            HitBox(738,y+4,119,39,toggle);

            int knob=VolumeLeft+(int)Math.Round((VolumeRight-VolumeLeft)*volume/100.0);
            int cy=y+67;
            Plate(g,VolumeLeft-3,cy-4,VolumeRight-VolumeLeft+6,8,Col("ded4bd"),Col("b6a889"),4);
            if(knob>VolumeLeft){
                using(Pen fill=new Pen(enabled?Col("829859"):Col("aa9e80"),6)){
                    fill.StartCap=LineCap.Round;fill.EndCap=LineCap.Round;
                    g.DrawLine(fill,VolumeLeft,cy,knob,cy);
                }
            }
            g.FillEllipse(B(Col("958166")),knob-10,cy-8,20,20);
            g.FillEllipse(B(enabled?Col("e9b95e"):Col("c8bfa6")),knob-10,cy-10,20,20);
            using(Pen outline=new Pen(hover==slider||draggedVolume==slider?Col("735621"):Col("967947"),1.6f))
                g.DrawEllipse(outline,knob-10,cy-10,20,20);
            g.FillEllipse(B(Col("fff0b5")),knob-4,cy-5,6,5);
            TextAt(g,volume+"%",16,ink,797,cy-17,62,34,true,true);
            HitBox(VolumeLeft-12,cy-19,VolumeRight-VolumeLeft+24,38,slider);
        }

        void SoundSettingsAction(string action)
        {
            if(action=="close"){CloseSoundSettings();return;}
            if(action=="music-volume"||action=="effects-volume"){
                draggedVolume=action;Capture=true;SetVolumeAt(mouse.X);return;
            }
            if(action=="toggle-music")audio.MusicEnabled=!audio.MusicEnabled;
            else if(action=="toggle-effects"){
                audio.EffectsEnabled=!audio.EffectsEnabled;
                PreviewEffect();
            }else return;
            soundSaveFailed=!audio.SavePreferences();
        }

        void SetVolumeAt(float x)
        {
            int volume=(int)Math.Round((x-VolumeLeft)*100.0/(VolumeRight-VolumeLeft));
            volume=Math.Max(0,Math.Min(100,volume));
            if(draggedVolume=="music-volume")audio.MusicVolume=volume;
            else if(draggedVolume=="effects-volume")audio.EffectsVolume=volume;
        }

        void PreviewEffect()
        {
            if(audio.EffectsEnabled&&audio.EffectsVolume>0)audio.Play(FarmSound.Sow);
        }

        void FinishVolumeDrag(bool preview)
        {
            if(draggedVolume==null)return;
            bool effects=draggedVolume=="effects-volume";
            draggedVolume=null;
            if(Capture)Capture=false;
            soundSaveFailed=!audio.SavePreferences();
            if(preview&&effects)PreviewEffect();
        }

        void CloseSoundSettings()
        {
            FinishVolumeDrag(false);
            soundSaveFailed=!audio.SavePreferences();
            panel=settingsReturnPanel;settingsReturnPanel="";hover="";
        }
    }
}
