# Rulership-Pin

An independent Windows desktop companion featuring Rulership from Umamusume.

## Download

Download the portable ZIP from [Releases](https://github.com/Subayaki/Rulership-Pin/releases/latest), extract it and open `Rulership_pin_custom/Rulership-Pin.exe`. Windows x64; no separate .NET installation needed.

## Controls

- Hover over the companion to reveal the pin button. Pin up to three live window previews.
- Click Chibi for a reaction; drag to move. Right-click for the menu.
- Model: Chibi or Original (Beta). Size and model choices are saved locally.
- Animations: random idle reactions, Lurk, Mini escape, and a manual-only Low cortisol dance with a temporary volume slider.
- Automatic Lurk & escape has its own saved toggle, independent of the other idle reactions.
- Minimize to orb keeps pinned previews stationary. Drag the orb or double-click to expand.
- Check for updates downloads a verified release and restarts the app after confirmation. Settings are preserved.
- On first start, a small speech bubble asks for your name. It is saved locally, used on the sign, and available to future animations. **Your name…** changes it later. Choosing Later keeps the app usable and asks again on next start.
- **Start with Windows** optionally starts this copy when you sign in. It is off until enabled and needs no administrator account. Keep the app in the same folder afterwards.
- Updates are checked quietly after startup and every six hours. A newer stable version shows a non-activating, personalized bubble above the character. Click it, confirm **Do you want to update now?**, and the verified updater handles the rest. Close the bubble to postpone it; no update or network error popup appears when nothing is available.

## Build and releases

Requires the .NET 10 SDK on Windows. Run `dotnet build Rulership-Pin.csproj -c Release`. `scripts/Package.ps1` builds the self-contained portable ZIP, including its `Rulership_pin_custom` folder, manifest and SHA-256 checksum.

GitHub Actions builds and tests each main-branch push. A new major.minor.patch Version in the project file creates a new release; an existing release is never overwritten. See [UPDATES.md](UPDATES.md).

## Privacy

No credentials, personal settings, logs, local references or debug symbols are included in source releases or portable downloads. Preferences remain in `companion-settings.json` beside the app. The sign greeting is configurable there through `Greeting`; the public default is neutral.

## Credits

Independent fan project; not affiliated with Cygames. Rulership/Umamusume game artwork belongs to Cygames. The original Chibi reference is the [Rulership gallery on GameTora](https://gametora.com/umamusume/characters/rulership). Additional reaction, running, peeking and orb artwork is generated fan art. Music belongs to its respective owners; it plays only when explicitly selected from the menu. The supplied Low cortisol excerpt is credited to the [source clip](https://www.youtube.com/shorts/Gj1r6yRrwBk).
