@echo off
:: MidiToMGBA ::

:: Usage: MidiToMGBA.exe --midi NameWithoutSpaces --rom mGB.gb --mgba libmgba.dll --mgba-sdl libmgba-sdl.dll
:: --midi is optional and defaults to the last connected MIDI input device.
:: --rom is optional and defaults to mGB.gb
:: --mgba is optional and defaults to libmgba.dll
:: --mgba-sdl is optional and defaults to libmgba-sdl.dll

:: Example - connect "Some Cool Device" to pushpin:
:: MidiToMGBA.exe --midi SomeCoolDevice --rom pushpin.gbc

:: Default: List all devices, then connect last MIDI input to mGBA running mGB.gb
MidiToMGBA.exe
