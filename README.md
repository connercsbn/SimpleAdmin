# SimpleAdmin

This is a very basic ban/unban/kick plugin for [CounterStrikeSharp](https://docs.cssharp.dev/) that uses SQLite.

Just install [package](https://github.com/connercsbn/SimpleAdmin/releases/) to `game/csgo/addons/counterstrikesharp/plugins/` and you should be good to go. 

## css_players
`Usage: css_players`

Running `css_players` will print out a list of active players 
in the form of `UserID: PlayerName`

For example:

> 1: ben
>
> 2: Wynnie

## css_ban
`Usage: css_ban <user_id | username | steam_id>`



There are three ways to identify a new user to ban:
#### user_id
Run `css_ban <user_id>` to ban a user by user ID. You can use css_players to get the user's ID.

#### username
Run `css_ban <username>` to ban an active player with the given username<sup>1</sup>.

#### steam_id
Run `css_ban <steam_id>`, where `steam_id` is a decimal steamID64. This is how you can ban someone who isn't connected to the server.

## css_unban
`Usage: css_unban <steam_id | username>`

Unban a user identified either by steam_id or username<sup>1</sup>.

## css_kick
`Usage: css_kick <user_id | username>`

Kick a user identified either by user_id or username<sup>1</sup>. You can use css_players to get the user's ID.


## Required Permissions

Permissions using CounterStrikeSharp's [admin framework](https://docs.cssharp.dev/features/admin-framework/)

| Command      | Permission   |
| ------------ | ------------ |
| `css_ban`    | @css/ban     |
| `css_unban`  | @css/unban   |
| `css_kick`   | @css/kick    |


- - - 
<sup>1</sup> Username matches are case-insensitive and can be partial, but if the given username is not unique to one user, the action won't continue.
