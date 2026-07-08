namespace Prism.Core;

/// <summary>Downloads a remote resource into the job temp folder and returns an INPUT record.</summary>
internal interface IFetchStrategy
{
    /// <summary>Returns true if this strategy can handle the given URL.</summary>
    bool CanHandle(string url);

    /// <summary>
    /// Downloads the resource at <paramref name="url"/> into <paramref name="jobTempFolder"/>
    /// and returns an INPUT record with <see cref="ImageRecord_INPUT.TempFilePath"/> set.
    /// </summary>
    Task<ImageRecord_INPUT> FetchAsync(string url, string jobTempFolder, string jobID, CancellationToken cancellationToken);
}
