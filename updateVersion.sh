#! /bin/bash

./bin/BuildVersion.exe --namespace org.herbal3d.convoar \
    --version $(cat VERSION) \
    --versionFile convoar/VersionInfo.cs \
    --assemblyInfoFile convoar/Properties/AssemblyInfo.cs
