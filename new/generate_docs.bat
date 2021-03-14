@echo off
mkdir "%~f0/../bin/docs/config"
%~f0/../util/NaturalDocs/NaturalDocs.exe -i "%~f0/../AutoItInterpreter" -p "%~f0/../bin/docs/config" -o HTML "%~f0/../bin/docs" %*