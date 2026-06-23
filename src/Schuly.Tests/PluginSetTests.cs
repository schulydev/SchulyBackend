using Schuly.API.Plugins;

namespace Schuly.Tests
{
    public class PluginSetTests
    {
        private static string TempFile() =>
            Path.Combine(Path.GetTempPath(), $"plugins-{Guid.NewGuid():N}.yml");

        [Test]
        public async Task Write_read_upsert_remove_round_trip()
        {
            var file = TempFile();
            try
            {
                var set = new PluginSet(file);
                set.Write([
                    new DesiredPlugin { Name = "Schuly.Plugin.Schulware", Version = "2.4.2" },
                    new DesiredPlugin { Name = "Schuly.Plugin.OdaOrg", Version = "latest" },
                ]);

                var read = set.Read();
                await Assert.That(read.Count).IsEqualTo(2);
                await Assert.That(read.Any(p => p.Name == "Schuly.Plugin.Schulware" && p.Version == "2.4.2")).IsTrue();

                set.Upsert("Schuly.Plugin.Schulware", "2.5.0");
                await Assert.That(new PluginSet(file).Read()
                    .First(p => p.Name == "Schuly.Plugin.Schulware").Version).IsEqualTo("2.5.0");

                set.RemoveEntry("Schuly.Plugin.OdaOrg");
                var after = new PluginSet(file).Read();
                await Assert.That(after.Count).IsEqualTo(1);
                await Assert.That(after[0].Name).IsEqualTo("Schuly.Plugin.Schulware");
            }
            finally { File.Delete(file); }
        }

        [Test]
        public async Task Missing_file_reads_empty()
        {
            await Assert.That(new PluginSet(TempFile()).Read().Count).IsEqualTo(0);
        }
    }
}
