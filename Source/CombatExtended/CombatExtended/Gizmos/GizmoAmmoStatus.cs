using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    [StaticConstructorOnStartup]
    public class GizmoAmmoStatus : Command
    {
        const float minHeight = 10f;    //Minimum height of an ammo bar
        const float idealRatio = 3.3f;    //Preferred width-to-height ratio of ammo status bars
        const float magSplit = 0.3f;    //Split between current and stored magazine bar sizes
        const float defaultWidth = 120f;
        const float margin = 6f;

        private static bool initialized;
        //Link
        List<CompAmmoUser> compAmmos;
        public CompAmmoUser compAmmo;

        private static Texture2D FullTex;
        private static Texture2D EmptyTex;
        private static new Texture2D BGTex;

        private static int cachedAmount;
        private static float oWidth, oHeight, oRows, oCols;

        public override bool GroupsWith(Gizmo other)
        {
            return other is GizmoAmmoStatus;
        }

        public override void MergeWith(Gizmo other)
        {
            if (compAmmos == null)
                compAmmos = new List<CompAmmoUser>();

            compAmmos.Add(((GizmoAmmoStatus)other).compAmmo);

            if (!compAmmos.Contains(compAmmo))
                compAmmos.Add(compAmmo);
        }

        public override float GetWidth(float maxWidth)
        {
            return Mathf.Min(maxWidth, Mathf.Max(defaultWidth, idealRatio * minHeight * oCols + 2f * margin));
        }

        //Based on https://math.stackexchange.com/questions/1627859/algorithm-to-get-the-maximum-size-of-n-rectangles-that-fit-into-a-rectangle-with
        void OptimizeGizmo(int amount)
        {
            if (amount == cachedAmount)
                return;

            float bWidth = 0f;
            float bHeight = 0f;
            float bRows = 0f;
            float bCols = 0f;

            for (int col = 1; col <= amount; col++)
            {
                int row = Mathf.CeilToInt((float)amount / (float)col);
                var containerWidth = Mathf.Max(defaultWidth, idealRatio * minHeight * col);
                var containerHeight = (Height - 2f * margin);
                var hScale = containerWidth / ((float)col * idealRatio);
                var vScale = containerHeight / (float)row;

                float width, height;

                if (hScale <= vScale)
                {
                    width = containerWidth / col;
                    height = width / idealRatio;
                }
                else
                {
                    height = containerHeight / row;
                    width = height * idealRatio;
                }
                
                if (width * height > bWidth * bHeight)
                {
                    bWidth = width;
                    bHeight = height;
                    bRows = (float)row;
                    bCols = (float)col;
                }
            }

            if (bWidth * bHeight > 0f)
            {
                cachedAmount = amount;

                oWidth = bWidth;
                oHeight = bHeight;
                oCols = bCols;
                oRows = bRows;
            }
        }
        
        void DrawEntry(Rect boxRect, CompAmmoUser user)
        {
            var inventory = user.CompInventory;
            int magsLeft = 0;

            if (inventory != null)
                magsLeft = Mathf.CeilToInt((float)inventory.AmmoCountOfDef(user.CurrentAmmo) / user.Props.magazineSize);
            
            var barsRect = boxRect;

            if (barsRect.width / barsRect.height != idealRatio)
            {
                if (barsRect.width / barsRect.height > idealRatio)
                {
                    barsRect.width = barsRect.height * idealRatio;
                    barsRect.x = boxRect.center.x - barsRect.width / 2f;
                }
                else if (barsRect.width / barsRect.height < idealRatio)
                {
                    barsRect.height = barsRect.width / idealRatio;
                    barsRect.y = boxRect.center.y - barsRect.height / 2f;
                }
            }
            
            // Top bar
            if (user.HasMagazine)
            {
                Rect barRect = (inventory != null && magsLeft > 0)
                    ? barsRect.TopPart(magSplit)
                    : barsRect;

                //Add colour of ammo type
                Widgets.DrawBoxSolid(barRect, Color.black);

                var innerRect = barRect.ContractedBy(2f);
                innerRect.width *= (float)user.CurMagCount / user.Props.magazineSize;

                Widgets.DrawBoxSolid(innerRect, Color.cyan);

                if (innerRect.height > 11f)
                {
                    Text.Font = innerRect.height > 15f ? GameFont.Small : GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(barRect, user.CurMagCount + " / " + user.Props.magazineSize);
                }
            }

            // Dividers, possibly bottom bar
            if (inventory != null)
            {
                Rect barRect = (user.HasMagazine)
                    ? barsRect.BottomPart(1f - magSplit)
                    : barsRect;

                if (magsLeft > 0)
                {
                    float magWidth = barsRect.width / magsLeft;

                    //Create magsLeft rectangles of appropriate width
                    for (int i = 0; i < magsLeft; i++)
                    {
                        var rect = new Rect(barRect.x + i * magWidth, barRect.y, magWidth, barRect.height);
                        Widgets.DrawBoxSolid(rect, Color.black);

                        var innerRect = rect.ContractedBy(2f);

                        if (i > 0)
                            innerRect.x -= 1f;

                        if (i < magsLeft - 1)
                            innerRect.xMax += 1f;

                        Widgets.DrawBoxSolid(innerRect, Color.cyan);
                    }
                    if (barRect.height > 15f && !user.HasMagazine)
                    {
                        Text.Font = barRect.height > 19f ? GameFont.Small : GameFont.Tiny;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.Label(barRect, magsLeft.ToString());
                    }
                }
            }
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth)
        {
            if (!initialized)
                InitializeTextures();

            Rect overRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
            Widgets.DrawBox(overRect);
            GUI.DrawTexture(overRect, BGTex);
            
            Rect inRect = overRect.ContractedBy(margin);

            if (compAmmos == null || !compAmmos.Except(compAmmo).Any())
            {
                // Ammo type
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(inRect.TopHalf(), compAmmo.CurrentAmmo == null ? compAmmo.parent.def.LabelCap : compAmmo.CurrentAmmo.ammoClass.LabelCap);
                
                DrawEntry(inRect.BottomHalf(), compAmmo);

                return new GizmoResult(GizmoState.Clear);
            }
            
            OptimizeGizmo(compAmmos.Count);

            //Too large, need to cull oCols
            while (maxWidth - 2f * margin < oCols * minHeight * idealRatio)
            {
                oCols--;

                //Should not happen
                if (oCols <= 0f)
                {
                    Log.Error("Combat Extended :: GizmoAmmoStatus suggests negative number of columns");
                    break;
                }
            }

            var boxHeight = inRect.height / oRows;
            var boxWidth = inRect.width / oCols;
            
            int col = 0;
            int row = 0;
            
            foreach (var user in compAmmos)
            {
                DrawEntry(
                    new Rect(
                        inRect.x + (float)col * boxWidth,
                        inRect.y + (float)row * boxHeight,
                        boxWidth, boxHeight),
                    user);
                
                row++;
                if (row == oRows)
                {
                    row = 0;
                    col++;
                }
            }
            
            Text.Anchor = TextAnchor.UpperLeft;
            return new GizmoResult(GizmoState.Clear);
        }

        private void InitializeTextures()
        {
            if (FullTex == null)
                FullTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.24f));
            if (EmptyTex == null)
                EmptyTex = SolidColorMaterials.NewSolidColorTexture(Color.clear);
            if (BGTex == null)
                BGTex = ContentFinder<Texture2D>.Get("UI/Widgets/DesButBG", true);
            initialized = true;
        }
    }
}
