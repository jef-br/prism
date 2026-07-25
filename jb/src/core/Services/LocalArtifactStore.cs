using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// Local-filesystem implementation of <see cref="IArtifactStore"/>, rooted at <c>%TEMP%/PRISM</c>.
/// Every job gets a folder named after its id; LAMBDA documents live under <c>{job}/lambda/</c>.
/// </summary>
public sealed class LocalArtifactStore : IArtifactStore {
    private const string RootFolderName = "PRISM";
    private const string LambdaSubfolder = "lambda";

    private static readonly JsonSerializerOptions DocumentOptions = new() {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Creates a store rooted at the default server temp location (<c>%TEMP%/PRISM</c>).</summary>
    public LocalArtifactStore() : this(Path.Combine(Path.GetTempPath(), RootFolderName)) { }

    /// <summary>Creates a store rooted at an explicit temp root. Used by tests.</summary>
    public LocalArtifactStore(string jobTempRoot) {
        this.JobTempRoot = jobTempRoot ?? throw new ArgumentNullException(nameof(jobTempRoot));
    }

    /// <inheritdoc/>
    public string JobTempRoot { get; }

    /// <inheritdoc/>
    public string JobFolder(Guid jobId) {
        string folder = Path.Combine(this.JobTempRoot, jobId.ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <inheritdoc/>
    public string LambdaDocumentPath(Guid jobId, string imageId) {
        string lambdaFolder = Path.Combine(this.JobFolder(jobId), LambdaSubfolder);
        Directory.CreateDirectory(lambdaFolder);
        return Path.Combine(lambdaFolder, $"{SafeImageId(imageId)}.json");
    }

    /// <inheritdoc/>
    public void SaveLambdaDocument(Guid jobId, string imageId, ImageRecord_LAMBDA lambda) {
        string path = this.LambdaDocumentPath(jobId, imageId);
        File.WriteAllText(path, JsonSerializer.Serialize(lambda, DocumentOptions));
    }

    /// <inheritdoc/>
    public ImageRecord_LAMBDA? LoadLambdaDocument(Guid jobId, string imageId) {
        string path = this.LambdaDocumentPath(jobId, imageId);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<ImageRecord_LAMBDA>(File.ReadAllText(path), DocumentOptions);
    }

    /// <summary>Collapses an image's source name into a filesystem-safe document stem.</summary>
    private static string SafeImageId(string imageId) {
        string stem = Path.GetFileNameWithoutExtension(imageId);
        string safe = string.Join("_", stem.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(safe) ? "image" : safe;
    }
}
