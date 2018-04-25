@echo off
:: MidiToMGBA ::

:: Usage: MidiToMGBA.exe --midi NameWithoutSpaces --rom mGB.gb
:: --midi is optional and defaults to the last connected MIDI input device.
:: --rom is optional and defaults to mGB.gb

:: Example - connect "Some Cool Device" to pushpin:
:: MidiToMGBA.exe --midi SomeCoolDevice --rom pushpin.gbc

:: Default: List all devices, then connect last MIDI input to mGBA running mGB.gb
MidiToMGBA.exe
