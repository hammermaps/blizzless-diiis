using System;
using System.Globalization;
using DiIiS_NA.LoginServer.AccountsSystem;
using DiIiS_NA.LoginServer.Battle;

namespace DiIiS_NA.GameServer.CommandManager;

/// <summary>
/// Live loot-rate control for testing. These are the same values the drop code reads
/// (<see cref="GameServerConfig.RateDrop"/> multiplies every item roll, RateChangeDrop the extra
/// roll normal monsters get), so a change applies to the next kill without restarting the server.
/// </summary>
[CommandGroup("loot", "Show or set the item drop rate multiplier", Account.UserLevels.Admin, inGameOnly: false)]
public class LootCommand : CommandGroup
{
    [DefaultCommand(Account.UserLevels.Admin, true)]
    public string Loot(string[] @params, BattleClient invokerClient)
    {
        var config = GameServerConfig.Instance;

        if (@params == null || @params.Length == 0 || @params[0].Equals("show", StringComparison.OrdinalIgnoreCase))
            return Format(config);

        if (!float.TryParse(@params[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) || rate <= 0f)
            return T("usage: {0}loot <rate> [changeRate] - e.g. {0}loot 10 3", CommandsConfig.Instance.CommandPrefix);

        config.RateDrop = rate;

        // The second value only affects the extra roll normal monsters get. Defaulting it to a
        // quarter of the drop rate keeps a one-number call useful for a quick test.
        var changeRate = @params.Length > 1 &&
                         float.TryParse(@params[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedChange)
            ? parsedChange
            : Math.Max(1f, rate / 4f);

        config.RateChangeDrop = changeRate;

        return T("Loot rates set: RateDrop={0}, RateChangeDrop={1}. Edit config.ini to keep them across restarts.",
            rate, changeRate);
    }

    [Command("show", "Shows the current loot rate multipliers.", Account.UserLevels.Admin)]
    public string Show(string[] @params, BattleClient invokerClient)
    {
        return Format(GameServerConfig.Instance);
    }

    private static string Format(GameServerConfig config)
    {
        return T("Loot rates: RateDrop={0}, RateChangeDrop={1}, ChanceHighQualityUnidentified={2}, ChanceNormalUnidentified={3}",
            config.RateDrop, config.RateChangeDrop, config.ChanceHighQualityUnidentified, config.ChanceNormalUnidentified);
    }
}
