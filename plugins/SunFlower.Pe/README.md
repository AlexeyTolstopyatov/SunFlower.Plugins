# Portable Executable: supported headers and data structures

<img src="assets/sunflower256.png" height="128" width="128" align="right"/>

Microsoft Windows NT applications format which bases on Unix COFF. 
Information about this segmentation format you can find everywhere, 
*so more features for analysis
will be added over time*. 

> [!WARNING]
> I'm not sure about completely support of
> Big Endian linked files. 
> I will definitely deal with this, but only when
> I have fully completed the support of all mandatory PE structures.

### Structures PE32/+

| Structures               | Status        |
|--------------------------|---------------|
| `IMAGE_DOS_HEADER`       | Done          |
| `IMAGE_FILE_HEADER`      | Done          |
| `IMAGE_OPTIONAL_HEADER`s | Done          |
| `IMAGE_OS2_HEADER`       | Done          |
| `COR20_HEADER`           | Done          |
| Rich header (MSVC info)  | Done          |
| Data Directories         | Done          |
| Section Headers          | Done          |
| Static Imports table     | Done          |
| Imporing Addresses Table | Not supported |
| Bound Imports Table      | Not supported |
| Delay Imports Table      | Done          |
| Exports Table            | Done          |
| Certifications Table     | Not supported |
| Base relocations table   | Not supported |
| Structured Exceptions    | Not supported |
| TLS Directory            | Not supported |
| Load Config Directory    | Not supported |
| Global Pointers Table    | Not supported |

### Image diagnostics

The dump never throws on malformed images. Structural inconsistencies are
reported instead through the `ImageDetails Anomalies` seed: raw data beyond the
end of the file, overlapping / misaligned sections, an entry point outside every
section, an undersized header region, an out-of-range `SizeOfImage` and trailing
data (overlay) after the last section.

### License
MIT