using System.Runtime.ExceptionServices;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Serialization;

namespace Kingdom.World.Editor.Services;

public static class CampaignEditorProjectSerializer
{
    private static readonly IReadOnlyList<string> ManagedFiles = Array.AsReadOnly(
    [
        CampaignWorldProjectSerializer.ManifestFileName,
        CampaignWorldProjectSerializer.CampaignTileFileName,
        CampaignWorldProjectSerializer.CustomTerrainFileName,
        CampaignResourceProjectSerializer.DefinitionsFileName,
        CampaignResourceProjectSerializer.GenerationFileName,
        CampaignResourceProjectSerializer.TilesFileName,
    ]);

    // Dependent sparse catalogs/data are committed before the manifest that exposes the new world definition.
    private static readonly string[] CommitFileNames =
    [
        CampaignWorldProjectSerializer.CustomTerrainFileName,
        CampaignWorldProjectSerializer.CampaignTileFileName,
        CampaignResourceProjectSerializer.DefinitionsFileName,
        CampaignResourceProjectSerializer.GenerationFileName,
        CampaignResourceProjectSerializer.TilesFileName,
        CampaignWorldProjectSerializer.ManifestFileName,
    ];

    public static IReadOnlyList<string> ManagedFileNames => ManagedFiles;

    public static async Task<CampaignEditorProjectLoadResult> LoadAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var terrain = await CampaignWorldProjectSerializer.LoadAsync(projectPath, cancellationToken)
            .ConfigureAwait(false);

        if (terrain.WasConvertedFromLegacy)
        {
            return new CampaignEditorProjectLoadResult(
                terrain.World,
                new CampaignResourceMap(terrain.World.Definition),
                ResourceGenerationSettings: null,
                WasConvertedFromLegacy: true,
                terrain.SourceProjectDirectory,
                terrain.NormalizedLegacyCoastalTileCount);
        }

        var resources = await CampaignResourceProjectSerializer.LoadAsync(
            terrain.World.Definition,
            terrain.SourceProjectDirectory,
            cancellationToken).ConfigureAwait(false);
        return new CampaignEditorProjectLoadResult(
            terrain.World,
            resources.ResourceMap,
            resources.GenerationSettings,
            WasConvertedFromLegacy: false,
            terrain.SourceProjectDirectory,
            terrain.NormalizedLegacyCoastalTileCount);
    }

    public static async Task SaveAsync(
        CampaignWorld world,
        CampaignResourceMap resourceMap,
        CampaignResourceGenerationSettings? resourceGenerationSettings,
        string projectDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(resourceMap);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        if (world.Definition != resourceMap.Definition)
        {
            throw new ArgumentException(
                "The editor resource map must use a value-equal world definition.",
                nameof(resourceMap));
        }

        resourceMap.EnsureValid();
        resourceGenerationSettings?.EnsureValid(resourceMap.Catalog);
        var capturedWorldRevision = world.Revision;
        var capturedResourceRevision = resourceMap.Revision;
        var fullProjectPath = NormalizeProjectDirectory(projectDirectory);
        var projectParent = Path.GetDirectoryName(fullProjectPath)
            ?? throw new ArgumentException(
                "The editor project folder must have a parent so a sibling staging folder can be created.",
                nameof(projectDirectory));
        Directory.CreateDirectory(projectParent);
        var projectName = Path.GetFileName(fullProjectPath);
        var stagingDirectory = Path.Combine(
            projectParent,
            $".{projectName}.{Guid.NewGuid():N}.stage");
        Directory.CreateDirectory(stagingDirectory);

        Exception? operationFailure = null;
        var commitCompleted = false;
        try
        {
            await CampaignWorldProjectSerializer.SaveAsync(
                world,
                stagingDirectory,
                cancellationToken).ConfigureAwait(false);
            await CampaignResourceProjectSerializer.SaveAsync(
                resourceMap,
                resourceGenerationSettings,
                stagingDirectory,
                cancellationToken).ConfigureAwait(false);

            var staged = await LoadAsync(stagingDirectory, cancellationToken).ConfigureAwait(false);
            if (staged.WasConvertedFromLegacy ||
                staged.World.Definition != world.Definition ||
                staged.ResourceMap.Definition != resourceMap.Definition)
            {
                throw new InvalidOperationException(
                    "The staged editor project did not reload with the captured world definition.");
            }

            EnsureRevisionsUnchanged(
                world,
                capturedWorldRevision,
                resourceMap,
                capturedResourceRevision);
            cancellationToken.ThrowIfCancellationRequested();
            CommitStagedProject(
                stagingDirectory,
                fullProjectPath,
                world,
                capturedWorldRevision,
                resourceMap,
                capturedResourceRevision);
            commitCompleted = true;
        }
        catch (Exception exception)
        {
            operationFailure = exception;
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
            }
            catch when (operationFailure is not null || commitCompleted)
            {
                // Keep an earlier failure, or report a completed managed-file commit as successful.
                // A locked orphaned staging folder is recoverable and must not leave the document falsely dirty.
            }
        }
    }

    public static Task ExportAsync(
        CampaignWorld world,
        CampaignResourceMap resourceMap,
        string packagePath,
        CancellationToken cancellationToken = default) =>
        CampaignWorldRuntimeExporter.ExportAsync(
            world,
            resourceMap,
            packagePath,
            cancellationToken);

    private static string NormalizeProjectDirectory(string projectDirectory)
    {
        var fullPath = Path.GetFullPath(projectDirectory);
        if (File.Exists(fullPath))
        {
            throw new IOException($"The editor project folder path is an existing file: {fullPath}");
        }

        var root = Path.GetPathRoot(fullPath);
        var trimmed = Path.TrimEndingDirectorySeparator(fullPath);
        if (string.Equals(trimmed, Path.TrimEndingDirectorySeparator(root ?? string.Empty), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A filesystem root cannot be used as an editor project folder.",
                nameof(projectDirectory));
        }

        return trimmed;
    }

    private static void CommitStagedProject(
        string stagingDirectory,
        string projectDirectory,
        CampaignWorld world,
        long capturedWorldRevision,
        CampaignResourceMap resourceMap,
        long capturedResourceRevision)
    {
        var projectDirectoryExisted = Directory.Exists(projectDirectory);
        Directory.CreateDirectory(projectDirectory);
        var backupDirectory = Path.Combine(stagingDirectory, ".managed-backup");
        Directory.CreateDirectory(backupDirectory);
        var installedFiles = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            foreach (var fileName in CommitFileNames)
            {
                EnsureRevisionsUnchanged(
                    world,
                    capturedWorldRevision,
                    resourceMap,
                    capturedResourceRevision);
                var stagedPath = Path.Combine(stagingDirectory, fileName);
                var destinationPath = Path.Combine(projectDirectory, fileName);
                var backupPath = Path.Combine(backupDirectory, fileName);
                if (Directory.Exists(destinationPath))
                {
                    throw new IOException(
                        $"Managed editor project path is a directory instead of a file: {destinationPath}");
                }

                if (File.Exists(destinationPath))
                {
                    File.Move(destinationPath, backupPath, overwrite: false);
                }

                if (File.Exists(stagedPath))
                {
                    File.Move(stagedPath, destinationPath, overwrite: false);
                    installedFiles.Add(fileName);
                }
            }

            EnsureRevisionsUnchanged(
                world,
                capturedWorldRevision,
                resourceMap,
                capturedResourceRevision);
        }
        catch (Exception exception) when (IsRollbackEligible(exception))
        {
            try
            {
                RollBackManagedFiles(
                    projectDirectory,
                    backupDirectory,
                    installedFiles,
                    projectDirectoryExisted);
            }
            catch (Exception rollbackException) when (IsRollbackEligible(rollbackException))
            {
                throw new IOException(
                    "The editor project commit failed and its managed-file rollback also failed.",
                    new AggregateException(exception, rollbackException));
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    private static void RollBackManagedFiles(
        string projectDirectory,
        string backupDirectory,
        IReadOnlySet<string> installedFiles,
        bool projectDirectoryExisted)
    {
        foreach (var fileName in CommitFileNames.Reverse())
        {
            var destinationPath = Path.Combine(projectDirectory, fileName);
            var backupPath = Path.Combine(backupDirectory, fileName);
            if (installedFiles.Contains(fileName) && File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            if (!File.Exists(backupPath))
            {
                continue;
            }

            if (Directory.Exists(destinationPath))
            {
                throw new IOException(
                    $"Managed editor project path became a directory during rollback: {destinationPath}");
            }

            File.Move(backupPath, destinationPath, overwrite: true);
        }

        if (!projectDirectoryExisted &&
            Directory.Exists(projectDirectory) &&
            !Directory.EnumerateFileSystemEntries(projectDirectory).Any())
        {
            Directory.Delete(projectDirectory);
        }
    }

    private static void EnsureRevisionsUnchanged(
        CampaignWorld world,
        long capturedWorldRevision,
        CampaignResourceMap resourceMap,
        long capturedResourceRevision)
    {
        if (world.Revision != capturedWorldRevision)
        {
            throw new InvalidOperationException(
                "The campaign terrain changed while the editor project was being saved.");
        }

        if (resourceMap.Revision != capturedResourceRevision)
        {
            throw new InvalidOperationException(
                "Campaign resources changed while the editor project was being saved.");
        }
    }

    private static bool IsRollbackEligible(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException;
}
