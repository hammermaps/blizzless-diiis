using System.Reflection;
using System.Text;
using DiIiS_NA.Core.MPQ;
using DiIiS_NA.GameServer.GSSystem.ItemsSystem;
using DiIiS_NA.GameServer.GSSystem.ObjectsSystem;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.GameServer.MessageSystem;
using DiIiS_NA.GameServer.MessageSystem.Message.Fields;
using static DiIiS_NA.Core.MPQ.FileFormats.GameBalance;
using static EtchedSigilChecks;

static class LegendaryGemChecks
{
    const int DamagePower = 483319, AncientPower = 483320;

    public static void Tooltips(Action<string, Action> check)
    {
        check("2.8 gem metadata reads the rank-25 unlock after the two attribute specifiers", () =>
        {
            var definition = Definition();
            if (definition.JewelSecondaryEffectUnlockRank != 25) throw new Exception("Unlock rank was not read");
            if (definition.Attribute1[0].SNOParam != DamagePower || definition.Attribute1[1].SNOParam != AncientPower)
                throw new Exception("Wrong primary/secondary power mapping");
        });
        check("Urshi stops at the gem's item-data maximum rank", () =>
        {
            var definition = Definition();
            Set(definition, "<JewelMaxRank>k__BackingField", 99);
            if (!definition.AllowsJewelRank(98)) throw new Exception("Rank 98 was blocked below the cap");
            if (definition.AllowsJewelRank(99)) throw new Exception("Rank 99 was not capped by the item data");
            Set(definition, "<JewelMaxRank>k__BackingField", 0);
            if (!definition.AllowsJewelRank(999)) throw new Exception("Uncapped gem rank was rejected");
        });

        check("LoD tooltip scales by rank, unlocks at 25, and refreshes without accumulating", () =>
        {
            var gem = Gem();
            foreach (var rank in new[] { 0, 24, 25, 99, 99, 24, 0 })
            {
                gem.Attributes[GameAttributes.Jewel_Rank] = rank;
                // Reflection also lets this regression run against the pre-fix server binary.
                typeof(Item).GetMethod("UpdateLegendaryGemAttributes")?.Invoke(gem, null);
                Near(gem.Attributes[GameAttributes.Item_Power_Passive, DamagePower], .0375f * (rank + 1));
                Near(gem.Attributes[GameAttributes.Item_Power_Passive, AncientPower], rank >= 25 ? 1 : 0);
            }
        });
    }

    public static void Run(Action<string, Action> check)
    {
        Tooltips(check);
        check("LoD mixed gear and primal items use the rank-25 boundary, then reach 98.5x/52% at 99", () =>
        {
            var f = Equipped(24, 5, 2);
            Refresh(f.Player);
            Near(f.Player.LegacyOfDreamsDamage, 1 + 5 * .9375f);
            Near(f.Player.LegacyOfDreamsDamageTaken, .9f);
            f.Item.Gems[0].Attributes[GameAttributes.Jewel_Rank] = 25;
            Refresh(f.Player);
            Near(f.Player.LegacyOfDreamsDamage, 1 + 7 * .975f);
            Near(f.Player.LegacyOfDreamsDamageTaken, .86f);
            var max = Equipped(99, 13, 13);
            Refresh(max.Player);
            Near(max.Player.LegacyOfDreamsDamage, 98.5f);
            Near(max.Player.LegacyOfDreamsDamageTaken, .48f);
            max.Item.Gems.Clear();
            Refresh(max.Player);
            Near(max.Player.LegacyOfDreamsDamage, 1);
            Near(max.Player.LegacyOfDreamsDamageTaken, 1);
            if (max.Player.LegacyOfDreamsRank.HasValue) throw new Exception("Removed LoD remains in telemetry");
        });
        check("LoD counts a lone set piece, but an active two-piece bonus disables damage and reduction", () =>
        {
            var f = Equipped(99, 3, 1);
            const int setId = -1900104;
            var setItem = Empty<ItemTable>();
            Set(setItem, "<SNOSet>k__BackingField", setId);
            ItemGenerator.Items[setId] = setItem;
            f.Items[2].GBHandle.GBID = setId;
            var effects = (List<SetItemBonusTable>)typeof(ItemGenerator)
                .GetField("ItemSetsEffects", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var bonus = Empty<SetItemBonusTable>();
            Set(bonus, "<Set>k__BackingField", setId);
            Set(bonus, "<Count>k__BackingField", 2);
            effects.Add(bonus);
            try
            {
                Refresh(f.Player);
                Near(f.Player.LegacyOfDreamsDamage, 16);
                f.Items[3].GBHandle.GBID = setId;
                Refresh(f.Player);
                Near(f.Player.LegacyOfDreamsDamage, 1);
                Near(f.Player.LegacyOfDreamsDamageTaken, 1);
                if (!f.Player.LegacyOfDreamsSetBonusActive) throw new Exception("Disabled reason missing");
            }
            finally { effects.Remove(bonus); ItemGenerator.Items.TryRemove(setId, out _); }
        });
    }

    internal static FixtureData Equipped(int rank, int legendary, int ancient)
    {
        var f = Fixture(0);
        f.Items.Clear();
        var definition = Empty<ItemTable>();
        Set(definition, "<SNOSet>k__BackingField", -1);
        const int itemId = -1900103;
        ItemGenerator.Items[itemId] = definition;
        for (uint i = 1; i <= 13; i++)
        {
            var item = i == 1 ? f.Item : Empty<Item>();
            Set(item, "<Attributes>k__BackingField", new GameAttributeMap(item));
            Set(item, "<GBHandle>k__BackingField", new GBHandle { GBID = itemId });
            item.Gems = new();
            item.Attributes[GameAttributes.Item_Quality_Level] = i <= legendary ? 9 : 6;
            item.Attributes[GameAttributes.Ancient_Rank] = i <= ancient ? (i == 1 ? 2 : 1) : 0;
            f.Items[i] = item;
        }
        var gem = Gem();
        gem.Attributes[GameAttributes.Jewel_Rank] = rank;
        f.Item.Gems.Add(gem);
        return f;
    }

    internal static void Refresh(Player player) => typeof(Player)
        .GetMethod("UpdateLegacyOfDreams", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(player, null);

    static Item Gem()
    {
        var definition = Definition();
        ItemGenerator.Items[definition.Hash] = definition;
        var gem = Empty<Item>();
        Set(gem, "<GBHandle>k__BackingField", new GBHandle { GBID = definition.Hash });
        Set(gem, "<Attributes>k__BackingField", new GameAttributeMap(gem));
        gem.Attributes[GameAttributes.Item_Power_Passive, DamagePower] = .0375f;
        return gem;
    }

    static ItemTable Definition()
    {
        // 2.8.0.99920 Items_Legendary.gam: base @624, per-rank @1248, secondary @1272,
        // unlock @1296. The formulas are constant 0.0375, 0.0375, and 1 respectively.
        var dataPath = Environment.GetEnvironmentVariable("D3_LOD_GAMEBALANCE");
        byte[] bytes;
        var start = 0;
        if (!string.IsNullOrEmpty(dataPath))
        {
            bytes = File.ReadAllBytes(dataPath);
            var offset = BitConverter.ToInt32(bytes, 56) + 16;
            var size = BitConverter.ToInt32(bytes, 60);
            start = Enumerable.Range(0, size / 1408).Select(i => offset + i * 1408)
                .Single(p => Encoding.ASCII.GetString(bytes, p, 256).TrimEnd('\0') == "Unique_Gem_023_x1");
        }
        else
        {
            bytes = new byte[1444];
            Encoding.ASCII.GetBytes("Unique_Gem_023_x1").CopyTo(bytes, 0);
            void Int(int offset, int value) => BitConverter.GetBytes(value).CopyTo(bytes, offset);
            foreach (var offset in Enumerable.Range(0, 16).Select(i => 504 + i * 24)) Int(offset, -1);
            void Attribute(int offset, int power, int formula, float value)
            {
                Int(offset, 1293); Int(offset + 4, power); Int(offset + 16, formula - 16); Int(offset + 20, 12);
                Int(formula, 6); Int(formula + 4, BitConverter.SingleToInt32Bits(value)); Int(formula + 8, 0);
            }
            Attribute(624, DamagePower, 1408, .0375f);
            Attribute(1248, DamagePower, 1420, .0375f);
            Attribute(1272, AncientPower, 1432, 1);
            Int(1296, 25);
        }
        var format = typeof(GameDataFormat).GetProperty(nameof(GameDataFormat.IsInstalledGame))!;
        var previous = GameDataFormat.IsInstalledGame;
        try
        {
            format.SetValue(null, true);
            using var stream = new MemoryStream(bytes);
            stream.Position = start;
            var definition = new ItemTable();
            definition.Read(stream);
            return definition;
        }
        finally { format.SetValue(null, previous); }
    }

    static void Near(float actual, float expected)
    {
        if (Math.Abs(actual - expected) > .0001f) throw new Exception($"Expected {expected}, got {actual}");
    }
}
