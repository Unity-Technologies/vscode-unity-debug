#!/bin/sh
rm -f unity-debug.zip
zip -r9 unity-debug.zip bin/ package.json -x "*.DS_Store"