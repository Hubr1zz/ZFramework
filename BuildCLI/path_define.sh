#!/bin/bash

cd "$(dirname "$0")"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
export WORKSPACE="${SCRIPT_DIR}/../UnityProject"
export UNITYEDITOR_PATH="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS"
export BUILD_DLL_LOGFILE="./build_dll.log"
export BUILD_LOGFILE="./build.log"

echo "环境变量已设置："
echo "WORKSPACE=${WORKSPACE}"
echo "UNITYEDITOR_PATH=${UNITYEDITOR_PATH}"
echo "BUILD_DLL_LOGFILE=${BUILD_DLL_LOGFILE}"
echo "BUILD_LOGFILE=${BUILD_LOGFILE}"
