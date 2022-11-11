using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;

namespace WorldRenderLib.Wrappers
{
    public interface IMainWrapper
    {
        void DrawStarsInBackground(Main.SceneArea sceneArea);
        void DrawSunAndMoon(Main.SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence);
        void SetBackColor(Main.InfoToSetBackColor info, out Color sunColor, out Color moonColor);
        void DrawCachedNPCs(List<int> npcCache, bool behindTiles);
        void DrawCachedProjs(List<int> projCache, bool startSpriteBatch = true);
        void SortDrawCacheWorms();
        void CacheNPCDraws();
        void CacheProjDraws();
        void DrawWoF();
        void DrawBackGore();
        void DrawRain();
        void DrawGore();
        void DrawDust();
        void DrawWaters(bool isBackground = false);
        void DrawBlack(bool force = false);
        void DrawBackground();
        void DrawProjectiles();
        void DrawBG();
        void DrawPlayers_AfterProjectiles();
        void DrawPlayers_BehindNPCs();
        void DoDraw_DrawNPCsOverTiles();
    }
}
