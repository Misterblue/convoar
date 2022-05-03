#! /bin/bash

./BuildVersion/BuildVersion.exe --namespace org.herbal3d.convoar \
    --verbose \
    --version $(cat VERSION) \
    --versionFile convoar/VersionInfo.cs \
    --assemblyInfoFile convoar/Properties/AssemblyInfo.cs
