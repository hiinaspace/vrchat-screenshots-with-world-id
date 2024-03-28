# VRChat Screenshots with World Id

Automatically rename your VRChat screenshots to include the world ID where they were taken, so you or others can look it up later.

[VRCX](https://github.com/vrcx-team/VRCX/) can rename your screenshots in the same way, but it also does a ton of other stuff; this does exactly one thing, and doesn't require your login info.

## Installation

### Just give me the .exe nerd

Just trust me bro. Get `vrchat-screenshots-with-world-id.exe` from the [GitHub release page](https://github.com/hiinaspace/vrchat-screenshots-with-world-id/releases). It's just an exe, no installer stuff.

### Build from Source

Use `dotnet build`, it should work.

## Usage

1. Launch the `vrchat-screenshots-with-world-id.exe` executable.
2. The tool will run in the background, and you will see a system tray icon.
3. Take screenshots in VRChat as usual.
4. The tool will automatically rename the screenshots to include the world ID.
5. Right-click on the system tray icon to access the context menu:
   - Click on "Autostart on Login" to toggle whether the tool should run automatically on system startup.
   - Click on "Current World" to open the URL of the user's current world in the default browser.
   - Hover over "Recent Screenshots" to see a list of recently renamed screenshots. Click on a screenshot to open its containing folder.

## Design

The core is a simple VRChat log file watcher that detect world changes and screenshot events. Then there's a bunch of cruft to make it run as a windows forms notification icon, since that seems to be the least bad way to make a windows background program without any spooky command line windows popping up. Otherwise I would've just made a powershell script.

Claude 3 Opus pretty much wrote the whole thing, including this readme. I am a professional prompt engineer.