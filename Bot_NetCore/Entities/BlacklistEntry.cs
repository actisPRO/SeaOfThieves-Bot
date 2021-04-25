﻿using System;
using System.Collections.Generic;
using Bot_NetCore.Misc;
using MySql.Data.MySqlClient;

namespace Bot_NetCore.Entities
{
    public class BlacklistEntry
    {
        public readonly string Id;

        private ulong _discordId;
        private string _username;
        private string _xbox;
        private DateTime _banDate;
        private ulong _moderatorId;
        private string _reason;
        private string _additional;

        private BlacklistEntry(string id, ulong discordId, string username, string xbox, DateTime banDate,
            ulong moderatorId, string reason, string additional)
        {
            Id = id;
            _discordId = discordId;
            _username = username;
            _xbox = xbox;
            _banDate = banDate;
            _moderatorId = moderatorId;
            _reason = reason;
            _additional = additional;
        }

        public static BlacklistEntry Create(string id, ulong discordId, string username, string xbox, DateTime banDate,
            ulong moderatorId, string reason, string additional)
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand();
            var statement =
                "INSERT INTO blacklist(id, discord_id, discord_username, xbox, ban_date, moderator_id, reason, additional) " +
                "VALUES (@id, @discordId, @username, @xbox, @banDate, @moderatorId, @reason, @additional);";

            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@discordId", discordId);
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@xbox", xbox);
            cmd.Parameters.AddWithValue("@banDate", banDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@moderatorId", moderatorId);
            cmd.Parameters.AddWithValue("@reason", reason);
            cmd.Parameters.AddWithValue("@additional", additional);

            cmd.CommandText = statement;
            cmd.Connection = connection;
            cmd.Connection.Open();

            cmd.ExecuteNonQuery();

            return new BlacklistEntry(id, discordId, username, xbox, banDate, moderatorId, reason, additional);
        }

        public static List<BlacklistEntry> GetAll()
        {
            var result = new List<BlacklistEntry>();
            using (var connection = new MySqlConnection(Bot.ConnectionString))
            {
                using var cmd = new MySqlCommand();
                cmd.CommandText = "SELECT * FROM blacklist;";
                cmd.Connection = connection;
                cmd.Connection.Open();

                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new BlacklistEntry(reader.GetString("id"),
                        reader.GetUInt64("discord_id"),
                        reader.GetStringSafe("discord_username"),
                        reader.GetStringSafe("xbox"),
                        reader.GetDateTime("ban_date"),
                        reader.GetUInt64("moderator_id"),
                        reader.GetStringSafe("reason"),
                        reader.GetStringSafe("additional")));
                }
            }

            return result;
        }
        
        public static void Remove(string id)
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand();
            var statement = "DELETE FROM blacklist WHERE id = @id;";

            cmd.Parameters.AddWithValue("@id", id);

            cmd.CommandText = statement;
            cmd.Connection = connection;
            cmd.Connection.Open();

            cmd.ExecuteNonQuery();
        }

        public static bool IsBlacklisted(ulong user)
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand();
            cmd.CommandText = "SELECT * FROM blacklist WHERE discord_id = @user;";

            cmd.Parameters.AddWithValue("@user", user);

            cmd.Connection = connection;
            cmd.Connection.Open();

            var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return false;
            }

            return true;
        }

        public static bool IsBlacklistedXbox(string xbox)
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand();
            cmd.CommandText = "SELECT * FROM blacklist WHERE xbox = @xbox;";

            cmd.Parameters.AddWithValue("@xbox", xbox);

            cmd.Connection = connection;
            cmd.Connection.Open();

            var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return false;
            }

            return true;
        }

        public ulong DiscordId => _discordId;

        public string Username => _username;

        public string Xbox => _xbox;

        public DateTime BanDate => _banDate;

        public ulong ModeratorId => _moderatorId;

        public string Reason => _reason;

        public string Additional => _additional;
    }
}