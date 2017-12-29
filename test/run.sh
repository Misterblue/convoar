#! /bin/bash
mkdir -p convoar
# format=collada
format=gltf2

CONVOAR=../../convoar/bin/Debug/convoar.exe

cd convoar
$CONVOAR --exportFormat $format ../testtest88.oar
exit

$CONVOAR --exportFormat $format ../Atropia_00.oar
$CONVOAR --exportFormat $format ../Atropia_01.oar
$CONVOAR --exportFormat $format ../Atropia_02.oar
$CONVOAR --exportFormat $format ../Atropia_10.oar
$CONVOAR --exportFormat $format ../Atropia_11.oar
$CONVOAR --exportFormat $format ../Atropia_12.oar
$CONVOAR --exportFormat $format ../Atropia_20.oar
$CONVOAR --exportFormat $format ../Atropia_21.oar
$CONVOAR --exportFormat $format ../Atropia_22.oar
$CONVOAR --exportFormat $format ../IMAOutpostAlphaForest.oar
$CONVOAR --exportFormat $format ../IMAOutpostAlphaTerrain.oar
$CONVOAR --exportFormat $format ../Region-3dworlds-20170604.oar

rsync -r ../convoar basil@nyxx:basil-git/Basiljs/
exit


