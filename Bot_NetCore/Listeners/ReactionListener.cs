﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Bot_NetCore.Entities;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bot_NetCore.Listeners
{
    public static class ReactionListener
    {
        /// <summary>
        ///     Словарь, содержащий в качестве ключа пользователя Discord, а в качестве значения - время истечения кулдауна.
        /// </summary>
        public static Dictionary<DiscordUser, DateTime> EmojiCooldowns = new Dictionary<DiscordUser, DateTime>();

        [AsyncListener(EventTypes.MessageReactionRemoved)]
        public static async Task ClientOnMessageReactionRemoved(DiscordClient client, MessageReactionRemoveEventArgs e)
        {
            var discordUser = await client.GetUserAsync(e.User.Id);
            if (discordUser.IsBot) return;

            //Проверка если сообщение с принятием правил
            if (e.Message.Id == Bot.BotSettings.CodexMessageId)
            {
                //При надобности добавить кулдаун
                //if (EmojiCooldowns.ContainsKey(e.User)) // проверка на кулдаун
                //    if ((EmojiCooldowns[e.User] - DateTime.Now).Seconds > 0) return;

                //// если проверка успешно пройдена, добавим пользователя
                //// в словарь кулдаунов
                //EmojiCooldowns[e.User] = DateTime.Now.AddSeconds(Bot.BotSettings.FastCooldown);

                //Забираем роль
                var member = await e.Guild.GetMemberAsync(discordUser.Id);
                if (member.Roles.Any(x => x.Id == Bot.BotSettings.CodexRole))
                    await member.RevokeRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.CodexRole));

                return;
            }

            //Проверка если сообщение с принятием правил рейда
            if (e.Message.Id == Bot.BotSettings.FleetCodexMessageId)
            {
                //Забираем роль
                var member = await e.Guild.GetMemberAsync(discordUser.Id);
                if (member.Roles.Any(x => x.Id == Bot.BotSettings.FleetCodexRole))
                    await member.RevokeRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.FleetCodexRole));

                return;
            }

            //Emissary Message
            if (e.Message.Id == Bot.BotSettings.EmissaryMessageId) return;
        }

        [AsyncListener(EventTypes.MessageReactionAdded)]
        public static async Task ClientOnMessageReactionAdded(DiscordClient client, MessageReactionAddEventArgs e)
        {
            var discordUser = await client.GetUserAsync(e.User.Id);
            if (discordUser.IsBot) return;

            //Проверка если сообщение с принятием правил сообщества
            if (e.Message.Id == Bot.BotSettings.CodexMessageId && e.Emoji.GetDiscordName() == ":white_check_mark:")
            {
                //При надобности добавить кулдаун
                /*if (EmojiCooldowns.ContainsKey(e.User)) // проверка на кулдаун
                    if ((EmojiCooldowns[e.User] - DateTime.Now).Seconds > 0) return;

                // если проверка успешно пройдена, добавим пользователя
                // в словарь кулдаунов
                EmojiCooldowns[e.User] = DateTime.Now.AddSeconds(Bot.BotSettings.FastCooldown);*/

                //Проверка на purge
                var hasPurge = false;
                ReportSQL validPurge = null;
                foreach (var purge in ReportSQL.GetForUser(discordUser.Id, ReportType.CodexPurge))
                {
                    if (purge.ReportEnd > DateTime.Now)
                    {
                        validPurge = purge;
                        hasPurge = true;
                        break;
                    }
                }

                if (hasPurge)
                {
                    var moderator = await e.Channel.Guild.GetMemberAsync(validPurge.Moderator);
                    try
                    {
                        await (await e.Guild.GetMemberAsync(discordUser.Id)).SendMessageAsync(
                            "**Возможность принять правила заблокирована**\n" +
                            $"**Снятие через:** {Utility.FormatTimespan(DateTime.Now - validPurge.ReportEnd)}\n" +
                            $"**Модератор:** {moderator.Username}#{moderator.Discriminator}\n" +
                            $"**Причина:** {validPurge.Reason}\n");
                    }

                    catch (UnauthorizedException)
                    {
                        //user can block the bot
                    }
                    return;
                }

                //Выдаем роль правил
                var member = await e.Guild.GetMemberAsync(discordUser.Id);

                //Проверка времени входа на сервер.
                if (member.JoinedAt > DateTime.Now.AddMinutes(-10))
                {
                    try
                    {
                        await member.SendMessageAsync(
                            $"{Bot.BotSettings.ErrorEmoji} Для принятия правил вы должны находиться на сервере минимум " +
                            $"**{Utility.FormatTimespan(TimeSpan.FromMinutes(10))}**.");

                        await e.Message.DeleteReactionAsync(DiscordEmoji.FromName(client, ":white_check_mark:"), member);
                    }
                    catch (UnauthorizedException) { }
                    return;
                }

                if (!member.Roles.Contains(e.Channel.Guild.GetRole(Bot.BotSettings.CodexRole)))
                {
                    //Выдаем роль правил
                    await member.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.CodexRole));

                    //Убираем роль блокировки правил
                    await member.RevokeRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.PurgeCodexRole));

                    client.Logger.LogInformation(BotLoggerEvents.Event, $"Пользователь {discordUser.Username}#{discordUser.Discriminator} ({discordUser.Id}) подтвердил прочтение правил через реакцию.");
                }

                return;
            }

            //Проверка если сообщение с принятием правил рейда
            if (e.Message.Id == Bot.BotSettings.FleetCodexMessageId && e.Emoji.GetDiscordName() == ":white_check_mark:")
            {
                //Проверка на purge
                var hasPurge = false;
                ReportSQL validPurge = null;
                foreach (var purge in ReportSQL.GetForUser(discordUser.Id, ReportType.FleetPurge))
                {
                    if (purge.ReportEnd > DateTime.Now)
                    {
                        validPurge = purge;
                        hasPurge = true;
                        break;
                    }
                }

                if (hasPurge)
                {
                    var moderator = await e.Channel.Guild.GetMemberAsync(validPurge.Moderator);
                    try
                    {
                        await (await e.Guild.GetMemberAsync(discordUser.Id)).SendMessageAsync(
                            "**Возможность принять правила заблокирована**\n" +
                            $"**Снятие через:** {Utility.FormatTimespan(DateTime.Now - validPurge.ReportEnd)}\n" +
                            $"**Модератор:** {moderator.Username}#{moderator.Discriminator}\n" +
                            $"**Причина:** {validPurge.Reason}\n");
                    }

                    catch (UnauthorizedException)
                    {
                        //user can block the bot
                    }
                    return;
                } //Удаляем блокировку если истекла

                var member = await e.Guild.GetMemberAsync(discordUser.Id);

                //Проверка времени входа на сервер.
                if (member.JoinedAt > DateTime.Now.AddDays(-Bot.BotSettings.FleetDateOffset))
                {
                    await member.SendMessageAsync(
                        $"{Bot.BotSettings.ErrorEmoji} Для получения доступа к рейдам вы должны находиться на сервере " +
                        $"**{Utility.FormatTimespan(TimeSpan.FromDays(Bot.BotSettings.FleetDateOffset))}**.");

                    await e.Message.DeleteReactionAsync(DiscordEmoji.FromName(client, ":white_check_mark:"), member);
                    return;
                }

                var voiceTime = VoiceListener.GetUpdatedVoiceTime(e.User.Id);
                //Проверка на время проведенное в голосовых каналах
                if (voiceTime < TimeSpan.FromHours(Bot.BotSettings.FleetVoiceTimeOffset))
                {
                    await member.SendMessageAsync(
                        $"{Bot.BotSettings.ErrorEmoji} Для получения доступа к рейдам вы должны провести " +
                        $"**{Utility.FormatTimespan(TimeSpan.FromHours(Bot.BotSettings.FleetVoiceTimeOffset))}** в голосовых каналах. " +
                        $"Ваше время: **{Utility.FormatTimespan(voiceTime)}**");

                    await e.Message.DeleteReactionAsync(DiscordEmoji.FromName(client, ":white_check_mark:"), member);
                    return;
                }

                //Проверка на регистрацию и привязку Xbox

                var webUser = WebUser.GetByDiscordId(member.Id);
                if (webUser == null)
                {
                    await member.SendMessageAsync(
                        $"{Bot.BotSettings.ErrorEmoji} Для получения доступа к рейдам вам нужно авторизоваться с помощью Discord на сайте {Bot.BotSettings.WebURL}login.");
                    await e.Message.DeleteReactionAsync(DiscordEmoji.FromName(client, ":white_check_mark:"), member);
                    return;
                }

                if (webUser.LastXbox == "")
                {
                    await member.SendMessageAsync($"{Bot.BotSettings.ErrorEmoji} Для получения доступа к рейдам вы должны привязать Xbox к своему аккаунту, затем перейдите по ссылке " +
                                                $"{Bot.BotSettings.WebURL}xbox - это обновит базу данных.");
                    await e.Message.DeleteReactionAsync(DiscordEmoji.FromName(client, ":white_check_mark:"), member);
                    return;
                }

                // Проверка ЧС

                if (BlacklistEntry.IsBlacklisted(member.Id) || BlacklistEntry.IsBlacklistedXbox(webUser.LastXbox))
                {
                    await member.SendMessageAsync(
                        $"{Bot.BotSettings.ErrorEmoji} Вы находитесь в чёрном списке рейдов и вам навсегда ограничен доступ к ним.");
                    return;
                }

                //Выдаем роль правил рейда
                if (!member.Roles.Any(x => x.Id == Bot.BotSettings.FleetCodexRole))
                {
                    await member.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.FleetCodexRole));

                    await e.Guild.GetChannel(Bot.BotSettings.FleetLogChannel)
                            .SendMessageAsync($"{DiscordEmoji.FromName(client, ":new:")} Пользователь **{e.User.Username}#{e.User.Discriminator}** ({e.User.Id}) получил роль рейда.");

                    client.Logger.LogInformation(BotLoggerEvents.Event, $"Пользователь {discordUser.Username}#{discordUser.Discriminator} ({discordUser.Id}) подтвердил прочтение правил рейда.");
                }

                return;
            }

            //Проверка на сообщение эмиссарства
            if (e.Message.Id == Bot.BotSettings.EmissaryMessageId)
            {
                await e.Message.DeleteReactionAsync(e.Emoji, discordUser);

                if (EmojiCooldowns.ContainsKey(discordUser)) // проверка на кулдаун
                    if ((EmojiCooldowns[discordUser] - DateTime.Now).Seconds > 0) return;

                // если проверка успешно пройдена, добавим пользователя
                // в словарь кулдаунов
                EmojiCooldowns[discordUser] = DateTime.Now.AddSeconds(Bot.BotSettings.FastCooldown);

                //Проверка у пользователя уже существующих ролей эмисарства и их удаление
                var member = await e.Guild.GetMemberAsync(discordUser.Id);
                member.Roles.Where(x => x.Id == Bot.BotSettings.EmissaryGoldhoadersRole ||
                                x.Id == Bot.BotSettings.EmissaryTradingCompanyRole ||
                                x.Id == Bot.BotSettings.EmissaryOrderOfSoulsRole ||
                                x.Id == Bot.BotSettings.EmissaryAthenaRole ||
                                x.Id == Bot.BotSettings.EmissaryReaperBonesRole ||
                                x.Id == Bot.BotSettings.HuntersRole ||
                                x.Id == Bot.BotSettings.ArenaRole).ToList()
                         .ForEach(async x => await member.RevokeRoleAsync(x) );

                //Выдаем роль в зависимости от реакции
                switch (e.Emoji.GetDiscordName())
                {
                    case ":moneybag:":
                        await member.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.EmissaryGoldhoadersRole));
                        break;
                    case ":pig:":
                        await member.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.EmissaryTradingCompanyRole));
                        break;
                    case ":skeleton:":
                        await member.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.EmissaryOrderOfSoulsRole));
                        break;
                    case ":gem:":
                        await member.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.EmissaryAthenaRole));
                        break;
                    case ":skull_and_crossbones:":
                        await member.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.EmissaryReaperBonesRole));
                        break;
                    case ":fish:":
                        await member.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.HuntersRole));
                        break;
                    case ":crossed_swords:":
                        await member.GrantRoleAsync(e.Channel.Guild.GetRole(Bot.BotSettings.ArenaRole));
                        break;
                    default:
                        break;
                }
                //Отправка в лог
                client.Logger.LogInformation(BotLoggerEvents.Event, $"{discordUser.Username}#{discordUser.Discriminator} получил новую роль эмиссарства.");

                return;
            }

            //Проверка на голосование
            if (e.Message.Channel.Id == Bot.BotSettings.VotesChannel)
            {
                var vote = Vote.GetByMessageId(e.Message.Id);

                await e.Message.DeleteReactionAsync(e.Emoji, discordUser);

                // Проверка на окончание голосования
                if (DateTime.Now > vote.End)
                {
                    return;
                }

                // Проверка на предыдущий голос
                if (vote.Voters.ContainsKey(discordUser.Id))
                {
                    return;
                }

                if (e.Emoji.GetDiscordName() == ":white_check_mark:")
                {
                    vote.Voters.Add(discordUser.Id, true);
                    ++vote.Yes;
                }
                else
                {
                    vote.Voters.Add(discordUser.Id, false);
                    ++vote.No;
                }

                var total = vote.Voters.Count;

                var author = await e.Guild.GetMemberAsync(vote.Author);
                var embed = Utility.GenerateVoteEmbed(
                    author,
                    DiscordColor.Yellow,
                    vote.Topic,
                    vote.End,
                    vote.Voters.Count,
                    vote.Yes,
                    vote.No,
                    vote.Id);

                Vote.Save(Bot.BotSettings.VotesXML);

                await e.Message.ModifyAsync(embed: embed);
                await (await e.Guild.GetMemberAsync(discordUser.Id)).SendMessageAsync($"{Bot.BotSettings.OkEmoji} Спасибо, ваш голос учтён!");
            }

            // Private ship confirmation message
            if (e.Channel.Id == Bot.BotSettings.PrivateRequestsChannel)
            {
                var ship = PrivateShip.GetByRequest(e.Message.Id);
                if (ship != null && ship.Channel == 0)
                {
                    if (e.Emoji == DiscordEmoji.FromName(client, ":white_check_mark:"))
                    {
                        var channel = await e.Guild.CreateChannelAsync($"☠{ship.Name}☠", ChannelType.Voice,
                            e.Guild.GetChannel(Bot.BotSettings.PrivateCategory), bitrate: Bot.BotSettings.Bitrate);
                        await channel.AddOverwriteAsync(e.Guild.GetRole(Bot.BotSettings.CodexRole),
                            Permissions.AccessChannels);
                        await channel.AddOverwriteAsync(e.Guild.EveryoneRole, Permissions.None, Permissions.UseVoice);

                        ship.Channel = channel.Id;
                        ship.CreatedAt = DateTime.Now;
                        ship.LastUsed = DateTime.Now;

                        var captain = (from member in ship.GetMembers()
                            where member.Role == PrivateShipMemberRole.Captain
                            select member).First();
                        var captainMember = await e.Guild.GetMemberAsync(captain.MemberId);
                        await channel.AddOverwriteAsync(captainMember, Permissions.UseVoice);
                        captain.Status = true;

                        await e.Channel.SendMessageAsync(
                            $"{Bot.BotSettings.OkEmoji} Администратор {e.User.Mention} подтвердил запрос на создание " +
                            $"корабля **{ship.Name}**.");
                        try
                        {
                            await captainMember.SendMessageAsync(
                                $"{Bot.BotSettings.OkEmoji} Администратор **{e.User.Username}#{e.User.Discriminator}** " +
                                $"подтвердил твой запрос на создание корабля **{ship.Name}**.");
                        }
                        catch (UnauthorizedException)
                        {
                            
                        }
                    }
                    else if (e.Emoji == DiscordEmoji.FromName(client, ":no_entry:"))
                    {
                        var captain = (from member in ship.GetMembers()
                            where member.Role == PrivateShipMemberRole.Captain
                            select member).First();
                        var captainMember = await e.Guild.GetMemberAsync(captain.MemberId);
                        
                        PrivateShip.Delete(ship.Name);
                        await e.Channel.SendMessageAsync(
                            $"{Bot.BotSettings.OkEmoji} Администратор {e.User.Mention} отклонил запрос на создание " +
                            $"корабля **{ship.Name}**.");
                        try
                        {
                            await captainMember.SendMessageAsync(
                                $"{Bot.BotSettings.ErrorEmoji} Администратор **{e.User.Username}#{e.User.Discriminator}** " +
                                $"отклонил твой запрос на создание корабля **{ship.Name}**.");
                        }
                        catch (UnauthorizedException)
                        {
                            
                        }
                    }

                    await e.Message.DeleteAllReactionsAsync();
                }
            }
        }
    }
}
