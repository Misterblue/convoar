#! /bin/bash
mkdir -p convoar

CONVOAR=../../convoar/bin/Debug/convoar.exe

# PARAMS=""
PARAMS="--DoubleSided false"
# PARAMS="--logGltfBuilding --verbose --LogBuilding --LogConversionStats"

cd convoar
$CONVOAR  $PARAMS ../testtest88.oar
rsync -r ../convoar basil@nyxx:basil-git/Basiljs/
exit

$CONVOAR  $PARAMS ../Atropia_00.oar
$CONVOAR  $PARAMS ../Atropia_01.oar
$CONVOAR  $PARAMS ../Atropia_02.oar
$CONVOAR  $PARAMS ../Atropia_10.oar
$CONVOAR  $PARAMS ../Atropia_11.oar
$CONVOAR  $PARAMS ../Atropia_12.oar
$CONVOAR  $PARAMS ../Atropia_20.oar
$CONVOAR  $PARAMS ../Atropia_21.oar
$CONVOAR  $PARAMS ../Atropia_22.oar
$CONVOAR  $PARAMS ../IMAOutpostAlphaForest.oar
$CONVOAR  $PARAMS ../IMAOutpostAlphaTerrain.oar
$CONVOAR  $PARAMS ../Region-3dworlds-20170604.oar

rsync -r ../convoar basil@nyxx:basil-git/Basiljs/
exit


