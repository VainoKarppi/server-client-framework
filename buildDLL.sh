#!/bin/sh

version=$(cat ./version)
startPath=$(pwd)
date=$(date +%F_%H-%M-%S)
buildPath=$".//releases//$date//server-client-framework.$version"

echo version:$version
echo buildPath:$buildPath

if [ -d "$buildPath" ]; then
    rm -r $buildPath/
fi

mkdir -p $buildPath
cd $buildPath

dotnet new classlib

cp -R ..//..//..//client//files client
cp -R ..//..//..//server//files server
cp -R ..//..//..//shared client
cp -R ..//..//..//shared server

cd server
header='#define SERVER\n'
find ./shared/*.cs -exec sed -i "1s|^|$header|" {} \;
cd ..

rm Class1.cs

csProjFile="server-client-framework.$version.csproj"
echo $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/  <AssemblyVersion>'$version'<\/AssemblyVersion>\n<\/PropertyGroup>/' $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <Version>'$version'<\/Version>\n<\/PropertyGroup>/' $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <FileVersion>'$version'<\/FileVersion>\n<\/PropertyGroup>/' $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <GenerateDocumentationFile>true<\/GenerateDocumentationFile>\n<\/PropertyGroup>/' $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <DebugSymbols>False<\/DebugSymbols>\n<\/PropertyGroup>/' $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <DebugType>None<\/DebugType>\n<\/PropertyGroup>/' $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <NoWarn>8622;1591<\/NoWarn>\n<\/PropertyGroup>/' $csProjFile


echo ""; echo "Building shared libary.."; echo ""
dotnet publish -p:PublishDir=..//shared//,Configuration=Release,AssemblyName=shared-framework.$version

echo ""; echo "Building server only libary.."; echo ""
rm -r bin
rm -r client
dotnet publish -p:PublishDir=..//server//,Configuration=Release,AssemblyName=server-framework.$version

echo ""; echo "Building client only libary.."; echo ""
rm -r bin
rm -r server
cp -R ..//..//..//client//files client
cp -R ..//..//..//shared client
dotnet publish -p:PublishDir=..//client//,Configuration=Release,AssemblyName=client-framework.$version

cd ..
rm -r "server-client-framework.$version"

echo ""
echo "BUILD(s) SUCCESS!"
echo ""
read -p "Press ENTER to exit"