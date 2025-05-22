namespace miro_utils.MiroAutomation;


internal class MiroAutomationResult(
    MiroAutomationGenericResult? BackupResult = null,
    MiroAutomationGenericResult? ChangePasswordResult = null
)
{
    // Public properties to expose the fields
    public MiroAutomationGenericResult? BackupResult { get; } = BackupResult;
    public MiroAutomationGenericResult? ChangePasswordResult { get; } = ChangePasswordResult;
    // Helper property to check if all operations were successful
    
    public bool Success =>
        (BackupResult?.Success ?? true) &&
        (ChangePasswordResult?.Success ?? true);
}

internal class MiroAutomationGenericResult
{
    public MiroAutomationGenericResult(List<string> urls)
    {
        Initialize(urls);
    }

    /// <summary>
    /// Initializes the result dictionary with the provided URLs.
    /// </summary>
    /// <param name="urls">Urls to process later</param>
    public void Initialize(List<string> urls)
    {
        foreach (var url in urls)
        {
            Results[url] = (string.Empty, false);
        }
    }

    public void UpdateBoardname(string url, string boardName)
    {
        if (Results.TryGetValue(url, out (string BoardName, bool Success) value))
        {
            Results[url] = (boardName, value.Success);
        }
    }

    public void UpdateSuccess(string url, bool success)
    {
        if (Results.TryGetValue(url, out (string BoardName, bool Success) value))
        {
            Results[url] = (value.BoardName, success);
        }
    }

    public Dictionary<string, (string BoardName, bool Success)> Results { get; } = new Dictionary<string, (string BoardName, bool Success)>();
    

    // Helper property to check if all URLs were processed successfully
    public bool Success => Results.Count > 0 && Results.All(x => x.Value.Success);
}




