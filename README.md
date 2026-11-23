# SecondSeat Audio

Play the same Windows audio through your speakers and a friend’s headphones at the same time.

[Download the free Windows x64 beta](https://github.com/kem0x/secondseat-audio/releases/tag/v0.1.0-beta)

1. Connect both audio devices in Windows. Keep your speakers selected as the Windows output.
2. Open SecondSeat Audio, choose your friend’s headphones, and click **Share audio**.
3. Click **Test both** and listen for two quiet notes on each device.

SecondSeat Audio remembers your headphone choice on this PC. While sharing is on, it tries to resume if the headphones disconnect and come back. Click **Stop sharing** or close the app to stop.

The download is a standalone, unsigned Windows x64 executable. Bluetooth audio can arrive slightly later than the speaker audio.

If you run into a problem, [open an issue](https://github.com/kem0x/secondseat-audio/issues/new) with your Windows version, both device models, and what happened.

## Build from source

On Windows with the .NET 10 SDK, run this from the repository root in PowerShell.

```powershell
dotnet publish .\src\SecondSeatAudio.csproj -c Release -r win-x64 --self-contained `
  -p:RuntimeFrameworkVersion=10.0.15 -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None -p:DebugSymbols=false -o .\publish
```

The standalone executable is `publish/SecondSeatAudio.exe`.

[Third-party notices](THIRD-PARTY-NOTICES.md)
