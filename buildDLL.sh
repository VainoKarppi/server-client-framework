rm -r server-client-framework-TEMP/

mkdir server-client-framework-TEMP
cd server-client-framework-TEMP

dotnet new classlib

cp -R ..//client//files client
cp -R ..//server//files server

rm Class1.cs

version=$(cat ..//version)
echo $version


sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/  <AssemblyVersion>'$version'<\/AssemblyVersion>\n<\/PropertyGroup>/' server-client-framework.csproj
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <Version>'$version'<\/Version>\n<\/PropertyGroup>/' server-client-framework.csproj
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <FileVersion>'$version'<\/FileVersion>\n<\/PropertyGroup>/' server-client-framework.csproj
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <GenerateDocumentationFile>true<\/GenerateDocumentationFile>\n<\/PropertyGroup>/' server-client-framework.csproj

dotnet publish -c Release

mv bin//Release//net7.0//publish ..//server-client-framework

cd ..
rm -r server-client-framework-TEMP/