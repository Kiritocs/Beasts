using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Beasts.Data;
using Beasts.ExileCore;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace Beasts;

public partial class Beasts
{
    public override void Render()
    {
        DrawInGameBeasts();
        DrawInGameSpecialBeasts();
        DrawBestiaryPanel();
        DrawBeastsWindow(Settings.Threshold);
        DrawMinimapPrice(Settings.Threshold);
    }

    private void DrawInGameBeasts()
    {
        foreach (var positioned in _trackedBeasts
                     .Select(beast => beast.Value.GetComponent<Positioned>())
                     .Where(positioned => positioned != null))
        {
            DrawFilledCircleInWorldPosition(
                GameController.IngameState.Data.ToWorldWithTerrainHeight(positioned.GridPosition), 50, SharpDX.Color.Red
            );
        }
    }

    private void DrawInGameSpecialBeasts()
    {
        foreach (var trackedBeast in _trackedSpecialBeasts
                     .Select(beast => new { Positioned = beast.Value.GetComponent<Positioned>(), beast.Value.Metadata })
                     .Where(beast => beast.Positioned != null))
        {
            var beast = BeastsDatabase.SpecialBeasts.Where(beast => trackedBeast.Metadata == beast.Path).First();

            var pos = GameController.IngameState.Data.ToWorldWithTerrainHeight(trackedBeast.Positioned.GridPosition);
            Graphics.DrawText(beast.DisplayName, GameController.IngameState.Camera.WorldToScreen(pos), SharpDX.Color.White, FontAlign.Center);

            DrawFilledCircleInWorldPosition(pos, 50, GetSpecialBeastColor(beast.DisplayName));
        }
    }

    private SharpDX.Color GetSpecialBeastColor(string beastName)
    {
        if (beastName.Contains("Vivid"))
        {
            return new SharpDX.Color(255, 250, 0);
        }

        if (beastName.Contains("Wild"))
        {
            return new SharpDX.Color(255, 0, 235);
        }

        if (beastName.Contains("Primal"))
        {
            return new SharpDX.Color(0, 245, 255);
        }

        if (beastName.Contains("Black")) {
            return new SharpDX.Color(255, 255, 255);
        }

        return SharpDX.Color.Red;
    }

    private void DrawMinimapPrice(int threshold)
    {

        foreach (var beast in _trackedBeasts.Values)
        {
            var b = BeastsDatabase.AllBeasts.Find(b => b.Path == beast.Metadata);
            if (b.Priority < threshold)
            {
                continue;
            }
            if (!Prices.ContainsKey(b.DisplayName))
            {
                DrawToLargeMiniMapText(beast, "?c");
                //continue;
            }
            string text;
            if (beast.Stats.TryGetValue(GameStat.MovementVelocityPct, out int pct) && pct == -100)
            {
                text = "V";
            }
            else
            {
                text = $"{b.DisplayName} {Prices[b.DisplayName]}c";
            }

            DrawToLargeMiniMapText(beast, text);
        }
    }

    private void DrawToLargeMiniMapText(Entity entity, string text)
    {
        var camera = GameController.Game.IngameState.Camera;
        var mapWindow = GameController.Game.IngameState.IngameUi.Map;
        if (mapWindow.LargeMap.IsVisible != true)
        {
            return;
        }
        if (GameController.Game.IngameState.UIRoot.Scale == 0)
        {
            DebugWindow.LogError(
                "ExpeditionIcons: Seems like UIRoot.Scale is 0. Icons will not be drawn because of that.");
        }

        var mapRect = mapWindow.GetClientRect();
        var playerPos = GameController.Player.GridPosNum;
        var posZ = GameController.Player.GetComponent<Render>().Z;
        var screenCenter = new Vector2(mapRect.Width / 2, mapRect.Height / 2).Translate(0, -20) +
                           new Vector2(mapRect.X, mapRect.Y) +
                           new Vector2(mapWindow.LargeMapShiftX, mapWindow.LargeMapShiftY);
        var diag = (float)Math.Sqrt(camera.Width * camera.Width + camera.Height * camera.Height);
        var k = camera.Width < 1024f ? 1120f : 1024f;
        var scale = k / camera.Height * camera.Width * 3f / 4f / mapWindow.LargeMapZoom;
        var render = entity.GetComponent<Render>();
        if (render is null)
        {
            return;
        }
        var iconZ = render.Z;
        var point = screenCenter + DeltaInWorldToMinimapDelta(entity.GridPosNum - playerPos, diag, scale,
            (iconZ - posZ) / (9f / mapWindow.LargeMapZoom));


        var size = Graphics.DrawText(text, point, SharpDX.Color.Green,
            20, FontAlign.Center);
        float maxWidth = 0;
        float maxheight = 0;
        //not sure about sizes below, need test
        point.Y += size.Y;
        maxheight += size.Y;
        maxWidth = Math.Max(maxWidth, size.X);
        var background = new SharpDX.RectangleF(point.X - maxWidth / 2 - 3, point.Y - maxheight, maxWidth + 6, maxheight);
        Graphics.DrawBox(background, SharpDX.Color.Black);
    }

    public static Vector2 DeltaInWorldToMinimapDelta(Vector2 delta, double diag, float scale, float deltaZ = 0)
    {
        const float CAMERA_ANGLE = 38 * MathUtil.Pi / 180;

        // Values according to 40 degree rotation of cartesian coordiantes, still doesn't seem right but closer
        var cos = (float)(diag * Math.Cos(CAMERA_ANGLE) / scale);
        var sin = (float)(diag * Math.Sin(CAMERA_ANGLE) /
                           scale); // possible to use cos so angle = nearly 45 degrees

        // 2D rotation formulas not correct, but it's what appears to work?
        return new Vector2((delta.X - delta.Y) * cos, deltaZ - (delta.X + delta.Y) * sin);
    }

    private void DrawBestiaryPanel()
    {
        var bestiary = GameController.IngameState.IngameUi.GetBestiaryPanel();
        if (bestiary == null || bestiary.IsVisible == false) return;

        var capturedBeastsPanel = bestiary.CapturedBeastsPanel;
        if (capturedBeastsPanel == null || capturedBeastsPanel.IsVisible == false) return;

        var beasts = bestiary.CapturedBeastsPanel.CapturedBeasts;
        foreach (var beast in beasts)
        {
            var beastMetadata = BeastsDatabase.AllBeasts.Find(b => b.DisplayName == beast.DisplayName);
            if (beastMetadata == null) continue;
            if (!Prices.ContainsKey(beastMetadata.DisplayName)) continue;

            var center = new Vector2(beast.GetClientRect().Center.X, beast.GetClientRect().Center.Y);

            Graphics.DrawBox(beast.GetClientRect(), new SharpDX.Color(0, 0, 0, 0.5f));
            Graphics.DrawFrame(beast.GetClientRect(), SharpDX.Color.White, 2);
            Graphics.DrawText(beastMetadata.DisplayName, center, SharpDX.Color.White, FontAlign.Center);

            var text = Prices[beastMetadata.DisplayName].ToString(CultureInfo.InvariantCulture) + "c";
            var textPos = center + new Vector2(0, 20);
            Graphics.DrawText(text, textPos, SharpDX.Color.White, FontAlign.Center);
        }
    }

    private void DrawBeastsWindow(int threshold)
    {
        ImGui.SetNextWindowSize(new Vector2(0, 0));
        ImGui.SetNextWindowBgAlpha(0.6f);
        ImGui.Begin("Beasts Window", ImGuiWindowFlags.NoDecoration);

        if (ImGui.BeginTable("Beasts Table", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersV))
        {
            ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthFixed, 48);
            ImGui.TableSetupColumn("Beast");

            foreach (var beastMetadata in _trackedBeasts
                         .Select(trackedBeast => trackedBeast.Value)
                         .Select(beast => BeastsDatabase.AllBeasts.Find(b => b.Path == beast.Metadata))
                         .Where(beastMetadata => beastMetadata != null && beastMetadata.Priority >= threshold))
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();

                ImGui.Text(Prices.TryGetValue(beastMetadata.DisplayName, out var price)
                    ? $"{price.ToString(CultureInfo.InvariantCulture)}c"
                    : "0c");

                ImGui.TableNextColumn();

                ImGui.Text(beastMetadata.DisplayName);
                foreach (var craft in beastMetadata.Crafts)
                {
                    ImGui.Text(craft);
                }
            }

            foreach (var beastMetadata in _trackedSpecialBeasts
                .Select(trackedBeast => trackedBeast.Value)
                .Select(beast => BeastsDatabase.AllBeasts.Find(b => b.Path == beast.Metadata))
                .Where(beastMetadata => beastMetadata != null))
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();

                ImGui.Text(Prices.TryGetValue(beastMetadata.DisplayName, out var price)
                    ? $"{price.ToString(CultureInfo.InvariantCulture)}c"
                    : "0c");

                ImGui.TableNextColumn();

                ImGui.Text(beastMetadata.DisplayName);
                foreach (var craft in beastMetadata.Crafts)
                {
                    ImGui.Text(craft);
                }
            }

            ImGui.EndTable();
        }

        ImGui.End();
    }

    private void DrawFilledCircleInWorldPosition(Vector3 position, float radius, SharpDX.Color color)
    {
        var circlePoints = new List<Vector2>();
        const int segments = 15;
        const float segmentAngle = 2f * MathF.PI / segments;

        for (var i = 0; i < segments; i++)
        {
            var angle = i * segmentAngle;
            var currentOffset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var nextOffset = new Vector2(MathF.Cos(angle + segmentAngle), MathF.Sin(angle + segmentAngle)) * radius;

            var currentWorldPos = position + new Vector3(currentOffset, 0);
            var nextWorldPos = position + new Vector3(nextOffset, 0);

            circlePoints.Add(GameController.Game.IngameState.Camera.WorldToScreen(currentWorldPos));
            circlePoints.Add(GameController.Game.IngameState.Camera.WorldToScreen(nextWorldPos));
        }

        Graphics.DrawConvexPolyFilled(circlePoints.ToArray(), color with { A = SharpDX.Color.ToByte((int)((double)0.2f * byte.MaxValue)) });
        Graphics.DrawPolyLine(circlePoints.ToArray(), color, 2);
    }
}