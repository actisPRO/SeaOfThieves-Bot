﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;

namespace Bot_NetCore.Listeners
{
    public static class VoiceListener
    {
        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task CreateOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            try
            {
                if (e.Channel != null)
                    if (e.Channel.Id == Bot.BotSettings.AutocreateGalleon ||
                        e.Channel.Id == Bot.BotSettings.AutocreateBrigantine ||
                        e.Channel.Id == Bot.BotSettings.AutocreateSloop
                    ) // мы создаем канал, если пользователь зашел в один из каналов автосоздания
                    {
                        if (Bot.ShipCooldowns.ContainsKey(e.User)) // проверка на кулдаун
                            if ((Bot.ShipCooldowns[e.User] - DateTime.Now).Seconds > 0)
                            {
                                var m = await e.Guild.GetMemberAsync(e.User.Id);
                                await m.PlaceInAsync(e.Guild.GetChannel(Bot.BotSettings.WaitingRoom));
                                await m.SendMessageAsync($"{Bot.BotSettings.ErrorEmoji} Вам нужно подождать " +
                                                         $"**{(Bot.ShipCooldowns[e.User] - DateTime.Now).Seconds}** секунд прежде чем " +
                                                         "создавать новый корабль!");
                                e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                                    $"Участник {e.User.Username}#{e.User.Discriminator} ({e.User.Discriminator}) был перемещён в комнату ожидания.",
                                    DateTime.Now);
                                return;
                            }

                        // если проверка успешно пройдена, добавим пользователя
                        // в словарь кулдаунов
                        Bot.ShipCooldowns[e.User] = DateTime.Now.AddSeconds(Bot.BotSettings.FastCooldown);

                        //Проверка на эмиссарство
                        var channelSymbol = Bot.BotSettings.AutocreateSymbol;
                        ((DiscordMember)e.User).Roles.ToList().ForEach(x =>
                        {
                            if (x.Id == Bot.BotSettings.EmissaryGoldhoadersRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":moneybag:");
                            else if (x.Id == Bot.BotSettings.EmissaryTradingCompanyRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":pig:");
                            else if (x.Id == Bot.BotSettings.EmissaryOrderOfSoulsRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":skull:");
                            else if (x.Id == Bot.BotSettings.EmissaryAthenaRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":gem:");
                            else if (x.Id == Bot.BotSettings.EmissaryReaperBonesRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":skull_crossbones:");
                            else if (x.Id == Bot.BotSettings.HuntersRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":fish:");
                            else if (x.Id == Bot.BotSettings.ArenaRole)
                                channelSymbol = DiscordEmoji.FromName((DiscordClient)e.Client, ":crossed_swords:");

                        });

                        DiscordChannel created = null;
                        // Проверяем канал в котором находится пользователь

                        if (e.Channel.Id == Bot.BotSettings.AutocreateSloop) //Шлюп
                            created = await e.Guild.CreateChannelAsync(
                                $"{channelSymbol} Шлюп {e.User.Username}", ChannelType.Voice,
                                e.Guild.GetChannel(Bot.BotSettings.AutocreateCategory), bitrate: Bot.BotSettings.Bitrate, userLimit: 2);
                        else if (e.Channel.Id == Bot.BotSettings.AutocreateBrigantine) // Бригантина
                            created = await e.Guild.CreateChannelAsync(
                                $"{channelSymbol} Бриг {e.User.Username}", ChannelType.Voice,
                                e.Guild.GetChannel(Bot.BotSettings.AutocreateCategory), bitrate: Bot.BotSettings.Bitrate, userLimit: 3);
                        else // Галеон
                            created = await e.Guild.CreateChannelAsync(
                                $"{channelSymbol} Галеон {e.User.Username}", ChannelType.Voice,
                                e.Guild.GetChannel(Bot.BotSettings.AutocreateCategory), bitrate: Bot.BotSettings.Bitrate, userLimit: 4);

                        var member = await e.Guild.GetMemberAsync(e.User.Id);

                        await member.PlaceInAsync(created);

                        e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot",
                            $"Участник {e.User.Username}#{e.User.Discriminator} ({e.User.Id}) создал канал через автосоздание.",
                            DateTime.Now);
                    }
            }
            catch (NullReferenceException) // исключение выбрасывается если пользователь покинул канал
            {
                // нам здесь ничего не надо делать, просто пропускаем
            }
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task FindOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            if (e.Channel != null && 
                e.Channel.Id == Bot.BotSettings.FindShip)
            {
                var shipCategory = e.Guild.GetChannel(Bot.BotSettings.AutocreateCategory);

                var membersLookingForTeam = new List<ulong>();
                foreach (var message in (await e.Guild.GetChannel(Bot.BotSettings.FindChannel).GetMessagesAsync(100)))
                {
                    if (message.Pinned) continue; // автор закрепленного сообщения не должен учитываться
                    if (membersLookingForTeam.Contains(message.Author.Id)) continue; // автор сообщения уже мог быть добавлен в лист

                    membersLookingForTeam.Add(message.Author.Id);
                }

                var possibleChannels = new List<DiscordChannel>();
                foreach (var ship in shipCategory.Children)
                    if (ship.Users.Count() < ship.UserLimit)
                        foreach (var user in ship.Users)
                            if (membersLookingForTeam.Contains(user.Id))
                                possibleChannels.Add(ship);

                var m = await e.Guild.GetMemberAsync(e.User.Id);
                if (possibleChannels.Count == 0)
                {
                    await m.PlaceInAsync(e.Guild.GetChannel(Bot.BotSettings.WaitingRoom));
                    await m.SendMessageAsync($"{Bot.BotSettings.ErrorEmoji} Не удалось найти подходящий корабль.");
                    return;
                }

                var random = new Random();
                var rShip = random.Next(0, possibleChannels.Count);

                await m.PlaceInAsync(possibleChannels[rShip]);
                e.Client.DebugLogger.LogMessage(LogLevel.Info, "Bot", $"Пользователь {m.Username}#{m.Discriminator} успешно воспользовался поиском корабля!", DateTime.Now);
                return;
            }
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task PrivateOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            if (e.Channel != null && 
                e.Channel.ParentId == Bot.BotSettings.PrivateCategory)
                foreach (var ship in ShipList.Ships.Values)
                    if (ship.Channel == e.Channel.Id)
                    {
                        ship.SetLastUsed(DateTime.Now);
                        ShipList.SaveToXML(Bot.BotSettings.ShipXML);
                        break;
                    }
            await Task.CompletedTask;
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task DeleteOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            e.Guild.Channels[Bot.BotSettings.AutocreateCategory].Children
                .Where(x => x.Type == ChannelType.Voice && x.Users.Count() == 0).ToList()
                .ForEach(async x =>
                    {
                        try
                        {
                            await x.DeleteAsync();
                        }
                        catch (NullReferenceException) { } // исключения выбрасывается если пользователь покинул канал
                        catch (NotFoundException) { }
                    });

            await Task.CompletedTask;
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task UpdateClientStatusOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            await Bot.UpdateBotStatusAsync(e.Client, e.Guild);
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task FleetLogOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            //Для проверки если канал рейда чекать если название КАТЕГОРИИ канала начинается с "рейд"

            // User changed voice channel
            if (e.Before != null && e.Before.Channel != null && e.After != null && e.After.Channel != null)
            {
                if (e.Before.Channel.Parent.Name.StartsWith("Рейд") ||
                    e.After.Channel.Parent.Name.StartsWith("Рейд"))
                {
                    await e.Guild.GetChannel(Bot.BotSettings.FleetLogChannel)
                        .SendMessageAsync($"Пользователь **{e.User.Username}#{e.User.Discriminator}** ({e.User.Id}) " +
                        $"сменил канал с **{e.Before.Channel.Name}** ({e.Before.Channel.Id}) " +
                        $"на **{e.After.Channel.Name}** ({e.After.Channel.Id})");
                }
            }
            //User left from voice
            else if (e.Before != null && e.Before.Channel != null)
            {
                if (e.Before.Channel.Parent.Name.StartsWith("Рейд"))
                {
                    await e.Guild.GetChannel(Bot.BotSettings.FleetLogChannel)
                        .SendMessageAsync($"Пользователь **{e.User.Username}#{e.User.Discriminator}** ({e.User.Id}) " +
                        $"покинул канал **{e.Before.Channel.Name}** ({e.Before.Channel.Id})");
                }
            }
            //User joined to server voice
            else if (e.After != null && e.After.Channel != null)
            {
                if (e.After.Channel.Parent.Name.StartsWith("Рейд"))
                {
                    await e.Guild.GetChannel(Bot.BotSettings.FleetLogChannel)
                        .SendMessageAsync($"Пользователь **{e.User.Username}#{e.User.Discriminator}** ({e.User.Id}) " +
                        $"подключился к каналу **{e.After.Channel.Name}** ({e.After.Channel.Id})");
                }
            }
        }

        [AsyncListener(EventTypes.VoiceStateUpdated)]
        public static async Task FleetDeleteOnVoiceStateUpdated(VoiceStateUpdateEventArgs e)
        {
            //Проверка на пустые рейды
            if (e.Before != null && e.Before.Channel != null)
            {
                var leftChannel = e.Before.Channel;

                //Пользователь вышел из автоматически созданных каналов рейда
                if (leftChannel.Parent.Name.StartsWith("Рейд") &&
                   leftChannel.ParentId != Bot.BotSettings.FleetCategory &&
                   !leftChannel.Users.Contains(e.User))
                {
                    //Проверка всех каналов рейда на присутствие в них игроков
                    var fleetIsEmpty = leftChannel.Parent.Children
                                            .Where(x => x.Type == ChannelType.Voice)
                                            .Where(x => x.Users.Count() > 0)
                                            .Count() == 0;

                    //Удаляем каналы и категорию
                    if (fleetIsEmpty)
                    {
                        await FleetLogging.LogFleetDeletionAsync(e.Guild, leftChannel.Parent);

                        foreach (var emptyChannel in leftChannel.Parent.Children)
                            await emptyChannel.DeleteAsync();
                        await leftChannel.Parent.DeleteAsync();
                    }
                }
            }
        }
    }
}
