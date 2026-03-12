using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChatBubbles
{
    public unsafe partial class ChatBubbles : IDalamudPlugin
    {
        private static readonly Vector2 SelfBubblePadding = new(12f, 8f);
        private static readonly Vector2 SelfBubbleOffset = new(34f, -18f);
        private const float SelfBubbleCornerRounding = 12f;
        private const float SelfBubbleOutlineThickness = 2f;
        private const float SelfBubbleTailInset = 20f;
        private const float SelfBubbleTailHeight = 14f;
        private const float SelfBubbleMaxTextWidth = 340f;

        private void DrawCustomSelfBubble()
        {
            if (!_switch || !_selfLock)
            {
                return;
            }

            var localPlayer = LocalPlayer;
            if (localPlayer == null)
            {
                return;
            }

            var currentBubble = GetVisibleCharData((int)localPlayer.EntityId);
            var text = currentBubble?.Message?.TextValue;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (!TryGetLocalPlayerBubbleAnchor(out var anchor))
            {
                return;
            }

            DrawCustomSelfBubbleText(text, currentBubble!.Type, anchor);
        }

        private bool TryGetLocalPlayerBubbleAnchor(out Vector2 anchor)
        {
            anchor = default;
            var localPlayer = LocalPlayer;
            if (localPlayer == null)
            {
                return false;
            }

            var headWorldPosition = localPlayer.Position + new Vector3(0f, 2.1f, 0f);
            if (!Services.GameGui.WorldToScreen(headWorldPosition, out var screenPosition))
            {
                return false;
            }

            anchor = screenPosition;
            return true;
        }

        private void DrawCustomSelfBubbleText(string text, XivChatType bubbleType, Vector2 anchor)
        {
            var wrappedLines = WrapBubbleText(text, SelfBubbleMaxTextWidth);
            if (wrappedLines.Count == 0)
            {
                return;
            }

            var lineHeight = ImGui.GetTextLineHeight();
            var bubbleWidth = 0f;
            foreach (var line in wrappedLines)
            {
                bubbleWidth = MathF.Max(bubbleWidth, ImGui.CalcTextSize(line).X);
            }

            bubbleWidth += SelfBubblePadding.X * 2f;
            var bubbleHeight = (wrappedLines.Count * lineHeight) + (SelfBubblePadding.Y * 2f);

            var bubbleMin = anchor + new Vector2(SelfBubbleOffset.X, SelfBubbleOffset.Y - bubbleHeight - SelfBubbleTailHeight);
            var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
            var displaySize = ImGui.GetIO().DisplaySize;
            bubbleMin.X = Math.Clamp(bubbleMin.X, 12f, MathF.Max(12f, displaySize.X - bubbleWidth - 12f));
            bubbleMax.X = bubbleMin.X + bubbleWidth;

            var fillColor = ImGui.ColorConvertFloat4ToU32(GetCustomBubbleFillColour(bubbleType));
            var outlineColor = ImGui.ColorConvertFloat4ToU32(GetCustomBubbleOutlineColour(bubbleType));
            var textColor = ImGui.ColorConvertFloat4ToU32(GetCustomBubbleTextColour(bubbleType));

            var drawList = ImGui.GetForegroundDrawList();
            drawList.AddRectFilled(bubbleMin, bubbleMax, fillColor, SelfBubbleCornerRounding);
            drawList.AddRect(
                bubbleMin,
                bubbleMax,
                outlineColor,
                SelfBubbleCornerRounding,
                ImDrawFlags.RoundCornersAll,
                SelfBubbleOutlineThickness);

            var tailBaseLeft = new Vector2(bubbleMin.X + SelfBubbleTailInset, bubbleMax.Y - 1f);
            var tailBaseRight = tailBaseLeft + new Vector2(18f, 0f);
            var tailTip = anchor + new Vector2(8f, 6f);
            drawList.AddTriangleFilled(tailBaseLeft, tailBaseRight, tailTip, fillColor);
            drawList.AddTriangle(tailBaseLeft, tailBaseRight, tailTip, outlineColor, SelfBubbleOutlineThickness);

            var textPosition = bubbleMin + SelfBubblePadding;
            foreach (var line in wrappedLines)
            {
                drawList.AddText(textPosition, textColor, line);
                textPosition.Y += lineHeight;
            }
        }

        private List<string> WrapBubbleText(string text, float maxWidth)
        {
            var wrappedLines = new List<string>();
            var paragraphs = text.Replace("\r", string.Empty).Split('\n');
            foreach (var paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    wrappedLines.Add(string.Empty);
                    continue;
                }

                var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var currentLine = string.Empty;
                foreach (var word in words)
                {
                    var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    if (ImGui.CalcTextSize(candidate).X <= maxWidth || string.IsNullOrEmpty(currentLine))
                    {
                        currentLine = candidate;
                        continue;
                    }

                    wrappedLines.Add(currentLine);
                    currentLine = word;
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    wrappedLines.Add(currentLine);
                }
            }

            return wrappedLines;
        }

        private Vector4 GetCustomBubbleFillColour(XivChatType bubbleType)
        {
            var colour = GetBubbleColour(bubbleType);
            return new Vector4(colour.X, colour.Y, colour.Z, 0.95f);
        }

        private Vector4 GetCustomBubbleOutlineColour(XivChatType bubbleType)
        {
            var tint = GetBubbleColour2(bubbleType);
            if (tint.X == 0f && tint.Y == 0f && tint.Z == 0f)
            {
                return new Vector4(0f, 0f, 0f, 0.45f);
            }

            return new Vector4(tint.X, tint.Y, tint.Z, 0.85f);
        }

        private Vector4 GetCustomBubbleTextColour(XivChatType bubbleType)
        {
            var index = _order.IndexOf(bubbleType);
            if (index < 0)
            {
                return Vector4.One;
            }

            var bytes = BitConverter.GetBytes(_textColour[index].Choice);
            var alpha = bytes[0] == 0 ? 1f : bytes[0] / 255f;
            return new Vector4(bytes[3] / 255f, bytes[2] / 255f, bytes[1] / 255f, alpha);
        }
    }
}
