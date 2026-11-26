using System;
using System.Collections.Generic;
using System.Linq;
using DiIiS_NA.Core.Helpers.Math;
using DiIiS_NA.GameServer.Core.Types.Math;
using DiIiS_NA.GameServer.GSSystem.ItemsSystem;
using DiIiS_NA.GameServer.MessageSystem;
using DiIiS_NA.GameServer.MessageSystem.Message.Definitions.Inventory;
using DiIiS_NA.LoginServer.AccountsSystem;
using DiIiS_NA.LoginServer.Battle;

namespace DiIiS_NA.GameServer.CommandManager;

[CommandGroup("item", "Spawns an item (with a name or type), rolled for your class.\nUsage: item [type <type>|<name>] [amount] [ancient] [rank <rank>]",
    Account.UserLevels.GM, inGameOnly: true)]
public class ItemCommand : CommandGroup
{
    [DefaultCommand(inGameOnly: true)]
    public string Spawn(string[] @params, BattleClient invokerClient)
    {
        if (invokerClient == null)
            return T("You cannot invoke this command from console.");

        if (invokerClient.InGameClient == null)
            return T("You can only invoke this command while in-game.");

        var player = invokerClient.InGameClient.Player;
        var name = "Dye_02";
        var amount = 1;


        if (@params == null)
            return Fallback();

        name = @params[0];

        if (!ItemGenerator.IsValidItem(name))
            return T("You need to specify a valid item name!");


        if (@params.Length == 1 || !int.TryParse(@params[1], out amount))
            amount = 1;

        if (amount > 100) amount = 100;

        // "ancient" anywhere after the name makes a level-70 legendary ancient; otherwise it isn't
        var ancient = @params.Skip(1).Any(p => p.Equals("ancient", StringComparison.OrdinalIgnoreCase));

        // "rank <rank>" sets a legendary gem's rank, which is 1 otherwise
        var rankAt = Array.FindIndex(@params, p => p.Equals("rank", StringComparison.OrdinalIgnoreCase));
        var rank = 1;
        if (rankAt > 0)
        {
            if (!name.StartsWith("Unique_Gem_", StringComparison.OrdinalIgnoreCase))
                return "Only legendary gems (Unique_Gem_...) have a rank.";
            if (rankAt + 1 >= @params.Length || !int.TryParse(@params[rankAt + 1], out rank) || rank < 1)
                return "Give the rank as a number, for example: item Unique_Gem_023_x1 rank 99";
        }

        for (var i = 0; i < amount; i++)
        {
            var position = new Vector3D(player.Position.X + (float)RandomHelper.NextDouble() * 20f,
                player.Position.Y + (float)RandomHelper.NextDouble() * 20f,
                player.Position.Z);

            var item = ItemGenerator.Cook(player, name, ancient ? 1 : 0);
            if (rankAt > 0)
            {
                item.Attributes[GameAttributes.Jewel_Rank] = rank;
                item.UpdateLegendaryGemAttributes();
            }
            item.EnterWorld(position);
        }

        return ancient || rankAt > 0
            ? T("Spawned {0} {1}{2}items with name: {3}", amount, ancient ? "ancient " : "",
                rankAt > 0 ? $"rank {rank} " : "", name)
            : T("Spawned {0} items with name: {1}", amount, name);
    }

    [Command("type", "Spawns random items of a given type.\nUsage: item type <type> [amount]")]
    public string Type(string[] @params, BattleClient invokerClient)
    {
        if (invokerClient == null)
            return T("You cannot invoke this command from console.");

        if (invokerClient.InGameClient == null)
            return T("You can only invoke this command while in-game.");

        var player = invokerClient.InGameClient.Player;
        var name = "Dye";
        var amount = 1;


        if (@params == null)
            return T("You need to specify a item type!");

        name = @params[0];

        var type = ItemGroup.FromString(name);

        if (type == null)
            return T("The type given is not a valid item type.");

        if (@params.Length == 1 || !int.TryParse(@params[1], out amount))
            amount = 1;

        if (amount > 100) amount = 100;

        for (var i = 0; i < amount; i++)
        {
            var position = new Vector3D(player.Position.X + (float)RandomHelper.NextDouble() * 20f,
                player.Position.Y + (float)RandomHelper.NextDouble() * 20f,
                player.Position.Z);

            var item = ItemGenerator.GenerateRandom(player, type);
            item.EnterWorld(position);
        }

        return T("Spawned {0} items with type: {1}", amount, name);
    }

    [Command("dropall", "Drops all items in Backpack.\nUsage: item dropall")]
    public string DropAll(string[] @params, BattleClient invokerClient)
    {
        if (invokerClient == null)
            return T("You cannot invoke this command from console.");

        if (invokerClient.InGameClient == null)
            return T("You can only invoke this command while in-game.");

        var player = invokerClient.InGameClient.Player;

        var bpItems = new List<Item>(player.Inventory.GetBackPackItems());


        foreach (var item in bpItems)
        {
            var msg = new InventoryDropItemMessage { ItemID = item.DynamicID(player) };
            player.Inventory.Consume(invokerClient.InGameClient, msg);
        }

        return T("Dropped {0} Items for you", bpItems.Count);
    }
}
