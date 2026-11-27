using System.Drawing;

namespace MountainTide
{
    internal sealed partial class Game
    {
        static readonly Color FarmSignFace = Color.FromArgb(59, 70, 47);
        static readonly Color HarborSignFace = Color.FromArgb(36, 72, 74);
        static readonly Color HouseSignFace = Color.FromArgb(93, 62, 42);
        static readonly Color SignRim = Color.FromArgb(219, 181, 121);
        static readonly Color SignLettering = Color.FromArgb(255, 243, 210);
        static readonly Color SignShadow = Color.FromArgb(124, 46, 34, 22);

        void DrawBuildingLabels(Graphics g)
        {
            if (panel != "") return;
            switch (scene)
            {
                case Scene.Farm:
                    DrawBuildingSign(g, "山景农舍", 145, 226, FarmSignFace);
                    DrawBuildingSign(g, "青芽种子铺", 541, 109, FarmSignFace);
                    DrawBuildingSign(g, "山村粮站", 866, 99, FarmSignFace);
                    break;
                case Scene.Harbor:
                    DrawBuildingSign(g, "码头收购摊", 352, 80, HarborSignFace);
                    DrawBuildingSign(g, "科技工坊", 948, 74, HarborSignFace);
                    break;
                case Scene.House:
                    DrawBuildingSign(g, "返回农场", 897, 77, HouseSignFace);
                    break;
            }
        }

        void DrawBuildingSign(Graphics g, string name, float centerX, float top, Color face)
        {
            const int fontSize = 18, height = 27;
            float width = name.Length * fontSize + 25;
            float left = centerX - width / 2;
            Plate(g, left, top + 2, width, height, SignShadow, SignShadow, 5);
            Plate(g, left, top, width, height, face, SignRim, 5);
            float fastenerY = top + height / 2f - 1.5f;
            g.FillEllipse(B(SignRim), left + 5, fastenerY, 3, 3);
            g.FillEllipse(B(SignRim), left + width - 8, fastenerY, 3, 3);
            TextAt(g, name, fontSize, SignLettering, left + 8, top - 1, width - 16, height + 1, true, true);
        }
    }
}
