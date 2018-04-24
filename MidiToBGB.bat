@echo off
:: MidiToBGB ::

:: Usage: MidiToBGB.exe --midi NameWithoutSpaces --bgb Host Port
:: --midi is optional and defaults to the last connected MIDI input device.
:: --bgb is optional and defaults to 127.0.0.1 8765

:: Example - connect "Some Cool Device" to BGB on another PC:
:: MidiToBGB.exe --midi SomeCoolDevice --bgb 192.168.2.123 8765

:: Default: List all devices, then connect last MIDI input to BGB at 127.0.0.1 8765
MidiToBGB.exe
