#! /bin/bash
mkdir -p convoar
# format=collada
format=gltf

CONVOAR=../../convoar/bin/Debug/convoar.exe

# PARAMS="--RemoveRedundantMaterials --ImproveCacheLocality --JoinIdenticalVertices --OptimizeMeshes --OptimizeGraph"
# --PreTransformVertices
# PARAMS="--ImproveCacheLocality --OptimizeMeshes"
# PARAMS=""
PARAMS="--DoubleSided false"
# PARAMS="--UseAssimp"
# PARAMS="--logGltfBuilding --verbose --LogBuilding --LogConversionStats"

cd convoar
$CONVOAR --exportFormat $format $PARAMS ../testtest88.oar
rsync -r ../convoar basil@nyxx:basil-git/Basiljs/
exit

$CONVOAR --exportFormat $format $PARAMS ../Atropia_00.oar
$CONVOAR --exportFormat $format $PARAMS ../Atropia_01.oar
$CONVOAR --exportFormat $format $PARAMS ../Atropia_02.oar
$CONVOAR --exportFormat $format $PARAMS ../Atropia_10.oar
$CONVOAR --exportFormat $format $PARAMS ../Atropia_11.oar
$CONVOAR --exportFormat $format $PARAMS ../Atropia_12.oar
$CONVOAR --exportFormat $format $PARAMS ../Atropia_20.oar
$CONVOAR --exportFormat $format $PARAMS ../Atropia_21.oar
$CONVOAR --exportFormat $format $PARAMS ../Atropia_22.oar
$CONVOAR --exportFormat $format $PARAMS ../IMAOutpostAlphaForest.oar
$CONVOAR --exportFormat $format $PARAMS ../IMAOutpostAlphaTerrain.oar
$CONVOAR --exportFormat $format $PARAMS ../Region-3dworlds-20170604.oar

rsync -r ../convoar basil@nyxx:basil-git/Basiljs/
exit


