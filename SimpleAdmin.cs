using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.Data;

namespace SimpleAdmin;

[MinimumApiVersion(132)]
public class SimpleAdmin : BasePlugin
{
    public override string ModuleName => "SimpleAdmin";
    public override string ModuleVersion => "0.1.1";

    private string? connectionString;


    private bool InitDatabase()
    {
        using var db = new SqliteConnection(connectionString);
        db.Open();
        using SqliteCommand command = new();
        command.Connection = db;
        command.CommandText = "CREATE TABLE IF NOT EXISTS banned_users (" +
                              "steam_id TEXT PRIMARY KEY, " +
                              "username TEXT, " + 
                              "minutes_banned INT, " +
                              "timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP)";
        command.ExecuteNonQuery();

        command.CommandText = "PRAGMA table_info(banned_users);";
        string[,] expectedSchema = new string[4, 2]
        {
            { "steam_id", "TEXT" },
            { "username", "TEXT" },
            { "minutes_banned", "INT" },
            { "timestamp", "TIMESTAMP" }
        };

        using SqliteDataReader reader = command.ExecuteReader();
        int i = 0;
        while (reader.Read())
        {
            if (expectedSchema[i, 0] != reader["name"].ToString() || expectedSchema[i, 1] != reader["type"].ToString()) return false;
            i++;
        }
        if (i != expectedSchema.GetLength(0))
        {
            return false;
        }


        Logger.LogInformation("Database initialized successfully.");
        return true;
    }

    public static bool IsValidPlayer(CCSPlayerController? player, bool can_be_bot = false)
    {
        return player != null && player.IsValid && (!player.IsBot || can_be_bot);
    }

    [RequiresPermissions("@css/ban")]
    [CommandHelper(minArgs: 1, usage: "<target | steamID64> [minutes]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [ConsoleCommand("css_ban", "Ban a user")]
    public void OnCommandBan(CCSPlayerController _, CommandInfo command)
    {
        List<string> args = command.ArgString.Trim().Split(" ").ToList();
        // get ban length
        ulong minutes = 0;
        if (args.Count > 1)
        { 
            if (ulong.TryParse(args[1], out ulong parsedMinutes))
            { 
                minutes = parsedMinutes;
            } else
            {
                command.ReplyToCommand("Invalid ban length");
                command.ReplyToCommand($"[CSS] Expected usage: {command.GetArg(0)} <target | steamID64> [minutes]");
                return;
            }
        } 

        // handle banning user in the server (using target)
        var targetedUsers = new Target(command.GetArg(0)).GetTarget(command.CallingPlayer).Where(p => p is { IsBot: false });

        

        if (targetedUsers.Count() > 1)
        {
            command.ReplyToCommand($"Identifier {args[0]} targets more than one person"); 
            command.ReplyToCommand($"[CSS] Expected usage: {command.GetArg(0)} <target | steamID64> [minutes]");
            return;
        }
        if (targetedUsers.Count() == 1)
        {
            var userToBan = targetedUsers.First();
            if (BanUser(new(userToBan), minutes, command)) Server.ExecuteCommand($"kickid {userToBan.UserId}");
            return;
        }

        // handle banning user not in server (using SteamID)
        var targetString = args[0].TrimStart('#');
        if (targetString.Length == 17 && UInt64.TryParse(targetString, out UInt64 steamId))
        { 
            BanUser(new BannedUser { SteamID = steamId }, minutes, command);
            return;
        } else command.ReplyToCommand($"Couldn't find user by identifier {args[0]}");

        command.ReplyToCommand($"[CSS] Expected usage: {command.GetArg(0)} <target | steamID64>");
    }
    [RequiresPermissions("@css/slay")]
    [CommandHelper(minArgs: 1, usage: "<target>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [ConsoleCommand("css_slay", "Kill a user")]
    public void OnCommandSlay(CCSPlayerController _, CommandInfo command)
    {
        var target = command.GetArgTargetResult(1);
        if (target.Players.Count > 0)
        {
            target.Players.ForEach(player => player.PlayerPawn.Value?.CommitSuicide(true, true));
            return; 
        }
        command.ReplyToCommand($"Couldn't find user(s) by identifier {command.GetArg(1)}");
        command.ReplyToCommand($"[CSS] Expected usage: {command.GetArg(0)} <target>");
    }

    [RequiresPermissions("@css/unban")]
    [CommandHelper(minArgs: 1, usage: "<steam id | username>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [ConsoleCommand("css_unban", "Unban a user")]
    public void OnCommandUnban(CCSPlayerController _, CommandInfo command)
    {
        BannedUser? bannedUser = IsUserBanned(command);
        if (bannedUser != null)
        {
            UnbanUser(bannedUser); 
            return;
        }
        command.ReplyToCommand($"Couldn't identify banned user by identifier {command.GetArg(1)}");
        command.ReplyToCommand($"[CSS] Expected usage: {command.GetArg(0)} <steam_id | username>");
    } 

    [RequiresPermissions("@css/kick")]
    [CommandHelper(minArgs: 1, usage: "<target>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [ConsoleCommand("css_kick", "Kick a user")]
    public void OnCommandKick(CCSPlayerController _, CommandInfo command)
    {
        var target = command.GetArgTargetResult(1); 
        if (!target.Players.Any())
        { 
            command.ReplyToCommand($"Couldn't find user by identifier {command.GetArg(1)}");
        }
        else if (target.Players.Count > 1)
        {
            command.ReplyToCommand($"Identifier {command.GetArg(1)} targets more than one person");
        }
        else if (target.Players.Count == 1)
        { 
            CCSPlayerController? userToKick = target.Players.First();
            if (userToKick != null)
            {
                Server.ExecuteCommand($"kickid {userToKick.UserId}");
                return;
            }
        }
        command.ReplyToCommand($"[CSS] Expected usage: {command.GetArg(0)} <target>");

    }
    [ConsoleCommand("css_players", "Get a list of current players")]
    public void OnCommandPlayers(CCSPlayerController _, CommandInfo command)
    {
        foreach (var player in Utilities.GetPlayers())
        { 
            if (IsValidPlayer(player))
            {
                command.ReplyToCommand($"{player.UserId}: {player.PlayerName}");
            }
        }
    }
    public override void Load(bool hotReload) 
    {
        connectionString = $"Filename={Path.Join(ModuleDirectory, "bans.db")}";
        if (!InitDatabase())
        { 
            throw new Exception("Database schema is outdated. Delete your current database (SimpleAdmin/bans.db) or revert to SimpleAdmin v0.0.3.");
        }
        RegisterListener<Listeners.OnClientConnected>((slot) =>
        {
            CCSPlayerController newPlayer = Utilities.GetPlayerFromSlot(slot);
            BannedUser? bannedUser = IsUserBanned(newPlayer.SteamID);
            if (bannedUser != null)
            { 
                if (bannedUser.ServedTheirTime)
                {
                    UnbanUser(bannedUser);
                    return;
                }
                Server.ExecuteCommand($"kickid {newPlayer.UserId}");
                Logger.LogInformation("Banned user {username} tried to join", newPlayer.PlayerName ?? "[unnamed]");
            }
        });
    }
    private bool BanUser(BannedUser user, ulong minutes, CommandInfo command)
    {
        if (IsUserBanned(user.SteamID) != null)
        { 
            Logger.LogInformation("{username} is already banned.", user.PlayerName ?? "[unnamed]");
            command.ReplyToCommand($"{user.PlayerName ?? "[unnamed]"} is already banned.");
            return false; 
        }
        using var db = new SqliteConnection(connectionString);
        db.Open();

        var insertCommand = new SqliteCommand
        {
            Connection = db,
            CommandText = "INSERT INTO banned_users VALUES (@steam_id, @username, @minutes_banned, CURRENT_TIMESTAMP);"
        };
        insertCommand.Parameters.AddWithValue("@steam_id", user.SteamID);
        insertCommand.Parameters.AddWithValue("@username", user.PlayerName ?? (object)DBNull.Value);
        insertCommand.Parameters.AddWithValue("@minutes_banned", minutes);

        if (insertCommand.ExecuteNonQuery() != 1)
        {
            throw new Exception($"Failed to ban user {user.PlayerName} (Steam ID: {user.SteamID})");
        }
        string minuteInfo = minutes > 0 ? $" for {minutes} minutes" : "";
        minuteInfo = minutes == 1 ? " for 1 minute" : minuteInfo;
        Logger.LogInformation("{user} with Steam ID {steam_id} has been banned{optional_time_info}.", user.PlayerName ?? "[unnamed]", user.SteamID, minuteInfo);
        command.ReplyToCommand($"{user.PlayerName ?? "[unnamed]"} with Steam ID {user.SteamID} has been banned{minuteInfo}.");
        return true;
    }
    private void UnbanUser(BannedUser user)
    {
        using var db = new SqliteConnection(connectionString);
        db.Open();
        var deleteCommand = new SqliteCommand
        {
            Connection = db,
            CommandText = "DELETE FROM banned_users WHERE steam_id = @steam_id"
        };
        deleteCommand.Parameters.AddWithValue("@steam_id", user.SteamID);

        if (deleteCommand.ExecuteNonQuery() != 1)
        {
            throw new Exception($"Failed to unban user {user.PlayerName} with Steam ID {user.SteamID}");
        }
        Logger.LogInformation("User {player_name} with Steam ID {steam_id} has been unbanned.", user.PlayerName ?? "[unnamed]", user.SteamID );
    }
    private BannedUser? IsUserBanned(CommandInfo command)
    {
        var identifier = command.GetArg(1);
        BannedUser? bannedUser = null;
        if (UInt64.TryParse(identifier, out UInt64 steamId))
        {
            bannedUser = IsUserBanned(steamId);
        } 
        return bannedUser ?? IsUserBanned(identifier);
    }
    private BannedUser? IsUserBanned(string username)
    {
        using var db = new SqliteConnection(connectionString);
        db.Open();
        var selectCommand = new SqliteCommand("SELECT * from banned_users WHERE username like @username", db);
        selectCommand.Parameters.AddWithValue("@username", $"%{username}%");

        int numResults = 0;
        BannedUser? user = null;

        using (var reader = selectCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                if (numResults++ > 0)
                { 
                    return null;
                }
                user = new BannedUser { SteamID = UInt64.Parse(reader.GetString(0)), PlayerName = reader.GetString(1) };
                return user;
            }
        }
        return user;
    } 
    private BannedUser? IsUserBanned(UInt64 steamId)
    {
        using var db = new SqliteConnection(connectionString);
        db.Open();
        var selectCommand = new SqliteCommand("SELECT * from banned_users WHERE steam_id = @steam_id", db);
        selectCommand.Parameters.AddWithValue("@steam_id", steamId);

        using var reader = selectCommand.ExecuteReader();
        if (reader.Read())
        { 
            var bannedUser = new BannedUser { SteamID = UInt64.Parse(reader.GetString(0)), PlayerName = reader.IsDBNull(1) ? null : reader.GetString(1) };
            var minutesBanned = reader.GetInt64(2);
            if (minutesBanned > 0)
            {
                var timeRemaining = reader.GetDateTime(3).AddMinutes(minutesBanned) - DateTime.Now;
                if (timeRemaining < TimeSpan.Zero)
                {
                    bannedUser.ServedTheirTime = true;
                }
            }
            return bannedUser;
        }
        return null;
    } 
    class BannedUser
    {
        public UInt64 SteamID { get; set; }
        public string? PlayerName { get; set; }
        public bool ServedTheirTime { get; set;  }

        public BannedUser() { }
        public BannedUser(CCSPlayerController player) 
        {
            SteamID = player.SteamID;
            PlayerName = player.PlayerName;
            ServedTheirTime = false;
        }
    }
}
