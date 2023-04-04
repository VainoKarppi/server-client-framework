#!/bin/sh

version=$(cat ./version)
startPath=$(pwd)
date=$(date +%F_%H-%M-%S)
buildPath=$".//releases//$date//server-client-framework.$version"

echo -e '\033[32mversion:'$version' \033[1;32m\033[0m'
echo -e '\033[32mbuildPath:'$buildPath' \033[1;32m\033[0m'

if [ -d "$buildPath" ]; then
    rm -r $buildPath/
fi

mkdir -p $buildPath
cd $buildPath

echo "";echo -e '\033[32mBuilding the template...\033[1;32m\033[0m';echo "";
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

sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/  <AssemblyVersion>'$version'<\/AssemblyVersion>\n<\/PropertyGroup>/' $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <Version>'$version'<\/Version>\n<\/PropertyGroup>/' $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <FileVersion>'$version'<\/FileVersion>\n<\/PropertyGroup>/' $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <GenerateDocumentationFile>true<\/GenerateDocumentationFile>\n<\/PropertyGroup>/' $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <DebugSymbols>False<\/DebugSymbols>\n<\/PropertyGroup>/' $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <DebugType>None<\/DebugType>\n<\/PropertyGroup>/' $csProjFile
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <NoWarn>8622;1591<\/NoWarn>\n<\/PropertyGroup>/' $csProjFile

echo "";echo -e '\033[32mBuilding shared libary...\033[1;32m\033[0m';echo "";
dotnet publish -p:PublishDir=..//shared//,Configuration=Release,AssemblyName=shared-framework.$version

echo "";echo -e '\033[32mBuilding server only libary...\033[1;32m\033[0m';echo "";
rm -r bin
rm -r client
dotnet publish -p:PublishDir=..//server//,Configuration=Release,AssemblyName=server-framework.$version

echo "";echo -e '\033[32mBuilding client only libary...\033[1;32m\033[0m';echo "";
rm -r bin
rm -r server
cp -R ..//..//..//client//files client
cp -R ..//..//..//shared client
dotnet publish -p:PublishDir=..//client//,Configuration=Release,AssemblyName=client-framework.$version

cd ..
rm -r "server-client-framework.$version"

echo "";echo -e '\033[32mBUILD(s) SUCCESS!\033[1;32m\033[0m';echo "";
read -p "Press ENTER to exit"