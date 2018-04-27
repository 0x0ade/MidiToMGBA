@echo off
:: MidiToMGBA ::

:: Usage: MidiToMGBA.exe --midi NameWithoutSpaces --rom mGB.gb --mgba libmgba.dll --mgba-sdl libmgba-sdl.dll
:: --midi is optional and defaults to the last connected MIDI input device.
:: --rom is optional and defaults to mGB.gb
:: --mgba is optional and defaults to libmgba.dll
:: --mgba-sdl is optional and defaults to libmgba-sdl.dll
:: --log-data is optional, disabled by default, and triggers the verbose data log.
:: --sync-sio is optional and defaults to 512
:: --sync-wait is optional and defaults to 64
:: --buffersize is optional and defaults to 1024
:: --samplerate is optional and defaults to 48000

:: Note: A samplerate of 44100 kHz introduces pacing issues.
:: Furthermore, if you want to use a lower samplerate, lower the buffer size.

:: Example - connect "Some Cool Device" to pushpin:
:: MidiToMGBA.exe --midi SomeCoolDevice --rom pushpin.gbc

:: Default: List all devices, then connect last MIDI input to mGBA running mGB.gb
MidiToMGBA.exe
