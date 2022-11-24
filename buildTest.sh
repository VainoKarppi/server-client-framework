rm -r server-client-framework-TEMP/

if [ ! -d "test" ]; then
  # Take action if $DIR exists. #
  echo "Installing config files..."

  mkdir test
  cd test
  dotnet new console
  
  rm Program.cs
  echo -e "using ServerFramework;\n\nNetwork.StartServer();\n\nConsole.ReadLine();" > Program.cs
  sed -i '/<\/Project>/ s/<\/Project>/  <ItemGroup>\n\t\t<Reference Include="server-client-framework">\n\t\t\t<HintPath>.\\server-client-framework\\server-client-framework.dll<\/HintPath>\n\t\t<\/Reference>\n\t<\/ItemGroup>\n<\/Project>/' test.csproj

  cd ..
fi

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

mv bin//Release//net7.0//publish ..//test//server-client-framework

cd ..
rm -r server-client-framework-TEMP/