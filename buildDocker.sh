#! /bin/bash
# Script to build convoar Docker image.
# Make user file 'VERSION' contains the latest version.
# Make sure you are logged onto DockerHub before running this script.
# Hacked version based on https://medium.com/travis-on-docker/how-to-version-your-docker-images-1d5c577ebf54

DOCKERHUB_USER=herbal3d
IMAGE=convoar

if [[ ! -e "./VERSION" ]] ; then
    echo "No file named 'VERSION'. You might be in the wrong directory."
    echo "Run the build script in the base project directory."
    exit
fi
VERSION=$(cat VERSION)

# Tag and create commit git sources with the version number.
# Not working yet... need to automate Visual Studio versioning.
DO_GIT_TAG=no

# Build the docker image of the latest git commited sources.
DO_DOCKER_BUILD=yes

# Push the built docker image to DockerHub.
DO_DOCKERHUB_PUSH=no

# Tag the version in git
if [[ "$DO_GIT_TAG" == "yes" ]] ; then
    git add -A
    git commit -m "version $VERSION"
    git tag -a "$VERSION" -m "version $VERSION"
    git push
    git push --tags
fi

if [[ "$DO_DOCKER_BUILD" == "yes" ]] ; then
    # docker build --no-cache --build-arg VERSION=${VERSION} -t herbal3d/convoar .
    docker build --build-arg VERSION=${VERSION} -t herbal3d/convoar .

    docker tag $DOCKERHUB_USER/$IMAGE:latest $DOCKERHUB_USER/$IMAGE:$VERSION
fi

if [[ "$DO_DOCKERHUB_PUSH" == "yes" ]] ; then
    docker push $DOCKERHUB_USER/$IMAGE:latest
    docker push $DOCKERHUB_USER/$IMAGE:$VERSION
fi


