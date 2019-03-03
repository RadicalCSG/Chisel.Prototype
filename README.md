# Chisel.Prototype

This is a work in progress prototype for the chisel editor
It is unfinished, and has bugs

It requires Unity 2018.3.6 or higher

Notes:
- Right now a temporary native dll is used for the CSG algorithm, this will be replaced by a managed implementation.
- Unity doesn't properly import this native dll until you reimport the 'Chisel Core' package. You only need to do this once
- The native dll is x64 only