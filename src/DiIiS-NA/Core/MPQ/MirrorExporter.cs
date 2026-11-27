using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DiIiS_NA.Core.MPQ.FileFormats;
using DiIiS_NA.Core.Storage;
using DiIiS_NA.GameServer.Core.Types.SNO;
using Microsoft.Data.Sqlite;

namespace DiIiS_NA.Core.MPQ
{
    /// <summary>
    /// Re-reads assets that the emulator currently takes from the bundled 2.7.4 mirror
    /// (DataBase/mpqdata.db) out of the installed 2.8.0 game and reports, per asset, how far the
    /// two versions drift apart. Only assets whose parser can read the shipped file are touched;
    /// groups the client no longer ships are reported as missing instead of being invented.
    ///
    /// Run with <c>--export-mpqdata &lt;group&gt; [--apply]</c>; without <c>--apply</c> it only prints
    /// the differences, so the decision stays reversible.
    /// </summary>
    public static class MirrorExporter
    {
        private static readonly Dictionary<SNOGroup, (string Folder, string Extension)> FileLayouts = new()
        {
            [SNOGroup.GameBalance] = ("GameBalance", "gam"),
            [SNOGroup.Worlds] = ("Worlds", "wrl"),
            [SNOGroup.ConversationList] = ("ConversationList", "cnl"),
            [SNOGroup.TreasureClass] = ("TreasureClass", "trs"),
        };

        public static int Run(string groupName, bool apply, int maxDiffsPerAsset = 6)
        {
            if (!Enum.TryParse<SNOGroup>(groupName, true, out var group))
            {
                Console.WriteLine($"Unknown group '{groupName}'. Known: {string.Join(", ", FileLayouts.Keys)}");
                return 2;
            }

            if (!FileLayouts.TryGetValue(group, out var layout))
            {
                Console.WriteLine($"Group '{group}' is not mirrored in mpqdata.db.");
                return 2;
            }

            var data = MPQStorage.Data;
            if (data?.CASC == null)
            {
                Console.WriteLine("No installed game opened; set Storage.GameDataPath in config.ini.");
                return 3;
            }

            if (!data.Parsers.TryGetValue(group, out var parserType))
            {
                Console.WriteLine($"No binary parser for {group}; the asset can only come from the mirror.");
                return 3;
            }

            if (group == SNOGroup.Worlds)
            {
                var rows0 = ReadToc(group);
                Console.WriteLine($"{group}: {rows0.Count} mirrored assets");
                Console.WriteLine("  The 2.8.0 client ships 200-424 byte world stubs without scene chunks;");
                Console.WriteLine("  the composition only exists in the 2.7.4 mirror. Nothing can be exported.");
                return 0;
            }

            var rows = ReadToc(group);
            Console.WriteLine($"{group}: {rows.Count} mirrored assets, parser {parserType.Name}, layout Base\\{layout.Folder}\\*.{layout.Extension}");

            var missing = 0;
            var unavailableStructural = 0;
            var identical = 0;
            var differing = 0;
            var updated = 0;

            foreach (var (snoId, name) in rows)
            {
                var path = $"Base\\{layout.Folder}\\{name}.{layout.Extension}";
                var file = data.CASC.GetFile(path);
                if (file == null)
                {
                    missing++;
                    Console.WriteLine($"  {snoId,-8} {name,-45} not shipped by the client");
                    continue;
                }

                FileFormat fresh;
                try
                {
                    fresh = (FileFormat)Activator.CreateInstance(parserType, new object[] { file });
                }
                catch (Exception e)
                {
                    missing++;
                    Console.WriteLine($"  {snoId,-8} {name,-45} parser failed: {e.GetType().Name}: {e.Message}");
                    continue;
                }

                var previous = TryLoadMirror(parserType, snoId);

                // A 2.8.0 world is a stub (200-424 bytes) without any scene chunks; the mirror still
                // carries the 2.7.4 composition. Writing the stub would erase that data, so refuse.
                if (fresh is World freshWorld && previous is World mirrorWorld &&
                    mirrorWorld.SceneParams != null && freshWorld.SceneParams == null)
                {
                    unavailableStructural++;
                    Console.WriteLine($"  {snoId,-8} {name,-45} client ships a stub (chunks {mirrorWorld.SceneParams.ChunkCount} only in the mirror)");
                    continue;
                }

                var diffs = previous == null ? new List<string> { "no mirror row" } : Compare(previous, fresh, maxDiffsPerAsset);

                if (diffs.Count == 0)
                {
                    identical++;
                    Console.WriteLine($"  {snoId,-8} {name,-45} identical");
                    continue;
                }

                differing++;
                Console.WriteLine($"  {snoId,-8} {name,-45} {diffs.Count} difference(s)");
                foreach (var diff in diffs.Take(maxDiffsPerAsset))
                    Console.WriteLine($"             {diff}");

                if (!apply) continue;

                try
                {
                    DeleteMirrorRows(group, snoId, parserType.Name);
                    PersistenceManager.Save(fresh, name, snoId.ToString());
                    updated++;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"             update failed: {e.GetType().Name}: {e.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"{group}: {identical} identical, {differing} different, {missing} missing, " +
                              $"{unavailableStructural} structurally unavailable, {updated} written");
            if (!apply)
                Console.WriteLine("Dry run only - pass --apply to write the differences into mpqdata.db (make a backup first).");

            return 0;
        }

        private static List<(int SnoId, string Name)> ReadToc(SNOGroup group)
        {
            var result = new List<(int, string)>();
            using var cmd = new SqliteCommand(
                $"SELECT SNOId, Name FROM TOC WHERE SNOGroup = '{group}' ORDER BY SNOId", DBManager.MPQMirror);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add((Convert.ToInt32(reader["SNOId"]), reader["Name"].ToString()));
            return result;
        }

        private static FileFormat TryLoadMirror(Type parserType, int snoId)
        {
            try
            {
                return (FileFormat)PersistenceManager.Load(parserType, snoId.ToString());
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Removes the old TOC entry and the entity rows the mirror keeps for one asset.</summary>
        private static void DeleteMirrorRows(SNOGroup group, int snoId, string typeName)
        {
            var statements = new List<string>
            {
                $"DELETE FROM TOC WHERE SNOGroup = '{group}' AND SNOId = {snoId}",
                $"DELETE FROM \"{typeName}\" WHERE Id = {snoId}",
            };

            // Relation tables are named <Parent>_<ListType>_<Property>; drop rows that point at the old id.
            using (var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table'", DBManager.MPQMirror))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var table = reader.GetString(0);
                    if (table.StartsWith(typeName + "_", StringComparison.Ordinal))
                        statements.Add($"DELETE FROM \"{table}\" WHERE {typeName}Id = {snoId}");
                }
            }

            foreach (var statement in statements)
            {
                try
                {
                    using var cmd = new SqliteCommand(statement, DBManager.MPQMirror);
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                    // Relation table without that column - nothing to clean up there.
                }
            }
        }

        /// <summary>Compares two parsed assets field by field, returning human readable differences.</summary>
        private static List<string> Compare(object old, object fresh, int max)
        {
            var diffs = new List<string>();
            Walk(old, fresh, string.Empty, diffs, max);
            return diffs;
        }

        private static void Walk(object old, object fresh, string path, List<string> diffs, int max)
        {
            if (diffs.Count >= max || old == null || fresh == null) return;

            var type = old.GetType();
            if (type.Namespace == null || type.Namespace.StartsWith("System"))
            {
                if (!Equals(old, fresh))
                    diffs.Add($"{path}: {Format(old)} -> {Format(fresh)}");
                return;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length != 0) continue;

                object left, right;
                try
                {
                    left = property.GetValue(old);
                    right = property.GetValue(fresh);
                }
                catch
                {
                    continue;
                }

                var childPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";

                if (left is IEnumerable leftList && right is IEnumerable rightList && left is not string)
                {
                    var leftItems = leftList.Cast<object>().ToList();
                    var rightItems = rightList.Cast<object>().ToList();
                    if (leftItems.Count != rightItems.Count)
                    {
                        diffs.Add($"{childPath}: {leftItems.Count} -> {rightItems.Count} entries");
                        continue;
                    }

                    for (var i = 0; i < leftItems.Count && diffs.Count < max; i++)
                        Walk(leftItems[i], rightItems[i], $"{childPath}[{i}]", diffs, max);
                    continue;
                }

                Walk(left, right, childPath, diffs, max);
            }
        }

        private static string Format(object value)
        {
            if (value == null) return "<null>";
            var text = value.ToString();
            return text.Length > 40 ? text[..40] + "…" : text;
        }
    }
}
