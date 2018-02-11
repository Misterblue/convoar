#! /bin/bash

TARGET=Release

cd ..
HERE=$(pwd)

# Assimp has been built as $TARGET
cd "${HERE}/.."
# cd assimp
# cmake -G "Visual Studio 14 2015 Win64" -DASSIMP_BUILD_TESTS=off
# Assimp must be built with debug info because AssimpNet uses that info
# /cygdrive/c/Windows/Microsoft.NET/Framework64/v4.0.30319/MSBuild.exe /p:Configuration=RelWithDebInfo AssimpNet.sln

cd "$HERE"
cp assimp/bin/RelWithDebInfo/assimp* AssimpNet/libs/Assimp
cd AssimpNet/libs/Assimp
mv assimp-vc140-mt.dll Assimp64.dll
mv assimp-vc140-mt.pdb Assimp64.pdb
mv assimp-vc140-mt.ilk Assimp64.ilk

# Now build AssimpNet
cd "$HERE"
cd "AssimpNet"
/cygdrive/c/Windows/Microsoft.NET/Framework64/v4.0.30319/MSBuild.exe /p:Configuration=Net45-$TARGET AssimpNet.sln

cd "$HERE"
cp AssimpNet/AssimpNet/bin/Net45-$TARGET/* convoar/libs
cp AssimpNet/AssimpNet/bin/Net45-$TARGET/* convoar/bin/$TARGET/
