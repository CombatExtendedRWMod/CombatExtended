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
        const float dividerBorderRatio = 0.5f;
        const float borderBarRatio = 0.1f;
        const float minHeight = 10f;    //Minimum height of an ammo bar
        const float idealRatio = 3.3f;    //Preferred width-to-height ratio of ammo status bars
        const float magSplit = 0.5f;    //Split between current and stored magazine bar sizes
        const float defaultWidth = 120f;
        const float margin = 6f;
        const float magAlpha = 0.9f;
        const bool borderBetween = false;
        static Color defaultColor = new Color(0.2f, 0.8f, 0.85f);

        //Link
        List<CompAmmoUser> compAmmos;
        public CompAmmoUser compAmmo;

        private static int cachedAmount, oRows, oCols;
        private static float oWidth, oHeight;

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
            int bRows = 0;
            int bCols = 0;

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
                    bRows = row;
                    bCols = col;
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
        
        void DrawEntry(Rect rect, CompAmmoUser user)
        {
            var inventory = user.CompInventory;

            int magsLeft = (Controller.settings.EnableAmmoSystem && inventory != null)
                ? Mathf.CeilToInt((float)inventory.AmmoCountOfDef(user.SelectedAmmo) / (user.HasMagazine ? user.Props.magazineSize : 1f))
                : 0;

            //If neither of the bars can be rendered, do not draw this ammoUser
            if (!user.HasMagazine && magsLeft == 0)
                return;

            var barsRect = rect;

            //Optimizing barsRect to match ideal aspect ratio
            if (barsRect.width / barsRect.height != idealRatio)
            {
                if (barsRect.width / barsRect.height > idealRatio)
                {
                    barsRect.width = barsRect.height * idealRatio;
                    barsRect.x = rect.center.x - barsRect.width / 2f;
                }
                else if (barsRect.width / barsRect.height < idealRatio)
                {
                    barsRect.height = barsRect.width / idealRatio;
                    barsRect.y = rect.center.y - barsRect.height / 2f;
                }
            }
            
            //Draw solid black background
            Widgets.DrawBoxSolid(barsRect, Color.black);

            //Draw white border
            //TODO

            var borderThickness = Mathf.Max(1f, borderBarRatio * barsRect.height);

            #region Top bar
            if (user.HasMagazine)
            {
                Rect barRect = (magsLeft > 0)
                    ? barsRect.TopPart(magSplit)
                    : barsRect;
                
                var innerRect = barRect.ContractedBy(borderThickness);
                innerRect.width *= (float)user.CurMagCount / user.Props.magazineSize;
                
                if (magsLeft > 0)
                    innerRect.height += (borderBetween ? 0.5f : 1f) * borderThickness;

                //Add texture of ammo type
                //TODO

                //Add colour of ammo type
                Widgets.DrawBoxSolid(innerRect,
                    (user.CurrentAmmo == null)
                        ? defaultColor
                        : user.CurrentAmmo.ammoClass.color);

                if (innerRect.height > 9f)
                {
                    Text.Font = innerRect.height > 11f ? GameFont.Small : GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(barRect.x, innerRect.y, barRect.width, innerRect.height).ExpandedBy(borderThickness),
                        user.CurMagCount + (oCols > 1 ? "/" : " / ") + user.Props.magazineSize);
                }
            }
            #endregion

            #region Dividers, possibly bottom bar
            if (magsLeft > 0)
            {
                Rect barRect = (user.HasMagazine)
                    ? barsRect.BottomPart(1f - magSplit)
                    : barsRect;
                
                var innerRect = barRect.ContractedBy(borderThickness);

                if (user.HasMagazine)
                {
                    innerRect.y -= (borderBetween ? 0.5f : 1f) * borderThickness;
                    innerRect.height += (borderBetween ? 0.5f : 1f) * borderThickness;
                }

                var color = (user.SelectedAmmo == null)
                    ? defaultColor
                    : user.SelectedAmmo.ammoClass.color;
                color.a = magAlpha;

                if (magsLeft > 1)
                {
                    var magWidth = innerRect.width / (float)magsLeft;
                    var halfDividerThickness = 0.5f * Mathf.Max(1f, dividerBorderRatio * borderThickness);

                    innerRect.width = magWidth;

                    //Create magsLeft rectangles of appropriate width
                    for (int i = 0; i < magsLeft; i++)
                    {
                        // For first iteration, if has neighbour, reduce width for divider
                        if (i == 0)
                            innerRect.width -= halfDividerThickness;
                        else
                            innerRect.x += magWidth;

                        // For last iteration, increase width (no divider at end)
                        if (i == magsLeft - 1)
                            innerRect.width += halfDividerThickness;

                        Widgets.DrawBoxSolid(innerRect, color);

                        // After first iteration, increase x-offset for divider, further reduce width to account for this
                        if (i == 0)
                        {
                            innerRect.x += halfDividerThickness;
                            innerRect.width -= halfDividerThickness;
                        }
                    }
                }
                else
                    Widgets.DrawBoxSolid(innerRect, color);

                if (innerRect.height > 9f && !user.HasMagazine)
                {
                    Text.Font = innerRect.height > 11f ? GameFont.Small : GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;

                    Widgets.Label(new Rect(barRect.x, innerRect.y, barRect.width, innerRect.height).ExpandedBy(borderThickness),
                        magsLeft.ToString());
                }
            }
            #endregion
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth)
        {
            Rect overRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);

            var gizmoState = GizmoState.Clear;
            
            if (Mouse.IsOver(overRect))
                gizmoState = GizmoState.Mouseover;

            if (Event.current.type != EventType.Repaint)
                return new GizmoResult(gizmoState);

            Widgets.DrawBox(overRect);
            GUI.DrawTexture(overRect, Command.BGTex);
            
            Rect inRect = overRect.ContractedBy(margin);

            if (compAmmos == null || !compAmmos.Except(compAmmo).Any())
            {
                // ThingCategory icon
                // Widgets.DrawTextureFitted(inRect.TopHalf(), compAmmo.CurrentAmmo.thingCategories.First().icon, 1f);

                // Ammo type label
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(inRect.TopHalf(),
                    compAmmo.CurrentAmmo == null
                        ? compAmmo.parent.def.LabelCap
                        : compAmmo.CurrentAmmo.ammoClass.LabelCap);
                
                DrawEntry(inRect.BottomHalf(), compAmmo);
                
                return new GizmoResult(gizmoState);
            }

            var count = compAmmos.Count;
            OptimizeGizmo(count);

            if (count != cachedAmount && Controller.settings.EnableAmmoSystem)
            {
                compAmmos.Sort((x, y) =>
                {
                    return x.CurrentAmmo.ammoClass.defName
                        .CompareTo(y.CurrentAmmo.ammoClass.defName);
                });
            }

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

            var boxHeight = inRect.height / (float)oRows;
            var boxWidth = inRect.width / (float)oCols;
            
            int col = 0;
            int row = 0;
            int colorIndex = 0;
            int j = 0;
            
            for (int i = 0; i < count; i++)
            {
                colorIndex = Mathf.FloorToInt(j / oCols) + (j % oCols) * oRows;

                while (colorIndex > count - 1)
                {
                    j++;
                    colorIndex = Mathf.FloorToInt(j / oCols) + (j % oCols) * oRows;
                }

                var user = compAmmos[colorIndex];

                j++;

                colorIndex += oRows;
                if (colorIndex > count)
                    colorIndex = 1 + colorIndex % oRows;

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
            return new GizmoResult(gizmoState);
        }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
        }
    }
}
