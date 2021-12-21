#!/bin/sh

# WARNING
# The app can't read the real 'current directory' using the default way
# To know the launch current directory you can use CD env variable

export APP=DirectPackageInstallerLinux
export TARGET=mono-6.0.0-ubuntu-18.04-x64

cp AppLinux.Config bin/Debug/DirectPackageInstallerLinux.exe.config

cd bin/Debug


mkbundle --fetch-target $TARGET


#cp /usr/lib/libmono-native.so ./

MONO_BUNDLED_OPTIONS=--debug MONO_LOG_LEVEL=debug MONO_LOG_MASK=dll  mkbundle --cross $TARGET -o $APP -L ./ -L ../../packages/DotNetZip.1.16.0/lib/net40 -L ../../packages/SharpCompress.0.30.1/lib/net461 -L ../../packages/SixLabors.ImageSharp.1.0.4/lib/net472/ -L ../../packages/UrlMatcher.1.0.0.2/lib/net452 -L ../../packages/CavemanTcp.1.3.8.1/lib/net461 -L ../../packages/RegexMatcher.1.0.7.1/lib/net462 -L ../../packages/ncalc.1.3.8/lib -L ../../packages/IpMatcher.1.0.4.3/lib/net452 -L ../../packages/SharpCompress.0.30.0/lib/net461/ -L /usr/lib/mono/4.7.3-api/Facades/ -L /usr/lib/mono/fsharp/ -L ../../LibOrbisPkg/bin/Debug/ -L ../../packages/IpMatcher.1.0.4.2/lib/net452/ -L ../../packages/Newtonsoft.Json.13.0.1/lib/net45/ -L ../../packages/RegexMatcher.1.0.7/lib/net462/ -L ../../packages/CavemanTcp.1.3.8/lib/net461/ -L ../../packages/UrlMatcher.1.0.0.1/lib/net452/ -L ../../packages/System.ValueTuple.4.5.0/lib/net461/ -L /usr/lib/mono/4.5/Facades/ -L /lib/mono/msbuild/Current/bin/ --config $APP.exe.config --machine-config /etc/mono/4.5/machine.config --deps $APP.exe

#mkbundle binary try load library with a full path, those 2 patches are to try load from the current directory
echo 'Applying patches to the final executable...'
cp $APP $APP-unpatched
sed -i 's/\/usr\/lib\x0\/etc/\.\x0\x0\x0\x0\x0\x0\x0\x0\/etc/' ./$APP #/usr/lib/ => .
sed -i 's/\.\.\/lib\x0/\x0\x0\x0\x0\x0\x0\x0/' ./$APP #../lib => NULL


export AppDir=$PWD/$APP.AppDir

rm -r "$AppDir"

export BIN=$AppDir/usr/bin
export LIB=$AppDir/usr/lib
export EXC=$AppDir/AppRun
export LNK=$AppDir/$APP.desktop
export ICO=$AppDir/$APP.png

mkdir -p "$BIN"
mkdir -p "$LIB"
cp $APP "$BIN"

echo '#!/bin/sh' >> "$EXC"
echo 'SELF=$(readlink -f "$0")' >> "$EXC"
echo 'HERE=${SELF%/*}' >> "$EXC"
echo 'export PATH="${HERE}/usr/bin/:${HERE}/usr/sbin/:${HERE}/usr/games/:${HERE}/bin/:${HERE}/sbin/${PATH:+:$PATH}"' >> "$EXC"
echo 'export LD_LIBRARY_PATH="${HERE}/usr/lib/:${HERE}/usr/lib/i386-linux-gnu/:${HERE}/usr/lib/x86_64-linux-gnu/:${HERE}/usr/lib32/:${HERE}/usr/lib64/:${HERE}/lib/:${HERE}/lib/i386-linux-gnu/:${HERE}/lib/x86_64-linux-gnu/:${HERE}/lib32/:${HERE}/lib64/${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"' >> "$EXC"
echo 'export PYTHONPATH="${HERE}/usr/share/pyshared/${PYTHONPATH:+:$PYTHONPATH}"' >> "$EXC"
echo 'export XDG_DATA_DIRS="${HERE}/usr/share/${XDG_DATA_DIRS:+:$XDG_DATA_DIRS}"' >> "$EXC"
echo 'export PERLLIB="${HERE}/usr/share/perl5/:${HERE}/usr/lib/perl5/${PERLLIB:+:$PERLLIB}"' >> "$EXC"
echo 'export GSETTINGS_SCHEMA_DIR="${HERE}/usr/share/glib-2.0/schemas/${GSETTINGS_SCHEMA_DIR:+:$GSETTINGS_SCHEMA_DIR}"' >> "$EXC"
echo 'export CD=$PWD' >> "$EXC"
echo 'export QT_PLUGIN_PATH="${HERE}/usr/lib/qt4/plugins/:${HERE}/usr/lib/i386-linux-gnu/qt4/plugins/:${HERE}/usr/lib/x86_64-linux-gnu/qt4/plugins/:${HERE}/usr/lib32/qt4/plugins/:${HERE}/usr/lib64/qt4/plugins/:${HERE}/usr/lib/qt5/plugins/:${HERE}/usr/lib/i386-linux-gnu/qt5/plugins/:${HERE}/usr/lib/x86_64-linux-gnu/qt5/plugins/:${HERE}/usr/lib32/qt5/plugins/:${HERE}/usr/lib64/qt5/plugins/${QT_PLUGIN_PATH:+:$QT_PLUGIN_PATH}"' >> "$EXC"
echo 'cd ${HERE}' >> "$EXC"
echo 'EXEC=$(grep -e '^Exec=.*' "${HERE}"/*.desktop | head -n 1 | cut -d "=" -f 2 | cut -d " " -f 1)' >> "$EXC"
echo 'exec "${EXEC}" "$@"' >> "$EXC"

chmod a+x "$EXC"

echo '[Desktop Entry]' >> "$LNK"
echo "Name=$APP" >> "$LNK"
echo "Exec=$APP" >> "$LNK"
echo "Icon=$APP" >> "$LNK"
echo 'Type=Application' >> "$LNK"
echo 'Categories=Network;' >> "$LNK"

echo "$APP.png" >> "$AppDir/.DirIcon"

cp ../../Icon.png "$ICO"
cp ../../Icon.png "$AppDir/.DirIcon"


for d in ~/.local/share/icons/hicolor/*x*/ ; do
    r=$(basename $d)
    echo "Generating $r Icon"
    convert -resize $r "$ICO" "$AppDir/$APP.$r.png"
done


#to the mkbundle binary patch to read from app dir
cp /usr/lib/libmono-native.so "$AppDir"
cp /usr/lib/libMonoPosixHelper.so "$AppDir"
cp /usr/lib/libgdiplus.so "$AppDir"

#correct library paths
cp /usr/lib/libmono-native.so "$LIB"
cp /usr/lib/libgdiplus.so "$LIB"
cp /usr/lib/libMonoPosixHelper.so "$LIB"


#cp /usr/lib/cli/gdk-sharp-2.0/libgdksharpglue-2.so "$LIB"
#cp /usr/lib/cli/gdk-sharp-2.0/libgdksharpglue-2.so "$LIB"
#cp /usr/lib/cli/glib-sharp-2.0/libglibsharpglue-2.so "$LIB"
#cp /usr/lib/cli/gtk-sharp-2.0/libgtksharpglue-2.so "$LIB"
#cp /usr/lib/cli/gtk-sharp-2.0/libgtksharpglue-2.so "$LIB"


wget -nc https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage
chmod a+x appimagetool-x86_64.AppImage
./appimagetool-x86_64.AppImage "$APP.AppDir"
