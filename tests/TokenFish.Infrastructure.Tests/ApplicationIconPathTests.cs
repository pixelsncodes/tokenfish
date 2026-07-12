using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class ApplicationIconPathTests
{
    [Fact]
    public void WindowIconResolvesFromApplicationBaseDirectory()
    {
        var baseDirectory = Path.Combine("TokenFish", "publish");

        var path = ApplicationIconPath.ResolveWindowIcon(baseDirectory);

        Assert.Equal(Path.Combine(baseDirectory, "Assets", "AppIcon.ico"), path);
    }

    [Fact]
    public void HeaderIconResolvesFromApplicationBaseDirectory()
    {
        var baseDirectory = Path.Combine("TokenFish", "publish");

        var path = ApplicationIconPath.ResolveHeaderIcon(baseDirectory);

        Assert.Equal(
            Path.Combine(
                baseDirectory,
                "Assets",
                "Square44x44Logo.targetsize-24_altform-unplated.png"),
            path);
    }

    [Fact]
    public void PublishLayoutIconResolutionFindsLooseAssets()
    {
        using var directory = TemporaryDirectory.Create();
        var assetsDirectory = Path.Combine(directory.Path, "Assets");
        Directory.CreateDirectory(assetsDirectory);
        File.WriteAllText(
            Path.Combine(assetsDirectory, ApplicationIconPath.WindowIconFileName),
            string.Empty);
        File.WriteAllText(
            Path.Combine(assetsDirectory, ApplicationIconPath.HeaderIconFileName),
            string.Empty);

        Assert.True(ApplicationIconPath.TryResolveExistingWindowIcon(directory.Path, out var windowIconPath));
        Assert.True(ApplicationIconPath.TryResolveExistingHeaderIcon(directory.Path, out var headerIconPath));
        Assert.Equal(
            Path.Combine(assetsDirectory, ApplicationIconPath.WindowIconFileName),
            windowIconPath);
        Assert.Equal(
            Path.Combine(assetsDirectory, ApplicationIconPath.HeaderIconFileName),
            headerIconPath);
    }

    [Fact]
    public void MissingOptionalIconResolutionDoesNotThrow()
    {
        using var directory = TemporaryDirectory.Create();

        var exception = Record.Exception(() =>
        {
            var foundWindowIcon = ApplicationIconPath.TryResolveExistingWindowIcon(
                directory.Path,
                out var windowIconPath);
            var foundHeaderIcon = ApplicationIconPath.TryResolveExistingHeaderIcon(
                directory.Path,
                out var headerIconPath);

            Assert.False(foundWindowIcon);
            Assert.False(foundHeaderIcon);
            Assert.EndsWith(
                Path.Combine("Assets", ApplicationIconPath.WindowIconFileName),
                windowIconPath);
            Assert.EndsWith(
                Path.Combine("Assets", ApplicationIconPath.HeaderIconFileName),
                headerIconPath);
        });

        Assert.Null(exception);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"tokenfish-icon-path-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
