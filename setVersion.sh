#!/bin/bash
 
echo "Enter New version: (eg. 1.8.0.0)"
read version
echo ""
echo "using the version: [${version}]"
echo ""

echo "Confirm version? [yes/no]"
read confirm
if [[ $confirm == "yes" ]]; then
	rm version
	echo $version >> version
	echo "Changed version!"
	
	# Update server version
	cd server
	sed -i 's#<Version>.*</Version>#<Version>'$version'</Version>#' server.csproj
	cd ..
	
	# Update client version
	cd client
	sed -i 's#<Version>.*</Version>#<Version>'$version'</Version>#' client.csproj
	cd ..
fi;

read -p "Press ENTER to exit"