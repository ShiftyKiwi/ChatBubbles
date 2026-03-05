using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Num = System.Numerics;

namespace ChatBubbles
{
    public unsafe partial class ChatBubbles : IDalamudPlugin
    {
        // What to do with chat messages
        private void Chat_OnChatMessage(XivChatType type, int senderId, ref SeString sender, ref SeString cmessage, ref bool isHandled)
        {
            if (isHandled) return;
            if (!_channels.Contains(type)) return;
            var localPlayer = LocalPlayer;
            var fmessage = new SeString(new List<Payload>());
            var nline = new SeString(new List<Payload>());
            nline.Payloads.Add(new TextPayload("\n"));


            //Stolen from Dragon (SheepGoMeh)
            PlayerPayload? playerPayload = null;

            List<char> toRemove = new()
            {
              //src: https://na.finalfantasyxiv.com/lodestone/character/10080203/blog/2891974/
              '','','','','','','','','','','','','','','','','','','','','','','','','','','','','','','','',
            };

            var sanitized = sender.ToString();

            foreach (var c in toRemove)
            {
                // Removes all special characters related to Party List numbering
                sanitized = sanitized.Replace(c.ToString(), string.Empty);
            }

            if (localPlayer != null && sanitized == localPlayer.Name.TextValue)
            {
                playerPayload = new PlayerPayload(localPlayer.Name.TextValue, localPlayer.HomeWorld.Value!.Region);
                if (type == XivChatType.CustomEmote)
                {
                    var playerName = new SeString(new List<Payload>());
                    playerName.Payloads.Add(new TextPayload(localPlayer.Name.TextValue));
                    fmessage.Append(playerName);
                }
            }
            else
            {
                if(type == XivChatType.StandardEmote)
				{
					playerPayload = sender.Payloads.OfType<PlayerPayload>().SingleOrDefault()
                        ?? cmessage.Payloads.OfType<PlayerPayload>().FirstOrDefault();
				}
                else
				{
					playerPayload = sender.Payloads.OfType<PlayerPayload>().SingleOrDefault();
                    if (type == XivChatType.CustomEmote && playerPayload != null)
					{
						fmessage.Append(playerPayload.PlayerName);
					}
				}
            }

            fmessage.Append(cmessage);
            var isEmoteType = type is XivChatType.CustomEmote or XivChatType.StandardEmote;
            if (isEmoteType)
            {
                fmessage.Payloads.Insert(0, new EmphasisItalicPayload(true));
                fmessage.Payloads.Add(new EmphasisItalicPayload(false));
            }

            var pName = playerPayload?.PlayerName ?? localPlayer?.Name.TextValue;
            var sName = sender.Payloads.OfType<PlayerPayload>().SingleOrDefault();
            var senderName = sName?.PlayerName ?? pName;

            if (string.IsNullOrEmpty(pName) || string.IsNullOrEmpty(senderName))
            {
                return;
            }

            if(!Services.DutyState.IsDutyStarted)
            {
			    if (_partyOnly && !IsPartyMember(senderName)) return;
			    if (_fcOnly && !IsFC(senderName)) return;
			    if (_friendsOnly && !IsFriend(senderName)) return; 
            }

            var actr = GetActorId(pName);
            var x = GetActorDistance(pName);
            if (x > _yalmCap) return;

            if (type == XivChatType.TellOutgoing)
            {
                if (localPlayer != null)
                {
                    actr = localPlayer.EntityId;
                    pName = localPlayer.Name.TextValue;
                }
            }

            fmessage.Payloads.Insert(0,
                new UIForegroundPayload((ushort)_textColour[_order.IndexOf(type)].Option));
            fmessage.Payloads.Add(new UIForegroundPayload(0));

            if (actr == 0) return;
            TryOpenCharacterBubble(actr, fmessage.TextValue);

            var update = 0;
            var time = new TimeSpan(0, 0, 0);
            var add = 0;
            var bn = -1;
            var timeTake = 0;

            foreach (var cd in _charDatas)
            {
                if (_debug)
                {
                    //PluginLog.Log($"Check: {actr}, Against: {cd.ActorId}");
                }

                if (cd.ActorId != actr) continue;

                if (timeTake == 0)
                {
                    if (_textScale)
                    {
                        var val = (double)(cd.Message?.TextValue + cmessage.TextValue).Length / 10;
                        if ((_timer * val) < _timer)
                        {
                            add += _timer;
                        }
                        else
                        {
                            add += (int)(_timer * val);
                        }
                    }
                    else
                    {
                        add += _timer;
                    }
                }

                update++;
                bn = cd.BubbleNumber;

                //queue
                if (_bubbleFunctionality == 0)
                {
                    timeTake = (int)(DateTime.Now - cd.MessageDateTime).TotalMilliseconds;
                    continue;
                }

                switch (_bubbleFunctionality)
                {
                    case 1: // stack
                        cd.Message?.Append(nline);
                        cd.Message?.Append(fmessage);
                        break;
                    case 2: // replace
                        cd.Message = nline;
                        cd.Message = fmessage;
                        break;
                    case 3: // Sensible
                        if (type == cd.Type)
                        {
                            cd.Message?.Append(nline);
                            cd.Message?.Append(fmessage);
                        }
                        else
                        {
                            timeTake = (int)(DateTime.Now - cd.MessageDateTime).TotalMilliseconds;
                            continue;
                        }
                        break;
                }
                update = 99999;
                cd.NewMessage = true;
                cd.MessageDateTime = DateTime.Now;
            }


            if (update == 0)
            {
                _charDatas.Add(new CharData
                {
                    ActorId = actr,
                    MessageDateTime = DateTime.Now,
                    Message = fmessage,
                    Name = pName,
                    Type = type
                });
            }
            else
            {
                if (update >= _queue)
                {
                    return;
                }
                time = new TimeSpan(0, 0, 0, add, -timeTake);


                _charDatas.Add(new CharData
                {
                    ActorId = actr,
                    MessageDateTime = DateTime.Now.Add(time),
                    Message = fmessage,
                    Name = pName,
                    Type = type,
                    BubbleNumber = bn
                });

            }
        }

        private void TryOpenCharacterBubble(uint actorId, string message)
        {
            if (actorId == 0 || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            foreach (var obj in Services.ObjectTable)
            {
                if (obj is not IPlayerCharacter playerCharacter || playerCharacter.EntityId != actorId)
                {
                    continue;
                }

                if (playerCharacter.Address == IntPtr.Zero)
                {
                    return;
                }

                var attachmentPointId = (byte)(_configuration.assBubbles ? 63 : 25);
                if (_configuration.chaosMode && attachmentPointId == 25)
                {
                    int[] chaos = [1, 6, 7, 8, 9, 10, 11, 25, 28, 30, 32, 33, 34, 35, 43, 44, 45, 48, 49, 50, 51, 52, 53, 54, 55, 62, 63, 64];
                    new Random().Shuffle(chaos);
                    attachmentPointId = (byte)chaos[0];
                }

                using var text = new Utf8String(message);
                var character = (Character*)playerCharacter.Address;
                character->YellBalloon.OpenBalloon(text.StringPtr, _timer, false, 0f, false, false, true, attachmentPointId);

                return;
            }
        }
    }
}
