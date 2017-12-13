#! /bin/bash
mkdir -p convoar
cd convoar
../../convoar/bin/Debug/convoar.exe ../testtest88.oar

../../convoar/bin/Debug/convoar.exe ../Atropia_00.oar
../../convoar/bin/Debug/convoar.exe ../Atropia_01.oar
../../convoar/bin/Debug/convoar.exe ../Atropia_02.oar
../../convoar/bin/Debug/convoar.exe ../Atropia_10.oar
../../convoar/bin/Debug/convoar.exe ../Atropia_11.oar
../../convoar/bin/Debug/convoar.exe ../Atropia_12.oar
../../convoar/bin/Debug/convoar.exe ../Atropia_20.oar
../../convoar/bin/Debug/convoar.exe ../Atropia_21.oar
../../convoar/bin/Debug/convoar.exe ../Atropia_22.oar
../../convoar/bin/Debug/convoar.exe ../IMAOutpostAlphaForest.oar
../../convoar/bin/Debug/convoar.exe ../IMAOutpostAlphaTerrain.oar
../../convoar/bin/Debug/convoar.exe ../Region-3dworlds-20170604.oar

rsync -r ../convoar basil@nyxx:basil-git/Basiljs/
exit


