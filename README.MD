# DirectPackageInstaller

A tool to preview and send PKG to your PS4 Remote Package Installer HB from direct links

### Features
- Preview PKGs Files
- Standalone PS4 package downloads
- Auto Proxy PS4 Downloads to Speed up
- Support for RAR/7z files as well.
- Resumable Downloads (Only from uncompressed files)
- Support for few file hosting links (GDrive, Mediafire, Zippyshare, PixelDrain)
- Support for PSN update manifest (Splited PKGs)

### How to Use 
- Direct Download Mode
	- Open the Remote Package Installer HB in your PS4.
	- Open the DirectPackageInstaller.
	- Put a direct download URL in the "PKG URL" field in the window.
	- Click at "Load" and wait.
	- Click at "Install" and wait again.
	- It's done, you can close the DirectPackageInstaller or turn off your computer.

- Proxied Download Mode (Usually Faster)
	- Open the Remote Package Installer HB in your PS4.
	- Open the DirectPackageInstaller.
	- Go to `Options -> Proxy Downloads` and check the option.
	- Put a direct download URL in the "PKG URL" field in the window.
	- Click at "Load" and wait.
	- Click at "Install" and wait again.
	- It's done, you can't close the DirectPackageInstaller

- Download from Compressed Files (Allways Proxied)
	- Open the Remote Package Installer HB in your PS4.
	- Open the DirectPackageInstaller.
	- Put direct URL to your compressed file in the "PKG URL" field.
	- Click at "Load" and wait.
	- [Optional] Select the correct PKG in the file list
	- Click at "Install" and wait again.
	- It's done, you can't close the DirectPackageInstaller or turn off your computer.

### How It Works
The tool it self is very simple, the Remote Package Installer have an API that allow you put packages from URLs into your PS4 download queue, the DirectPackageInstaller just use that API to push to your PS4 the download URL, helping only to you preview the PKG that you're installing.

With that said, the tool can do more than that as well, the PS4 have a very poor speed to download things from the web, but that is limited to WAN connections, when he is downloading something from LAN connection he can reach best speeds, with that in mind this tool have an automatically proxy server that your PS4 will automatically use to download your PKG, and with help of this proxy the poor speed will be basically solved.

The tool allow you to install pkgs from RAR as well, in this case the tool will work in a very similar way to the other existing ones, but with the advantage that everything is done simultaneously, while the file is downloaded from the internet, it is being decompressed to your disk temporarily and is sent to your PS4, in this scenario you don't need to waste time extracting large PKGs or depending on your file manager downloads, doing the post-download file reconstruction, everything is done simultaneously saving a lot of time of your life.


### Build on Linux
- Install MonoDevelop
- Build the DirectPackageInstallerLinux.sln project as Release with the MonoDevelop
- [Optional] Run ./build.sh in the repo main directory to create the AppImage (You can need edit the script a bit before)

### Warnings
- The proxy feature in turn has proven to work in games over 90 GB, and I believe it is relatively stable as it depends on your internet connection and download server.
- Compressed file downloads generate a temporary PKG 'cache' on your hard drive, which will require free space according to the size of the PKG on your hard drive.

## Credits:
LibOrbisPkg by **maxton**  
HttpServerLite by **jchristn**  
DirectPackageInstaller by **marcussacana**
