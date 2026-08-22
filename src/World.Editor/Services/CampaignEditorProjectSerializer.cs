using System.Runtime.ExceptionServices;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
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

    private static readonly IReadOnlyList<string> ManagedFilesWithSeasons = Array.AsReadOnly(
    [
        CampaignWorldProjectSerializer.ManifestFileName,
        CampaignWorldProjectSerializer.CampaignTileFileName,
        CampaignWorldProjectSerializer.CustomTerrainFileName,
        CampaignResourceProjectSerializer.DefinitionsFileName,
        CampaignResourceProjectSerializer.GenerationFileName,
        CampaignResourceProjectSerializer.TilesFileName,
        CampaignSeasonProjectSerializer.DefinitionsFileName,
        CampaignSeasonProjectSerializer.GenerationFileName,
        CampaignSeasonProjectSerializer.LayerFileName,
    ]);

    private static readonly string[] CommitFileNamesWithSeasons =
    [
        CampaignWorldProjectSerializer.CustomTerrainFileName,
        CampaignWorldProjectSerializer.CampaignTileFileName,
        CampaignResourceProjectSerializer.DefinitionsFileName,
        CampaignResourceProjectSerializer.GenerationFileName,
        CampaignResourceProjectSerializer.TilesFileName,
        CampaignSeasonProjectSerializer.DefinitionsFileName,
        CampaignSeasonProjectSerializer.GenerationFileName,
        CampaignSeasonProjectSerializer.LayerFileName,
        CampaignWorldProjectSerializer.ManifestFileName,
    ];

    public static IReadOnlyList<string> ManagedFileNames => ManagedFiles;

    public static IReadOnlyList<string> ManagedFileNamesWithSeasons => ManagedFilesWithSeasons;

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

    public static async Task<CampaignEditorSeasonProjectLoadResult> LoadWithSeasonsAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var project = await LoadAsync(projectPath, cancellationToken).ConfigureAwait(false);
        CampaignSeasonProjectLoadResult seasons;
        if (project.WasConvertedFromLegacy)
        {
            // A legacy import is a detached compatibility projection. Never attach unrelated
            // season sidecars that happen to sit beside its source files.
            seasons = CampaignSeasonProjectSerializer.CreateImplicit(
                project.World.Definition,
                project.SourceProjectDirectory);
        }
        else
        {
            seasons = await CampaignSeasonProjectSerializer.LoadAsync(
                project.World.Definition,
                project.SourceProjectDirectory,
                cancellationToken).ConfigureAwait(false);
        }

        return new CampaignEditorSeasonProjectLoadResult(
            project.World,
            project.ResourceMap,
            project.ResourceGenerationSettings,
            seasons.SeasonMap,
            seasons.SavedGeneration?.Settings.EnabledSeasonIds ??
                seasons.SeasonMap.Catalog.Definitions.Select(static definition => definition.Id).ToArray(),
            seasons.SavedGeneration,
            project.WasConvertedFromLegacy,
            project.SourceProjectDirectory,
            project.NormalizedLegacyCoastalTileCount,
            seasons.WasImplicitCompatibility);
    }

    public static Task SaveAsync(
        CampaignWorld world,
        CampaignResourceMap resourceMap,
        CampaignResourceGenerationSettings? resourceGenerationSettings,
        string projectDirectory,
        CancellationToken cancellationToken = default) =>
        SaveCoreAsync(
            world,
            resourceMap,
            resourceGenerationSettings,
            seasonMap: null,
            seasonEnabledIds: null,
            seasonSavedGeneration: null,
            projectDirectory,
            cancellationToken);

    public static Task SaveWithSeasonsAsync(
        CampaignWorld world,
        CampaignResourceMap resourceMap,
        CampaignResourceGenerationSettings? resourceGenerationSettings,
        CampaignSeasonMap seasonMap,
        IEnumerable<string> seasonEnabledIds,
        CampaignSeasonSavedGeneration? seasonSavedGeneration,
        string projectDirectory,
        CancellationToken cancellationToken = default) =>
        SaveCoreAsync(
            world,
            resourceMap,
            resourceGenerationSettings,
            seasonMap,
            seasonEnabledIds,
            seasonSavedGeneration,
            projectDirectory,
            cancellationToken);

    private static async Task SaveCoreAsync(
        CampaignWorld world,
        CampaignResourceMap resourceMap,
        CampaignResourceGenerationSettings? resourceGenerationSettings,
        CampaignSeasonMap? seasonMap,
        IEnumerable<string>? seasonEnabledIds,
        CampaignSeasonSavedGeneration? seasonSavedGeneration,
        string projectDirectory,
        CancellationToken cancellationToken)
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
        string[]? seasonEnabled = null;
        if (seasonMap is not null)
        {
            if (world.Definition != seasonMap.Definition)
            {
                throw new ArgumentException(
                    "The editor season map must use a value-equal world definition.",
                    nameof(seasonMap));
            }

            ArgumentNullException.ThrowIfNull(seasonEnabledIds);
            seasonMap.EnsureValid();
            seasonEnabled = seasonEnabledIds.Order(StringComparer.Ordinal).ToArray();
            new CampaignSeasonGenerationSettings(0, enabledSeasonIds: seasonEnabled)
                .EnsureValid(seasonMap.Catalog, seasonMap.Definition);
            if (seasonSavedGeneration is not null)
            {
                seasonSavedGeneration.Settings.EnsureValid(
                    seasonMap.Catalog,
                    seasonMap.Definition);
                if (!seasonEnabled.SequenceEqual(
                        seasonSavedGeneration.Settings.EnabledSeasonIds,
                        StringComparer.Ordinal))
                {
                    throw new ArgumentException(
                        "Saved season generation settings must use the editor enabled-season selection.",
                        nameof(seasonSavedGeneration));
                }
            }
        }
        else if (seasonEnabledIds is not null || seasonSavedGeneration is not null)
        {
            throw new ArgumentException(
                "Season selection or generation settings cannot be saved without a season map.",
                nameof(seasonMap));
        }

        var capturedWorldRevision = world.Revision;
        var capturedResourceRevision = resourceMap.Revision;
        var capturedSeasonRevision = seasonMap?.Revision;
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

            if (seasonMap is not null)
            {
                await CampaignSeasonProjectSerializer.SaveAsync(
                    seasonMap,
                    seasonSavedGeneration,
                    stagingDirectory,
                    cancellationToken).ConfigureAwait(false);
            }

            CampaignEditorProjectLoadResult staged;
            CampaignEditorSeasonProjectLoadResult? stagedWithSeasons = null;
            if (seasonMap is null)
            {
                staged = await LoadAsync(stagingDirectory, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                stagedWithSeasons = await LoadWithSeasonsAsync(
                    stagingDirectory,
                    cancellationToken).ConfigureAwait(false);
                staged = new CampaignEditorProjectLoadResult(
                    stagedWithSeasons.World,
                    stagedWithSeasons.ResourceMap,
                    stagedWithSeasons.ResourceGenerationSettings,
                    stagedWithSeasons.WasConvertedFromLegacy,
                    stagedWithSeasons.SourceProjectDirectory,
                    stagedWithSeasons.NormalizedLegacyCoastalTileCount);
            }

            if (staged.WasConvertedFromLegacy ||
                staged.World.Definition != world.Definition ||
                staged.ResourceMap.Definition != resourceMap.Definition ||
                (seasonMap is not null &&
                 (stagedWithSeasons is null ||
                  stagedWithSeasons.SeasonMap.Definition != seasonMap.Definition ||
                  stagedWithSeasons.SeasonsWereImplicitCompatibility)))
            {
                throw new InvalidOperationException(
                    "The staged editor project did not reload with the captured world definition.");
            }

            EnsureRevisionsUnchanged(
                world,
                capturedWorldRevision,
                resourceMap,
                capturedResourceRevision,
                seasonMap,
                capturedSeasonRevision);
            cancellationToken.ThrowIfCancellationRequested();
            CommitStagedProject(
                stagingDirectory,
                fullProjectPath,
                world,
                capturedWorldRevision,
                resourceMap,
                capturedResourceRevision,
                seasonMap,
                capturedSeasonRevision,
                seasonMap is null ? CommitFileNames : CommitFileNamesWithSeasons);
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

    public static Task ExportWithSeasonsAsync(
        CampaignWorld world,
        CampaignResourceMap resourceMap,
        CampaignSeasonMap seasonMap,
        string packagePath,
        CancellationToken cancellationToken = default) =>
        CampaignWorldRuntimeExporter.ExportAsync(
            world,
            resourceMap,
            seasonMap,
            packagePath,
            cancellationToken);

    public static Task ExportJsonWithSeasonsAsync(
        CampaignWorld world,
        CampaignResourceMap resourceMap,
        CampaignSeasonMap seasonMap,
        string jsonPath,
        CancellationToken cancellationToken = default) =>
        CampaignWorldJsonExporter.ExportAsync(
            world,
            resourceMap,
            seasonMap,
            jsonPath,
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
        long capturedResourceRevision,
        CampaignSeasonMap? seasonMap,
        long? capturedSeasonRevision,
        IReadOnlyList<string> commitFileNames)
    {
        var projectDirectoryExisted = Directory.Exists(projectDirectory);
        Directory.CreateDirectory(projectDirectory);
        var backupDirectory = Path.Combine(stagingDirectory, ".managed-backup");
        Directory.CreateDirectory(backupDirectory);
        var installedFiles = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            foreach (var fileName in commitFileNames)
            {
                EnsureRevisionsUnchanged(
                    world,
                    capturedWorldRevision,
                    resourceMap,
                    capturedResourceRevision,
                    seasonMap,
                    capturedSeasonRevision);
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
                capturedResourceRevision,
                seasonMap,
                capturedSeasonRevision);
        }
        catch (Exception exception) when (IsRollbackEligible(exception))
        {
            try
            {
                RollBackManagedFiles(
                    projectDirectory,
                    backupDirectory,
                    installedFiles,
                    projectDirectoryExisted,
                    commitFileNames);
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
        bool projectDirectoryExisted,
        IReadOnlyList<string> commitFileNames)
    {
        foreach (var fileName in commitFileNames.Reverse())
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
        long capturedResourceRevision,
        CampaignSeasonMap? seasonMap,
        long? capturedSeasonRevision)
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

        if (seasonMap is not null &&
            seasonMap.Revision != capturedSeasonRevision)
        {
            throw new InvalidOperationException(
                "Campaign seasons changed while the editor project was being saved.");
        }
    }

    private static bool IsRollbackEligible(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException;
}
