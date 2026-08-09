# LoonClipper

A minimal Windows app for saving part of a public Twitch VOD or clip as an MP3.

## Build

The developer machine needs the .NET 8 SDK. End users do not need .NET or any
other installed software.

1. Download the latest Windows `yt-dlp.exe` from the
   [yt-dlp releases](https://github.com/yt-dlp/yt-dlp/releases/latest).
2. Download a Windows FFmpeg build from a provider linked by the
   [FFmpeg download page](https://ffmpeg.org/download.html).
3. Place `yt-dlp.exe`, `ffmpeg.exe`, and `ffprobe.exe` in [`tools`](tools).
4. Publish the app:

   ```powershell
   dotnet publish LoonClipper.csproj -c Release -o artifacts/publish
   ```

The portable app is written to `artifacts/publish`. Its ZIP-ready layout is:

```text
artifacts/publish/
├── LoonClipper.exe
├── tools/
│   ├── yt-dlp.exe
│   ├── ffmpeg.exe
│   └── ffprobe.exe
└── licenses/
```

Keep the whole directory together. The supplied FFmpeg build is static, and the
LoonClipper publish is self-contained, so no adjacent DLL files are required.

## Use

Run `LoonClipper.exe`, paste a public Twitch VOD or clip link, set the start and
end hours, minutes, and seconds, and click **Download & trim**. Choose **Use
duration** when the second time should describe the clip length instead of its
end timestamp. The time fields accept numbers only and can also be adjusted
with the arrow keys.

A Twitch VOD link containing a timestamp, such as `?t=2h51m25s`, automatically
fills the start time. Other URL query parameters are ignored.

After the download, drag the handles on the waveform to fine-tune the clip.
The preview window can play the selection, normalize its volume, regenerate the
waveform, and save the final MP3 beside the app. Closing the preview without
saving discards its temporary files.

Private, deleted, subscriber-only, and otherwise unavailable media is not
supported.

If you redistribute the bundled tools, include their license notices and follow
the license terms of the exact yt-dlp and FFmpeg builds you ship.
