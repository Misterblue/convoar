#! /bin/bash

HERE=$(PWD)
CONVOAR=$HERE/../convoar/bin/Debug/convoar.exe

# PARAMS=""
# PARAMS="--MeshShareThreshold 1000 --MergeSharedMaterialMeshes false"
PARAMS="--MeshShareThreshold 1000"
# PARAMS="--logGltfBuilding --verbose --LogBuilding --LogConversionStats"

OARS=""
OARS="$OARS testtest88.oar"
# OARS="$OARS PalmyraTemple.oar"
# OARS="$OARS Atropia_00.oar Atropia_01.oar Atropia_02.oar Atropia_10.oar"
# OARS="$OARS Atropia_11.oar Atropia_12.oar Atropia_20.oar Atropia_21.oar Atropia_22.oar"
# OARS="$OARS IMAOutpostAlphaForest.oar IMAOutpostAlphaTerrain.oar Region-3dworlds-20170604.oar"
# OARS="$OARS universal_campus_01_0.7.3_03022012.oar"

for OAR in $OARS ; do
    cd "$HERE"
    DIR="convoar/$(basename -s .oar $OAR)"
    mkdir -p "$DIR"
    cd "$DIR"
    echo "DIR=$DIR"
    $CONVOAR  $PARAMS "../../$OAR"
    rsync -r * "basil@nyxx:basil-git/Basiljs/convoar/"
done


