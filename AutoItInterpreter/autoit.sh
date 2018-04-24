#!/bin/sh
_dir=$( readlink -e "$( dirname "$0" )" )
_dll="$_dir/autoitipt.dll"
dotnet "\"$_dll\"" $@