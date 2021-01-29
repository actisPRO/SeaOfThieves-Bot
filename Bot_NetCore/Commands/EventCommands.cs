﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot_NetCore.Attributes;
using Bot_NetCore.Listeners;
using Bot_NetCore.Misc;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity.Extensions;

namespace Bot_NetCore.Commands
{
    [Group("event")]
    [RequireGuild]
    [RequireCustomRole(RoleType.Helper)]
    [Description("Команды для ивента с скриншотами")]
    public class EventCommands : BaseCommandModule
    {
        [Command("cleanup")]
        [RequirePermissions(Permissions.Administrator)]
        [Description("Команда очистки скриншотов по реакциям. (Вводить в канале конкурса)")]
        public async Task EventCleanUp(CommandContext ctx)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();

            //Getting all messages from channel
            List<DiscordMessage> allMessages = new List<DiscordMessage>();
            var messages = await ctx.Channel.GetMessagesAsync();
            while (messages.Count() != 0)
            {
                allMessages.AddRange(messages);
                messages = await ctx.Channel.GetMessagesBeforeAsync(messages.Last().Id);
            }

            var deletedCount = 0;
            var approvedCount = 0;
            var i = 0;
            foreach (var message in allMessages)
            {
                if (message.Reactions.Count != 0)
                {
                    if (message.Reactions.FirstOrDefault().Emoji.GetDiscordName() == ":ok:")
                    {
                        approvedCount++;
                    }
                    else if (message.Reactions.FirstOrDefault().Emoji.GetDiscordName() == ":no_entry_sign:")
                    {
                        deletedCount++;
                        try
                        {
                            var member = await ctx.Guild.GetMemberAsync(message.Author.Id);
                            await member.SendMessageAsync($"**Ваш скриншот будет автоматически удалён из канала** <#{ctx.Channel.Id}>.\n" +
                                "**Причина:** несоответствие с требованиями конкурса. \n" +
                                "Внимательнее читайте условия в канале <#718099718369968199>.");
                        }
                        catch { }
                        try
                        {
                            await message.DeleteAsync();
                        }
                        catch { }
                        if (i % 5 == 0)
                            await Task.Delay(3000);
                        else
                            await Task.Delay(400);
                        i++;
                    }
                }
            }

            await ctx.RespondAsync($"Total messages in channel: {allMessages.Count} \n" +
                $"Total deleted messages:  {deletedCount}\n" +
                $"Total approved messages: {approvedCount}");
        }

        [Command("createreactions")]
        [RequirePermissions(Permissions.Administrator)]
        [Description("Создает реакции под сообщением (Вводить в канале конкурса)")]
        public async Task EventCreateReactions(CommandContext ctx, [RemainingText, Description("Реакции которые будут добавлены под каждым скриншотом (Через пробел)")] params DiscordEmoji[] emojis)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();

            List<DiscordMessage> allMessages = new List<DiscordMessage>();
            var messages = await ctx.Channel.GetMessagesAsync();
            while (messages.Count() != 0)
            {
                allMessages.AddRange(messages);
                messages = await ctx.Channel.GetMessagesBeforeAsync(messages.Last().Id);
            }

            foreach (var message in allMessages)
            {
                if (message.Attachments.Count != 0 || message.Embeds.Count != 0)
                {
                    await message.DeleteAllReactionsAsync();

                    foreach(var emoji in emojis)
                    {
                        await Task.Delay(400);
                        await message.CreateReactionAsync(emoji);
                    }

                    await Task.Delay(400);
                }
            }
        }

        [Command("top")]
        [Description("Отправляет в лс список топ 10 по голосам (Вводить в канале конкурса)")]
        public async Task EventTop(CommandContext ctx, int number = 10)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();

            List<DiscordMessage> allMessages = new List<DiscordMessage>();
            var messages = await ctx.Channel.GetMessagesAsync();
            while (messages.Count() != 0)
            {
                allMessages.AddRange(messages);
                messages = await ctx.Channel.GetMessagesBeforeAsync(messages.Last().Id);
            }

            var topMessages = allMessages.Where(x => x.Reactions.Count != 0).OrderByDescending(x => x.Reactions.FirstOrDefault().Count).Take(number);

            var top = new List<string>();
            foreach (var topMessage in topMessages)
            {
                var votes = topMessage.Reactions.FirstOrDefault().Count;
                top.Add($"Голосов: **{votes}** | https://discord.com/channels/{ctx.Guild.Id}/{ctx.Channel.Id}/{topMessage.Id}");
            }

            var members_pagination = Utility.GeneratePagesInEmbeds(top, $"**Топ {number} скриншотов**");

            var interactivity = ctx.Client.GetInteractivity();
            if (members_pagination.Count() > 1)
                await interactivity.SendPaginatedMessageAsync(await ctx.Member.CreateDmChannelAsync(), ctx.User, members_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
            else
                await ctx.Member.SendMessageAsync(embed: members_pagination.First().Embed);
        }

        [Command("check")]
        [Description("Отправляет в статистику по проголосовавшим \n" +
            "(Вводить в канале конкурса) \n" +
            "C - CreatedAt, J - JoinedAt, V - VoiceTime")]
        public async Task EventCheck(CommandContext ctx, DiscordMessage message, DiscordEmoji reaction)
        {
            await ctx.Message.DeleteAsync();
            await ctx.TriggerTypingAsync();

            var allReactions = new List<DiscordUser>();
            var reactions = await message.GetReactionsAsync(reaction);

            while (reactions.Count() != 0)
            {
                allReactions.AddRange(reactions);
                reactions = await message.GetReactionsAsync(reaction, after: reactions.Last().Id);
            }

            var allReactionsMembers = await Task.WhenAll(allReactions.Select(async x =>
            {
                try
                {
                    await Task.Delay(400);
                    return await ctx.Guild.GetMemberAsync(x.Id);
                } catch { }
                return null;
            }));

            var reactionsList = allReactionsMembers.OrderByDescending(x => x.CreationTimestamp).Select(x => $"C: **{x.CreationTimestamp:dd.MM.yyyy}** J: **{x.JoinedAt:dd.MM.yyyy}** V: **{VoiceListener.GetUpdatedVoiceTime(x.Id)}** \n" +
                                                                                                       $"{x.Username} ({x.Id})").ToList();

            var members_pagination = Utility.GeneratePagesInEmbeds(reactionsList, $"Список проголосовавших (По дате создания аккаунта).");

            var interactivity = ctx.Client.GetInteractivity();
            if (members_pagination.Count() > 1)
                await interactivity.SendPaginatedMessageAsync(await ctx.Member.CreateDmChannelAsync(), ctx.User, members_pagination, timeoutoverride: TimeSpan.FromMinutes(5));
            else
                await ctx.Member.SendMessageAsync(embed: members_pagination.First().Embed);
        }

        //Запихну сюда, так как это по любому временный код
        [AsyncListener(EventTypes.MessageReactionAdded)]
        public static async Task ClientOnEventReationAdded(DiscordClient client, MessageReactionAddEventArgs e)
        {
            //Check channel id in dev and main server
            if (e.Channel.Id == 803193543426441217 || e.Channel.Id == 801834857504178206)
            {
                var dayTime = new DateTime(2021, 01, 29, 18, 0, 0);

                var member = await e.Guild.GetMemberAsync(e.User.Id);

                if (member.JoinedAt > new DateTime(2021, 01, 29, 18, 0, 0) ||
                    new DateTime(2021, 01, 29).AddDays(-7) < member.CreationTimestamp)
                {
                    await e.Message.DeleteReactionAsync(e.Emoji, e.User);
                }
            }
        }
    }
}
