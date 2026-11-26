using System.Reflection;
using DiIiS_NA.GameServer.Core.Types.Math;
using DiIiS_NA.GameServer.GSSystem.ActorSystem;
using DiIiS_NA.GameServer.GSSystem.ObjectsSystem;
using DiIiS_NA.GameServer.GSSystem.PowerSystem;
using DiIiS_NA.GameServer.GSSystem.PowerSystem.Implementations;
using DiIiS_NA.GameServer.GSSystem.PowerSystem.Payloads;
using DiIiS_NA.GameServer.GSSystem.SkillsSystem;
using DiIiS_NA.GameServer.MessageSystem;
using static EtchedSigilChecks;

static class WizardChannelingChecks
{
    const int Torrent = Skills.Wizard.Offensive.ArcaneTorrent;
    const int Mantle = 446640;

    public static void Run(Action<string, Action> check)
    {
        check("Mantle applies the saved 21% roll immediately to all damage on each Wizard channel", () =>
        {
            foreach (var power in new[] { Torrent, Skills.Wizard.Offensive.RayOfFrost, Skills.Wizard.Offensive.Disintegrate })
            {
                var f = Wizard(power);
                var enemy = Enemy(f);
                Near(Hit(f.Player, enemy).TotalDamage, 100);
                f.Channel.Run().ToArray();
                Near(Hit(f.Player, enemy).TotalDamage, 121);
                f.Channel.Run().ToArray();
                Assert(f.Player.World.BuffManager.GetBuffs<MantleOfChannelingBuff>(f.Player).Count == 1, "Duplicate Mantle buff");
                Near(f.Player.Attributes[GameAttributes.Buff_Icon_Count0, Mantle], 0);
                f.Item.Attributes[GameAttributes.Item_Power_Passive, Mantle] = .25f;
                Near(Hit(f.Player, enemy).TotalDamage, 125);
            }
            var unsupported = Wizard(0);
            unsupported.Channel.Run().ToArray();
            Near(Hit(unsupported.Player, Enemy(unsupported)).TotalDamage, 100);
        });

        check("Mantle activates for the other classes' listed channels", () =>
        {
            foreach (var power in new[]
            {
                Skills.Barbarian.FurySpenders.Whirlwind,
                Skills.DemonHunter.HatredGenerators.Strafe,
                Skills.DemonHunter.HatredSpenders.RapidFire,
                Skills.Monk.SpiritSpenders.TempestRush,
                Skills.WitchDoctor.PhysicalRealm.Firebats,
                Skills.Necromancer.MainSkills.SiphonBlood
            })
            {
                var f = Wizard(power);
                var enemy = Enemy(f);
                Near(Hit(f.Player, enemy).TotalDamage, 100);
                f.Channel.Run().ToArray();
                Near(Hit(f.Player, enemy).TotalDamage, 121);
                f.Channel.CloseChannel();
                Near(Hit(f.Player, enemy).TotalDamage, 100);
            }
        });

        check("Mantle reduces incoming damage by 25% and multiplies with Flame Ward", () =>
        {
            var f = Wizard(Torrent);
            var enemy = Wizard(0).Player;
            enemy.World = f.Player.World;
            var target = Enemy(f);
            Near(Hit(enemy, target).TotalDamage, 100);
            f.Channel.Run().ToArray();
            // An inert target avoids unrelated Player/Toon initialization (which opens CASC).
            // The actual buff and incoming HitPayload multipliers are still exercised.
            var mantle = (Buff)Activator.CreateInstance(typeof(MantleOfChannelingBuff),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { f.Channel }, null)!;
            f.Player.World.BuffManager.AddBuff(f.Player, target, mantle);
            Near(Hit(enemy, target).TotalDamage, 75);
            f.Player.Attributes[GameAttributes.Rune_A, Torrent] = 1;
            f.Player.World.BuffManager.AddBuff(f.Player, target, new FlameWardBuff(f.Channel, .15f, .05f, 2));
            Near(Hit(enemy, target).TotalDamage, 63.75f);
            Set(f.Game, "_tickCounter", 60);
            Near(Hit(enemy, target).TotalDamage, 60);
            Set(f.Game, "_tickCounter", 120);
            Near(Hit(enemy, target).TotalDamage, 56.25f);
            Set(f.Game, "_tickCounter", 600);
            Near(Hit(enemy, target).TotalDamage, 56.25f);
            f.Channel.CloseChannel();
            Near(Hit(enemy, target).TotalDamage, 100);
        });

        check("Mantle stops on cancellation, unequip, breakage, death, control, and world change", () =>
        {
            Action<FixtureData>[] stop = {
                f => f.Channel.CloseChannel(), f => f.Items.Clear(),
                f => f.Item.Attributes[GameAttributes.Durability_Cur] = 0,
                f => f.Player.Dead = true, f => f.Player.Attributes[GameAttributes.Disabled] = true,
                f => f.Player.Attributes[GameAttributes.Stunned] = true,
                f => f.Player.Attributes[GameAttributes.Frozen] = true,
                f => f.Player.Attributes[GameAttributes.Feared] = true,
                f => f.Player.World = Fixture(0).Player.World
            };
            foreach (var interrupt in stop)
            {
                var f = Wizard(Torrent);
                f.Channel.Run().ToArray();
                var buff = f.Player.World.BuffManager.GetFirstBuff<MantleOfChannelingBuff>(f.Player);
                interrupt(f);
                Near(buff.DamageBonus, 0);
                Near(buff.DamageReduction, 0);
                Assert(buff.Update(), "Inactive Mantle was retained");
            }
        });

        check("Switching channels replaces Mantle without stacking and resets Flame Ward's ramp", () =>
        {
            var f = Wizard(Torrent);
            f.Player.Attributes[GameAttributes.Rune_A, Torrent] = 1;
            f.Channel.Run().ToArray();
            var ward = new FlameWardBuff(f.Channel, .15f, .05f, 2);
            f.Player.World.BuffManager.AddBuff(f.Player, f.Player, ward);
            Set(f.Game, "_tickCounter", 120);
            Near(ward.DamageReduction, .25f);
            f.Channel.CloseChannel();
            Near(ward.DamageReduction, 0);
            f.Channel.Run().ToArray();
            Near(ward.DamageReduction, .15f);
            var other = new EtchedSigilChannelProbe { User = f.Player, World = f.Player.World,
                PowerSNO = Skills.Wizard.Offensive.Disintegrate, TargetPosition = new Vector3D(10, 0, 0) };
            other.Run().ToArray();
            Assert(f.Player.World.BuffManager.GetBuffs<MantleOfChannelingBuff>(f.Player).Count == 1, "Channel switch stacked Mantle");
        });

        check("Flame Ward uses fire damage and cannot grant reduction with another rune", () =>
        {
            var f = Wizard(Torrent);
            var torrent = new WizardArcaneTorrent { User = f.Player, World = f.Player.World, PowerSNO = Torrent };
            var element = typeof(WizardArcaneTorrent).GetProperty("ChannelDamageType", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert(element.GetValue(torrent) == DamageType.Arcane, "Unruned element changed");
            f.Player.Attributes[GameAttributes.Rune_A, Torrent] = 1;
            Assert(element.GetValue(torrent) == DamageType.Fire, "Flame Ward is not fire");
            f.Channel.Run().ToArray();
            var ward = new FlameWardBuff(f.Channel, .15f, .05f, 2);
            f.Player.World.BuffManager.AddBuff(f.Player, f.Player, ward);
            Near(ward.DamageReduction, .15f);
            f.Player.Attributes[GameAttributes.Rune_A, Torrent] = 0;
            Near(ward.DamageReduction, 0);
        });

        check("Deflection creates guaranteed four-percent shields at the attack-speed limit", () =>
        {
            var f = Wizard(Torrent);
            var type = typeof(MagicWeapon).GetNestedType("MagicWeaponBuff", BindingFlags.NonPublic)!;
            var buff = (Buff)Activator.CreateInstance(type, true)!;
            buff.User = buff.Target = f.Player;
            buff.World = f.Player.World;
            var create = type.GetMethod("TryCreateShield", BindingFlags.Instance | BindingFlags.NonPublic)!;
            void Cast() => create.Invoke(buff, new object[] { .04f, 3f });
            f.Player.Attributes[GameAttributes.Hitpoints_Max] = 1000;
            f.Player.Attributes[GameAttributes.Hitpoints_Max_Percent_Bonus_Multiplicative] = 1;
            f.Player.Attributes[GameAttributes.Attacks_Per_Second] = 2;
            Cast();
            Near(Shield(f), 40);
            Cast();
            Near(Shield(f), 40);
            Set(f.Game, "_tickCounter", 29);
            Cast();
            Near(Shield(f), 40);
            Set(f.Game, "_tickCounter", 30);
            Cast();
            Near(Shield(f), 80);
            // No target or hit was required. The shield parent owns no extra Magic Weapon icon.
            Near(f.Player.Attributes[GameAttributes.Buff_Icon_Count2, Skills.Wizard.Utility.MagicWeapon], 0);
        });

        check("Deflection excludes item procs and non-attacking Teleport runes", () =>
        {
            var f = Wizard(Torrent);
            var type = typeof(MagicWeapon).GetNestedType("MagicWeaponBuff", BindingFlags.NonPublic)!;
            var buff = (Buff)Activator.CreateInstance(type, true)!;
            buff.User = buff.Target = f.Player;
            buff.World = f.Player.World;
            buff.PowerSNO = Skills.Wizard.Utility.MagicWeapon;
            f.Player.Attributes[GameAttributes.Rune_E, buff.PowerSNO] = 1;
            Set(f.Player.World.BuffManager, "_buffs", new Dictionary<Actor, List<Buff>> { [f.Player] = new() { buff } });
            var onCast = typeof(MagicWeapon).GetMethod("OnCast", BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (var (power, proc) in new[] { (Skills.Wizard.Offensive.EnergyTwister, true), (Skills.Wizard.Utility.Teleport, false) })
            {
                var cast = new PowerContext { User = f.Player, World = f.Player.World, PowerSNO = power, IsItemProc = proc };
                onCast.Invoke(null, new object[] { cast });
                Near(Shield(f), 0);
            }
        });

        check("Deflection layers expire independently without refreshing older shields or imposing a stack cap", () =>
        {
            var f = Wizard(Torrent);
            for (int tick = 0; tick < 180; tick += 20)
            {
                Set(f.Game, "_tickCounter", tick);
                AddShield(f, 40);
            }
            Near(Shield(f), 360);
            Set(f.Game, "_tickCounter", 180);
            f.Player.World.BuffManager.Update();
            Near(Shield(f), 320);
            Set(f.Game, "_tickCounter", 340);
            f.Player.World.BuffManager.Update();
            Near(Shield(f), 0);
            Assert(f.Player.World.BuffManager.GetFirstBuff<MagicWeapon.DeflectionBuff>(f.Player) == null, "Expired shields retained");
        });

        check("Deflection absorbs oldest capacity first, including non-weapon damage, and keeps the display correct", () =>
        {
            var f = Wizard(Torrent);
            f.Player.Attributes[GameAttributes.Damage_Shield_Amount] = 100; // Another shield's contribution.
            AddShield(f, 40);
            Set(f.Game, "_tickCounter", 60);
            AddShield(f, 40);
            var buff = f.Player.World.BuffManager.GetFirstBuff<MagicWeapon.DeflectionBuff>(f.Player);
            var hit = Empty<HitPayload>();
            hit.Target = f.Player;
            hit.TotalDamage = 50;
            buff.OnPayload(hit);
            Near(hit.TotalDamage, 0);
            Near(Shield(f), 130);
            Set(f.Game, "_tickCounter", 180);
            f.Player.World.BuffManager.Update();
            Near(Shield(f), 130); // The consumed first layer cannot remove capacity twice.
            hit.TotalDamage = 50;
            buff.OnPayload(hit);
            Near(hit.TotalDamage, 20);
            Near(Shield(f), 100);
            AddShield(f, 40);
            f.Player.World.BuffManager.RemoveBuffs(f.Player, typeof(MagicWeapon.DeflectionBuff));
            Near(Shield(f), 100);
        });

        check("An expired Deflection shield cannot absorb a hit before the next world cleanup", () =>
        {
            var f = Wizard(Torrent);
            AddShield(f, 40);
            var buff = f.Player.World.BuffManager.GetFirstBuff<MagicWeapon.DeflectionBuff>(f.Player);
            Set(f.Game, "_tickCounter", 180);
            var hit = Empty<HitPayload>();
            hit.Target = f.Player;
            hit.TotalDamage = 100;
            buff.OnPayload(hit);
            Near(hit.TotalDamage, 100);
            Near(Shield(f), 0);
        });

        check("Galvanizing Ward displays the installed client's shield icon in slot 1", () =>
        {
            var f = Wizard(Torrent);
            var type = typeof(GalvanizingBuff).Assembly.GetType("DiIiS_NA.GameServer.GSSystem.PowerSystem.Implementations.GalvShieldBuff")!;
            var buff = (Buff)Activator.CreateInstance(type, true)!;
            f.Player.World.BuffManager.AddBuff(f.Player, f.Player, buff);
            Near(f.Player.Attributes[GameAttributes.Buff_Icon_Count1, 208541], 1);
            Near(f.Player.Attributes[GameAttributes.Buff_Icon_Count2, 208541], 0);
            f.Player.World.BuffManager.RemoveBuff(f.Player, buff);
            Near(f.Player.Attributes[GameAttributes.Buff_Icon_Count1, 208541], 0);
        });
    }

    static FixtureData Wizard(int power)
    {
        var f = Fixture(power);
        f.Item.Attributes[GameAttributes.Item_Power_Passive, 484596] = 0;
        f.Item.Attributes[GameAttributes.Item_Power_Passive, Mantle] = .21f;
        f.Player.Attributes[GameAttributes.Level] = 70;
        f.Player.Attributes[GameAttributes.Cannot_Dodge] = true;
        Set(f.Player, "<LegacyOfDreamsDamage>k__BackingField", 1f);
        return f;
    }

    static EffectActor Enemy(FixtureData f)
    {
        var enemy = Empty<EffectActor>();
        enemy.World = f.Player.World;
        enemy.Position = new Vector3D(10, 0, 0);
        Set(enemy, "<Visible>k__BackingField", true);
        Set(enemy, "<Attributes>k__BackingField", new GameAttributeMap(enemy));
        enemy.Attributes[GameAttributes.Level] = 70;
        return enemy;
    }

    static HitPayload Hit(Actor user, Actor target)
    {
        var attack = new AttackPayload(new PowerContext { User = user, World = user.World, PowerSNO = 0 });
        attack.AddDamage(100, 0, DamageType.Arcane);
        return new HitPayload(attack, false, target);
    }
    static void AddShield(FixtureData f, float amount) => f.Player.World.BuffManager.AddBuff(f.Player, f.Player,
        new MagicWeapon.DeflectionBuff(amount, new PowerContext { World = f.Player.World }.WaitSeconds(3)));
    static float Shield(FixtureData f) => f.Player.Attributes[GameAttributes.Damage_Shield_Amount];
    static void Near(float actual, float expected)
    {
        if (!float.IsFinite(actual) || Math.Abs(actual - expected) > .001f)
            throw new Exception($"Expected {expected}, got {actual}");
    }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
}
