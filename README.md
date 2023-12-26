# SimpleAdmin

This is a basic admin plugin for [CounterStrikeSharp](https://docs.cssharp.dev/) that uses SQLite. 
It can do:
- `ban` (with optional time limit)
- `unban`
- `kick`
- `slay`	

## Installation
Just extract [package](https://github.com/connercsbn/SimpleAdmin/releases/) to `game/csgo/addons/counterstrikesharp/plugins/` and you should be good to go. 

*Requires CSS v87+*

## css_players
Usage: `css_players`

Running `css_players` will print out a list of active players in the form of `UserID: PlayerName`

For example:

> 1: ben
> 
> 2: TopHATTwaffle

## css_ban
Usage: `css_ban <target | SteamID64>`

Use `target` for banning people in the server. 
Use `SteamID64` to ban someone with that SteamID64 even if they're not in the server. 

## css_unban
Usage: `css_unban <SteamID64 | username>`

Unban a user identified either by SteamID64 or username. Username match is partial and case-insensitive. Command fails if multiple users are matched.

## css_kick
Usage: `css_kick <target>`

Kick a user by target. Fails if multiple users are targeted.

## css_slay
Usage: `css_slay <target>`

Kill a user or users by target.

##  Available \<target\> strings
 - @all
 - @bots
 - @human
 - @alive
 - @dead
 - @!me
 - @me
 - @ct
 - @t
 - @spec
 - #user_id
 - #steam_id
 - username (can be partial, case-insensitive)




## Required Permissions

Permissions using CounterStrikeSharp's [admin framework](https://docs.cssharp.dev/features/admin-framework/)

|       Command         |       Permission   |
| -------------------   | ------------------ |
| `css_ban`             | @css/ban           |
| `css_unban`           | @css/unban         |
| `css_kick`            | @css/kick          |
| `css_slay`            | @css/slay          |


