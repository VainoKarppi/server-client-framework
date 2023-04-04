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

dotnet publish -c Release -o ../

cd ..
rm -r "server-client-framework.$version"

echo ""
echo "BUILD SUCCESS!"
echo finalDir:$startPath//$date//server-client-framework.$version.dll
echo ""
read -p "Press enter to continue"