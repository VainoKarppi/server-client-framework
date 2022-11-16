mkdir server-client-framework
cd server-client-framework

dotnet new classlib


cp -R ..//client//files client
cp -R ..//server//files server

rm Class1.cs



version=$(cat ..//version)
echo $version

sed -i '/<PropertyGroup>/,/<\/PropertyGroup>/ s/<\/PropertyGroup>/   <Version>'$version'<\/Version>\n<\/PropertyGroup>/' server-client-framework.csproj

dotnet pack -p:PackageVersion=$version

fileName=server-client-framework.$version.nupkg

mv bin//Debug//$fileName ..//$fileName

cd ..
rm -r server-client-framework/


scp $fileName pi@karppi2.asuscomm.com:/home/pi/shared/NuGet/server-client-framework/