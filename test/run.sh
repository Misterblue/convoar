#! /bin/bash

HERE=$(PWD)

DOCLEAN=no
DOBUILD=yes
DOCOPY=yes
GENINDEX=yes

# if set to 'yes', will attempt to run the docker version of convoar
USEDOCKER=no

CONVOAR=$HERE/../dist/convoar.exe

# PROCESSING="UNOPTIMIZED"
# PROCESSING="SMALLASSETS"
# PROCESSING="MERGEDMATERIALS"
PROCESSING="UNOPTIMIZED SMALLASSETS MERGEDMATERIALS"

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
# OARS="$OARS ZadarooSwamp.oar"

cd "$HERE"
OARS=$(ls *.oar)

for OAR in $OARS ; do
    BASENAME="$(basename -s .oar $OAR)"
    for PROCESS in $PROCESSING ; do
        if [[ "$PROCESS" == "UNOPTIMIZED" ]] ; then
            PARAMS="$DOVERBOSE --TextureMaxSize 4096 --HalfRezTerrain false"
            SUBDIR=unoptimized
        fi
        if [[ "$PROCESS" == "SMALLASSETS" ]] ; then
            PARAMS="$DOVERBOSE"
            SUBDIR=smallassets
        fi
        if [[ "$PROCESS" == "MERGEDMATERIALS" ]] ; then
            PARAMS="$DOVERBOSE --MergeSharedMaterialMeshes true"
            SUBDIR=mergedmaterials
        fi
        # PARAMS="$PARAMS --logGltfBuilding --verbose --LogBuilding --LogConversionStats"

        # put a copy of the original OAR into the built tree
        cd "$HERE"
        echo "======= copying $OAR to convoar/${BASENAME}"
        cp "$OAR" convoar/${BASENAME}
        # Add a JPG of the OAR file to the build tree if it exists
        if [[ -e "jpg/${BASENAME}.jpg" ]] ; then
            cp "jpg/${BASENAME}.jpg" "convoar/${BASENAME}"
        fi

        DIR="convoar/${BASENAME}/$SUBDIR"

        # Optionally clean out the directory for a clean build
        if [[ "$DOCLEAN" == "yes" ]] ; then
            echo "======= cleaning $DIR"
            cd "$HERE"
            rm -rf "$DIR"
            mkdir -p "$DIR"
        fi

        # If doing build and files have not already been built, do the build
        if [[ "$DOBUILD" == "yes" ]] ; then
            if [[ ! -e "${DIR}/${BASENAME}.gltf" ]] ; then
                echo "======= building $DIR"
                cd "$HERE"
                rm -rf "$DIR"
                mkdir -p "$DIR"
                cd "$DIR"
                if [[ "$USEDOCKER" == "yes" ]] ; then
                    cp "../../../$OAR" .
                    docker run -v $(pwd):/oar herbal3d/convoar:latest "$PARAMS" "$OAR"
                    rm -f "$OAR"
                else
                    $CONVOAR  $PARAMS "../../../$OAR"
                fi
                # Create a single TGZ file with all the content for the 3DWebWorldz people
                cd "$HERE"
                cd "$DIR"
                tar -czf "${BASENAME}.tgz" *
                # Create a single ZIP file with all the content for the 3DWebWorldz people
                cd "$HERE"
                cd "$DIR"
                zip -r ${BASENAME} *.gltf *.buf images
            else
                echo "======= not building $DIR: already exists"
            fi
        fi
    done
done

# Update the Internet repositories with new version of everything
cd "$HERE"
if [[ "$DOCOPY" == "yes" ]] ; then
    if [[ "$HOSTNAME" == "lakeoz ]] ; then
        # if running on the Windows system, copy stuff to the linux system
        echo "======= copying convoar to nyxx"
        cd "$HERE"
        rsync -r -v --delete-after convoar "basil@nyxx:basil-git/Basiljs"
    fi
    echo "======= copying convoar to misterblue"
    cd "$HERE"
    rsync -r -v --delete-after convoar "${REMOTEACCT}@${REMOTEHOST}:$REMOTEBASE"
fi

# Generate an indes for the directory
cd "$HERE"
if [[ "$GENINDEX" == "yes" ]] ; then
    ./genIndex.sh > convoar/index.json
fi
