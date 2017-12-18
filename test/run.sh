#! /bin/bash
mkdir -p convoar
# format=collada
format=gltf2

cd convoar
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../Atropia_12.oar
exit
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../testtest88.oar

../../convoar/bin/Debug/convoar.exe --exportFormat $format ../Atropia_00.oar
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../Atropia_01.oar
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../Atropia_02.oar
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../Atropia_10.oar
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../Atropia_11.oar
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../Atropia_12.oar
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../Atropia_20.oar
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../Atropia_21.oar
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../Atropia_22.oar
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../IMAOutpostAlphaForest.oar
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../IMAOutpostAlphaTerrain.oar
../../convoar/bin/Debug/convoar.exe --exportFormat $format ../Region-3dworlds-20170604.oar

rsync -r ../convoar basil@nyxx:basil-git/Basiljs/
exit


