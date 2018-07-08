#! /bin/bash

HERE=$(PWD)
CONVOAR=$HERE/../dist/convoar.exe

DOBUILD=yes
DOCOPY=yes

# PROCESSING="UNOPTIMIZED"
# PROCESSING="MERGEDMATERIALS"
PROCESSING="UNOPTIMIZED MERGEDMATERIALS"

if [[ -z "$MB_REMOTEACCT" || -z "$MB_REMOTEHOST" ]] ; then
    echo "Cannot run script without MB_REMOTEACCT and MB_REMOTEHOST environment variables set"
    exit
fi
REMOTEACCT=${MB_REMOTEACCT:-mb}
REMOTEHOST=${MB_REMOTEHOST:-someplace.misterblue.com}

DOVERBOSE=""
# DOVERBOSE="--Verbose"

REMOTEBASE=files.misterblue.com/BasilTest

OARS=""
# OARS="$OARS testtest88.oar"
# OARS="$OARS PalmyraTemple.oar"
# OARS="$OARS Atropia_00.oar Atropia_01.oar Atropia_02.oar"
# OARS="$OARS Atropia_10.oar Atropia_11.oar Atropia_12.oar"
# OARS="$OARS Atropia_20.oar Atropia_21.oar Atropia_22.oar"
# OARS="$OARS IMAOutpostAlphaForest.oar IMAOutpostAlphaTerrain.oar Region-3dworlds-20170604.oar"
# OARS="$OARS universal_campus_01_0.7.3_03022012.oar"
# OARS="$OARS IST_01-14.10.03.oar"
# OARS="$OARS alfea3.oar"
# OARS="$OARS art_city_2025.oar"
# OARS="$OARS epiccastle.oar"
# OARS="$OARS large_structures_01.oar"
# OARS="$OARS EpicCitadel.oar"
# OARS="$OARS GoneCity.oar"
# OARS="$OARS OSGHUG-cyberlandia.oar"
# OARS="$OARS OSGHUG-Mars.oar"
# OARS="$OARS OSGHUG-maya3.oar"
# OARS="$OARS OSGHUG-reefs.oar"
# OARS="$OARS sierpinski_triangle_122572_prims_01.oar"
# OARS="$OARS WinterLand.oar"
# OARS="$OARS Fantasy.oar"
OARS="$OARS ZadarooSwamp.oar"

for OAR in $OARS ; do
    BASENAME="$(basename -s .oar $OAR)"
    for PROCESS in $PROCESSING ; do
        if [[ "$PROCESS" == "UNOPTIMIZED" ]] ; then
            PARAMS="$DOVERBOSE "
            SUBDIR=unoptimized
        fi
        if [[ "$PROCESS" == "MERGEDMATERIALS" ]] ; then
            PARAMS="$DOVERBOSE --MergeSharedMaterialMeshes true"
            SUBDIR=mergedmaterials
        fi
        # PARAMS="$PARAMS --logGltfBuilding --verbose --LogBuilding --LogConversionStats"

        cd "$HERE"
        DIR="convoar/${BASENAME}/$SUBDIR"
        if [[ "$DOBUILD" == "yes" ]] ; then
            echo "======= building $DIR"
            rm -rf "$DIR"
            mkdir -p "$DIR"
            cd "$DIR"
            $CONVOAR  $PARAMS "../../../$OAR"
            # Create a single TGZ file with all the content for the 3DWebWorldz people
            cd "$HERE"
            cd "$DIR"
            tar -czf "${BASENAME}.tgz" *
        fi
        cd "$HERE"
        if [[ "$DOCOPY" == "yes" ]] ; then
            echo "======= copying $DIR to nyxx"
            ssh basil@nyxx "mkdir -p basil-git/Basiljs/$DIR"
            rsync -r --delete-after "${DIR}/" "basil@nyxx:basil-git/Basiljs/$DIR"
            echo "======= copying $DIR to misterblue"
            ssh ${REMOTEACCT}@${REMOTEHOST} "mkdir -p $REMOTEBASE/$DIR"
            rsync -e "/usr/bin/ssh" -r --delete-after "${DIR}/" "${REMOTEACCT}@${REMOTEHOST}:$REMOTEBASE/$DIR"
        fi
    done
done
