using System.Collections.Generic;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.GameServer.GSSystem.TickerSystem;
using DiIiS_NA.LoginServer;

namespace DiIiS_NA.GameServer.GSSystem.PowerSystem.Implementations.General
{
    //30211   class DrinkHealthPotion
    [ImplementsPowerSNO(30211)]
    public class DrinkHealthPotion : PowerScript
    {
        public override IEnumerable<TickTimer> Run()
        {
            if (User is not Player player) yield break;
            player.AddPercentageHP(GameServerConfig.Instance.HealthPotionRestorePercentage);
            if (!GameServerConfig.Instance.HealthPotionConsumable)
            {
                AddBuff(player, player, new CooldownBuff(30211, TickTimer.WaitSeconds(player.World.Game, GameServerConfig.Instance.HealthPotionCooldown)));
            }

            // Season 28 – Altar of Rites Potion Powers (timed buff; reverses cleanly on expiry)
            // Remove any existing instance first so re-drinking while the buff is active
            // correctly removes the old bonuses and re-applies with the current mask.
            if (player.AltarPotionPowerMask != 0)
            {
                player.World.BuffManager.RemoveBuffs(player, typeof(Season28PotionPowerBuff));
                AddBuff(player, player, new Season28PotionPowerBuff(player.AltarPotionPowerMask));
            }
        }
    }
}

