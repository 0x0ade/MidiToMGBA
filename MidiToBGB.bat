@echo off
:: MidiToBGB ::

:: Usage: MidiToBGB.exe --midi ID --bgb HOST PORT

:: --midi is optional and defaults to the last connected input device.
:: --bgb is optional and defaults to 127.0.0.1 8765

:: To get a list of all found MIDI input devices:
:: MidiToBGB.exe --midi list

:: Example - connecting to MIDI device #0:
:: MidiToBGB.exe --midi 0

:: Example - connecting SomeCoolDevice to BGB on another PC:
:: MidiToBGB.exe --midi SomeCoolDevice --bgb 192.168.2.123 8765

:: Default: List all devices, then run with default settings.
MidiToBGB.exe --midi list
MidiToBGB.exe
