version=$(cat ./version)
echo $version

name=$"server-client-framework.$version"

rm -r $name/

mkdir $name
cd $name

dotnet new classlib

cp -R ..//client//files client
cp -R ..//server//files server

cp -R ..//shared client
cp -R ..//shared server

cd server
header='#define SERVER\n'
find ./shared/*.cs -exec sed -i "1s|^|$header|" {} \;
cd ..

rm Class1.cs


sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/  <AssemblyVersion>'$version'<\/AssemblyVersion>\n<\/PropertyGroup>/' $name.csproj
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <Version>'$version'<\/Version>\n<\/PropertyGroup>/' $name.csproj
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <FileVersion>'$version'<\/FileVersion>\n<\/PropertyGroup>/' $name.csproj
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <GenerateDocumentationFile>true<\/GenerateDocumentationFile>\n<\/PropertyGroup>/' $name.csproj

dotnet publish -c Release -o ..//$name

rm $name.csproj
rm -r bin
rm -r obj
rm -r client
rm -r server

cd ..
rm -r $name/