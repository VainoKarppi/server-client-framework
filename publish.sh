
version=$(cat ./version)
echo $version

name=$"server-client-framework.$version"
if [ -d "$name" ]; then
    rm -r $name
fi

mkdir $name
cd $name

dotnet new classlib


cp -R ..//client//files client
cp -R ..//server//files server

rm Class1.cs


sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/  <AssemblyVersion>'$version'<\/AssemblyVersion>\n<\/PropertyGroup>/' $name.csproj
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <Version>'$version'<\/Version>\n<\/PropertyGroup>/' $name.csproj
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <FileVersion>'$version'<\/FileVersion>\n<\/PropertyGroup>/' $name.csproj
sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/    <GenerateDocumentationFile>true<\/GenerateDocumentationFile>\n<\/PropertyGroup>/' $name.csproj

dotnet pack -p:PackageVersion=$version

fileName=server-client-framework.$version.nupkg

mv bin//Debug//$fileName ..//$fileName

#scp bin//Debug//$fileName pi@karppi.dy.fi:/home/pi/shared/NuGet/server-client-framework/

cd ..
#rm -r $name

read -p "Press enter to continue"