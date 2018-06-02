#! /bin/bash
# Run convoar in a special target directory
# The idea would be the person running the docker container would
#    mount the remote directory in  that place.

for DIR in /home/convoar/oar /oar /tmp/oar ; do
    echo "Checking $DIR"
    if [[ -d "$DIR" ]] ; then
        echo "Found $DIR"
        cd "$DIR"
        echo "Doing: /home/convoar/convoar/dist/convoar.exe $@"
        mono /home/convoar/convoar/dist/convoar.exe $@
    fi
done
