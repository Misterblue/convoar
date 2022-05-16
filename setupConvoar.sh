#! /bin/bash
# Script run after checkout out Convoar to get the needed other libraries.

if [[ -z "$OPENSIMBIN" ]] ; then
    echo "Will use OpenSimulator binaries supplied in checkouted bin/"
fi

export TARGET=Release

export PLACE=$(pwd)

export CONVOAR_BRANCH=${CONVOAR_BRANCH:-master}
export COMMON_ENTITIES_BRANCH=${COMMON_ENTITIES_BRANCH:-master}
export COMMON_UTIL_BRANCH=${COMMON_UTIL_BRANCH:-main}

if [[ ! -z "$OPENSIMBIN" ]] ; then
    if [[ -z "$LIBOMVBIN" ]] ; then
        echo "Assuming libOpenMetaverse binaries are in $OPENSIMBIN"
        export LIBOMVBIN="$OPENSIMBIN"
    fi
    echo "Fetching OpenSimulator binaries from $OPENSIMBIN"
    mkdir -p bin
    ./gatherLibs.sh
fi

# Update the version info from the checkout
./updateVersion.sh

# Gather all the sources for the tool libraries
mkdir -p addon-modules
cd addon-modules
git clone --depth 1 -b $COMMON_ENTITIES_BRANCH --single-branch https://github.com/Herbal3d/HerbalCommonEntitiesCS.git
git clone --depth 1 -b $COMMON_UTIL_BRANCH --single-branch https://github.com/Herbal3d/HerbalCommonUtilCS.git
