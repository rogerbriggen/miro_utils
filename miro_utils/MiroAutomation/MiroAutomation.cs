using Microsoft.Playwright;
using Serilog;


namespace miro_utils.MiroAutomation;


/// <summary>
/// This class contains all the methods to automate Miro with Playwright.
/// </summary>
internal class MiroAutomation
{
    /// <summary>
    /// Selector for the button in the upper right corner of the board. This is used to check if the board is ready.
    /// </summary>
    public const string BoardButtonOnTopRightSelector = "[data-testid='board-title__container']";

    
    public static async Task<MiroAutomationResult> InitPlaywrightAsync(MiroAutomationParams miroAutomationParams) {
        // Do some initialization
        var errorsDuringExport = false;
        var miroAutomationResult = new MiroAutomationResult(miroAutomationParams.BackupParams == null ? null : new MiroAutomationGenericResult(miroAutomationParams.BackupParams.Urls), miroAutomationParams.ChangePasswordParams == null ? null : new MiroAutomationGenericResult(miroAutomationParams.ChangePasswordParams.Urls));

        // Do some sanity checks
        if (miroAutomationParams == null || string.IsNullOrEmpty(miroAutomationParams.Username) || string.IsNullOrEmpty(miroAutomationParams.UserPassword) || miroAutomationParams.PlaywrightParams == null)
        {
            Log.Error("MiroAutomationParams is null!");
            Environment.ExitCode = 1; // Error
            return miroAutomationResult;
        }
        try
        { 
            // Initialize Playwright
            using var playwright = await Playwright.CreateAsync();

            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = miroAutomationParams.PlaywrightParams.Channel, Headless = miroAutomationParams.PlaywrightParams.Headless });

            Log.Information("Logging in");
            var context = await browser.NewContextAsync();
            var loginPage = await context.NewPageAsync();

            try
            {
                await LoginAccountAsync(loginPage, miroAutomationParams.Username, miroAutomationParams.UserPassword, miroAutomationParams.PlaywrightParams.Headless);
                Log.Information("Logged in");

                // Do we need to do backup?
                if (miroAutomationParams.BackupParams != null && miroAutomationResult.BackupResult != null)
                {
                    Log.Information("Starting backup");
                    await DoExportAsync(context, miroAutomationParams, miroAutomationResult.BackupResult);
                }

                // Do we need to change the password?
                if (miroAutomationParams.ChangePasswordParams != null && miroAutomationResult.ChangePasswordResult != null)
                {
                    Log.Information("Changing board password");
                    await DoChangeBoardPasswordAsync(context, miroAutomationParams, miroAutomationResult.ChangePasswordResult);
                }
            }
            finally
            {
                // Close the login page
                await loginPage.CloseAsync();

                // Close the context
                await context.CloseAsync();

                // Close the browser
                await browser.CloseAsync();
            }
        }
        catch (Exception e)
        {
            errorsDuringExport = true;
            Log.Error(e, "Error during login!");
        }
        finally
        {
            if (errorsDuringExport)
            {
                Log.Error("Testing completed with errors!");
                Environment.ExitCode = 1; // Error
            }
            else
            {
                Log.Information("Testing completed.");
                Environment.ExitCode = 0;   // Success
            }
        }
        return miroAutomationResult;
    }

    public static async Task DoExportAsync(IBrowserContext context, MiroAutomationParams miroAutomationParams, MiroAutomationGenericResult miroAutomationGenericResult)
    {
        if (miroAutomationParams.BackupParams == null)
        {
            Log.Information("No backup parameters provided. Skipping backup.");
            return;
        }
        foreach (var url in miroAutomationParams.BackupParams.Urls)
        {
            var boardName = string.Empty;
            var page = await context.NewPageAsync();
            try
            {
                Log.Information("Backing up URL: {Url}", url);

                // Navigate to the URL
                await page.GotoAsync(url);

                // Wait for the page
                boardName = await WaitForBoardReadyAsync(page);

                // Update the boardname in our results
                miroAutomationGenericResult.UpdateBoardname(url, boardName);

                // Ok, ready for the workflow
                // Export as PDF
                if (miroAutomationParams.BackupParams.CreatePdf)
                {
                    await ExportBoardAsPdfAsync(page, boardName);
                }

                // Export as Image (pdf)
                if (miroAutomationParams.BackupParams.CreateImagePdf)
                {
                    await ExportBoardAsImagePdfAsync(page, boardName);
                }

                // Export as Backup
                var exportSucceeded = await ExportBoardAsBackup(page, boardName);
                if (exportSucceeded)
                {
                    // Update the success status in our results
                    miroAutomationGenericResult.UpdateSuccess(url, true);
                } 
            }
            catch (Exception e)
            {
                Log.Error(e, "DoExportAsync: Error for URL {Url} Boardname {Boardname}", url, boardName);
            }
            finally
            {
                // We are done with this page
                await page.CloseAsync();
            }
        }
    }


    public static async Task DoChangeBoardPasswordAsync(IBrowserContext context, MiroAutomationParams miroAutomationParams, MiroAutomationGenericResult miroAutomationGenericResult)
    {
        if (miroAutomationParams.ChangePasswordParams == null)
        {
            Log.Information("No change board password parameters provided. Skipping this step.");
            return;
        }
        foreach (var url in miroAutomationParams.ChangePasswordParams.Urls)
        {
            var boardName = string.Empty;
            var page = await context.NewPageAsync();
            try
            {
                Log.Information("Changing board password for URL: {Url}", url);

                // Navigate to the URL
                await page.GotoAsync(url);

                // Wait for the page
                boardName = await WaitForBoardReadyAsync(page);

                // Update the boardname in our results
                miroAutomationGenericResult.UpdateBoardname(url, boardName);

                // Ok, ready for the workflow
                var changePasswordSucceeded = await ChangeBoardPasswordInternalAsync(page, boardName, miroAutomationParams.ChangePasswordParams.NewBoardPassword!);
                if (changePasswordSucceeded)
                {
                    // Update the success status in our results
                    miroAutomationGenericResult.UpdateSuccess(url, true);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "ChangePasswordAsync: Error for URL {Url} Boardname {Boardname}", url, boardName);
            }
            finally
            {
                // We are done with this page
                await page.CloseAsync();
            }
        }
    }



    /// <summary>
    /// Wait for the board to be ready. This is done by waiting for the button in the upper right corner
    /// </summary>
    /// <param name="page">Page to work with. No navigation is done</param>
    /// <returns>Board name</returns>
    public static async Task<string> WaitForBoardReadyAsync(IPage page)
    {
        var button = await page.WaitForSelectorAsync(BoardButtonOnTopRightSelector, new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible
        });

        await button!.WaitForElementStateAsync(ElementState.Enabled);

        //Check if we can fetch the name of the board
        var boardName = await button.InnerTextAsync();
        Log.Information("Board name is: {BoardName}", boardName);
        return boardName;
    }

    /// <summary>
    /// Export the board as PDF
    /// </summary>
    /// <param name="page">Page to work with. No navigation is done</param>
    /// <param name="boardName">Board name for better diagnostics</param>
    public static async Task ExportBoardAsPdfAsync(IPage page, string boardName)
    {
        Log.Information("Exporting {BoardName} as PDF...", boardName);
        await ExportBoardAsync(page);

        // Export as PDF
        await WaitForButtonAndPressAsync(page, "[data-testid='board-settings__app_BOARD_EXPORT_PDF']");

        // Best quality is preselected
        // Export
        await DownloadAndSaveAsync(page, "PDF_",
            async (IPage p) =>
                await WaitForButtonAndPressAsync(p, "[data-testid='export-quality-settings-dialog__export-button']"));

        // Wait until export is done
        await WaitForBoardReadyAsync(page);

        // The file has the same name as the board
        Log.Information("PDF export for {BoardName} finished", boardName);
    }

    /// <summary>
    /// Export the board as image
    /// </summary>
    /// <param name="page">Page to work with. No navigation is done</param>
    /// <param name="boardName">Board name for better diagnostics</param>
    public static async Task ExportBoardAsImagePdfAsync(IPage page, string boardName)
    {
        Log.Information("Exporting {BoardName} as Image...", boardName);
        await ExportBoardAsync(page);

        // Export as Image
        await WaitForButtonAndPressAsync(page, "[data-testid='board-settings__app_BOARD_EXPORT_IMAGE']");

        // Vector is preselected
        // Export
        await DownloadAndSaveAsync(page, "IMG_PDF_",
            async (IPage p) =>
                await WaitForButtonAndPressAsync(p, "[data-testid='export-quality-settings-dialog__export-button']"));

        // Wait until export is done
        await WaitForBoardReadyAsync(page);

        // The file has the same name as the board
        Log.Information("Image (as pdf) export for {BoardName} finished", boardName);
    }


    /// <summary>
    /// Export the board as backup
    /// </summary>
    /// <param name="page">Page to work with. No navigation is done</param>
    /// <param name="boardName">Board name for better diagnostics</param>
    public static async Task<bool> ExportBoardAsBackup(IPage page, string boardName)
    {
        Log.Information("Exporting {BoardName} as backup...", boardName);
        await ExportBoardAsync(page);

        // Check if we have access rights, only owner can export a board
        // Export as backup
        try
        {
            await DownloadAndSaveAsync(page, null,
                async (IPage p) =>
                    await WaitForButtonAndPressAsync(p, "[data-testid='board-settings__app_BOARD_EXPORT_BACKUP']", new ElementHandleWaitForElementStateOptions { Timeout = 2000 }));
        }
        catch (Exception e)
        {
            Log.Error(e, "Backup of {Url} {BoardName} failed! Most probably you are not owner of the board!", page.Url, boardName);
            return false;
        }

        // Wait until export is done
        await WaitForBoardReadyAsync(page);

        // The file has the same name as the board
        Log.Information("Backup export for {BoardName} finished", boardName);
        return true;
    }

    /// <summary>
    /// Export the board as backup
    /// </summary>
    /// <param name="page">Page to work with. No navigation is done</param>
    /// <param name="boardName">Board name for better diagnostics</param>
    /// <param name="newBoardPassword">New board password</param>
    public static async Task<bool> ChangeBoardPasswordInternalAsync(IPage page, string boardName, string newBoardPassword)
    {
        Log.Information("Change board password for {BoardName}...", boardName);

        // Open board details
        await OpenBoardDetailsAsync(page);

        // Press "Share" button
        await WaitForButtonAndPressAsync(page, "[data-testid='board-info-modal-share']");


        /*
        // Wait for the board info modal to be visible
        // Wait 300ms
        await Task.Delay(3000);
        //await page.WaitForPopupAsync();
        
        var element = await page.WaitForSelectorAsync("[data-testid='board-info-modal']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 5000
        });
        */

        // Wait for the board info modal to be visible
        // Wait 3000ms
        await Task.Delay(3000);

        // Press "Edit password" drop down
        try
        {
            //await WaitForButtonAndPressAsync(page, "[data-testid='public-link-access-action__edit-password']");
            await page.GetByTestId("public-link-access-action__edit-password").ClickAsync();
        }
        catch (Exception e)
        {
            Log.Warning(e, "Edit password drop down not found. Is the board already public? Are you the owner of the board?");
            throw;
        }

        /*
        // Wait 1000ms
        await Task.Delay(1000);

        // Press "Change password" button (it is not a real button but a div)
        element = await page.WaitForSelectorAsync("[data-testid='public-link-access-action__change-password'][role='menuitem']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });
        await element?.ClickAsync();
        */
        await Task.Delay(2000);
        await page.GetByTestId("public-link-access-action__change-password").ClickAsync();
     

        // Press "Change password" button (it is not a real button but a div) 
        //await WaitForDivAndPressAsync(page, "[data-testid='public-link-access-action__change-password']");

        await Task.Delay(3000);

        // Press "Change password" button in the confirmation dialog
        await WaitForButtonAndPressAsync(page, "[data-testid='confirm-change-password-dialog__submit-button']");

        // Wait for the password panel to be visible
        // Wait 1000ms
        await Task.Delay(1000);

        // Press "Change password" button (it is not a real button but a div)
        var element = await page.WaitForSelectorAsync("[data-testid='shareMdContainer']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });
        

        // Enter the password
        await page.FillAsync("[data-testid='public-link-access-password-panel__input']", newBoardPassword);

        // And click Set Password button
        await page.ClickAsync("[data-testid='public-link-access-password-panel__set-button']");

        // Wait 3000ms
        await Task.Delay(3000);

        try
        {
            // After it is successful, the edit password drop down should be shown again
            await page.WaitForSelectorAsync("[data-testid='public-link-access-action__edit-password']", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 3000
            });
        }
        catch (Exception e)
        {
            Log.Error(e, "Error setting password for {BoardName}. Is the password long enough?", boardName);
            throw;
        }

        // The file has the same name as the board
        Log.Information("Changing board password for {BoardName} finished", boardName);
        return true;

    }

    /// <summary>
    /// Delegate to start the download process. This is used to start the download process
    /// </summary>
    /// <param name="page">Page to work with. No navigation is done</param>
	public delegate Task StartDownloadDelegate(IPage page);


    /// <summary>
    /// Download a file and save it to the user's folder
    /// </summary>
    /// <param name="page">Page to work with. No navigation is done.</param>
    /// <param name="filenamePrefix">Prefix added to the file to download</param>
    /// <param name="startDownload">Delegate to start the download</param>
    public static async Task DownloadAndSaveAsync(IPage page, string? filenamePrefix, StartDownloadDelegate startDownload)
    {
        // We prepare for the file download
        // Start waiting for download before clicking. Note no await.
        var downloadTask = page.WaitForDownloadAsync(new PageWaitForDownloadOptions { Timeout = 5 * 60 * 1000 });

        // Use the provided delegate to press the button
        await startDownload(page);

        var download = await downloadTask;

        // Wait for the download process to complete and save the downloaded file somewhere.
        // Get the user's folder path
        var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Get the current date
        var formattedDate = DateTime.Now.ToString("yyyy.MM.dd");

        // Create the filename
        var filename = $"{formattedDate}_{filenamePrefix}{download.SuggestedFilename}";

        // Combine the folder path and filename
        var fullPath = Path.Combine(userFolder, filename);
        await download.SaveAsAsync(fullPath);
        Log.Information("File saved to {FullPath}", fullPath);
    }

    /// <summary>
    /// Open the details page of the board
    /// </summary>
    /// <param name="page">page to work with. No navigation will be done</param>
    /// <exception cref="InvalidOperationException">Thrown if the button is not found on the page</exception>
    public static async Task OpenBoardDetailsAsync(IPage page)
    {
        // three dots in upper right corner after board name
        await WaitForButtonAndPressAsync(page, "[data-testid='board-settings__toggle-icon']");

        // Board
        await WaitForButtonAndPressAsync(page, "[data-testid='board-settings__item_board_menu']");

        // Details
        await WaitForButtonAndPressAsync(page, "[data-testid='board-settings__app_BOARD_DETAILS']");
    }


    /// <summary>
    /// Export the board. This is the first step for all exports
    /// </summary>
    /// <param name="page">page to work with. No navigation will be done</param>
    /// <exception cref="InvalidOperationException">Thrown if the button is not found on the page</exception>
    public static async Task ExportBoardAsync(IPage page)
    {
        // three dots in upper right corner after board name
        await WaitForButtonAndPressAsync(page, "[data-testid='board-settings__toggle-icon']");

        // Board
        await WaitForButtonAndPressAsync(page, "[data-testid='board-settings__item_board_menu']");

        // Export
        await WaitForButtonAndPressAsync(page, "[data-testid='settings__export_subMenu']");
    }

    /// <summary>
    /// Wait for a button to be visible and enabled and press it
    /// </summary>
    /// <param name="page">page to work with. No navigation will be done</param>
    /// <param name="selector">Selector to find the correct button</param>
    /// <param name="waitEnabledOptions">You can overwrite the default wait options, mainly the timeout</param>
    /// <exception cref="InvalidOperationException">Thrown if the button is not found on the page</exception>
    public static async Task WaitForButtonAndPressAsync(IPage page, string selector, ElementHandleWaitForElementStateOptions? waitEnabledOptions = null)
    {
        var button = await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible
        });

        if (button == null)
        {
            throw new InvalidOperationException($"Button is not ready. Page: {page.Url} - selector: {selector}");
        }
        await button.WaitForElementStateAsync(ElementState.Enabled, waitEnabledOptions);

        await button.ClickAsync();
    }

    /// <summary>
    /// Wait for a div to be visible and press it
    /// </summary>
    /// <param name="page">page to work with. No navigation will be done</param>
    /// <param name="selector">Selector to find the correct button</param>
    /// <param name="waitEnabledOptions">You can overwrite the default wait options, mainly the timeout</param>
    /// <exception cref="InvalidOperationException">Thrown if the element is not found on the page</exception>
    public static async Task WaitForDivAndPressAsync(IPage page, string selector)
    {
        var element = await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,            
        });

        if (element == null)
        {
            throw new InvalidOperationException($"Element is not ready. Page: {page.Url} - selector: {selector}");
        }

        await element.ClickAsync();
    }

    /// <summary>
    /// Login to Miro account
    /// </summary>
    /// <param name="page">The page to work on. It will be navigated to the login page.</param>
    /// <param name="userName">Miro user email to login</param>
    /// <param name="userPassword">Miro user password to login</param>
    /// <param name="headless">True if the browser is not visible</param>
    /// <returns></returns>
    public static async Task LoginAccountAsync(IPage page, string userName, string userPassword, bool headless)
    {
        // Navigate to the URL
        await page.GotoAsync("https://miro.com/login/");

        // Wait until Miro's page script is ready (React adds its props to the elements), otherwise entered values or clicks can get lost
        try
        {
            await page.WaitForFunctionAsync(
                "() => Object.keys(document.querySelector(\"[data-testid='mr-form-login-btn-signin-1']\") ?? {}).some(key => key.startsWith('__reactProps$'))",
                null, new PageWaitForFunctionOptions { Timeout = 10000 });
        }
        catch (Exception e)
        {
            Log.Debug(e, "Could not detect if the login page is ready. Continuing anyway.");
        }

        // Enter the email
        await page.FillAsync("[data-testid='mr-form-login-email-1']", userName);

        // Miro can switch off the login with password server side (passwordless only). Then no password field is rendered
        // and the sign in button sends an email with a login link and/or a code instead.
        var passwordInput = page.Locator("[data-testid='mr-form-login-password-1']");
        try
        {
            await passwordInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            // No password field on the page, so Miro wants us to use the passwordless login
        }
        if (!await passwordInput.IsVisibleAsync())
        {
            await LoginWithMagicLinkAsync(page, headless);
            return;
        }

        // Enter the password
        await passwordInput.FillAsync(userPassword);

        // And click the button
        await page.ClickAsync("[data-testid='mr-form-login-btn-signin-1']");

        // Wait until we are on the dashboard
        await page.WaitForURLAsync("https://miro.com/app/dashboard/");
    }

    /// <summary>
    /// Login without a password. This is used when Miro does not offer the login with password.
    /// Miro sends an email with a login link and/or a code, the user finishes the login in the browser window of this tool.
    /// </summary>
    /// <param name="page">The login page with the email already entered</param>
    /// <param name="headless">True if the browser is not visible. Then the user can not solve a captcha</param>
    private static async Task LoginWithMagicLinkAsync(IPage page, bool headless)
    {
        Log.Warning("Miro does not offer the login with password. Requesting an email to login instead.");

        // The sign in button sends the email when the login with password is not offered
        await page.ClickAsync("[data-testid='mr-form-login-btn-signin-1']");

        // Miro may ask for a captcha first (e.g. because the browser is automated). Only the user can solve it
        var outcome = await WaitForMagicLinkRequestAsync(page, detectCaptcha: true, TimeSpan.FromSeconds(30));
        if (outcome == MagicLinkRequestOutcome.CaptchaRequired)
        {
            if (headless)
            {
                throw new InvalidOperationException("Miro wants a captcha to be solved before it sends the email, but the browser is headless (not visible). " +
                    "Run the tool again with a visible browser by adding --MiroAutomationParams:PlaywrightParams:Headless=false to the command line, " +
                    "e.g. miro_utils backup --userPassword <password> --MiroAutomationParams:PlaywrightParams:Headless=false");
            }
            Log.Warning("Miro wants a captcha to be solved before it sends the email. Solve it in the browser window and then click the sign in button there again.");
            outcome = await WaitForMagicLinkRequestAsync(page, detectCaptcha: false, TimeSpan.FromMinutes(5));
        }

        if (outcome == MagicLinkRequestOutcome.EmailError)
        {
            var reason = await page.Locator(EmailErrorSelector).InnerTextAsync();
            throw new InvalidOperationException($"Miro did not send the email to login: {reason}");
        }
        if (outcome == MagicLinkRequestOutcome.Timeout)
        {
            var screenshotPath = await SaveScreenshotAsync(page, "magic_link_request_failed");
            throw new InvalidOperationException($"Miro did not send the email to login and showed no error. Screenshot of the login page: {screenshotPath}");
        }

        await WaitForPasswordlessLoginAsync(page, headless, TimeSpan.FromMinutes(10));
    }

    /// <summary>
    /// Wait until the user finished the login with the email from Miro. That email contains a login link and/or a code.
    /// The user does this in the browser window of this tool, a link opened in his own browser would only log in that browser.
    /// If the console can be used (it can not when the output is redirected, e.g. in Visual Studio), the user can also paste the link here.
    /// </summary>
    /// <param name="page">The check your email page of Miro</param>
    /// <param name="headless">True if the browser is not visible. Then the user can only use the console</param>
    /// <param name="timeout">How long the user has to finish the login</param>
    private static async Task WaitForPasswordlessLoginAsync(IPage page, bool headless, TimeSpan timeout)
    {
        // Console.ReadLine is used instead of a Spectre.Console prompt, a prompt throws when the input is redirected
        var canUseConsole = !Console.IsInputRedirected;
        if (headless && !canUseConsole)
        {
            throw new InvalidOperationException("Miro sent an email to finish the login, but the browser is not visible and the console can not be used for input. " +
                "Run the tool again in a console window and with a visible browser by adding --MiroAutomationParams:PlaywrightParams:Headless=false to the command line.");
        }

        Log.Warning("Miro sent an email to finish the login. Open that email now.");
        Log.Warning("Do NOT click the link in the email, that would log in your own browser instead of the browser of this tool.");
        if (!headless)
        {
            Log.Warning("In the browser window of this tool you can either enter the code from the email and press the continue button, " +
                "or paste the link address from the email (right click on the link, copy link address) into the address bar.");
        }
        if (canUseConsole)
        {
            Log.Warning("You can also paste the link address from the email here in this console window and press Enter.");
        }
        Log.Information("Waiting up to {Minutes} minutes for the login to be finished...", timeout.TotalMinutes);

        var consoleReader = canUseConsole ? Task.Run(() => Console.ReadLine()) : null;
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (IsLoggedIn(page))
            {
                Log.Information("Logged in with the email from Miro");
                return;
            }

            if (consoleReader != null && consoleReader.IsCompleted)
            {
                var pastedLink = (await consoleReader)?.Trim();
                if (pastedLink == null)
                {
                    // No more console input available, we only watch the browser from now on
                    consoleReader = null;
                }
                else
                {
                    consoleReader = Task.Run(() => Console.ReadLine());
                    if (pastedLink.Length > 0)
                    {
                        if (Uri.TryCreate(pastedLink, UriKind.Absolute, out var pastedUri) && (pastedUri.Scheme == Uri.UriSchemeHttps || pastedUri.Scheme == Uri.UriSchemeHttp))
                        {
                            Log.Information("Opening the pasted link in the browser of this tool...");
                            try
                            {
                                await page.GotoAsync(pastedUri.AbsoluteUri);
                            }
                            catch (Exception e)
                            {
                                Log.Warning(e, "Could not open the pasted link. It may be expired or already used.");
                            }
                        }
                        else
                        {
                            Log.Warning("This is not a complete link. Copy the link address from the email (right click on the link, copy link address).");
                        }
                    }
                }
            }

            await Task.Delay(500);
        }

        var screenshotPath = await SaveScreenshotAsync(page, "passwordless_login_timeout");
        throw new TimeoutException($"The login was not finished within {timeout.TotalMinutes:0} minutes. Screenshot of the page: {screenshotPath}");
    }

    /// <summary>
    /// Check if we are logged in. Miro uses /app/ for everything behind the login
    /// </summary>
    /// <param name="page">Page to check</param>
    private static bool IsLoggedIn(IPage page)
    {
        return Uri.TryCreate(page.Url, UriKind.Absolute, out var uri)
            && (uri.Host.Equals("miro.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".miro.com", StringComparison.OrdinalIgnoreCase))
            && uri.AbsolutePath.StartsWith("/app/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Selector for the error shown below the email on the login page
    /// </summary>
    private const string EmailErrorSelector = "[data-testid='login-page__email-error']";

    /// <summary>
    /// Result of requesting the login email on the login page
    /// </summary>
    private enum MagicLinkRequestOutcome
    {
        EmailSent,
        EmailError,
        CaptchaRequired,
        Timeout
    }

    /// <summary>
    /// Wait until Miro sent the login email (redirect to the check your email page), shows an error or asks for a captcha
    /// </summary>
    /// <param name="page">The login page after the sign in button was clicked</param>
    /// <param name="detectCaptcha">Return as soon as a captcha is shown. False while the user is solving it</param>
    /// <param name="timeout">How long to wait</param>
    private static async Task<MagicLinkRequestOutcome> WaitForMagicLinkRequestAsync(IPage page, bool detectCaptcha, TimeSpan timeout)
    {
        var emailError = page.Locator(EmailErrorSelector);
        // The captcha challenge is shown inside the login form, the invisible reCAPTCHA badge is outside of it
        var captcha = page.Locator("[data-testid='common-login-form'] iframe[src*='recaptcha']").First;
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (page.Url.Contains("/check-your-email"))
            {
                return MagicLinkRequestOutcome.EmailSent;
            }
            try
            {
                if (await emailError.IsVisibleAsync())
                {
                    return MagicLinkRequestOutcome.EmailError;
                }
                if (detectCaptcha && await captcha.IsVisibleAsync())
                {
                    return MagicLinkRequestOutcome.CaptchaRequired;
                }
            }
            catch (PlaywrightException)
            {
                // The page is navigating, check again
            }
            await Task.Delay(500);
        }
        return MagicLinkRequestOutcome.Timeout;
    }

    /// <summary>
    /// Save a screenshot of the page to the logs folder for diagnostics
    /// </summary>
    /// <param name="page">Page to take the screenshot of</param>
    /// <param name="name">Added to the filename</param>
    /// <returns>Full path of the screenshot</returns>
    private static async Task<string> SaveScreenshotAsync(IPage page, string name)
    {
        var path = Path.GetFullPath(Path.Combine("logs", $"{DateTime.Now:yyyy.MM.dd_HH.mm.ss}_{name}.png"));
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
        return path;
    }

    /// <summary>
    /// Login to a board with password protection
    /// </summary>
    /// <param name="page">page to enter the password. You must already be navigated to that page</param>
    /// <param name="boardPassword">Board password to enter</param>
    public static async Task LoginPageAsync(IPage page, string boardPassword)
    {
        // Enter the password
        await page.FillAsync("[data-testid='rtb-password-access-input__input']", boardPassword);

        // And click the button
        await page.ClickAsync("[data-testid='rtb-password-access-input__set-btn']");

        var button = await page.WaitForSelectorAsync("[data-testid='board-title__container']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible
        });
    }
}

