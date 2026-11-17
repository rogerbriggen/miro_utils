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

    /// <summary>
    /// How long to wait (in seconds) for a board to finish loading before giving up
    /// </summary>
    public int BoardLoadTimeoutSeconds { get; set; } = 60;

    public TimeSpan BoardLoadTimeout => TimeSpan.FromSeconds(BoardLoadTimeoutSeconds);

    /// <summary>
    /// Default timeout (in seconds) for all Playwright actions (clicks, waits for elements, ...)
    /// that do not specify an explicit timeout. Playwright's own default is 30 seconds.
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 30;

    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(DefaultTimeoutSeconds);

    /// <summary>
    /// Timeout (in seconds) for a single click on a menu entry while opening the export menu.
    /// If it is exceeded, the menu is closed and opened again (up to 3 attempts).
    /// </summary>
    public int MenuClickTimeoutSeconds { get; set; } = 10;

    public TimeSpan MenuClickTimeout => TimeSpan.FromSeconds(MenuClickTimeoutSeconds);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Channel))
        {
            throw new ArgumentException("Channel cannot be null or empty.");
        }
        if (BoardLoadTimeoutSeconds <= 0)
        {
            throw new ArgumentException("BoardLoadTimeoutSeconds must be greater than 0.");
        }
        if (DefaultTimeoutSeconds <= 0)
        {
            throw new ArgumentException("DefaultTimeoutSeconds must be greater than 0.");
        }
        if (MenuClickTimeoutSeconds <= 0)
        {
            throw new ArgumentException("MenuClickTimeoutSeconds must be greater than 0.");
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
