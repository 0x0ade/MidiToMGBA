# MidiToMGBA - MIDI input -> mGBA link

### License: MIT

----

**Note:** This previously was MidiToBGB, but as BGB's TCP link is suboptimal for this task, development moved on to a mGBA-centric solution.  
MidiToMGBA ships with a devbuild of mGBA 0.7, split into multiple libraries, and with the SDL platform built as `libmgba-sdl.dll` instead of `mgba-sdl.exe`.

MidiToMGBA acts as a simple MIDI message forwarder, listening to a MIDI input device and passing on all MIDI messages to mGBA's internal link interface.

### Setup:
- If you want to use MidiToBGB with your DAW: Download and install a MIDI "loopback" driver, f.e. [loopmidi by Tobias Erichsen](http://www.tobias-erichsen.de/software/loopmidi.html)
- [**Download MidiToMGBA**](https://github.com/0x0ade/MidiToMGBA/releases) from the releases tab.
- **Run `MidiToMGBA.bat`**
    - If you want to change the settings, open the .bat in any text editor.
