# DirectPackageInstaller

A tool to preview and send PKG to your PS4 from direct links, using your GoldHEN payloader server,  
Or alternatively the RemotePackageInstaller Homebrew.

### Features
- Preview PKGs Files
- Standalone PS4 package downloads
- Auto Proxy PS4 Downloads to Speed up
- Support for RAR/7z files as well.
- Resumable Downloads (Only from uncompressed files)
- Segmented Downloads
- Support for few file hosting links
- Support for PSN update manifest (Splited PKGs)
- Works with or without RemotePackageInstaller
- Command Line Interface
- JDown Click'n Load Support


### Supported File Hostings
- Any direct link (Without Authentication)
- AllDebrid (API Key Required)
- RealDebrid ([API Key Required](https://real-debrid.com/apitoken))
- Google Drive (With or Without Authentication)
- Mediafire (Without Authentication)
- DataNodes (Freemium Links Only)
- PixelDrain
- 1Fichier (Must wait 60 minutes after downloaded)

### How to Install
- Download and Install the [.NET 6 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime)
- Download one of those builds:
	- Windows: [ARM](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/Windows-ARM.zip), [ARM64](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/Windows-ARM64.zip), [x64](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/Windows-X64.zip), [x86](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/Windows-X86.zip)
	- Android: [ARM](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/Android-ARM.zip), [ARM64](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/Android-ARM64.zip), [x64](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/Android-X64.zip), [x86](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/Android-X86.zip)
	- Linux: [ARM](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/Linux-ARM.zip), [ARM64](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/Linux-ARM64.zip), [x64](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/Linux-X64.zip)
	- OSX CLI: [ARM64](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/OSX-ARM64.zip), [x64](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/OSX-X64.zip)
    - OSX App: [ARM64](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/OSX-ARM64-APP.zip), [x64](https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/OSX-X64-APP.zip)

### How to Use 
- Direct Download Mode
	- Open the Remote Package Installer or Enable Goldhen Payload Server.
	- Open the DirectPackageInstaller.
	- Put a direct download URL in the "PKG URL" field in the window.
	- Click at "Load" and wait.
	- Click at "Install" and wait again.
	- It's done, you can close the DirectPackageInstaller or turn off your computer.

- Proxied Download Mode (for PS4 that has no Internet access)
	- Open the Remote Package Installer or Enable Goldhen Payload Server.
	- Open the DirectPackageInstaller.
	- Go to `Options -> Proxy Downloads` and check the option.
	- Put a direct download URL in the "PKG URL" field in the window.
	- Click at "Load" and wait.
	- Click at "Install" and wait again.
	- It's done, you can't close the DirectPackageInstaller
	
- Segmented Download Mode (Fastest)
	- Ensure you have enough free space in your computer disk
	- Open the Remote Package Installer or Enable Goldhen Payload Server.
	- Open the DirectPackageInstaller.
	- Go to `Options -> Segmented Downloads` and check the option.
	- Put a direct download URL in the "PKG URL" field in the window.
	- Click at "Load" and wait.
	- Click at "Install" and wait the PKG be pre-loaded.
	- It's done, you can't close the DirectPackageInstaller

- Download from Compressed Files (Allways Proxied)
	- Ensure you have enough free space in your computer disk
	- Open the Remote Package Installer or Enable Goldhen Payload Server.
	- Open the DirectPackageInstaller.
	- Put direct URL to your compressed file in the "PKG URL" field.
	- Click at "Load" and wait.
	- [Optional] Select the correct PKG in the file list
	- Click at "Install" and wait again.
	- It's done, you can't close the DirectPackageInstaller or turn off your computer.

### For OSX CLI Users
You should run the `./DirectPackageInstaller.Desktop` file with help of the terminal
Some cases run with `dotnet DirectPackageInstaller.Desktop.dll` might be necessary.

### How It Works
The tool it self is very simple, the Remote Package Installer have an API that allow you put packages from URLs into your PS4 download queue, the DirectPackageInstaller just use that API to push to your PS4 the download URL, helping only to you preview the PKG that you're installing.

With that said, the tool can do more than that as well, the PS4 have a very poor speed to download things from the web, but that is limited to WAN connections, when he is downloading something from LAN connection he can reach best speeds, with that in mind this tool have an automatically proxy server that your PS4 will automatically use to download your PKG, and with help of this proxy the poor speed will be basically solved.

The tool can works without the Remote Package Installer if you want, for that, just enable your binloader and the DirectPackageInstaller will be able to tell how the package should be installed. If the Bin Loader server is running and the Remote Package Installer too, the DirectPackageInstaller will give priority to the Remote Package Installer.

This tool allow you to install pkgs from RAR as well, in this case the tool will work in a very similar way to the other existing ones, but with the advantage that everything is done simultaneously, while the file is downloaded from the internet, it is being decompressed to your disk temporarily and is sent to your PS4, in this scenario you don't need to waste time extracting large PKGs or depending on your file manager downloads, doing the post-download file reconstruction, everything is done simultaneously saving a lot of time of your life.

The Segmented Download mode will start a background multithreaded download of the PKG to your computer and send to your PS4 at the same time, this should be faster than the Proxied Mode with the disadvantage of having to allocate the PKG on your HD.

Notice that the Segmented Download is primally downloaded in your PC, this mean the PS4 can't show the real download progress and his remaining time.

If your PS4 has a LAN-Only connection, you can use the "Proxy Downloads" option, then the DirectPackageInstaller will act as a bridge to the PS4 be able to download your package from the internet.

### Command Line Interface
The DirectPackageInstaller has an experimental CLI,  
In his current state isn't possible push RAR/7z or do Segmented Dowloads yet.  
For more details run `DirectPackageInstaller.Desktop -help`  
Or if you're on windows: `DirectPackageInstaller.Desktop.exe -help`

### Dependencies
The Direct Package Installer Requires the .NET 6.0 Runtime installed in your machine, you can dowload it [here](https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime)

### How To Build
To build you need install the .NET 6.x SDK and run the `Build.cmd` file,  
this file is actually a hybrid bash and batch file, so despite the `cmd` extension you can use it to compile on unix systems too.  
A "Release" Directory will be creted in the root of this repo with the binaries.

The Payload must be manually built if you want modify the retail one, just run `make` in the payload source directory and replace the genareted payload.bin with the `DirectPackageInstaller/DirectPackageInstaller/payload` and rebuild the project.

To do android builds, the program target the API Level 26, so, install the Android SDK and NDK, accept the Android SDK Licenses, and **modify the `Build.cmd` file with your SDK/NDK installation path.**
After that you will need run the `Build.cmd` as sudo/admin in the first time, to restore the dotnet workload

Alternatively, create an fork and start the github action build

### Q&A
- My download is taking a long time just to begin
	- Let me guess, Downloading multipart compressed files?  
	This will happen when the file is splitted in many volumes, how much more volumes it was,more time will take it.
	This slowdown is caused by the random access in the compressed files that are made to check if any part is missing.
	There's no way to fix this since isn't a bug at all, just a problem when someone try open compressed files remotely.
	Then... Just wait a bit more...
	
- My download speed is very slow even using segmented mode.  
	- The segmented mode make the PS4 unaware of the real download progress, don't trust in the PS4 informations,
	for an real estimation of your download speed, check the network usage in your task manager.
	
- Segmented mode and just 2mbps of download speed?!
	- I noticied this problem during downloads from Zippyshare, but that isn't a problem of the DirectPackageInstaller,
	is just the zippyshare servers being slow or limited, who knows. If you want confirm by your self, I recommend upload
	an PKG to google drive and try install by yourself, since the google drive have a high download speed.
	
- What is the file hostings that you recommend?
	- Alldebrid/Realdebrid > Google Drive > Mediafire > Zippyshare > The whole universe > 1Fichier  
	This is the list of which hosts has best download performances.
	
- When I tries to install a **insert your error here** error happens
	- Create an issue! Just... Just Create! Serious, no one will bite you, if it was just a mistake there's no problem as well!  
	By the way, if is an crash the "DPI-Crash.log" will contains good informations about the error.

### Disclaimer
This program was made for legitimate purposes, you can use it only for download the your backups that you saved in cloud, and for that reason I condemn and do not support any kind of piracy.  
I'm no responsible for what you do with this tool, like, you know very well what you're doing.

### Warnings
- The DirectPackageInstaller don't handle well parallel decompressions, If you're downloading from a compressed file, try download one per time.
- Compressed file downloads generate a temporary PKG 'cache' on your hard drive, which will require free space according to the size of the PKG on your hard drive.

## Credits:
LibOrbisPkg by **maxton**  
HttpServerLite by **jchristn**  
Payload template by **sleirsgoevy**  
Help with PS4 OS internals by **LM**  
PS4 exports definitions by **OpenOrbis SDK**  
DirectPackageInstaller by **marcussacana**
