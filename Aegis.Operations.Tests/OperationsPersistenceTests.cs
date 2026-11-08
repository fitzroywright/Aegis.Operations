using Microsoft.Extensions.Configuration;
using Xunit;

[Collection("Operations persistence console")]
public sealed class OperationsPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "operations-persistence-" + Guid.NewGuid().ToString("N"));

    private OperationsV1Store Store() => new(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Operations:DataDirectory"] = _root }).Build());

    [Theory]
    [InlineData("application-logs")]
    [InlineData("activity-events")]
    public async Task Cancellation_after_temp_creation_preserves_committed_json_and_releases_gate(string bucket)
    {
        var store = Store();
        await store.SaveAsync(bucket, new[] { "committed" }, CancellationToken.None);
        string path = Path.Combine(_root, bucket + ".json");
        byte[] before = await File.ReadAllBytesAsync(path);
        using var cancellation = new CancellationTokenSource();
        bool sawTemp = false;
        var value = new WriteProbe(() =>
        {
            sawTemp = Directory.GetFiles(_root, "*.json.tmp-*").Length == 1;
            cancellation.Cancel();
            return new string('x', 128 * 1024);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(bucket, new[] { value }, cancellation.Token));

        Assert.True(sawTemp);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.GetFiles(_root, "*.json.tmp-*"));
        await store.SaveAsync(bucket, new[] { "next committed" }, CancellationToken.None);
        Assert.Equal(new[] { "next committed" }, await store.LoadAsync<string>(bucket, CancellationToken.None));
    }

    [Fact]
    public async Task Serialization_failure_after_temp_creation_preserves_committed_json()
    {
        var store = Store();
        await store.SaveAsync("application-logs", new[] { "committed" }, CancellationToken.None);
        string path = Path.Combine(_root, "application-logs.json");
        byte[] before = await File.ReadAllBytesAsync(path);
        var failure = new IOException("injected serialization failure");
        bool sawTemp = false;
        var value = new WriteProbe(() =>
        {
            sawTemp = Directory.GetFiles(_root, "*.json.tmp-*").Length == 1;
            throw failure;
        });

        var actual = await Assert.ThrowsAsync<IOException>(() => store.SaveAsync("application-logs", new[] { value }, CancellationToken.None));

        Assert.Same(failure, actual);
        Assert.True(sawTemp);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.GetFiles(_root, "*.json.tmp-*"));
    }

    [Fact]
    public async Task Startup_removes_only_stale_closed_temp_files()
    {
        var store = Store();
        await store.SaveAsync("application-logs", new[] { "committed" }, CancellationToken.None);
        string committed = Path.Combine(_root, "application-logs.json");
        byte[] before = await File.ReadAllBytesAsync(committed);
        string stale = TempFile();
        string recent = TempFile();
        string active = TempFile();
        string unknown = Path.Combine(_root, "application-logs.json.tmp-unrecognized");
        File.WriteAllText(unknown, "keep");
        foreach (string path in new[] { stale, active, unknown, committed })
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-2));

        using (var handle = new FileStream(active, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            _ = Store();
            Assert.False(File.Exists(stale));
            Assert.True(File.Exists(active));
            Assert.True(File.Exists(recent));
            Assert.True(File.Exists(unknown));
            Assert.Equal(before, await File.ReadAllBytesAsync(committed));
        }

        _ = Store();
        Assert.False(File.Exists(active));
    }

    [Fact]
    public async Task Promotion_failure_removes_temp_and_preserves_existing_destination()
    {
        var store = Store();
        // A directory at the destination forces rename to fail on every platform.
        string destination = Path.Combine(_root, "application-logs.json");
        Directory.CreateDirectory(destination);
        string marker = Path.Combine(destination, "existing");
        await File.WriteAllTextAsync(marker, "keep");

        await Assert.ThrowsAnyAsync<IOException>(() => store.SaveAsync("application-logs", new[] { "new" }, CancellationToken.None));

        Assert.Equal("keep", await File.ReadAllTextAsync(marker));
        Assert.Empty(Directory.GetFiles(_root, "*.json.tmp-*"));
    }

    private string TempFile()
    {
        string path = Path.Combine(_root, "application-logs.json.tmp-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, "partial");
        return path;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleanup_failure_reports_to_stderr_without_masking_original_error(bool brokenSink)
    {
        if (OperatingSystem.IsWindows()) return; // Unix permits unlinking our open test file.
        var store = Store();
        await store.SaveAsync("application-logs", new[] { "committed" }, CancellationToken.None);
        string path = Path.Combine(_root, "application-logs.json");
        byte[] before = await File.ReadAllBytesAsync(path);
        var failure = new IOException("original failure");
        var value = new WriteProbe(() =>
        {
            string temp = Assert.Single(Directory.GetFiles(_root, "*.json.tmp-*"));
            File.Delete(temp);
            Directory.CreateDirectory(temp); // Force File.Delete in finally to fail.
            throw failure;
        });
        TextWriter previous = Console.Error;
        using var diagnostic = new StringWriter();
        try
        {
            Console.SetError(brokenSink ? new BrokenWriter() : diagnostic);
            var actual = await Assert.ThrowsAsync<IOException>(() => store.SaveAsync("application-logs", new[] { value }, CancellationToken.None));
            Assert.Same(failure, actual);
        }
        finally { Console.SetError(previous); }

        if (!brokenSink) Assert.Contains("Operations temporary-file cleanup failed", diagnostic.ToString());
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    private sealed class BrokenWriter : StringWriter
    {
        public override void WriteLine(string? value) => throw new IOException("diagnostic sink failure");
    }

    public sealed class WriteProbe(Func<string> read)
    {
        public string Value => read();
    }

    public void Dispose() => Directory.Delete(_root, true);
}

[CollectionDefinition("Operations persistence console", DisableParallelization = true)]
public sealed class OperationsPersistenceConsoleCollection { }
