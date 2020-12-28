﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;
using Bot_NetCore.Commands;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bot_NetCore.Listeners
{
    public static class Timers
    {
        private static DiscordClient Client;
        private static int RainbowColor = 0;

        [AsyncListener(EventTypes.Ready)]
        public static async Task RegisterTimers(DiscordClient client, ReadyEventArgs e)
        {
            Client = client;

            //Таймер, который каждую минуту проверяет все баны и удаляет истёкшие.
            var checkExpiredReports = new Timer(60000);
            checkExpiredReports.Elapsed += CheckExpiredReports;
            checkExpiredReports.AutoReset = true;
            checkExpiredReports.Enabled = true;

            //Таймер который каждую минуту проверяет истекшие сообщения в каналах
            var clearChannelMessages = new Timer(60000);
            clearChannelMessages.Elapsed += ClearChannelMessagesOnElapsed;
            clearChannelMessages.AutoReset = true;
            clearChannelMessages.Enabled = true;

            var clearVotes = new Timer(60000);
            clearVotes.Elapsed += ClearAndRepairVotesOnElapsed;
            clearVotes.AutoReset = true;
            clearVotes.Enabled = true;

            var deleteShips = new Timer(60000 * 10);
            deleteShips.Elapsed += DeleteShipsOnElapsed;
            deleteShips.AutoReset = true;
            deleteShips.Enabled = true;

            var clearSubscriptions = new Timer(60000);
            clearSubscriptions.Elapsed += ClearSubscriptionsOnElapsed;
            clearSubscriptions.AutoReset = true;
            clearSubscriptions.Enabled = true;

            var updateVoiceTimes = new Timer(60000 * 5);
            updateVoiceTimes.Elapsed += UpdateVoiceTimesOnElapsedAsync;
            updateVoiceTimes.AutoReset = true;
            updateVoiceTimes.Enabled = true;
            
            var sendMessagesOnExactTime = new Timer(60000);
            sendMessagesOnExactTime.Elapsed += SendMessagesOnExactTimeOnElapsed;
            sendMessagesOnExactTime.AutoReset = true;
            sendMessagesOnExactTime.Enabled = true;

            var checkExpiredTickets = new Timer(60000 * 30);
            updateVoiceTimes.Elapsed += CheckExpiredTicketsAsync;
            updateVoiceTimes.AutoReset = true;
            updateVoiceTimes.Enabled = true;

            var checkExpiredFleetPoll = new Timer(10000);
            checkExpiredFleetPoll.Elapsed += CheckExpiredFleetPoll;
            checkExpiredFleetPoll.AutoReset = true;
            checkExpiredFleetPoll.Enabled = true;
            
            var rainbowRole = new Timer(Bot.BotSettings.RainbowCooldown * 1000);
            rainbowRole.Elapsed += RainbowRoleOnElapsed;
            rainbowRole.AutoReset = true;
            rainbowRole.Enabled = true;

            await Task.CompletedTask;
        }

        private static async void RainbowRoleOnElapsed(object sender, ElapsedEventArgs e)
        {
            if (Bot.BotSettings.RainbowEnabled)
            {
                var role = Client.Guilds[Bot.BotSettings.Guild].GetRole(Bot.BotSettings.RainbowRole);
                var color = DiscordColor.Red;
                switch (RainbowColor)
                {
                    default:
                        RainbowColor = 1;
                        color = DiscordColor.Red;
                        break;
                    case 2:
                        color = DiscordColor.Orange;
                        break;
                    case 3:
                        color = DiscordColor.Yellow;
                        break;
                    case 4:
                        color = DiscordColor.Green;
                        break;
                    case 5:
                        color = DiscordColor.Cyan;
                        break;
                    case 6:
                        color = DiscordColor.Blue;
                        break;
                    case 7:
                        color = DiscordColor.Purple;
                        break;
                }

                await role.ModifyAsync(color: color);

                ++RainbowColor;
            }
        }

        private static async void SendMessagesOnExactTimeOnElapsed(object sender, ElapsedEventArgs e)
        {
            // send a new year message
            DateTime currentTime = DateTime.Now;
            if (currentTime.Month == 1 && currentTime.Day == 1 && currentTime.Hour == 0 && currentTime.Minute == 0)
                await Client.Guilds[Bot.BotSettings.Guild].GetChannel(435730405077811200).SendMessageAsync("**:christmas_tree: С Новым Годом, пираты! :christmas_tree:**");
        }

        private static async void ClearSubscriptionsOnElapsed(object sender, ElapsedEventArgs e)
        {
            for (int i = 0; i < Subscriber.Subscribers.Count; ++i)
            {
                var sub = Subscriber.Subscribers.Values.ToArray()[i];
                if (DateTime.Now > sub.SubscriptionEnd)
                {
                    try
                    {
                        var guild = Client.Guilds[Bot.BotSettings.Guild];
                        var member = await guild.GetMemberAsync(sub.Member);
                        if (member != null)
                        {
                            await member.SendMessageAsync("Ваша подписка истекла :cry:");
                        }

                        try
                        {
                            await DonatorCommands.DeletePrivateRoleAsync(guild, member.Id);
                        }
                        catch (Exceptions.NotFoundException) { }

                        Subscriber.Subscribers.Remove(sub.Member);
                        Subscriber.Save(Bot.BotSettings.SubscriberXML);
                    }
                    catch (Exception ex)
                    {
                        Client.Logger.LogError(BotLoggerEvents.Timers, ex, $"Возникла ошибка при очистке подписок.");
                    }
                }
            }
        }

        private static async void DeleteShipsOnElapsed(object sender, ElapsedEventArgs e)
        {
            for (int i = 0; i < ShipList.Ships.Count; ++i)
            {
                var ship = ShipList.Ships.Values.ToArray()[i];
                if ((DateTime.Now - ship.LastUsed).Days >= 3)
                {
                    var channel = Client.Guilds[Bot.BotSettings.Guild].GetChannel(ship.Channel);

                    ulong ownerId = 0;
                    foreach (var member in ship.Members.Values)
                        if (member.Type == MemberType.Owner)
                        {
                            ownerId = member.Id;
                            break;
                        }

                    DiscordMember owner = null;
                    try
                    {
                        owner = await Client.Guilds[Bot.BotSettings.Guild].GetMemberAsync(ownerId);
                        await owner.SendMessageAsync(
                            "Ваш приватный корабль был неактивен долгое время и поэтому он был удалён. \n**Пожалуйста, не отправляйте новый запрос на создание, если" +
                            " не планируете пользоваться этой функцией**");
                    }
                    catch (NotFoundException)
                    {
                        // ничего не делаем, владелец покинул сервер
                    }

                    ship.Delete();
                    ShipList.SaveToXML(Bot.BotSettings.ShipXML);

                    await channel.DeleteAsync();

                    var doc = XDocument.Load("data/actions.xml");
                    foreach (var action in doc.Element("actions").Elements("action"))
                        if (Convert.ToUInt64(action.Value) == ownerId)
                            action.Remove();
                    doc.Save("data/actions.xml");

                    await Client.Guilds[Bot.BotSettings.Guild].GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                        "**Удаление корабля**\n\n" +
                        $"**Модератор:** {Client.CurrentUser}\n" +
                        $"**Корабль:** {ship.Name}\n" +
                        $"**Владелец:** {owner}\n" +
                        $"**Дата:** {DateTime.Now}");
                }
            }
        }

        private static async void ClearAndRepairVotesOnElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                var channelMessages = await Client.Guilds[Bot.BotSettings.Guild].GetChannel(Bot.BotSettings.VotesChannel)
                            .GetMessagesAsync();
                for (int i = 0; i < Vote.Votes.Count; ++i)
                {
                    var vote = Vote.Votes.Values.ToArray()[i];
                    try
                    {
                        var message = channelMessages.FirstOrDefault(x => x.Id == vote.Message);
                        if (message != null)
                        {
                            if (DateTime.Now >= vote.End && (DateTime.Now - vote.End).Days < 10) // выключение голосования
                            {
                                if (message.Reactions.Count == 0) continue;

                                var author = await Client.Guilds[Bot.BotSettings.Guild].GetMemberAsync(vote.Author);
                                var embed = Utility.GenerateVoteEmbed(
                                    author,
                                    vote.Yes > vote.No ? DiscordColor.Green : DiscordColor.Red,
                                    vote.Topic, vote.End,
                                    vote.Voters.Count,
                                    vote.Yes,
                                    vote.No,
                                    vote.Id);

                                await message.ModifyAsync(embed: embed);
                                await message.DeleteAllReactionsAsync();
                            }
                            else if (DateTime.Now >= vote.End && (DateTime.Now - vote.End).Days >= 3 && !message.Pinned) // архивирование голосования
                            {
                                var author = await Client.Guilds[Bot.BotSettings.Guild].GetMemberAsync(vote.Author);
                                var embed = Utility.GenerateVoteEmbed(
                                    author,
                                    vote.Yes > vote.No ? DiscordColor.Green : DiscordColor.Red,
                                    vote.Topic, vote.End,
                                    vote.Voters.Count,
                                    vote.Yes,
                                    vote.No,
                                    vote.Id);

                                var doc = new XDocument();
                                var root = new XElement("Voters");
                                foreach (var voter in vote.Voters)
                                    root.Add(new XElement("Voter", voter));
                                doc.Add(root);
                                doc.Save($"generated/voters-{vote.Id}.xml");

                                var channel = Client.Guilds[Bot.BotSettings.Guild].GetChannel(Bot.BotSettings.VotesArchive);
                                await channel.SendFileAsync($"generated/voters-{vote.Id}.xml", embed: embed);

                                await message.DeleteAsync();
                            }
                            else if (DateTime.Now < vote.End) // починка голосования
                            {
                                if (message.Reactions.Count < 2)
                                {
                                    await message.DeleteAllReactionsAsync();
                                    await message.CreateReactionAsync(DiscordEmoji.FromName(Client, ":white_check_mark:"));
                                    await Task.Delay(400);
                                    await message.CreateReactionAsync(DiscordEmoji.FromName(Client, ":no_entry:"));
                                }
                            }
                        }
                    }
                    catch (ArgumentNullException)
                    {
                        //Do nothing, message not found
                    }
                }
            }
            catch (Exception ex)
            {
                Client.Logger.LogError(BotLoggerEvents.Timers, ex, $"Возникла ошибка при очистке голосований.");
            }
        }


        /// <summary>
        ///     Очистка сообщений из каналов
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void ClearChannelMessagesOnElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                var guild = Client.Guilds[Bot.BotSettings.Guild];

                var channels = new Dictionary<DiscordChannel, TimeSpan>
                {
                    { guild.GetChannel(Bot.BotSettings.FindChannel), new TimeSpan(0, 30, 0) },           //30 минут для канала поиска
                    { guild.GetChannel(Bot.BotSettings.FleetCreationChannel), new TimeSpan(24, 0, 0) }   //24 часа для канала создания рейда
                };

                foreach (var channel in channels)
                {
                    try
                    {
                        var messages = await channel.Key.GetMessagesAsync();
                        var toDelete = messages.ToList()
                            .Where(x => !x.Pinned).ToList()                                             //Не закрепленные сообщения
                            .Where(x =>
                            {
                                if (x.IsEdited && x.Embeds.Count() != 0 &&
                                    x.Embeds.FirstOrDefault().Footer.Text.Contains("заполнен"))
                                    return DateTimeOffset.Now.Subtract(x.EditedTimestamp.Value.Add(new TimeSpan(0, 5, 0))).TotalSeconds > 0;
                                else
                                    return DateTimeOffset.Now.Subtract(x.CreationTimestamp.Add(channel.Value)).TotalSeconds > 0;
                            });     //Опубликованные ранее определенного времени

                        //Clear FindChannelInvites from deleted messages
                        foreach (var message in toDelete)
                            if (VoiceListener.FindChannelInvites.ContainsValue(message.Id))
                            {
                                VoiceListener.FindChannelInvites.Remove(VoiceListener.FindChannelInvites.FirstOrDefault(x => x.Value == message.Id).Key);
                                await VoiceListener.SaveFindChannelMessagesAsync();
                            }

                        if (toDelete.Count() > 0)
                        {
                            await channel.Key.DeleteMessagesAsync(toDelete);
                            Client.Logger.LogInformation(BotLoggerEvents.Timers, $"Канал {channel.Key.Name} был очищен.");
                        }

                    }
                    catch (Exception ex)
                    {
                        Client.Logger.LogWarning(BotLoggerEvents.Timers, ex, $"Ошибка при удалении сообщений в {channel.Key.Name}.", DateTime.Now);
                    }
                }
            }
            catch (Exception ex)
            {
                Client.Logger.LogError(BotLoggerEvents.Timers, ex, $"Ошибка при удалении сообщений в каналах.");
            }
        }

        private static async void CheckExpiredReports(object sender, ElapsedEventArgs e)
        {
            var guild = await Client.GetGuildAsync(Bot.BotSettings.Guild);

            //Check for expired bans
            var toUnban = BanSQL.GetExpiredBans();

            if (toUnban.Any())
            {
                var bans = await guild.GetBansAsync();
                foreach (var ban in toUnban)
                {
                    for (int i = 0; i < bans.Count; ++i)
                    {
                        if (bans[i].User.Id == ban.User)
                        {
                            await guild.UnbanMemberAsync(ban.User);
                            var user = await Client.GetUserAsync(ban.User);
                            await guild.GetChannel(Bot.BotSettings.ModlogChannel).SendMessageAsync(
                                "**Снятие бана**\n\n" +
                                $"**Модератор:** {Client.CurrentUser.Username}\n" +
                                $"**Пользователь:** {user}\n" +
                                $"**Дата:** {DateTime.Now}\n");

                            Client.Logger.LogInformation(BotLoggerEvents.Timers, $"Пользователь {user} был разбанен.");
                            break;
                        }
                    }
                }
            }

            //Check for expired mutes
            var reports = ReportSQL.GetExpiredReports();
            foreach (var report in reports)
            {
                if (report.ReportType == ReportType.Mute)
                {
                    try
                    {
                        var user = await guild.GetMemberAsync(report.User);
                        await user.RevokeRoleAsync(guild.GetRole(Bot.BotSettings.MuteRole), "Unmuted");
                        ReportSQL.Delete(report.Id);
                    }
                    catch (NotFoundException)
                    {
                        //Пользователь не найден
                    }
                }
                else if (report.ReportType == ReportType.VoiceMute)
                {
                    try
                    {
                        var user = await guild.GetMemberAsync(report.User);
                        await user.RevokeRoleAsync(guild.GetRole(Bot.BotSettings.VoiceMuteRole), "Unmuted");
                        ReportSQL.Delete(report.Id);
                    }
                    catch (NotFoundException)
                    {
                        //Пользователь не найден
                    }
                }
            }
        }


        /// <summary>
        ///     Обновление времени в голосовых каналах
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void UpdateVoiceTimesOnElapsedAsync(object sender, ElapsedEventArgs e)
        {
            try
            {
                foreach (var entry in VoiceListener.VoiceTimeCounters)
                {
                    try
                    {
                        var time = DateTime.Now - entry.Value;
                        VoiceTimeSQL.AddForUser(entry.Key, time);
                    }
                    catch (Exception ex)
                    {
                        Client.Logger.LogError(BotLoggerEvents.Timers, ex, $"Ошибка при обновлении времени активности пользователя ({entry.Key}).");
                    }
                }

                //Clear old values
                VoiceListener.VoiceTimeCounters.Clear();
                var guild = Client.Guilds[Bot.BotSettings.Guild];
                foreach (var entry in guild.VoiceStates.Where(x => x.Value.Channel != null && x.Value.Channel.Id != guild.AfkChannel.Id && x.Value.Channel.Id != Bot.BotSettings.WaitingRoom).ToList())
                {
                    if (!VoiceListener.VoiceTimeCounters.ContainsKey(entry.Key))
                        VoiceListener.VoiceTimeCounters.Add(entry.Key, DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                Client.Logger.LogError(BotLoggerEvents.Timers, ex, $"Ошибка при обновлении времени активности пользователей.");
            }
        }

        /// <summary>
        ///     Удаление старых тикетов.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void CheckExpiredTicketsAsync(object sender, ElapsedEventArgs e)
        {
            var expiredTickets = TicketSQL.GetClosedFor(TimeSpan.FromDays(2));

            var guild = Client.Guilds[Bot.BotSettings.Guild];

            foreach (var ticket in expiredTickets)
            {
                ticket.Status = TicketSQL.TicketStatus.Deleted;
                try
                {
                    await guild.GetChannel(ticket.ChannelId).DeleteAsync();
                }
                catch (NotFoundException) { }
            }
        }

        /// <summary>
        ///     Удаление старых тикетов.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void CheckExpiredFleetPoll(object sender, ElapsedEventArgs e)
        {
            try
            {
                var message = await Client.Guilds[Bot.BotSettings.Guild].GetChannel(Bot.BotSettings.FleetCreationChannel)
                    .GetMessageAsync(Bot.BotSettings.FleetVotingMessage);

                if (message.Embeds.Count > 0)
                {
                    var embed = new DiscordEmbedBuilder(message.Embeds.FirstOrDefault());
                    var date = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 8, 0, 0);

                    if (date - embed.Timestamp > TimeSpan.FromSeconds(1))
                    {
                        if (embed.Fields.Count < 3)
                        {
                            embed.AddFieldOrEmpty("\u200B\nРезультаты:", "");
                        }
                        embed.Fields[2].Value = "\u200B";
                        foreach (var reaction in message.Reactions)
                        {
                            if (reaction.Emoji.GetDiscordName() != ":black_small_square:")
                                embed.Fields[2].Value += $"{reaction.Emoji} - **{reaction.Count - 1}**; ";
                        }

                        embed.WithTimestamp(DateTime.Now);

                        await message.ModifyAsync(embed: embed.Build());

                        await message.DeleteAllReactionsAsync();

                        var emojis = new DiscordEmoji[]
                        {
                            DiscordEmoji.FromName(Client, ":one:"),
                            DiscordEmoji.FromName(Client, ":two:"),
                            DiscordEmoji.FromName(Client, ":three:"),
                            DiscordEmoji.FromName(Client, ":black_small_square:"),
                            DiscordEmoji.FromGuildEmote(Client, Bot.BotSettings.BrigEmoji),
                            DiscordEmoji.FromGuildEmote(Client, Bot.BotSettings.GalleonEmoji)
                        };

                        foreach (var emoji in emojis)
                        {
                            await Task.Delay(400);
                            await message.CreateReactionAsync(emoji);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Client.Logger.LogError(BotLoggerEvents.Timers, ex, $"Ошибка при обновлении голосования рейдов.");
            }
        }
    }
}
