using Dalamud.Game.Text;
using Dalamud.Plugin;
using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ChatBubbles
{
    public unsafe partial class ChatBubbles : IDalamudPlugin
    {
        private void RefreshBubbleNodesAndVisuals()
        {
            var addon = Services.GameGui.GetAddonByName("_MiniTalk", 1);
            if (addon.Address == IntPtr.Zero)
            {
                for (var slot = 0; slot < 10; slot++)
                {
                    _bubblesAtk2[slot] = null;
                    _bubbleActive[slot] = false;
                    _bubbleActiveType[slot] = XivChatType.Debug;
                }

                ClearTrackedPlayerBubble();
                return;
            }

            var miniTalk = (AddonMiniTalk*)addon.Address;
            for (var slot = 0; slot < 10; slot++)
            {
                _bubblesAtk2[slot] = (AtkResNode*)miniTalk->TalkBubbles[slot].ComponentNode;
            }

            UpdateTrackedBubbleNodes();
        }

        private void OnceUponAFrame(IFramework framework)
        {
            try
            {
                RefreshBubbleNodesAndVisuals();
            }
            catch (Exception e)
            {
                Services.PluginLog.Error($"Error while populating the bubbles: {e}");
            }

            for (var i = 0; i < _charDatas.Count; i++)
            {
                if ((DateTime.Now - _charDatas[i].MessageDateTime).TotalMilliseconds > (_timer * 950))
                {
                    _charDatas.RemoveAt(i);
                    i--;
                }
            }
        }

        private void UpdateTrackedBubbleNodes()
        {
            for (var slot = 0; slot < 10; slot++)
            {
                var bubbleNode = _bubblesAtk2[slot];
                if (bubbleNode == null)
                {
                    continue;
                }

                if (!bubbleNode->IsVisible())
                {
                    _bubbleActive[slot] = false;
                    _bubbleActiveType[slot] = XivChatType.Debug;

                    if (_playerBubble == slot)
                    {
                        ClearTrackedPlayerBubble();
                    }

                    continue;
                }

                if (!_bubbleActive[slot] && _pendingVisualBubbles.Count > 0)
                {
                    var pendingVisual = _pendingVisualBubbles.Dequeue();
                    _bubbleActive[slot] = true;
                    _bubbleActiveType[slot] = pendingVisual.Type;

                    if (IsLocalPlayerActor(pendingVisual.ActorId))
                    {
                        SetTrackedPlayerBubbleSlot(slot);
                    }
                }

                if (_bubbleActive[slot])
                {
                    if (_playerBubble == slot)
                    {
                        if (_selfLock)
                        {
                            StabilizeSelfBubblePosition(bubbleNode);
                        }
                        else
                        {
                            _selfBubbleOffsetX = null;
                        }
                    }

                    ApplyBubbleAppearance(bubbleNode, _bubbleActiveType[slot]);
                    bubbleNode->ScaleX = _bubbleSize;
                    bubbleNode->ScaleY = _bubbleSize;
                    continue;
                }

                ResetBubbleNodeAppearance(bubbleNode, _defaultScale);
            }
        }

        private void ApplyBubbleAppearance(AtkResNode* bubbleNode, XivChatType bubbleType)
        {
            var colour = GetBubbleColour(bubbleType);
            var colour2 = GetBubbleColour2(bubbleType);

            if (!_pride)
            {
                bubbleNode->AddRed = (short)(colour2.X * 255);
                bubbleNode->AddGreen = (short)(colour2.Y * 255);
                bubbleNode->AddBlue = (short)(colour2.Z * 255);

                var resNodeNineGrid = bubbleNode->GetComponent()->UldManager.SearchNodeById(5);
                var resNodeDangly = bubbleNode->GetComponent()->UldManager.SearchNodeById(4);
                resNodeDangly->Color.R = (byte)(colour.X * 255);
                resNodeDangly->Color.G = (byte)(colour.Y * 255);
                resNodeDangly->Color.B = (byte)(colour.Z * 255);

                resNodeNineGrid->Color.R = (byte)(colour.X * 255);
                resNodeNineGrid->Color.G = (byte)(colour.Y * 255);
                resNodeNineGrid->Color.B = (byte)(colour.Z * 255);
                return;
            }

            bubbleNode->AddBlue += (short)(_f1 ? Random.Shared.Next(0, 2) : -Random.Shared.Next(0, 2));
            bubbleNode->AddRed += (short)(_f2 ? Random.Shared.Next(0, 2) : -Random.Shared.Next(0, 2));
            bubbleNode->AddGreen += (short)(_f3 ? Random.Shared.Next(0, 2) : -Random.Shared.Next(0, 2));

            if (bubbleNode->AddBlue >= 100)
            {
                bubbleNode->AddBlue = 100;
                _f1 = !_f1;
            }

            if (bubbleNode->AddRed >= 100)
            {
                bubbleNode->AddRed = 100;
                _f2 = !_f2;
            }

            if (bubbleNode->AddGreen >= 100)
            {
                bubbleNode->AddGreen = 100;
                _f3 = !_f3;
            }

            if (bubbleNode->AddBlue <= 10) _f1 = !_f1;
            if (bubbleNode->AddRed <= 10) _f2 = !_f2;
            if (bubbleNode->AddGreen <= 10) _f3 = !_f3;
        }

        private void StabilizeSelfBubblePosition(AtkResNode* bubbleNode)
        {
            if (!TryGetLocalPlayerScreenX(out var screenX))
            {
                return;
            }

            _selfBubbleOffsetX ??= bubbleNode->X - screenX;
            bubbleNode->SetPositionFloat(screenX + _selfBubbleOffsetX.Value, bubbleNode->Y);
        }
    }
}
