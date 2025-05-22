namespace miro_utils.MiroAutomation;


internal class MiroAutomationParams
{
    // Public properties to expose the fields
    public string? Username { get; set; }
    public string? UserPassword { get; set; }

    public MiroAutomationPlaywrightParams? PlaywrightParams { get; set; }

    public MiroAutomationBackupParams? BackupParams { get; set; }

    public MiroAutomationChangePasswordParams? ChangePasswordParams { get; set; }

    // Do some checks to ensure that the parameters are valid
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new ArgumentException("Username cannot be null or empty.");
        }
        if (string.IsNullOrWhiteSpace(UserPassword))
        {
            throw new ArgumentException("UserPassword cannot be null or empty.");
        }
        if (BackupParams == null && ChangePasswordParams == null)
        {
            throw new ArgumentException("At least one of BackupParams or ChangePasswordParams must be provided.");
        }
        if (PlaywrightParams == null)
        {
            throw new ArgumentException("PlaywrightParams cannot be null.");
        }
        PlaywrightParams.Validate();
        BackupParams?.Validate();
        ChangePasswordParams?.Validate();
    }
}

internal class MiroAutomationPlaywrightParams
{
    // Public properties to expose the fields
    public string Channel { get; set; } = "msedge";
    public bool Headless { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Channel))
        {
            throw new ArgumentException("Channel cannot be null or empty.");
        }
    }
}


internal class MiroAutomationBackupParams
{     
    // Public properties to expose the fields
    public List<string> Urls { get; set; } = new List<string>();
    public bool CreatePdf { get; set; }
    public bool CreateImagePdf { get; set; }

    public void Validate()
    {
        if (Urls == null || Urls.Count == 0)
        {
            throw new ArgumentException("Urls cannot be null or empty.");
        }
    }
}

internal class MiroAutomationChangePasswordParams
{     
    // Public properties to expose the fields
    public List<string> Urls { get; set; } = new List<string>();

    public string? NewBoardPassword { get; set; }

    public void Validate()
    {
        if (Urls == null || Urls.Count == 0)
        {
            throw new ArgumentException("Urls cannot be null or empty.");
        }
        if (string.IsNullOrWhiteSpace(NewBoardPassword))
        {
            throw new ArgumentException("NewBoardPassword cannot be null or empty.");
        }
        if (NewBoardPassword.Length < 8)
        {
            throw new ArgumentException("NewBoardPassword must be at least 8 characters long.");
        }
    }
}
