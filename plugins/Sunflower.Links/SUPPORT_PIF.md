# Program Information File: supported headers and data structures

<img src="../../assets/sunflower256.png" height="128" width="128" align="right"/>

A program information file (PIF) defines how a given DOS program 
should be run in a multitasking environment, especially in order to 
avoid giving it unnecessary resources which could remain available to 
other programs. TopView was the originator of PIFs; 
they were then inherited and extended by `DESQview` and Microsoft Windows, 
where they are most often seen. PIFs are not used today in software due to 
the absence of DOS applications.

PIF file contains binary sections and has versions or 
revisions which little differs between

| Section Name     | Status |
|------------------|--------|
| Microsoft PIFex  | [x]    |
| Windows 3.0 386  | [x]    |
| Windows 3.0 286  | [x]    |
| Windows NT 4 VMM | [ ]    |
| Windows NT 3     | [ ]    |
| AUTOEXEC.BAT 4   | []     |
| CONFIG.SYS 4     |        |      

> [!WARNING]
> Section names of early PIF might be written with IBM-850 codepage.

# License
MIT