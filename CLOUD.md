Project: Notepads (UWP)

Goal
- Provide guidance for build and test commands in CI or cloud environments.

Build (Windows required)
- msbuild "src/Notepads/Notepads.csproj" /p:Configuration=Debug /p:Platform=x64

Testing
- No automated tests are configured.
- Manual validation: launch the app and verify editor behavior.

Notes
- This repo targets UWP and requires Windows build tooling.
