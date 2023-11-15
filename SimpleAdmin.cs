using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Data.Sqlite;

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

    public CCSPlayerController? TryParseUser(CommandInfo command)
    { 
        if (!Int32.TryParse(command.GetArg(1), out int userId))
        {
            command.ReplyToCommand("Couldn't find user by that id.");
            command.ReplyToCommand($"Usage: {command.GetArg(0)} <user_id>");
            return null;
        };
        return Utilities.GetPlayerFromUserid(userId);
    }



    [RequiresPermissions("@css/root")]
    [CommandHelper(minArgs: 1, usage: "[user_id]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [ConsoleCommand("css_ban", "Ban a user")]
    public void OnCommandBan(CCSPlayerController? player, CommandInfo command)
    {
        CCSPlayerController? userToBan = TryParseUser(command);
        if (userToBan == null) return;
        BanUser(userToBan);
        Server.ExecuteCommand($"kickid {userToBan.UserId}"); 
    } 

    [RequiresPermissions("@css/root")]
    [CommandHelper(minArgs: 1, usage: "[user_id]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [ConsoleCommand("css_unban", "Unban a user")]
    public void OnCommandUnban(CCSPlayerController? player, CommandInfo command)
    {
        CCSPlayerController? bannedUser = TryParseUser(command);
        if (bannedUser == null) return;
        UnbanUser(bannedUser); 
    } 

    [RequiresPermissions("@css/root")]
    [CommandHelper(minArgs: 1, usage: "[user_id]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [ConsoleCommand("css_kick", "Kick a user")]
    public void OnCommandKick(CCSPlayerController? player, CommandInfo command)
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
            if (IsUserBanned(newPlayer))
            {
                Server.ExecuteCommand($"kickid {newPlayer.UserId}");
                Server.PrintToConsole($"Banned user {newPlayer.PlayerName} tried to join");
            }
        });
    }
    private void BanUser(CCSPlayerController user)
    {
        if (IsUserBanned(user))
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
        Server.PrintToConsole($"{user.PlayerName} has been banned.");
    }
    private void UnbanUser(CCSPlayerController user)
    {
        if (!IsUserBanned(user))
        {
            Server.PrintToConsole($"{user.PlayerName} is not currently banned.");
        }
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
                throw new Exception($"Failed to unban user {user.PlayerName} (Steam ID: {user.SteamID})");
            }
        }
        Server.PrintToConsole($"{user.PlayerName} has been unbanned.");
    }
    private bool IsUserBanned(CCSPlayerController user)
    {
        using (var db = new SqliteConnection(connectionString))
        {
            db.Open();
            var selectCommand = new SqliteCommand("SELECT steam_id from banned_users WHERE steam_id = @steam_id", db);
            selectCommand.Parameters.AddWithValue("@steam_id", user.SteamID);

            return selectCommand.ExecuteScalar() != null;
        } 
    } 
}