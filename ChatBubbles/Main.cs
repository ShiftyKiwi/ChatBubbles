using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Bindings.ImGui;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Balloon = FFXIVClientStructs.FFXIV.Client.Game.Balloon;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using Num = System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Lumina.Excel.Sheets;

namespace ChatBubbles
{
    internal class UiColorComparer : IEqualityComparer<UIColor>
    {
        public bool Equals(UIColor x, UIColor y)
        {
            return x.Dark == y.Dark; // based on variable i
        }
        public int GetHashCode(UIColor obj)
        {
            return obj.Dark.GetHashCode(); // hashcode of variable to compare
        }
    }

    public unsafe partial class ChatBubbles : IDalamudPlugin
    {
        public string Name => "Chat Bubbles+";
        private readonly List<UIColor> _uiColours;
        private readonly Config _configuration;
        private bool _picker;
        private readonly List<CharData> _charDatas = new();
        private int _timer;
        private UiColorPick? _chooser;
        private int _queue;
        private int _bubbleFunctionality;
        private bool _hide;
        private bool _friendsOnly;
        private bool _fcOnly;
        private bool _partyOnly;
        private readonly bool _textScale;
        private readonly List<Vector4> _bubbleColours;
        private readonly List<Vector4> _bubbleColours2;
        private float _defaultScale;
        private bool _switch;
        private float _bubbleSize;
        private bool _selfLock;
        private int _playerBubble = 99;
        private float? _selfBubbleOffsetX;
        private float? _selfBubbleSecondaryOffsetX;
        private float? _selfBubbleLocalOffsetX;
        private bool _config;
        private bool _oneTimeModal = true;
        private bool _debug;
        //TODO : check pauser usage ; uncomment below if found
        //private int pauser = 0;
        //#Pride
        private bool _f1;
        private bool _f2;
        private bool _f3;
        private bool _pride;
        //Distance
        private int _yalmCap;



        private readonly List<XivChatType> _channels;

        private readonly List<XivChatType> _order = new()
        {
            XivChatType.None, XivChatType.None, XivChatType.None, XivChatType.None, XivChatType.Say,
            XivChatType.Shout, XivChatType.TellOutgoing, XivChatType.TellIncoming, XivChatType.Party,
            XivChatType.Alliance,
            XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3, XivChatType.Ls4, XivChatType.Ls5,
            XivChatType.Ls6, XivChatType.Ls7, XivChatType.Ls8, XivChatType.FreeCompany, XivChatType.NoviceNetwork,
            XivChatType.CustomEmote, XivChatType.StandardEmote, XivChatType.Yell, XivChatType.CrossParty,
            XivChatType.PvPTeam,
            XivChatType.CrossLinkShell1, XivChatType.Echo, XivChatType.None, XivChatType.None, XivChatType.None,
            XivChatType.None, XivChatType.None, XivChatType.None, XivChatType.None, XivChatType.CrossLinkShell2, XivChatType.CrossLinkShell3,
            XivChatType.CrossLinkShell4,
            XivChatType.CrossLinkShell5, XivChatType.CrossLinkShell6, XivChatType.CrossLinkShell7,
            XivChatType.CrossLinkShell8
        };
        
         private readonly bool[] _yesno =
        {
            false, false, false, false, true,
            true, true, true, true, true,
            true, true, true, true, true,
            true, true, true, true, true,
            true, true, true, true, true,
            true, true, false, false, false,
            false, false, false, false, true,
            true, true,true, true, true,
            true
        };

        private readonly XivChatType[] _allowedChannels =
            {
            XivChatType.Say, XivChatType.Shout, XivChatType.TellOutgoing, XivChatType.TellIncoming, XivChatType.Party,
            XivChatType.Alliance, XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3, XivChatType.Ls4, XivChatType.Ls5,
            XivChatType.Ls6, XivChatType.Ls7, XivChatType.Ls8, XivChatType.FreeCompany, XivChatType.NoviceNetwork,
            XivChatType.CustomEmote, XivChatType.StandardEmote, XivChatType.Yell, XivChatType.CrossParty, XivChatType.CrossLinkShell1,
            XivChatType.CrossLinkShell2, XivChatType.CrossLinkShell3, XivChatType.CrossLinkShell4, XivChatType.CrossLinkShell5,
            XivChatType.CrossLinkShell6, XivChatType.CrossLinkShell7, XivChatType.CrossLinkShell8
        };

        //TODO : check bubbleNumber usage ; uncomment below if found
        //private int bubbleNumber = 0;

        private readonly bool[] _bubbleActive = new bool[10];
        private readonly XivChatType[] _bubbleActiveType = Enumerable.Repeat(XivChatType.Debug, 10).ToArray();
        private readonly AtkResNode*[] _bubblesAtk2 = new AtkResNode*[10];
        private readonly AtkResNode*[] _bubbleSecondaryNodes = new AtkResNode*[10];
        private readonly AtkResNode*[] _bubbleRoots = new AtkResNode*[10];
        private readonly UiColorPick[] _textColour;
        private PendingBubbleRequest? _pendingBubbleRequest;
        private readonly Queue<PendingVisualBubble> _pendingVisualBubbles = new();
        private CopiedChannelStyle? _copiedChannelStyle;


        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate IntPtr UpdateBubble(Balloon* bubble, IntPtr actor, IntPtr dunnoA, IntPtr dunnoB);
        private readonly Hook<UpdateBubble>? _updateBubbleFuncHook;


        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate IntPtr OpenBubble(IntPtr self, IntPtr actor, IntPtr textPtr, bool notSure, int attachmentPointID);
        private readonly Hook<OpenBubble>? _openBubbleFuncHook;

        private IPlayerCharacter? LocalPlayer => Services.ObjectTable.LocalPlayer;
        
        public ChatBubbles(IDalamudPluginInterface pluginInt)
        {
            pluginInt.Create<Services>();
 
            _configuration = Services.PluginInterface.GetPluginConfig() as Config ?? new Config();
            _timer = _configuration.Timer;
            _channels = _configuration.Channels;
            _textColour = _configuration.TextColour;
            _queue = _configuration.Queue;
            _bubbleFunctionality = _configuration.BubbleFunctionality;
            _hide = _configuration.Hide;
            _fcOnly = _configuration.fcOnly;
            _partyOnly = _configuration.partyOnly;
            _friendsOnly = _configuration.friendsOnly;
            _textScale = _configuration.TextScale;
            _bubbleColours = _configuration.BubbleColours;
            _bubbleColours2 = _configuration.BubbleColours2;
            _bubbleSize = _configuration.BubbleSize;
            _selfLock = _configuration.SelfLock;
            _defaultScale = _configuration.DefaultScale;
            _switch = _configuration.Switch;
            _yalmCap = _configuration.YalmCap;

            NormalizeSettings();

            //Added two enums in dalamud update
            if (_bubbleColours.Count == 39)
            {
                _bubbleColours.Insert(32, new Vector4(1, 1, 1, 0));
                _bubbleColours.Insert(32, new Vector4(1, 1, 1, 0));
                _bubbleColours2.Insert(32, new Vector4(0,0, 0, 0));
                _bubbleColours2.Insert(32, new Vector4(0, 0, 0, 0));

                var temp = new UiColorPick[_bubbleColours.Count];
                for (int i =0; i<39; i++)
                {
                    if(i >= 32)
                    {
                        temp[i+2] = _textColour[i];
                    }
                    else
                    {
                        temp[i] = _textColour[i];
                    }
                }
                temp[32] =  new() { Choice = 0, Option = 0 };
                temp[33] = new() { Choice = 0, Option = 0 };

                _textColour = temp;
            }

            while (_bubbleColours.Count < 41) _bubbleColours.Add(new Vector4(1,1,1,0));
            while (_bubbleColours2.Count < 41) _bubbleColours2.Add(new Vector4(0,0,0,0));
            
            var list = new List<UIColor>(Services.DataManager.Excel.GetSheet<UIColor>()!.Distinct());
            list.Sort((a, b) =>
            {
                var colorA = ConvertUIColorToColor(a);
                var colorB = ConvertUIColorToColor(b);
                var aH = 0f;
                var aS = 0f;
                var aV = 0f;
                var bH = 0f;
                var bS = 0f;
                var bV = 0f;
                ImGui.ColorConvertRGBtoHSV(colorA.X, colorA.Y, colorA.Z, ref aH, ref aS, ref aV);
                ImGui.ColorConvertRGBtoHSV(colorB.X, colorB.Y, colorB.Z, ref bH, ref bS, ref bV);

                var hue = aH.CompareTo(bH);
                if (hue != 0) { return hue; }

                var saturation = aS.CompareTo(bS);
                if (saturation != 0) { return saturation; }

                var value = aV.CompareTo(bV);
                return value != 0 ? value : 0;
            });
            _uiColours = list;


            

            Services.Framework.Update += OnceUponAFrame;
            Services.ChatGui.ChatMessage += Chat_OnChatMessage;
            Services.PluginInterface.UiBuilder.Draw += BubbleConfigUi;
            Services.PluginInterface.UiBuilder.OpenConfigUi += BubbleConfig;
            Services.CommandManager.AddHandler("/bub", new CommandInfo(Command)
            {
                HelpMessage = "Opens the Chat Bubble config menu"
            });
            
            var updateBubblePtr = Services.SigScannerD.ScanText("48 85 D2 0F 84 ?? ?? ?? ?? 48 89 6C 24 ?? 56 48 83 EC 30 8B 41 0C");
            UpdateBubble updateBubbleFunc = UpdateBubbleFuncFunc;
            try
            {
                _updateBubbleFuncHook = Services.GameInteropProvider.HookFromAddress<UpdateBubble>(updateBubblePtr + 0x9, updateBubbleFunc);
                _updateBubbleFuncHook.Enable();
                Services.PluginLog.Debug("GOOD");
            }
            catch (Exception e)
            { Services.PluginLog.Error("BAD\n" + e); }

            
            var openBubblePtr = Services.SigScannerD.ScanText("E8 ?? ?? ?? FF 48 8B 7C 24 48 C7 46 0C 01 00 00 00");
            OpenBubble openBubbleFunc = OpenBubbleFuncFunc;
            try
            {
                _openBubbleFuncHook = Services.GameInteropProvider.HookFromAddress<OpenBubble>(openBubblePtr, openBubbleFunc);
                _openBubbleFuncHook.Enable();
                Services.PluginLog.Debug("GOOD2");
            }
            catch (Exception e)
            { Services.PluginLog.Error("BAD2\n" + e); }
        }

        private Vector4 ConvertUIColorToColor(UIColor uiColor)
        {
            var temp = BitConverter.GetBytes(uiColor.Dark);
            return new Vector4((float)temp[3] / 255,
                (float)temp[2] / 255,
                (float)temp[1] / 255,
                (float)temp[0] / 255);
        }

        private void NormalizeSettings()
        {
            _timer = Math.Max(1, _timer);
            _queue = Math.Max(1, _queue);
            _bubbleSize = Math.Max(0.1f, _bubbleSize);
            _defaultScale = Math.Max(0.1f, _defaultScale);
            _yalmCap = Math.Max(0, _yalmCap);
        }

        private void SaveConfig()
        {
            NormalizeSettings();
            _configuration.Timer = _timer;
            _configuration.Channels = _channels;
            _configuration.TextColour = _textColour;
            _configuration.Queue = _queue;
            _configuration.BubbleFunctionality = _bubbleFunctionality;
            _configuration.Hide = _hide;
            _configuration.fcOnly = _fcOnly;
            _configuration.friendsOnly = _friendsOnly;
            _configuration.partyOnly = _partyOnly;
            _configuration.BubbleColours = _bubbleColours;
            _configuration.BubbleColours2 = _bubbleColours2;
            _configuration.TextScale = _textScale;
            _configuration.BubbleSize = _bubbleSize;
            _configuration.SelfLock = _selfLock;
            _configuration.DefaultScale = _defaultScale;
            _configuration.Switch = _switch;
            _configuration.YalmCap = _yalmCap;
            Services.PluginInterface.SavePluginConfig(_configuration);
        }


        private Vector4 GetBubbleColour(XivChatType type)
        {
            for (int i = 0; i < _order.Count; i++)
            {
                if (type == _order[i])
                {
                    return _bubbleColours[i];
                }
            }

            return new Vector4(0, 0, 0, 0);
        }
        private Vector4 GetBubbleColour2(XivChatType type)
        {
            for (int i = 0; i < _order.Count; i++)
            {
                if (type == _order[i])
                {
                    return _bubbleColours2[i];
                }
            }

            return new Vector4(0, 0, 0, 0);
        }

        private void ResetBubbleNodeAppearance(AtkResNode* bubbleNode, float scale)
        {
            bubbleNode->AddRed = 0;
            bubbleNode->AddBlue = 0;
            bubbleNode->AddGreen = 0;
            bubbleNode->ScaleX = scale;
            bubbleNode->ScaleY = scale;

            var component = bubbleNode->GetComponent();
            ref var uldManager = ref component->UldManager;
            var resNodeNineGrid = uldManager.SearchNodeById(5);
            var resNodeDangly = uldManager.SearchNodeById(4);

            resNodeDangly->Color.R = byte.MaxValue;
            resNodeDangly->Color.G = byte.MaxValue;
            resNodeDangly->Color.B = byte.MaxValue;
            resNodeNineGrid->Color.R = byte.MaxValue;
            resNodeNineGrid->Color.G = byte.MaxValue;
            resNodeNineGrid->Color.B = byte.MaxValue;
        }

        private void CleanBubbles()
        {
            var addon = Services.GameGui.GetAddonByName("_MiniTalk", 1);
            if (addon.Address == IntPtr.Zero)
            {
                return;
            }

            var miniTalk = (AddonMiniTalk*)addon.Address;
            for (var i = 0; i < 10; i++)
            {
                var bubbleNode = (AtkResNode*)miniTalk->TalkBubbles[i].ComponentNode;
                if (bubbleNode == null)
                {
                    continue;
                }

                ResetBubbleNodeAppearance(bubbleNode, 1f);
            }
        }

        private bool IsLocalPlayerActor(uint actorId)
        {
            return actorId != 0 && actorId == LocalPlayer?.EntityId;
        }

        private void SetTrackedPlayerBubbleSlot(int slot)
        {
            if (_playerBubble != slot)
            {
                _selfBubbleOffsetX = null;
                _selfBubbleSecondaryOffsetX = null;
                _selfBubbleLocalOffsetX = null;
            }

            _playerBubble = slot;
        }

        private void ClearTrackedPlayerBubble()
        {
            _playerBubble = 99;
            _selfBubbleOffsetX = null;
            _selfBubbleSecondaryOffsetX = null;
            _selfBubbleLocalOffsetX = null;
        }

        private bool TryGetLocalPlayerScreenX(out float screenX)
        {
            screenX = 0;
            var localPlayer = LocalPlayer;
            if (localPlayer == null)
            {
                return false;
            }

            return Services.GameGui.WorldToScreen(localPlayer.Position, out var screenPosition)
                && (screenX = screenPosition.X) >= 0;
        }
        
        private IntPtr UpdateBubbleFuncFunc(Balloon* bubble, IntPtr actor, IntPtr dunnoA, IntPtr dunnoB)
        {
            if (actor != IntPtr.Zero || _pendingBubbleRequest != null)
            {
                var actorId = TryReadActorId(actor);
                var pending = GetPendingBubbleRequest(actorId);
                var trackedActorId = actorId != 0 ? (uint)actorId : pending?.ActorId ?? 0;
                var charData = GetCurrentCharData(actorId) ?? (pending != null ? GetCurrentCharData((int)pending.ActorId) : null);
                var bubbleType = charData?.Type ?? pending?.Type;

                if (bubbleType != null)
                {
                    if (bubble->State == BalloonState.Inactive && _switch)
                    {

                        //Get the slot that will turn into the bubble
                        var freeSlot = GetFreeBubbleSlot();
                        if (freeSlot == -1)
                        {
                            return _updateBubbleFuncHook!.Original(bubble, actor, dunnoA, dunnoB);
                        }

                        _bubbleActive[freeSlot] = true;
                        _bubbleActiveType[freeSlot] = bubbleType.Value;

                        if (IsLocalPlayerActor(trackedActorId))
                        {
                            SetTrackedPlayerBubbleSlot(freeSlot);
                        }

                        bubble->State = BalloonState.Closing;

                        if (_textScale)
                        {
                            var val = (double)(charData?.Message?.TextValue.Length ?? pending?.Message?.TextValue.Length ?? 0) / 10;
                            if ((float) (_timer * val) < _timer)
                            {
                                bubble->PlayTimer = _timer;
                            }
                            else
                            {
                                bubble->PlayTimer = (float) (_timer * val);
                            }
                        }
                        else
                        {
                            bubble->PlayTimer = _timer;
                        }
                    }

                    if (bubble->State == BalloonState.Active && charData?.NewMessage == true)
                    {
                        bubble->State = BalloonState.Inactive;
                        bubble->PlayTimer = 0;
                        if (charData != null)
                        {
                            charData.NewMessage = false;
                        }
                    }
                }
            }


            return _updateBubbleFuncHook!.Original(bubble, actor, dunnoA, dunnoB);
        }
        
        private int GetFreeBubbleSlot()
        {
            var addonPtr2 =  Services.GameGui.GetAddonByName("_MiniTalk",1);
            if (addonPtr2 != IntPtr.Zero)
            {
                for (int i = 0; i < 10; i++)
                {
                    var bubbleNode = _bubblesAtk2[i];
                    if (bubbleNode == null || bubbleNode->IsVisible())
                    {
                        continue;
                    }
                    return i;
                }
                return -1;
            }
            else return -1;
        }

        private IntPtr OpenBubbleFuncFunc(IntPtr self, IntPtr actor, IntPtr textPtr, bool notSure, int attachmentPointID)
        {
            // S Rank Atticus the Primogenitor I hate you.
            // This check lets the hook go through since most NPCs & Players have the 25 APID
            if (attachmentPointID != 25 && attachmentPointID != 0)
            {
                Services.PluginLog.Warning($"Unusual AttachmentPoint Detected: {attachmentPointID.ToString()}");
                return _openBubbleFuncHook!.Original(self, actor, textPtr, notSure, attachmentPointID);
            }

            var actorId = TryReadActorId(actor);
            var pending = GetPendingBubbleRequest(actorId);
            int newAttachmentPointID = 25;
            IntPtr allocatedTextPtr = IntPtr.Zero;

            foreach (var cd in GetCandidateCharDatas(actorId, pending))
            {
                var freeSlot = GetFreeBubbleSlot();
                if (freeSlot == -1)
                {
                    break;
                }

                _bubbleActiveType[freeSlot] = cd.Type;
                _bubbleActive[freeSlot] = true;
                if (IsLocalPlayerActor((uint)actorId))
                {
                    SetTrackedPlayerBubbleSlot(freeSlot);
                }

                if (cd.Message?.TextValue.Length > 0)
                {
                    var bytes = cd.Message.Encode();
                    allocatedTextPtr = Marshal.AllocHGlobal(bytes.Length + 1);
                    Marshal.Copy(bytes, 0, allocatedTextPtr, bytes.Length);
                    Marshal.WriteByte(allocatedTextPtr, bytes.Length, 0);
                    textPtr = allocatedTextPtr;
                }

                break;
            }

            try
            {
                return _openBubbleFuncHook!.Original(self, actor, textPtr, notSure, newAttachmentPointID);
            }
            finally
            {
                if (allocatedTextPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(allocatedTextPtr);
                }
            }
        }


        private int TryReadActorId(IntPtr actor)
        {
            if (actor == IntPtr.Zero)
            {
                return 0;
            }

            const int idOffset = 116;
            return Marshal.ReadInt32(actor + idOffset);
        }

        private CharData? GetCurrentCharData(int actorId)
        {
            if (actorId == 0)
            {
                return null;
            }

            return _charDatas
                .Where(cd => cd.ActorId == actorId)
                .OrderByDescending(cd => cd.MessageDateTime)
                .FirstOrDefault();
        }

        private IEnumerable<CharData> GetCandidateCharDatas(int actorId, PendingBubbleRequest? pending)
        {
            var current = GetCurrentCharData(actorId);
            if (current != null)
            {
                yield return current;
                yield break;
            }

            if (pending != null)
            {
                var pendingCurrent = GetCurrentCharData((int)pending.ActorId);
                if (pendingCurrent != null)
                {
                    yield return pendingCurrent;
                    yield break;
                }
            }
        }

        private PendingBubbleRequest? GetPendingBubbleRequest(int actorId)
        {
            if (_pendingBubbleRequest == null)
            {
                return null;
            }

            if ((DateTime.UtcNow - _pendingBubbleRequest.CreatedAtUtc).TotalSeconds > 2)
            {
                _pendingBubbleRequest = null;
                return null;
            }

            if (actorId != 0 && _pendingBubbleRequest.ActorId != (uint)actorId)
            {
                return null;
            }

            return _pendingBubbleRequest;
        }

        void IDisposable.Dispose()
        {
            Services.Framework.Update -= OnceUponAFrame;
            Services.ChatGui.ChatMessage -= Chat_OnChatMessage;
            Services.PluginInterface.UiBuilder.Draw -= BubbleConfigUi;
            Services.PluginInterface.UiBuilder.OpenConfigUi -= BubbleConfig;
            Services.CommandManager.RemoveHandler("/bub");
            _updateBubbleFuncHook?.Dispose();
            _openBubbleFuncHook?.Dispose();
            CleanBubbles();
        }

        private void BubbleConfig()
        {
            _config = !_config;
        } 


        // What to do when command is called
        private void Command(string command, string arguments)
        {
            if (arguments == "clean")
            {
                var chat = new XivChatEntry();
                chat.Message = "Cleaning Bubbles";
                Services.ChatGui.Print(chat);
                CleanBubbles();
            }
            else if (arguments == "toggle")
            {
                var tog = "ON";
                if (_switch)
                {
                    tog = "OFF";
                }
                var chat = new XivChatEntry();
                chat.Message = $"Toggling Bubbles {tog}";
                Services.ChatGui.Print(chat);
                _switch = !_switch;
            }
            else
            {
                _config = !_config;
            }
        }


        private uint GetActorId(string nameInput)
        {
            if (_hide && nameInput == LocalPlayer?.Name.TextValue) return 0;

            foreach (var t in Services.ObjectTable)
            {
                if (!(t is IPlayerCharacter pc)) continue;
                if (pc.Name.TextValue == nameInput) return pc.EntityId;
            }
            return 0;
        }

        private bool IsFriend(string nameInput)
        {
            if (nameInput == LocalPlayer?.Name.TextValue) return true;

            foreach (var t in Services.ObjectTable)
            {
                if (!(t is IPlayerCharacter pc)) continue;

                if (pc.Name.TextValue == nameInput)
                {
                    return pc.StatusFlags.HasFlag(StatusFlags.Friend);
                }
            }
            return false;
        }

        private bool IsPartyMember(string nameInput)
        {
            if (nameInput == LocalPlayer?.Name.TextValue) return true;

            foreach (var t in Services.ObjectTable)
            {
                if (!(t is IPlayerCharacter pc)) continue;
                if (pc.Name.TextValue == nameInput)
                {
                    return pc.StatusFlags.HasFlag(StatusFlags.PartyMember);
                }
            }
            return false;
        }

        private bool IsFC(string nameInput)
        { 
            if (nameInput == LocalPlayer?.Name.TextValue) return true;

			foreach (var t in Services.ObjectTable)
            { 
                if (!(t is IPlayerCharacter pc)) continue;
                if (pc.Name.TextValue == nameInput)

                {
					return LocalPlayer?.CompanyTag.TextValue == pc.CompanyTag.TextValue;
                }
            }
            return false;
        }

        private int GetActorDistance(string name)
        {
            if (name == LocalPlayer?.Name.TextValue) return 0;

            foreach (var t in Services.ObjectTable)
            {
                if (!(t is IPlayerCharacter pc)) continue;
                if (pc.Name.TextValue == name)
                {
                    return (int)Math.Sqrt( Math.Pow(pc.YalmDistanceX, 2) + Math.Pow(pc.YalmDistanceZ, 2));
                }
            }
            return 0;            
            
        }
        
        private class CharData
        {
            public SeString? Message;
            public XivChatType Type;
            public uint ActorId;
            public DateTime MessageDateTime;
            public bool NewMessage { get; set; }
        }
    }
    
    public class UiColorPick
    {
        public uint Choice { get; set; }
        public uint Option { get; set; }
    }

    public class Config : IPluginConfiguration
    {
        public int Version { get; set; } = 1;
        public List<XivChatType> Channels { get; set; } = new() {XivChatType.Say};
        public int Timer { get; set; } = 7;
        public int BubbleFunctionality { get; set; } = 0;
		public bool Hide { get; set; } = false;
		public bool friendsOnly { get; set; } = false;
		public bool fcOnly { get; set; } = false;
		public bool partyOnly { get; set; } = false;
		public bool TextScale { get; set; } = false;
        public bool SelfLock { get; set; } = false;
        public float BubbleSize { get; set; } = 1f;
        public float DefaultScale { get; set; } = 1f;
        public bool Switch { get; set; } = true;
        public int YalmCap { get; set; } = 99;
        public bool oneTimeModal { get; set; } = true;

        public UiColorPick[] TextColour { get; set; } =
        {
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }, new () { Choice = 0, Option =0 },
            new() { Choice = 0, Option =0 }, new() { Choice = 0, Option =0 }
        };

        public List<Vector4> BubbleColours { get; set; } = new List<Num.Vector4>();
        public List<Vector4> BubbleColours2 { get; set; } = new List<Num.Vector4>();

        public int Queue { get; set; } = 3;
    }

    internal class PendingBubbleRequest
    {
        public uint ActorId { get; init; }
        public XivChatType Type { get; init; }
        public SeString? Message { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }

    internal class PendingVisualBubble
    {
        public XivChatType Type { get; init; }
        public uint ActorId { get; init; }
    }

    internal class CopiedChannelStyle
    {
        public UiColorPick TextColor { get; init; } = new();
        public Vector4 BubbleBackground { get; init; }
        public Vector4 BubbleTint { get; init; }
    }
    
}

/*
                            var rand = new Random();
                            
                            if(f1) {bub->AddBlue += (ushort)rand.Next(0, 10);}
                            else {bub->AddBlue -= (ushort)rand.Next(0, 10);}
                            
                            if(f2) {bub->AddRed += (ushort)rand.Next(0, 10);}
                            else {bub->AddRed -= (ushort)rand.Next(0, 10);}
                            
                            if(f3) {bub->AddGreen += (ushort)rand.Next(0, 10);}
                            else {bub->AddGreen -= (ushort)rand.Next(0, 10);}
                            
                            if(bub->AddBlue>=100){bub->AddBlue = 100; f1=!f1;}
                            if(bub->AddRed>=100){bub->AddRed = 100; f2=!f2;}
                            if(bub->AddGreen>=100){bub->AddGreen = 100; f3=!f3;}
                            
                            if(bub->AddBlue<=10) f1=!f1;
                            if(bub->AddRed<=10) f2=!f2;
                            if(bub->AddGreen<=10) f3=!f3;
*/
