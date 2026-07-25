# New Executable Flower set: support of headers/data structures and code

<img src="../../assets/sunflower256.png" height="128" width="128" align="right"/>

The `SunFlower.NE.dll` .NET library represents a plugins/extensions
for a SunFlower kernel to deconstruct and 
disassemble Win16 and OS/2 1.x applications. 

The `NewExecutableFlowerSeed` class presents a FlowerSeed API
to extract all nested binary structures/tables in the program image
Table presented next shows what is actually supports by this extension

| Structures              | Status          |
|-------------------------|-----------------|
| `IMAGE_DOS_HEADER`      | Done            |
| `IMAGE_OS2_HEADER`      | Done            |
| Segment Table           | Done            |
| Entry Table             | Done            |
| Import Names            | Done            |
| Resident Name Table     | Done            |
| Non-resident Name Table | Done            |
| Resources Table         | Not implemented |
| Modules Reference Table | Done            |
| Per-Segment Relocations | Done            |

The `NewDisassemblerSeed` class presents a FlowerSeed API to
disassemble program image using Microsoft NE format specification. 

| Plugin Specific              | Status        |
|------------------------------|---------------|
| 286 Instruction Set          | Done          |
| Near procedures control flow | Done          |
| Near labels control flow     | Done          |
| Far Jumps                    | Not supported | 
| Far procedures control flow  | Not Supported |
| Data objects                 | Not Supported |
| Applying of Fixups           | Done          |
| Resolving Runtime Imports    | Done          |
| Resolving Exports            | Done          |

Some of the program entry points will be ignored because of 
applied program relocations will be point to the `.data` objects

### License 
MIT