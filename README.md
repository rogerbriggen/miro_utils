# Miro Utils - A tool to batch process Miro.com boards

## About

This tool uses Playwrigth to automate a browser so you can

- backup any miro.com boards as long as you have owner rights
- Change the password of miro.com boards as long as you have owner rights
- Export any miro.com boards as PDF


## Tools

- Uses Dotnet 9
- Uses Playwright and MS Edge Browser

## Configure

- Use appsettings.json to add the boards to backup or change the board password
- Use appsettings.json or command line to configure Miro.com username
- Use command line to configure Miro.com password
- Use command line to configure the new miro board password


## Usage

### verbs

  backup            Backup Miro boards
  changePassword    Change password of Miro boards
  help              Display more information on a specific command.
  version           Display version information.

### Help

```bash
miro_utils help

# Display more information on a specific command.
miro_utils help backup
```

### Backup Miro boards

```bash
miro_utils backup --username my.email@formiro.com --userPassword abcdefg

# if you have a user set in appsettings.json, you can skip the --username parameter
miro_utils backup --userPassword abcdefg
```

#### Change Miro board passwords

Make sure, your new board password is at least 8 characters long!

```bash
miro_utils changePassword --username my.email@formiro.com --userPassword abcdefg --newBoardPassword theNewPasswordToSet

# if you have a user set in appsettings.json, you can skip the --username parameter
miro_utils changePassword --userPassword abcdefg --newBoardPassword theNewPasswordToSet
```

#### Advanced: Overwrite a parameter set in appsettings.json on the command line

Use all the names of the parameters in appsettings.json and join them with a colon.

```bash
miro_utils backup --username my.email@formiro.com --MiroAutomationParams:UserPassword Password
```
  
## Versions

### 1.0.6

- Update to Dotnet 10
- Update Playwright to 1.56.0
- Update libraries to latest versions

### 1.0.5

- Update Playwright to 1.55.0
- Update libraries to latest versions

### 1.0.4

- Update Playwright to 1.54.0
- Update libraries to latest versions

### 1.0.3

- Update Playwright to 1.52.0

### 1.0.0

- Initial release
