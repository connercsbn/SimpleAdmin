using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Data.Sqlite;
using CounterStrikeSharp.API.Core.Attributes;

namespace SimpleAdmin;

[MinimumApiVersion(87)]
public class SimpleAdmin : BasePlugin
{
    public override string ModuleName => "SimpleAdmin";
    public override string ModuleVersion => "0.0.1";

    private string? connectionString;


    private void InitDatabase()
    {
        using var db = new SqliteConnection(connectionString);
        db.Open();
        using SqliteCommand command = new();
        command.Connection = db;
        command.CommandText = "CREATE TABLE IF NOT EXISTS banned_users (" +
                              "steam_id UNSIGNED BIG INT PRIMARY KEY, " +
                              "username TEXT)";
        command.ExecuteNonQuery();
        Server.PrintToConsole("Database initialized successfully.");
    }

    public static bool IsValidPlayer(CCSPlayerController? player, bool can_be_bot = false)
    {
        return player != null && player.IsValid && (!player.IsBot || can_be_bot);
    }

    [RequiresPermissions("@css/ban")]
    [CommandHelper(minArgs: 1, usage: "<target | steam id>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [ConsoleCommand("css_ban", "Ban a user")]
    public void OnCommandBan(CCSPlayerController _, CommandInfo command)
    {
        var targetedUsers = command.GetArgTargetResult(1);
        if (targetedUsers.Players.Count == 1)
        {
            var userToBan = targetedUsers.Players.First();
            BanUser(new(userToBan));
            Server.ExecuteCommand($"kickid {userToBan.UserId}");
            return;
        }
        var targetString = command.GetArg(1).TrimStart('#');
        if (targetString.Length == 17 && ulong.TryParse(targetString, out ulong steamId))
        { 
            BanUser(new BannedUser { SteamID = steamId });
        }
        command.ReplyToCommand($"Couldn't find user by identifier {command.GetArg(1)}");
        command.ReplyToCommand($"[CSS] Expected usage: {command.GetArg(0)} <target | steam_id>");
    }
    [RequiresPermissions("@css/slay")]
    [CommandHelper(minArgs: 1, usage: "<user_id | username>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
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
    [CommandHelper(minArgs: 1, usage: "<user_id | username>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [ConsoleCommand("css_kick", "Kick a user")]
    public void OnCommandKick(CCSPlayerController _, CommandInfo command)
    {
        var target = command.GetArgTargetResult(1);
        if (target.Players.Count == 0)
        { 
            CCSPlayerController? userToKick = target.Players.First();
            if (userToKick != null)
            {
                Server.ExecuteCommand($"kickid {userToKick.UserId}");
                return;
            }
        }
        command.ReplyToCommand($"Couldn't find user by identifier {command.GetArg(1)}");
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
        InitDatabase();
        RegisterListener<Listeners.OnClientConnected>((slot) =>
        {
            CCSPlayerController newPlayer = Utilities.GetPlayerFromSlot(slot);
            if (IsUserBanned(newPlayer.SteamID) != null)
            {
                Server.ExecuteCommand($"kickid {newPlayer.UserId}");
                Server.PrintToConsole($"Banned user {newPlayer.PlayerName} tried to join");
            }
        });
    }
    private void BanUser(BannedUser user)
    {
        if (IsUserBanned(user.SteamID) != null)
        { 
            Server.PrintToConsole($"{user.PlayerName} is already banned.");
            return; 
        }
        using var db = new SqliteConnection(connectionString);
        db.Open();

        var insertCommand = new SqliteCommand
        {
            Connection = db,
            CommandText = "INSERT INTO banned_users VALUES (@steam_id, @username);"
        };
        insertCommand.Parameters.AddWithValue("@steam_id", user.SteamID);
        insertCommand.Parameters.AddWithValue("@username", user.PlayerName ?? (object)DBNull.Value);

        if (insertCommand.ExecuteNonQuery() != 1)
        {
            throw new Exception($"Failed to ban user {user.PlayerName} (Steam ID: {user.SteamID})");
        }
        Server.PrintToConsole($"{user.PlayerName} with Steam ID {user.SteamID} has been banned.");
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
        Server.PrintToConsole($"User {user.PlayerName} with Steam ID {user.SteamID} has been unbanned.");
    }
    private BannedUser? IsUserBanned(CommandInfo command)
    {
        var identifier = command.GetArg(1);
        if (ulong.TryParse(identifier, out ulong steamId))
        {
            var player = IsUserBanned(steamId);
            if (player != null) return player;
        }
        return IsUserBanned(identifier);
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
                if (numResults++ > 0) return null;
                user = new BannedUser { SteamID = (ulong)reader.GetInt64(0), PlayerName = reader.GetString(1) };
                return user;
            }
        }
        return user;
    } 
    private BannedUser? IsUserBanned(ulong steamId)
    {
        using var db = new SqliteConnection(connectionString);
        db.Open();
        var selectCommand = new SqliteCommand("SELECT * from banned_users WHERE steam_id = @steam_id", db);
        selectCommand.Parameters.AddWithValue("@steam_id", steamId);

        using var reader = selectCommand.ExecuteReader();
        if (reader.Read())
        {
            return new BannedUser { SteamID = (ulong)reader.GetInt64(0), PlayerName = reader.IsDBNull(1) ? null : reader.GetString(1) };
        }
        return null;
    } 
    class BannedUser
    {
        public ulong SteamID { get; set; }
        public string? PlayerName { get; set; }

        public BannedUser() { }
        public BannedUser(CCSPlayerController player) 
        {
            SteamID = (ulong)player.SteamID;
            PlayerName = player.PlayerName;
        }
    }
}
