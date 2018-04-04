#! /bin/bash

# TARGET=Release
TARGET=Debug

CMAKE="/cygdrive/c/Program Files (x86)/CMake/bin/cmake.exe"
MSBUILD="/cygdrive/f/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/bin/MSBuild.exe"

cd ..
HERE=$(pwd)

# Assimp has been built as $TARGET
cd "${HERE}"
cd assimp
"${CMAKE}"  -G "Visual Studio 14 2015 Win64" -DASSIMP_BUILD_TESTS=off -DASSIMP_BUILD_ASSIMP_VIEW=off
"${MSBUILD}" /p:Configuration=$TARGET Assimp.sln

cd "${HERE}"
cp assimp/bin/$TARGET/assimp-*.dll assimp-net/libs/Assimp/Assimp64.dll
rm -f assimp-net/libs/Assimp/Assimp32.dll
cp assimp/bin/$TARGET/zlibd.dll assimp-net/libs/Assimp/

# Now build assimp-net
cd "${HERE}"
cd "assimp-net"
"${MSBUILD}" /Restore
"${MSBUILD}" /property:Configuration=$TARGET /target:AssimpNet AssimpNet.sln

cd "$HERE"
for dir in convoar/libs convoar/convoar/bin/Debug convoar/convoar/bin/Release ; do
    cp assimp-net/bin/$TARGET/AssimpNet/netstandard2.0/AssimpNet.* "${dir}"
    cp assimp-net/libs/Assimp/Assimp64.dll "${dir}"
    cp assimp-net/libs/Assimp/zlibd.dll "${dir}"
done
