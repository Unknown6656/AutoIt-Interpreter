#!/bin/sh
_dir=$( readlink -e "$( dirname "$0" )" )
_dll="$_dir/autoitutests.dll"
dotnet "\"$_dll\"" $@