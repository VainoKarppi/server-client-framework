
version=$(cat ./version)

if [ ! -d "test" ]; then
  # Take action if $DIR exists. #
  echo "Installing config files..."

  mkdir test
  cd test
  dotnet new console
  
  rm Program.cs
  echo -e "using ServerFramework;\n\nNetwork.StartServer();\n\nConsole.ReadLine();" > Program.cs
  sed -i '/<\/Project>/ s/<\/Project>/  <ItemGroup>\n\t\t<Reference Include="server-client-framework">\n\t\t\t<HintPath>..\\server-client-framework.'$version'\\server-client-framework.'$version'.dll<\/HintPath>\n\t\t<\/Reference>\n\t<\/ItemGroup>\n<\/Project>/' test.csproj

  cd ..
fi

sh buildDLL.sh