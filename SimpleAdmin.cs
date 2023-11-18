using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Data.Sqlite;
using System.Data.Common;
using System.Data;

namespace SimpleAdmin;
public class SimpleAdmin : BasePlugin
{
    public override string ModuleName => "Simple Admin";
    public override string ModuleVersion => "0.0.1";

    private string? connectionString;


    private void InitDatabase()
    {
        using (SqliteConnection db = new (connectionString))
        {
            db.Open();
            using SqliteCommand command = new();
            command.Connection = db;
            command.CommandText = "CREATE TABLE IF NOT EXISTS banned_users (" +
                                  "steam_id UNSIGNED BIG INT PRIMARY KEY, " +
                                  "username TEXT NOT NULL)";
            command.ExecuteNonQuery();
        }
        Server.PrintToConsole("Database initialized successfully.");
    }

    // Try to get user from command line argument <user_id | username>
    public CCSPlayerController? TryParseUser(CommandInfo command)
    {
        CCSPlayerController? user;
        if (Int32.TryParse(command.GetArg(1), out int userId))
        { 
            user = Utilities.GetPlayerFromUserid(userId);
            if (IsValidHuman(user)) return user;
        }
        user = TryGetPlayerFromName(command.GetArg(1));
        if (IsValidHuman(user)) return user;
        command.ReplyToCommand("Couldn't find this user.");
        command.ReplyToCommand($"[CSS] Expected usage: {command.GetArg(0)} <user_id | username>");
        return null;
    }

    public CCSPlayerController? TryGetPlayerFromName(string name)
    {
        var results = Utilities.GetPlayers().Where(player => player.PlayerName.ToLower().Contains(name.ToLower()));
        if (results.Count() == 1)
        {
            return results.ElementAt(0);
        }
        return null;
    }
    public bool IsValidHuman(CCSPlayerController? player)
    {
        return player != null && player.IsValid && !player.IsBot; 
    }



    [RequiresPermissions("@css/root")]
    [CommandHelper(minArgs: 1, usage: "<user_id>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [ConsoleCommand("css_ban", "Ban a user")]
    public void OnCommandBan(CCSPlayerController _, CommandInfo command)
    {
        CCSPlayerController? userToBan = TryParseUser(command);
        if (userToBan == null) return;
        BanUser(userToBan);
        Server.ExecuteCommand($"kickid {userToBan.UserId} You have been banned from this server."); 
    } 

    [RequiresPermissions("@css/root")]
    [CommandHelper(minArgs: 1, usage: "<steam id | username>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [ConsoleCommand("css_unban", "Unban a user")]
    public void OnCommandUnban(CCSPlayerController _, CommandInfo command)
    {
        BannedUser? bannedUser = IsUserBanned(command);
        if (bannedUser == null) return;
        UnbanUser(bannedUser); 
    } 

    [RequiresPermissions("@css/root")]
    [CommandHelper(minArgs: 1, usage: "<user_id>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [ConsoleCommand("css_kick", "Kick a user")]
    public void OnCommandKick(CCSPlayerController _, CommandInfo command)
    {
        CCSPlayerController? bannedUser = TryParseUser(command);
        if (bannedUser == null) return;
        Server.ExecuteCommand($"kickid {bannedUser.UserId}");

    }
    public override void Load(bool hotReload) 
    {
        connectionString = $"Filename={Path.Join(ModuleDirectory, "bans.db")}";
        InitDatabase();
        RegisterListener<Listeners.OnClientConnected>((slot) =>
        {
            CCSPlayerController newPlayer = Utilities.GetPlayerFromSlot(slot);
            if (IsUserBanned(newPlayer.UserId) != null)
            {
                Server.ExecuteCommand($"kickid {newPlayer.UserId}");
                Server.PrintToConsole($"Banned user {newPlayer.PlayerName} tried to join");
            }
        });
    }
    private void BanUser(CCSPlayerController user)
    {
        if (IsUserBanned(user.UserId) != null)
        { 
            Server.PrintToConsole($"{user.PlayerName} is already banned.");
            return; 
        }
        using (var db = new SqliteConnection(connectionString))
        {
            db.Open();

            var insertCommand = new SqliteCommand
            {
                Connection = db,
                CommandText = "INSERT INTO banned_users VALUES (@steam_id, @username);"
            };
            insertCommand.Parameters.AddWithValue("@steam_id", user.SteamID);
            insertCommand.Parameters.AddWithValue("@username", user.PlayerName);

            if (insertCommand.ExecuteNonQuery() != 1)
            {
                throw new Exception($"Failed to ban user {user.PlayerName} (Steam ID: {user.SteamID})");
            }
        }
        Server.PrintToConsole($"{user.PlayerName} with Steam ID {user.SteamID} has been banned.");
    }
    private void UnbanUser(BannedUser user)
    {
        using (var db = new SqliteConnection(connectionString))
        { 
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
        }
        Server.PrintToConsole($"User {user.PlayerName} with Steam ID {user.SteamID} has been unbanned.");
    }
    private BannedUser? IsUserBanned(CommandInfo command)
    {
        string identifier = command.GetArg(1);
        if (Int32.TryParse(identifier, out int userId))
        {
            return IsUserBanned(userId);
        }
        return IsUserBanned(identifier);
    }
    private BannedUser? IsUserBanned(string username)
    {
        using var db = new SqliteConnection(connectionString);
        db.Open();
        var selectCommand = new SqliteCommand("SELECT * from banned_users WHERE username like @username", db);
        selectCommand.Parameters.AddWithValue("@username", "%" + username + "%");

        int numResults = 0;
        BannedUser? user = null;

        using (var reader = selectCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                if (numResults++ > 0) return null;
                user = new BannedUser { SteamID = reader.GetInt64(0), PlayerName = reader.GetString(1) };
                Server.PrintToConsole("found: " + user.PlayerName + " " + user.SteamID);
                return user;
            }
        }
        return user;
    } 
    private BannedUser? IsUserBanned(long? steamId)
    {
        using var db = new SqliteConnection(connectionString);
        db.Open();
        var selectCommand = new SqliteCommand("SELECT * from banned_users WHERE steam_id = @steam_id", db);
        selectCommand.Parameters.AddWithValue("@steam_id", steamId);

        using var reader = selectCommand.ExecuteReader();
        if (reader.Read())
        {
            return new BannedUser { SteamID = reader.GetInt64(0), PlayerName = reader.GetString(1) };
        }
        return null;
    } 
    class BannedUser
    {
        public long SteamID { get; set; }
        public required string PlayerName { get; set; }

        public BannedUser() { }
        public BannedUser(CCSPlayerController player) 
        {
            SteamID = (long)player.SteamID;
            PlayerName = player.PlayerName;
        }
    }
}