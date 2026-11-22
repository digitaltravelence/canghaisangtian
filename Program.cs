using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MountainTide
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool first;
            using(Mutex mutex=new Mutex(true,@"Local\CanghaiSangtianFarmGame",out first)){
                if(!first){MessageBox.Show("游戏已经打开，可以从任务栏回到农场。","沧海桑田");return;}
                string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"沧海桑田");
                SaveFile save=new SaveFile(Path.Combine(directory,"自由农田.xml"),Path.Combine(directory,"农场存档.xml"));
                using(Game game=new Game(save.Load(),save)){Application.Run(game);}
            }
        }
    }
}
