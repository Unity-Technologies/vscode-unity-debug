UNITY_DEBUG = bin/UnityDebug.exe

all: $UNITY_DEBUG

clean:
	rm -rf bin/
	xbuild /p:Configuration=Release /t:Clean

$UNITY_DEBUG:
	xbuild /p:Configuration=Release

build:
	tsc -p ./typescript
	@echo "build finished"

zip: $UNITY_DEBUG
	rm -f unity-debug.zip
	zip -r9 unity-debug.zip bin/ attach.ts package.json Changelog.txt -x "*.DS_Store"

vsix: clean $UNITY_DEBUG
	rm -f *.vsix
	vsce package