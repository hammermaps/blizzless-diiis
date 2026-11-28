using System;
using System.Linq;
using DiIiS_NA.Core.MPQ.FileFormats;
using DiIiS_NA.GameServer.Core.Types.SNO;

namespace DiIiS_NA.Core.MPQ
{
    /// <summary>
    /// Offline probe for monster records: prints the declared skills together with the
    /// distance band the record carries (MonsterSkillDeclarations[i].UseRangeMin/Max).
    /// Used to answer "why does this monster not attack" without a running server -
    /// MonsterBrain.PickPowerToUse skips powers whose band does not contain the target.
    /// </summary>
    public static class MonsterInspector
    {
        /// <summary>
        /// Prints the rare-name table of the installed GameBalance assets: name and hash (GBID).
        /// The client resolves the hash, so a hash the 2.8.0 data does not know shows up as
        /// '!!!Missing!!! - &lt;id&gt;' under the monster's health bar.
        /// </summary>
        public static int RunNames()
        {
            var data = MPQStorage.Data;
            if (data?.CASC == null)
            {
                Console.WriteLine("No installed game opened; set Storage.GameDataPath in config.ini.");
                return 3;
            }

            foreach (var asset in data.Assets[SNOGroup.GameBalance].Values.OrderBy(a => a.SNOId))
            {
                if (asset.Data is not GameBalance balance) continue;
                if (balance.Type != GameBalance.BalanceType.MonsterNames) continue;

                Console.WriteLine($"asset {asset.SNOId,6}  {asset.Name}  type={balance.Type}  entries={balance.RareMonsterNames.Count}");

                var hashOfInterest = 52018;
                var found = false;
                foreach (var entry in balance.RareMonsterNames)
                {
                    if (entry.Hash == hashOfInterest) found = true;
                    Console.WriteLine($"   {entry.Name,-40} hash={entry.Hash,10}  affixType={entry.AffixType}");
                }

                Console.WriteLine($"   -> contains 52018: {found}");
            }

            return 0;
        }

        public static int Run(string nameFilter)
        {
            var data = MPQStorage.Data;
            if (data?.CASC == null)
            {
                Console.WriteLine("No installed game opened; set Storage.GameDataPath in config.ini.");
                return 3;
            }

            var monsters = data.Assets[SNOGroup.Monster];
            var matches = monsters
                .Where(kv => string.IsNullOrEmpty(nameFilter) ||
                             (kv.Value.Name?.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderBy(kv => kv.Key)
                .ToList();

            if (matches.Count == 0)
            {
                Console.WriteLine($"No monster matched '{nameFilter}'.");
                return 2;
            }

            foreach (var kv in matches)
            {
                if (kv.Value.Data is not Monster monster)
                {
                    Console.WriteLine($"{kv.Key,7}  {kv.Value.Name}  (no parsed data)");
                    continue;
                }

                Console.WriteLine($"{kv.Key,7}  {kv.Value.Name}  skills={monster.SkillDeclarations.Length}  skillKit={monster.SNOSkillKit}");

                if (monster.SNOSkillKit > 0 && data.Assets[SNOGroup.SkillKit].TryGetValue(monster.SNOSkillKit, out var kitAsset))
                {
                    if (kitAsset.Data is FileFormats.SkillKit kit)
                    {
                        foreach (var entry in kit.ActiveSkillEntries)
                            Console.WriteLine($"         kit power {entry.SNOPower,7}  category {entry.Category}");
                        foreach (var trait in kit.TraitEntries)
                            Console.WriteLine($"         kit trait {trait.SNOPower,7}  category {trait.Category}");
                    }
                    else
                    {
                        Console.WriteLine($"         skillKit {monster.SNOSkillKit} did not parse");
                    }
                }

                for (var i = 0; i < monster.SkillDeclarations.Length; i++)
                {
                    var power = monster.SkillDeclarations[i].SNOPower;
                    if (power == -1) continue;

                    var decl = monster.MonsterSkillDeclarations[i];
                    Console.WriteLine($"         power {power,7}  band {decl.UseRangeMin,7:0.##} .. {decl.UseRangeMax,7:0.##}  weight {decl.Weight,4}  timer {decl.Timer/10f,5:0.##}s");
                }
            }

            return 0;
        }
    }
}
