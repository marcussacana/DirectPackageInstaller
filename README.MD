# DirectPackageInstaller

A tool to preview and send PKG to your PS4 from direct links

### Features
- Preview PKGs Files
- Standalone PS4 package downloads
- Auto Proxy PS4 Downloads to Speed up
- Support for RAR/7z files as well.
- Resumable Downloads (Only from uncompressed files)
- Segmented Downloads
- Support for few file hosting links
- Support for PSN update manifest (Splited PKGs)
- Works with Rem. Pack. Installer or Bin Loader


### Supported File Hostings
- Any direct link (Without Authentication)
- AllDebrid (API Key Required)
- Google Drive (With or Without Authentication)
- Mediafire (Without Authentication)
- Zippyshare
- PixelDrain
- 1Fichier (Must wait 60 minutes after downloaded)

### How to Install
- Download and Install the [.NET 6 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime)
- Download one of those builds:
	- [Windows x64]()
	- [Windows x86]()
	- [Windows ARM]()
	- [Linux x64]()
	- [Linux x86]()
	- [Linux ARM]()
	- [OSX x64]()
	- [OSX ARM]()


### How to Use 
- Direct Download Mode
	- Open the Remote Package Installer or Enable Goldhen Payload Server.
	- Open the DirectPackageInstaller.
	- Put a direct download URL in the "PKG URL" field in the window.
	- Click at "Load" and wait.
	- Click at "Install" and wait again.
	- It's done, you can close the DirectPackageInstaller or turn off your computer.

- Proxied Download Mode (Faster)
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

### How It Works
The tool it self is very simple, the Remote Package Installer have an API that allow you put packages from URLs into your PS4 download queue, the DirectPackageInstaller just use that API to push to your PS4 the download URL, helping only to you preview the PKG that you're installing.

With that said, the tool can do more than that as well, the PS4 have a very poor speed to download things from the web, but that is limited to WAN connections, when he is downloading something from LAN connection he can reach best speeds, with that in mind this tool have an automatically proxy server that your PS4 will automatically use to download your PKG, and with help of this proxy the poor speed will be basically solved.

The tool can works without the Remote Package Installer if you want, for that, just enable your binloader and the DirectPackageInstaller will be able to tell how the package should be installed. If the Bin Loader server is running and the Remote Package Installer too, the DirectPackageInstaller will give priority to the Remote Package Installer.

The tool allow you to install pkgs from RAR as well, in this case the tool will work in a very similar way to the other existing ones, but with the advantage that everything is done simultaneously, while the file is downloaded from the internet, it is being decompressed to your disk temporarily and is sent to your PS4, in this scenario you don't need to waste time extracting large PKGs or depending on your file manager downloads, doing the post-download file reconstruction, everything is done simultaneously saving a lot of time of your life.

The Segmented Download mode will start a background multithreaded download of the PKG to your computer and send to your PS4 at the same time, this should be faster than the Proxied Mode with the disadvantage of having to allocate the PKG on your HD.

### Dependencies
The Direct Package Installer Requires the .NET 6.0 Runtime installed in your machine, you can dowload it [here](https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime)

### How To Build
The build you need install the .NET 6.x SDK and run the `Build.cmd` file,  
this file is actually a hybrid bash and batch file, so despite the `cmd` extension you can use it to compile on unix systems too.  
A "Release" Directory will be creted in the root of this repo with the binaries.

The Payload must be manually built if you want modify the retail one, just run `make` in the payload source directory and replace the genareted payload.bin with the DirectPackageInstaller embedded resource file.

### Warnings
- The proxy feature in turn has proven to work in games over 90 GB, and I believe it is relatively stable as it depends on your internet connection and download server.
- Compressed file downloads generate a temporary PKG 'cache' on your hard drive, which will require free space according to the size of the PKG on your hard drive.

## Credits:
LibOrbisPkg by **maxton**  
HttpServerLite by **jchristn**  
Payload template by **sleirsgoevy**
DirectPackageInstaller by **marcussacana**
