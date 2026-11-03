# Updates and releases

## Users

Extract the portable ZIP into a writable folder and run the app. Releases up to
1.0 did not have an updater, so the first updater-enabled version must be
downloaded once manually. Later releases can be installed using **Check for
updates…** in the right-click menu. No GitHub login or access token is required.

The app checks only `Subayaki/Rulership-Pin` public, stable releases. A download
must match the release SHA-256, expected version, root folder and per-file
manifest. Path traversal, links, duplicate paths and bundled personal settings
are rejected. Cancelling a download leaves the installed app unchanged.

After verification, a temporary copy of the new app acts as the installer. It
waits for the running instance to exit, takes the instance lock, validates the
package again, backs up replaced files into `update-history`, copies the new
files and restarts Rulership. Pins close during restart. A copy failure restores
the previous files; backups remain available for manual recovery. This is file
rollback, not a guarantee of recovery from power loss or a broken future build.

`companion-settings.json` is never part of a release and is not replaced. The
personal greeting and future profile fields remain local. When the installer
can inspect an older installation before replacing its DLL, it migrates a
legacy hardcoded greeting into that settings file. If manually replacing an
old ZIP version, save the desired greeting in settings first; the overwritten
old DLL cannot be inspected afterwards.

## Maintainers

Source is built on Windows with .NET 10. Only app source and the explicitly
referenced assets are tracked. Personal settings, internal notes, reference
downloads, logs, debug symbols and local build output are excluded.

1. Make and test the change.
2. Increase `<Version>` in `Rulership-Pin.csproj` using major.minor.patch.
3. Commit with a GitHub No-Reply email and push to main.
4. The release workflow builds, runs updater/animation checks, packages the app
   and publishes a new GitHub release with ZIP and checksum. Existing versions
   are not overwritten. A manual workflow dispatch can retry a failed build.

The portable package always contains `Rulership_pin_custom/`. Its manifest
lists every distributed file and hash. `scripts/Package.ps1` produces the same
package locally. Keep releases and the repository under the same trusted owner.

The application is not code-signed. Windows can show a reputation prompt for
new executables. SHA-256 verifies transport/package integrity against the
trusted release; it is not an independent signing identity.
