using System;
using System.IO;
using BreakoutIsle.Gameplay;
using NUnit.Framework;

namespace BreakoutIsle.Tests
{
    public sealed class QuestSaveStoreTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "BreakoutIsleSaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void SaveRoundTripsSnapshotAndBuildsBackupOnReplacement()
        {
            var store = new AtomicJsonSaveStore(Path.Combine(_directory, "profile.json"));
            var first = SaveSnapshot.Create("level-1", "checkpoint-1");
            first.questItemIds.Add("key-blue");
            first.worldEntities.Add(new WorldEntityRecord { stableId = "door-east", state = "open" });
            store.Save(first);

            first.checkpointId = "checkpoint-2";
            store.Save(first);

            var loaded = store.Load();
            Assert.That(loaded.status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(loaded.snapshot.checkpointId, Is.EqualTo("checkpoint-2"));
            Assert.That(loaded.snapshot.questItemIds, Is.EquivalentTo(new[] { "key-blue" }));
            Assert.That(File.Exists(Path.Combine(_directory, "profile.json.bak")), Is.True);
        }

        [Test]
        public void CorruptPrimaryRecoversValidBackupWithoutChangingEitherFile()
        {
            var path = Path.Combine(_directory, "profile.json");
            var store = new AtomicJsonSaveStore(path);
            store.Save(SaveSnapshot.Create("level-1", "checkpoint-safe"));
            store.Save(SaveSnapshot.Create("level-1", "checkpoint-new"));
            File.WriteAllText(path, "{ broken json");
            var corruptPrimary = File.ReadAllText(path);

            var loaded = store.Load();

            Assert.That(loaded.status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(loaded.snapshot.checkpointId, Is.EqualTo("checkpoint-safe"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(corruptPrimary));
        }

        [Test]
        public void SavingAfterBackupRecoveryPreservesTheLastValidBackup()
        {
            var path = Path.Combine(_directory, "profile.json");
            var store = new AtomicJsonSaveStore(path);
            store.Save(SaveSnapshot.Create("level-1", "checkpoint-good"));
            store.Save(SaveSnapshot.Create("level-1", "checkpoint-newer"));
            File.WriteAllText(path, "damaged-primary");
            Assert.That(store.Load().status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));

            store.Save(SaveSnapshot.Create("level-1", "checkpoint-recovered-progress"));
            File.WriteAllText(path, "damaged-again");
            var loadedAgain = store.Load();

            Assert.That(loadedAgain.status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(loadedAgain.snapshot.checkpointId, Is.EqualTo("checkpoint-good"));
        }

        [Test]
        public void CorruptPrimaryAndBackupRequireExplicitRestartConfirmation()
        {
            var path = Path.Combine(_directory, "profile.json");
            var store = new AtomicJsonSaveStore(path);
            File.WriteAllText(path, "not-json");
            File.WriteAllText(path + ".bak", "also-not-json");

            var loaded = store.Load();

            Assert.That(loaded.status, Is.EqualTo(SaveLoadStatus.NeedsRestartConfirmation));
            Assert.That(loaded.snapshot, Is.Null);
        }

        [Test]
        public void InvalidSnapshotCannotOverwriteLastValidSave()
        {
            var path = Path.Combine(_directory, "profile.json");
            var store = new AtomicJsonSaveStore(path);
            store.Save(SaveSnapshot.Create("level-1", "checkpoint-safe"));
            var before = File.ReadAllBytes(path);
            var invalid = SaveSnapshot.Create("level-1", "checkpoint-bad");
            invalid.schemaVersion = 0;

            Assert.Throws<ArgumentException>(() => store.Save(invalid));

            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(store.Load().snapshot.checkpointId, Is.EqualTo("checkpoint-safe"));
        }

        [Test]
        public void MissingSchemaAndDuplicateStableIdsAreRejected()
        {
            var missingVersion = SaveSnapshot.Create("level-1", "checkpoint-1");
            missingVersion.schemaVersion = 0;
            Assert.That(missingVersion.IsValid, Is.False);

            var duplicateWorldIds = SaveSnapshot.Create("level-1", "checkpoint-1");
            duplicateWorldIds.worldEntities.Add(new WorldEntityRecord { stableId = "door-a", state = "open" });
            duplicateWorldIds.worldEntities.Add(new WorldEntityRecord { stableId = "door-a", state = "closed" });
            Assert.That(duplicateWorldIds.IsValid, Is.False);

            var invalidSlot = SaveSnapshot.Create("level-1", "checkpoint-1");
            invalidSlot.selectedSupportSlot = 3;
            Assert.That(invalidSlot.IsValid, Is.False);
        }
    }
}
