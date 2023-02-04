@echo off
@title Building protobuf...
cd ../assets/protoc-win64/bin
protoc -I=../../../Models --csharp_out=../../../Models ../../../Models/*.proto
@exit
