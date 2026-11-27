using System.IO.Pipes;
using System.Text.Json;
using D3Offline.CombatMeter;
using DiIiS_NA.GameServer.Core.Types.Math;
using DiIiS_NA.GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.GSSystem.ActorSystem;
using DiIiS_NA.GameServer.GSSystem.ObjectsSystem;
using DiIiS_NA.GameServer.GSSystem.PowerSystem;
using DiIiS_NA.GameServer.GSSystem.PowerSystem.Implementations;
using DiIiS_NA.GameServer.GSSystem.PowerSystem.Payloads;
using DiIiS_NA.GameServer.GSSystem.SkillsSystem;
using DiIiS_NA.GameServer.MessageSystem;
using static EtchedSigilChecks;

static class CombatMeterChecks
{
    public static void Run(Action<string, Action> check, bool assets = false)
    {
        var f = Fixture(134456);
        f.Player.Attributes[GameAttributes.Level] = 70;
        Set(f.Player, "<LegacyOfDreamsDamage>k__BackingField", 1f);
        Set(f.Player, "<CubedPowerValues>k__BackingField", new Dictionary<int, float>());
        var target = Empty<EffectActor>();
        target.World = f.Player.World;
        target.Position = new Vector3D(10, 0, 0);
        Set(target, "<Visible>k__BackingField", true);
        Set(target, "<ActorSNO>k__BackingField", new SNOHandle(0));
        Set(target, "<Attributes>k__BackingField", new GameAttributeMap(target));
        target.Attributes[GameAttributes.Hitpoints_Max] = 1000;
        target.Attributes[GameAttributes.Hitpoints_Cur] = 1000;

        HitPayload Hit()
        {
            var context = new EnergyTwister { User = f.Player, World = target.World, PowerSNO = 77113 };
            var attack = new AttackPayload(context);
            attack.AddDamage(100, 0, DamageType.Cold);
            return new HitPayload(attack, false, target) { AutomaticHitEffects = false };
        }

        check("Meter disabled has no hit trace allocation", () =>
        {
            if (Hit().MeterHit != null) throw new Exception("Tracing enabled without a viewer");
        });

        // Keep the pipe name short and skip when the temp directory leaves no room: on Unix the
        // name becomes a domain-socket path, which is limited to 108 bytes in total.
        var pipeName = MeterProtocol.PipeName + "." + Guid.NewGuid().ToString("N").Substring(0, 8);
        var pipePath = Path.Combine(Path.GetTempPath(), "CoreFxPipe_" + pipeName);
        if (!OperatingSystem.IsWindows() && pipePath.Length > 108)
        {
            Console.WriteLine($"SKIP meter pipe checks: temp path is {pipePath.Length} bytes (limit 108): {pipePath}");
            return;
        }
        CombatMeter.Start(pipeName);
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        pipe.Connect(5000);
        if (!SpinWait.SpinUntil(() => CombatMeter.Enabled, 3000)) throw new Exception("Meter server did not enable tracing");
        var reader = new StreamReader(pipe, leaveOpen: true);
        var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        MeterFrame Read()
        {
            writer.Write('?');
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            return JsonSerializer.Deserialize<MeterFrame>(reader.ReadLineAsync(timeout.Token).GetAwaiter().GetResult()!)!;
        }

        check("Meter groups Galvanizing Ward controller and shield and distinguishes recharge", () =>
        {
            var wardFixture = Fixture(0);
            var skills = Empty<SkillSet>();
            Set(skills, "<PassiveSkills>k__BackingField", new[] { 208541, -1, -1, -1 });
            Set(wardFixture.Player, "<SkillSet>k__BackingField", skills);
            wardFixture.Player.Attributes[GameAttributes.Hitpoints_Max] = 1000;
            var buffs = wardFixture.Player.World.BuffManager;
            var controller = new GalvanizingBuff();
            buffs.AddBuff(wardFixture.Player, wardFixture.Player, controller);
            CombatBuff Snapshot()
            {
                CombatMeter.CapturePlayers(new[] { wardFixture.Player }, false);
                return Read().Players.Single().Buffs.Single();
            }
            var row = Snapshot();
            if (row.Name != "Galvanizing Ward" || row.State != "Recharging" || row.IsStacking)
                throw new Exception("Passive controller appeared as an active shield or stack");
            var shieldType = typeof(GalvanizingBuff).Assembly.GetType(typeof(GalvanizingBuff).Namespace + ".GalvShieldBuff")!;
            var shield = (Buff)Activator.CreateInstance(shieldType, true)!;
            buffs.AddBuff(wardFixture.Player, wardFixture.Player, shield);
            // This is a snapshot test, not a test of the inert actor's uninitialized life formulas.
            Set(shield, "HPTreshold", 600f);
            row = Snapshot();
            if (row.State != "Shield active" || !row.Detail.StartsWith("Shield remaining: 600. "))
                throw new Exception("Shield capacity was omitted or the controller was counted twice");
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "combat-meter-buffs-fixture.json"), JsonSerializer.Serialize(Read()));
            Set(shield, "HPTreshold", 0f);
            if (Snapshot().State != "Depleted") throw new Exception("Empty shield claimed protection or recharge");
            skills.PassiveSkills[0] = -1;
            if (Snapshot().State != "Unexpected effect") throw new Exception("Stale passive effect was hidden or reported as selected");
            skills.PassiveSkills[0] = 208541;
            buffs.RemoveBuff(wardFixture.Player, shield);
            if (Snapshot().State != "Recharging") throw new Exception("Removed shield remained active");
            buffs.RemoveBuff(wardFixture.Player, controller);
            CombatMeter.CapturePlayers(new[] { wardFixture.Player }, false);
            if (Read().Players.Single().Buffs.Count != 0) throw new Exception("Removed passive remained visible");
        });

        if (assets) check("Meter explains LoD rank, ancient doubling, applied multiplier, and damage reduction", () =>
        {
            var lod = LegendaryGemChecks.Equipped(99, 13, 13);
            LegendaryGemChecks.Refresh(lod.Player);
            CombatMeter.CapturePlayers(new[] { lod.Player }, false);
            var frame = Read();
            var row = frame.Players.Single().Buffs.Single();
            if (row.Name != "Legacy of Dreams" || row.State != "Active" || row.IsStacking ||
                !new[] { "Rank 99", "+375%", "+750%", "13 ancient", "+9750%", "×98.5", "52%", "×0.48" }.All(row.Detail.Contains))
                throw new Exception("Incorrect LoD explanation: " + row.Detail);
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "combat-meter-lod-fixture.json"), JsonSerializer.Serialize(frame));
            lod.Item.Gems[0].Attributes[GameAttributes.Jewel_Rank] = 24;
            LegendaryGemChecks.Refresh(lod.Player);
            CombatMeter.CapturePlayers(new[] { lod.Player }, false);
            if (!Read().Players.Single().Buffs.Single().Detail.Contains("unlocks at rank 25"))
                throw new Exception("Locked ancient bonus was reported as active");
            Set(lod.Player, "<LegacyOfDreamsSetBonusActive>k__BackingField", true);
            Set(lod.Player, "<LegacyOfDreamsDamage>k__BackingField", 1f);
            Set(lod.Player, "<LegacyOfDreamsDamageTaken>k__BackingField", 1f);
            CombatMeter.CapturePlayers(new[] { lod.Player }, false);
            row = Read().Players.Single().Buffs.Single();
            if (row.State != "Inactive" || !row.Detail.Contains("Disabled by an active set bonus") || !row.Detail.Contains("Total: +0%"))
                throw new Exception("Disabled LoD claimed a bonus");
        });

        check("Meter groups Storm Armor with Power of the Storm without claiming another multiplier", () =>
        {
            var armorFixture = Fixture(0);
            var buffs = armorFixture.Player.World.BuffManager;
            // Snapshot production objects directly without loading Storm Armor's formula assets.
            var armor = new StormArmor.StormArmorBuff { PowerSNO = 74499 };
            var runeType = typeof(StormArmor).GetNestedType("GoldenBuff", System.Reflection.BindingFlags.NonPublic)!;
            var rune = (Buff)Activator.CreateInstance(runeType, true)!;
            rune.PowerSNO = 74499;
            var list = new List<Buff> { armor, rune };
            foreach (var buff in list) { buff.User = armorFixture.Player; buff.Target = armorFixture.Player; buff.World = armorFixture.Player.World; }
            Set(buffs, "_buffs", new Dictionary<Actor, List<Buff>> { [armorFixture.Player] = list });
            CombatMeter.CapturePlayers(new[] { armorFixture.Player }, false);
            var row = Read().Players.Single().Buffs.Single();
            if (row.Name != "Storm Armor · Power of the Storm" || row.State != "Active" || row.IsStacking || !row.Detail.Contains("resource costs"))
                throw new Exception("Storm Armor's rune was exposed as a separate buff or multiplier");
            armor.Removed = true;
            CombatMeter.CapturePlayers(new[] { armorFixture.Player }, false);
            if (Read().Players.Single().Buffs.Single().State != "Unexpected effect")
                throw new Exception("Orphaned rune was concealed");
        });

        check("Meter leaves unknown server effects unclassified and excludes expired buffs", () =>
        {
            var unknownFixture = Fixture(0);
            var unknown = new MeterShield();
            unknownFixture.Player.World.BuffManager.AddBuff(unknownFixture.Player, unknownFixture.Player, unknown);
            var expired = new GalvanizingBuff { Timeout = unknown.WaitSeconds(0) };
            unknownFixture.Player.World.BuffManager.AddBuff(unknownFixture.Player, unknownFixture.Player, expired);
            CombatMeter.CapturePlayers(new[] { unknownFixture.Player }, false);
            var row = Read().Players.Single().Buffs.Single();
            if (row.State != "Observed" || row.IsStacking || row.Detail.StartsWith("Active"))
                throw new Exception("Unknown effect claimed confirmed activity or a multiplier");
        });

        check("Meter records real Sigil multiplier without changing the hit", () =>
        {
            f.Channel.Run().ToArray();
            var hit = Hit();
            Near(hit.TotalDamage, 343);
            Near(hit.MeterHit.BaseDamage, 100);
            Near(hit.MeterHit.Factors.Single(s => s.Name == "Etched Sigil").Multiplier, 3.43);
            hit.Apply();
            CombatMeter.CapturePlayers(new[] { f.Player }, false);
            var frame = Read();
            var recorded = frame.Hits.Single();
            Near(recorded.HealthRemoved, 343);
            if (recorded.Element != "Cold" || recorded.Direction != "Outgoing") throw new Exception("Missing hit identity");
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "combat-meter-fixture.json"), JsonSerializer.Serialize(frame));
            if (Read().Hits.Count != 0) throw new Exception("Pipe repeated hits");
        });

        check("Meter names both cubed Twister multipliers and their Cube source", () =>
        {
            Set(f.Player, "<CubedPowerValues>k__BackingField", new Dictionary<int, float> { [484597] = 4f, [478491] = 3f });
            try
            {
                var hit = Hit();
                Near(hit.TotalDamage, 6860);
                Near(hit.MeterHit.Factors.Single(s => s.Name == "Valthek's Rebuke").Multiplier, 5);
                Near(hit.MeterHit.Factors.Single(s => s.Name == "Ranslor's Folly").Multiplier, 4);
                if (!CombatMeter.ItemSource(f.Player, 484597, "Energy Twister").Contains("Cube") ||
                    !CombatMeter.ItemSource(f.Player, 478491, "Energy Twister").Contains("Cube"))
                    throw new Exception("Cube source was not identified");
            }
            finally { Set(f.Player, "<CubedPowerValues>k__BackingField", new Dictionary<int, float>()); }
        });

        check("Meter traces shield callbacks and actual health removed", () =>
        {
            target.World.BuffManager.AddBuff(f.Player, target, new MeterShield());
            target.Attributes[GameAttributes.Hitpoints_Cur] = 500;
            var hit = Hit();
            hit.Apply();
            var recorded = Read().Hits.Single();
            Near(recorded.FinalDamage, 293);
            Near(recorded.HealthRemoved, 293);
            Near(recorded.Overkill, 0);
            var shield = recorded.Factors.Single(s => s.Name == "Meter Shield");
            Near(shield.Before - shield.After, 50);
            if (shield.Kind != "Adjustment") throw new Exception("Shield was mislabeled as an outgoing multiplier");
        });

        check("Meter overkill accounting uses the final HP write", () =>
        {
            CombatMeter.Record(new CombatHit(), 293, 80, 0);
            var recorded = Read().Hits.Single();
            Near(recorded.HealthRemoved, 80);
            Near(recorded.Overkill, 213);
        });

        check("Meter shows immunity as zero delivered damage", () =>
        {
            target.Dead = false;
            target.Attributes[GameAttributes.Invulnerable] = true;
            var hit = Hit();
            hit.Apply();
            var recorded = Read().Hits.Single();
            Near(recorded.HealthRemoved, 0);
            if (recorded.Outcome != "Immune") throw new Exception("Immunity outcome lost");
            target.Attributes[GameAttributes.Invulnerable] = false;
        });

        check("Meter buffer reports dropped hits instead of concealing incomplete totals", () =>
        {
            for (var i = 0; i < 2050; i++) CombatMeter.Record(new CombatHit { PlayerId = "probe" }, 1, 2, 1);
            var frame = Read();
            if (frame.Hits.Count != 2048 || frame.LostHits != 2) throw new Exception("Wrong bounded buffer loss count");
        });

        check("Bucket reference adds shared bonuses and multiplies independent powers", () =>
        {
            var factors = new[] {
                new DamageFactor { Name = "Skill affix", ReferenceBucket = MeterProtocol.Additive, Multiplier = 1.15 },
                new DamageFactor { Name = "Glass Cannon", ReferenceBucket = MeterProtocol.Additive, Multiplier = 1.15 },
                new DamageFactor { Name = "Audacity", ReferenceBucket = MeterProtocol.Independent, Multiplier = 1.3 },
                new DamageFactor { Name = "Unverified", Multiplier = 4 }
            };
            Near(MeterProtocol.ReferenceProduct(factors), 1.3 * 1.3);
            if (Math.Abs(1.15 * 1.15 * 1.3 - MeterProtocol.ReferenceProduct(factors)) < .001)
                throw new Exception("Reference mirrored the server's incorrect multiplicative grouping");
        });

        check("Meter DPS decays while idle and preserves totals beyond retained hit details", () =>
        {
            var history = new MeterHistory();
            var hits = Enumerable.Range(1, 1100).Select(i => new CombatHit { Sequence = i, Time = i * .01,
                PlayerId = "p", HealthRemoved = 10 }).ToList();
            history.Accept(new MeterFrame { Session = "one", Now = 11, LastSequence = 1100, Hits = hits });
            Near(history.Stats("p", "Outgoing").Total, 11000);
            if (history.Hits.Count != 1000) throw new Exception("History was unbounded");
            history.Accept(new MeterFrame { Session = "one", Now = 15, LastSequence = 1100 });
            Near(history.Stats("p", "Outgoing").Rolling, 0);
            Near(history.Stats("p", "Outgoing").Total, 11000);
            history.Accept(new MeterFrame { Session = "two", Now = 0 });
            Near(history.Stats("p", "Outgoing").Total, 0);
        });

        check("Meter encounter reset does not replay already received hits", () =>
        {
            var history = new MeterHistory();
            var frame = new MeterFrame { Session = "one", Now = 1, LastSequence = 1,
                Hits = new() { new CombatHit { Sequence = 1, Time = 1, PlayerId = "p", HealthRemoved = 100 } } };
            history.Accept(frame);
            history.Reset();
            history.Accept(frame);
            Near(history.Stats("p", "Outgoing").Total, 0);
        });
        writer.Dispose();
        reader.Dispose();
        pipe.Dispose();
        check("Meter disconnect disables tracing", () =>
        {
            if (!SpinWait.SpinUntil(() => !CombatMeter.Enabled, 4000)) throw new Exception("Tracing remained enabled");
        });
    }

    static void Near(double actual, double expected)
    {
        if (Math.Abs(actual - expected) > .001) throw new Exception($"Expected {expected}, got {actual}");
    }
}

sealed class MeterShield : Buff
{
    public override void OnPayload(Payload payload)
    {
        if (payload is HitPayload hit) hit.TotalDamage = Math.Max(0, hit.TotalDamage - 50);
    }
}
